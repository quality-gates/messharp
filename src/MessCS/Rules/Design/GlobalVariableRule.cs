using MessCS.Model;
using MessCS.Rule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessCS.Rules.Design;

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
            switch (node)
            {
                // Simple assignment: Field = value  or  ClassName.Field = value
                case AssignmentExpressionSyntax assign:
                    {
                        var name = ExtractStaticFieldName(assign.Left, cls.Name);
                        if (name != null) mutated.Add(name);
                        break;
                    }

                // Prefix/postfix ++/--: Field++, Field--
                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(SyntaxKind.PostIncrementExpression)
                      || postfix.IsKind(SyntaxKind.PostDecrementExpression):
                    {
                        var name = ExtractSimpleOrQualifiedName(postfix.Operand, cls.Name);
                        if (name != null) mutated.Add(name);
                        break;
                    }

                case PrefixUnaryExpressionSyntax prefix
                    when prefix.IsKind(SyntaxKind.PreIncrementExpression)
                      || prefix.IsKind(SyntaxKind.PreDecrementExpression):
                    {
                        var name = ExtractSimpleOrQualifiedName(prefix.Operand, cls.Name);
                        if (name != null) mutated.Add(name);
                        break;
                    }

                // ref/out argument: ref Field, out Field
                case ArgumentSyntax arg
                    when arg.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                      || arg.RefKindKeyword.IsKind(SyntaxKind.OutKeyword):
                    {
                        var name = ExtractSimpleOrQualifiedName(arg.Expression, cls.Name);
                        if (name != null) mutated.Add(name);
                        break;
                    }
            }
        }

        return mutated;
    }

    private static string? ExtractStaticFieldName(ExpressionSyntax expr, string className)
    {
        return ExtractSimpleOrQualifiedName(expr, className);
    }

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
