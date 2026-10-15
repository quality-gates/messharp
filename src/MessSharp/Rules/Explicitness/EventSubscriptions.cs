using MessSharp.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// <c>E += handler</c> and <c>E -= handler</c> on a same-file event with custom accessors run its add or remove
/// accessor with <c>value</c> bound to the handler; they write no storage. An event that a derived type can override,
/// custom or field-like, runs an accessor that depends on the runtime type, so it is found but not resolved.
/// </summary>
internal sealed class EventSubscriptions(IReadOnlyList<CallableOwner> owners)
{
    public IEnumerable<EventSubscription> Find(CallableOwner caller, SyntaxNode body) =>
        BodyWalk.Nodes(body).Select(node => Match(caller, node)).OfType<EventSubscription>();

    public static IReadOnlySet<SyntaxNode> Assignments(CallableOwner caller, IReadOnlyList<CallableOwner> owners, SyntaxNode body) =>
        new EventSubscriptions(owners).Find(caller, body).Select(s => (SyntaxNode)s.Assignment).ToHashSet();

    private EventSubscription? Match(CallableOwner caller, SyntaxNode node)
    {
        if (node is not AssignmentExpressionSyntax assignment || Keyword(assignment) is not { } keyword) return null;
        if (Name(assignment.Left) is not { } name || DeclaringClass(caller, assignment.Left) is not { } cls) return null;
        return EventDeclarations.Resolve(owners, cls, assignment, keyword + "_" + name, name);
    }

    private static string? Keyword(AssignmentExpressionSyntax assignment) => assignment.Kind() switch
    {
        SyntaxKind.AddAssignmentExpression => "add",
        SyntaxKind.SubtractAssignmentExpression => "remove",
        _ => null,
    };

    private static string? Name(ExpressionSyntax left) => left switch
    {
        IdentifierNameSyntax name => name.Identifier.Text,
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        _ => null,
    };

    private ClassModel? DeclaringClass(CallableOwner caller, ExpressionSyntax left) => left switch
    {
        IdentifierNameSyntax name when !LocalShadowing.IsShadowedByLocal(name, name.Identifier.Text) => caller.Method.Class,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } => caller.Method.Class,
        MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } when caller.Method.Class is { } cls =>
            ConstructorTypes.BaseClass(cls),
        MemberAccessExpressionSyntax member => ReceiverClass(caller, member.Expression),
        _ => null,
    };

    /// <summary>A same-file type named by the receiver, or else the receiver's declared type.</summary>
    private ClassModel? ReceiverClass(CallableOwner caller, ExpressionSyntax receiver)
    {
        var type = receiver is IdentifierNameSyntax name && !LocalShadowing.IsShadowedByLocal(name, name.Identifier.Text)
            ? name.Identifier.Text : new ExpressionTypes(caller, owners).TypeOf(receiver);
        if (type is null) return null;
        var classes = caller.Method.File.Classes.Where(cls => cls.Name == TypeNames.Simple(type)).Take(2).ToList();
        return classes is [{ Node: ClassDeclarationSyntax } cls] ? cls : null;
    }
}

internal static class EventDeclarations
{
    public static EventSubscription? Resolve(IReadOnlyList<CallableOwner> owners, ClassModel cls,
        AssignmentExpressionSyntax assignment, string accessor, string name) => Declaration(cls, name, []) switch
        {
            MemberDeclarationSyntax member when IsReplaceable(member) => new EventSubscription(assignment, accessor, null),
            EventDeclarationSyntax custom when Accessor(owners, custom, accessor) is { } owner =>
                new EventSubscription(assignment, accessor, owner),
            _ => null,
        };

    /// <summary>The member the class declares with this name, or else the one a same-file base declares.</summary>
    private static MemberDeclarationSyntax? Declaration(ClassModel cls, string name, HashSet<ClassModel> visited)
    {
        if (!visited.Add(cls)) return null;
        var declared = cls.Node.ChildNodes().OfType<MemberDeclarationSyntax>().FirstOrDefault(member => Declares(member, name));
        if (declared is not null) return declared;
        return ConstructorTypes.BaseClass(cls) is { } baseClass ? Declaration(baseClass, name, visited) : null;
    }

    private static bool Declares(MemberDeclarationSyntax member, string name) => member switch
    {
        BaseFieldDeclarationSyntax field => field.Declaration.Variables.Any(v => v.Identifier.Text == name),
        PropertyDeclarationSyntax property => property.Identifier.Text == name,
        EventDeclarationSyntax declared => declared.Identifier.Text == name,
        _ => false,
    };

    private static CallableOwner? Accessor(IReadOnlyList<CallableOwner> owners, EventDeclarationSyntax declared, string accessor) =>
        owners.FirstOrDefault(o => o.Parent is null && o.Method.Name == accessor
            && o.Method.Node is AccessorDeclarationSyntax { Parent.Parent: var parent } && parent == declared);

    /// <summary>An event a derived type can override; a sealed override ends the chain.</summary>
    private static bool IsReplaceable(MemberDeclarationSyntax member) =>
        member is EventDeclarationSyntax or EventFieldDeclarationSyntax
        && !member.Modifiers.Any(SyntaxKind.SealedKeyword)
        && (member.Modifiers.Any(SyntaxKind.VirtualKeyword) || member.Modifiers.Any(SyntaxKind.OverrideKeyword)
            || member.Modifiers.Any(SyntaxKind.AbstractKeyword));
}

/// <summary>A subscription and the accessor it runs; a replaceable event's accessor is not resolved.</summary>
internal sealed record EventSubscription(AssignmentExpressionSyntax Assignment, string AccessorName, CallableOwner? Accessor);
