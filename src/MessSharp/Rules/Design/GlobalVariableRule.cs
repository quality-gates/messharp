using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Design;

/// <summary>
/// ADAPTED: flags mutable static fields in a class that are actually mutated
/// somewhere in the file (assigned outside initializer, incremented, passed
/// ref/out). `static readonly` and `const` are never flagged.
/// `report-immutable=true` also flags un-mutated mutable statics.
/// </summary>
public sealed class GlobalVariableRule : BaseRule, IClassRule
{
    public void Apply(RuleContext ctx, ClassModel cls)
    {
        bool reportImmutable = ctx.Props.Bool("report-immutable", false);

        var mutableStatics = CollectMutableStaticFields(cls);
        if (mutableStatics.Count == 0) return;

        var mutated = FindMutatedFieldNames(cls);

        foreach (var (fieldName, fieldLine) in mutableStatics)
        {
            if (mutated.Contains(fieldName) || reportImmutable)
                ctx.Report(fieldLine, fieldLine, fieldName);
        }
    }

    /// <summary>
    /// Returns (name, line) pairs for non-readonly, non-const static fields.
    /// </summary>
    private static List<(string Name, int Line)> CollectMutableStaticFields(ClassModel cls)
    {
        var result = new List<(string, int)>();
        foreach (var member in ModelBuilderHelpers.MembersOf(cls.Node).OfType<FieldDeclarationSyntax>())
        {
            var mods = member.Modifiers;
            bool isStatic = mods.Any(SyntaxKind.StaticKeyword);
            bool isReadonly = mods.Any(SyntaxKind.ReadOnlyKeyword);
            bool isConst = mods.Any(SyntaxKind.ConstKeyword);

            if (!isStatic || isReadonly || isConst) continue;

            foreach (var v in member.Declaration.Variables)
            {
                var line = v.SyntaxTree.GetLineSpan(v.Span).StartLinePosition.Line + 1;
                result.Add((v.Identifier.Text, line));
            }
        }
        return result;
    }

    /// <summary>
    /// Scans the class body for mutations of static fields (assignments,
    /// compound assignments, increments/decrements, ref/out arguments).
    /// </summary>
    private static HashSet<string> FindMutatedFieldNames(ClassModel cls)
    {
        var mutated = new HashSet<string>(StringComparer.Ordinal);
        var targets = new StaticFieldTargets(cls.Name, LexicalScopes.From(cls.Node));
        foreach (var node in cls.Node.DescendantNodes())
        {
            if (node is AssignmentExpressionSyntax assign)
            {
                mutated.UnionWith(AssignmentTargetNames(assign.Left, targets));
                continue;
            }
            var name = ExtractMutationTarget(node, targets);
            if (name != null) mutated.Add(name);
        }
        return mutated;
    }

    /// <summary>
    /// Yields the field names written by an assignment's LHS. A tuple
    /// deconstruction (`(A, (Cls.B, _)) = ...`) writes every target it
    /// contains, recursing into nested tuples.
    /// </summary>
    private static IEnumerable<string> AssignmentTargetNames(ExpressionSyntax left, StaticFieldTargets targets)
    {
        if (left is TupleExpressionSyntax tuple)
            return tuple.Arguments.SelectMany(a => AssignmentTargetNames(a.Expression, targets));

        var name = targets.NameOf(left);
        return name == null ? Enumerable.Empty<string>() : new[] { name };
    }

    private static string? ExtractMutationTarget(SyntaxNode node, StaticFieldTargets targets)
    {
        if (node is PostfixUnaryExpressionSyntax postfix && IsIncrDecr(postfix.Kind()))
            return targets.NameOf(postfix.Operand);

        if (node is PrefixUnaryExpressionSyntax prefix && IsIncrDecr(prefix.Kind()))
            return targets.NameOf(prefix.Operand);

        if (node is ArgumentSyntax arg && IsRefOrOut(arg.RefKindKeyword.Kind()))
            return targets.NameOf(arg.Expression);

        return null;
    }

    private static bool IsIncrDecr(SyntaxKind kind) =>
        kind is SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression
              or SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression;

    private static bool IsRefOrOut(SyntaxKind kind) =>
        kind is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword;

    /// <summary>
    /// Resolves a written expression to the static field of one class it
    /// targets, given the class's lexical scopes.
    /// </summary>
    private sealed record StaticFieldTargets(string ClassName, LexicalScopes Scopes)
    {
        /// <summary>
        /// Returns the field name if expr is a bare identifier (FieldName) or
        /// a class-qualified access (ClassName.FieldName or ClassName&lt;T&gt;.FieldName).
        /// Returns null otherwise.
        /// </summary>
        public string? NameOf(ExpressionSyntax expr)
        {
            // A bare identifier may be bound by a local or parameter that shadows
            // the static field; that is a mutation of the local, not the field.
            if (expr is IdentifierNameSyntax id)
                return Scopes.IsShadowed(id) ? null : id.Identifier.Text;

            if (expr is MemberAccessExpressionSyntax ma &&
                ma.Expression is SimpleNameSyntax cls2 &&
                cls2.Identifier.Text == ClassName)
                return ma.Name.Identifier.Text;

            return null;
        }
    }
}
