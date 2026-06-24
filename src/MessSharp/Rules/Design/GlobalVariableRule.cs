using MessSharp.Model;
using MessSharp.Rule;
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
        foreach (var member in cls.Node.Members.OfType<FieldDeclarationSyntax>())
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
        foreach (var node in cls.Node.DescendantNodes())
        {
            var name = ExtractMutationTarget(node, cls.Name);
            if (name != null) mutated.Add(name);
        }
        return mutated;
    }

    private static string? ExtractMutationTarget(SyntaxNode node, string className)
    {
        if (node is AssignmentExpressionSyntax assign)
            return ExtractSimpleOrQualifiedName(assign.Left, className);

        if (node is PostfixUnaryExpressionSyntax postfix && IsIncrDecr(postfix.Kind()))
            return ExtractSimpleOrQualifiedName(postfix.Operand, className);

        if (node is PrefixUnaryExpressionSyntax prefix && IsIncrDecr(prefix.Kind()))
            return ExtractSimpleOrQualifiedName(prefix.Operand, className);

        if (node is ArgumentSyntax arg && IsRefOrOut(arg.RefKindKeyword.Kind()))
            return ExtractSimpleOrQualifiedName(arg.Expression, className);

        return null;
    }

    private static bool IsIncrDecr(SyntaxKind kind) =>
        kind is SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression
              or SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression;

    private static bool IsRefOrOut(SyntaxKind kind) =>
        kind is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword;

    /// <summary>
    /// Returns the field name if expr is a bare identifier (FieldName) or
    /// a class-qualified access (ClassName.FieldName). Returns null otherwise.
    /// </summary>
    private static string? ExtractSimpleOrQualifiedName(ExpressionSyntax expr, string className)
    {
        if (expr is IdentifierNameSyntax id)
            return id.Identifier.Text;

        if (expr is MemberAccessExpressionSyntax ma &&
            ma.Expression is IdentifierNameSyntax cls2 &&
            cls2.Identifier.Text == className)
            return ma.Name.Identifier.Text;

        return null;
    }
}
