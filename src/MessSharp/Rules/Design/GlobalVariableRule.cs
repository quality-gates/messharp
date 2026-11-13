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
    private static HashSet<string> FindMutatedFieldNames(ClassModel cls) =>
        cls.Node.DescendantNodes().SelectMany(MutationClassifier.Classify)
            .Select(mutation => ExtractSimpleOrQualifiedName(mutation.Target, cls.Name))
            .OfType<string>().ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Returns the field name if expr is a bare identifier (FieldName) or
    /// a class-qualified access (ClassName.FieldName or ClassName&lt;T&gt;.FieldName).
    /// Returns null otherwise.
    /// </summary>
    private static string? ExtractSimpleOrQualifiedName(ExpressionSyntax expr, string className)
    {
        // A bare identifier may be bound by a local or parameter that shadows
        // the static field; that is a mutation of the local, not the field.
        if (expr is IdentifierNameSyntax id)
            return LexicalScopes.IsShadowed(id, id.Identifier.Text)
                ? null
                : id.Identifier.Text;

        if (expr is MemberAccessExpressionSyntax ma &&
            ma.Expression is SimpleNameSyntax cls2 &&
            cls2.Identifier.Text == className)
            return ma.Name.Identifier.Text;

        return null;
    }
}
