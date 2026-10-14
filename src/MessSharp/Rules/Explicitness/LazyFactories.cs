using MessSharp.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal enum LazyFactoryKind { Factory, None, Default, Unresolved }

/// <summary>
/// The value factory a Lazy creation binds. <see cref="LazyFactoryKind.None"/> means no factory runs:
/// the value is preset. <see cref="LazyFactoryKind.Default"/> constructs T when forced;
/// <see cref="LazyFactoryKind.Unresolved"/> must fail closed.
/// </summary>
internal sealed record LazyFactory(LazyFactoryKind Kind, ExpressionSyntax? Expression = null)
{
    public static readonly LazyFactory None = new(LazyFactoryKind.None);
    public static readonly LazyFactory Default = new(LazyFactoryKind.Default);
    public static readonly LazyFactory Unresolved = new(LazyFactoryKind.Unresolved);
}

/// <summary>
/// Binds the <c>valueFactory</c> parameter of every Lazy constructor overload by name or by the
/// type of the first positional argument, and follows derived classes through <c>base(...)</c>.
/// </summary>
internal static class LazyFactories
{
    public static LazyFactory Select(CallableOwner owner, BaseObjectCreationExpressionSyntax creation)
    {
        var type = new ExpressionTypes(owner).TypeOf(creation);
        if (type is null) return LazyFactory.Unresolved;
        var arguments = creation.ArgumentList?.Arguments ?? default;
        if (LazyTypes.IsSystemLazy(owner, type))
            return new LazyArguments(owner, type, new ExpressionTypes(owner).TypeOf).Select(arguments);
        return LazyTypes.Subclass(owner, type) is { } cls ? Derived(owner, cls, creation.ArgumentList) : LazyFactory.Unresolved;
    }

    /// <summary>Whether <paramref name="node"/> is the factory a Lazy creation binds, so Value runs it.</summary>
    public static bool IsFactory(CallableOwner owner, SyntaxNode node)
    {
        var current = node;
        while (current.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax) current = current.Parent;
        if (current.Parent is not ArgumentSyntax { Parent: ArgumentListSyntax { Parent: BaseObjectCreationExpressionSyntax creation } list } argument
            || (list.Arguments.IndexOf(argument) != 0 && argument.NameColon?.Name.Identifier.Text != "valueFactory")
            || !KnownExecutionOperations.IsLazy(owner, creation)) return false;
        var selected = Select(owner, creation);
        return selected.Kind == LazyFactoryKind.Factory && CallableSyntax.Unwrap(selected.Expression!) == node;
    }

    private static LazyFactory Derived(CallableOwner owner, ClassModel cls, ArgumentListSyntax? actuals)
    {
        var count = actuals?.Arguments.Count ?? 0;
        var constructors = cls.Node.ChildNodes().OfType<ConstructorDeclarationSyntax>()
            .Where(ctor => !ctor.Modifiers.Any(SyntaxKind.StaticKeyword)).ToList();
        var matches = constructors.Where(ctor => ctor.ParameterList.Parameters.Count == count).Take(2).ToList();
        if (constructors.Count == 0 && count == 0) return BaseDefault(owner, cls);
        if (matches.Count != 1) return LazyFactory.Unresolved;
        return matches[0].Initializer switch
        {
            null => BaseDefault(owner, cls),
            { RawKind: (int)SyntaxKind.BaseConstructorInitializer } initializer when Base(owner, cls) is { } baseType =>
                Forward(owner, matches[0], baseType, initializer.ArgumentList.Arguments, actuals),
            _ => LazyFactory.Unresolved,
        };
    }

    private static LazyFactory BaseDefault(CallableOwner owner, ClassModel cls) =>
        Base(owner, cls) is not null ? LazyFactory.Default : LazyFactory.Unresolved;

    private static string? Base(CallableOwner owner, ClassModel cls) =>
        cls.BaseTypes.FirstOrDefault() is { } baseType && LazyTypes.IsSystemLazy(owner, baseType) ? baseType : null;

    private static LazyFactory Forward(CallableOwner owner, ConstructorDeclarationSyntax ctor, string baseType,
        SeparatedSyntaxList<ArgumentSyntax> arguments, ArgumentListSyntax? actuals)
    {
        var parameters = ctor.ParameterList.Parameters;
        string? ParameterType(ExpressionSyntax value) => value is IdentifierNameSyntax name
            ? parameters.FirstOrDefault(p => p.Identifier.Text == name.Identifier.Text)?.Type?.ToString() : null;
        var selected = new LazyArguments(owner, baseType, ParameterType).Select(arguments);
        if (selected.Kind != LazyFactoryKind.Factory) return selected;
        var index = selected.Expression is IdentifierNameSyntax forwarded
            ? parameters.IndexOf(p => p.Identifier.Text == forwarded.Identifier.Text) : -1;
        if (index < 0 || actuals is null) return LazyFactory.Unresolved;
        return CallArguments.Actual(actuals, parameters[index].Identifier.Text, index) is { } actual
            ? new LazyFactory(LazyFactoryKind.Factory, actual) : LazyFactory.Unresolved;
    }
}

/// <summary>Classifies the arguments of one System.Lazy constructor call.</summary>
internal sealed class LazyArguments(CallableOwner owner, string lazyType, Func<ExpressionSyntax, string?> typeOf)
{
    private static readonly IReadOnlySet<string> Delegates = new HashSet<string>(StringComparer.Ordinal)
    {
        "Func", "Action", "Delegate", "MulticastDelegate",
    };

    public LazyFactory Select(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        if (arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == "valueFactory") is { } named)
            return new LazyFactory(LazyFactoryKind.Factory, named.Expression);
        if (arguments.Count == 0) return LazyFactory.Default;
        if (arguments[0].NameColon is { } name)
            return name.Name.Identifier.Text == "value"
                ? new LazyFactory(LazyFactoryKind.None, arguments[0].Expression) : LazyFactory.Default;
        return Positional(arguments[0].Expression);
    }

    private LazyFactory Positional(ExpressionSyntax first)
    {
        var shape = new ValueShapes(owner, typeOf).For(first);
        if (shape == ValueShape.Boolean || IsSetting(first)) return LazyFactory.Default;
        if (shape == ValueShape.Value || IsValue(first)) return new LazyFactory(LazyFactoryKind.None, first);
        return ValueIsDelegate() ? LazyFactory.Unresolved : new LazyFactory(LazyFactoryKind.Factory, first);
    }

    private bool IsSetting(ExpressionSyntax value)
    {
        value = CallableSyntax.Unwrap(value);
        if (value is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression or (int)SyntaxKind.DefaultLiteralExpression })
            return true;
        if (value is MemberAccessExpressionSyntax member && LazyTypes.Name(member.Expression.ToString()) == "LazyThreadSafetyMode")
            return true;
        return typeOf(value) is { } type && (type is "bool" or "Boolean" or "System.Boolean"
            || LazyTypes.Name(type) == "LazyThreadSafetyMode");
    }

    private bool IsValue(ExpressionSyntax value) =>
        ExpressionTypes.Element(lazyType) is { } element && typeOf(value) == element;

    private bool ValueIsDelegate()
    {
        if (ExpressionTypes.Element(lazyType) is not { } element) return true;
        var name = LazyTypes.Name(element);
        return Delegates.Contains(name) || owner.Method.File.Root.DescendantNodes().OfType<DelegateDeclarationSyntax>()
            .Any(declared => declared.Identifier.Text == name);
    }
}
