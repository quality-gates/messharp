using MessSharp.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.CleanCode;

/// <summary>
/// Analyzes invocation receiver expressions for StaticAccessRule, distinguishing
/// static class access from instance member chains, locals, fields, and parameters.
/// </summary>
internal static class StaticAccessReceiverClassifier
{
    internal static string? GetTargetClassName(ExpressionSyntax expression, HashSet<string> instanceNames)
    {
        var unwrapped = UnwrapParentheses(expression);
        return unwrapped switch
        {
            SimpleNameSyntax simpleName => GetSimpleTargetClassName(simpleName, instanceNames),
            MemberAccessExpressionSyntax qualifiedName => GetQualifiedTargetClassName(qualifiedName, instanceNames),
            _ => null,
        };
    }

    private static string? GetSimpleTargetClassName(SimpleNameSyntax simpleName, HashSet<string> instanceNames)
    {
        var name = simpleName.Identifier.Text;
        if (name.Length == 0 || !char.IsUpper(name[0])) return null;
        if (instanceNames.Contains(name)) return null;
        return name;
    }

    private static string? GetQualifiedTargetClassName(MemberAccessExpressionSyntax qualifiedName,
        HashSet<string> instanceNames)
    {
        if (!qualifiedName.IsKind(SyntaxKind.SimpleMemberAccessExpression)) return null;

        var className = qualifiedName.Name.Identifier.Text;
        if (className.Length == 0 || !char.IsUpper(className[0])) return null;

        if (!IsValidReceiverChain(qualifiedName.Expression, instanceNames)) return null;

        return className;
    }

    private static bool IsValidReceiverChain(ExpressionSyntax expression, HashSet<string> instanceNames)
    {
        var current = UnwrapParentheses(expression);
        while (current is MemberAccessExpressionSyntax ma)
        {
            if (!ma.IsKind(SyntaxKind.SimpleMemberAccessExpression)) return false;
            var segmentName = ma.Name.Identifier.Text;
            if (segmentName.Length == 0 || !char.IsUpper(segmentName[0])) return false;
            current = UnwrapParentheses(ma.Expression);
        }

        return current switch
        {
            SimpleNameSyntax simple => IsValidRootName(simple.Identifier.Text, instanceNames),
            AliasQualifiedNameSyntax alias => IsValidRootName(alias.Name.Identifier.Text, instanceNames),
            _ => false,
        };
    }

    private static bool IsValidRootName(string rootName, HashSet<string> instanceNames) =>
        rootName.Length > 0 && char.IsUpper(rootName[0]) && !instanceNames.Contains(rootName);

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expr)
    {
        while (expr is ParenthesizedExpressionSyntax paren)
            expr = paren.Expression;
        return expr;
    }

    internal static HashSet<string> CollectInstanceNames(MethodModel method, SyntaxNode body)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        AddParameters(names, method);
        AddClassMembers(names, method.Class);
        AddLocalDeclarations(names, body);
        return names;
    }

    private static void AddParameters(HashSet<string> names, MethodModel method)
    {
        foreach (var p in method.Parameters)
        {
            if (!string.IsNullOrEmpty(p.Name))
                names.Add(p.Name);
        }
    }

    private static void AddClassMembers(HashSet<string> names, ClassModel? classModel)
    {
        if (classModel?.Node == null) return;
        foreach (var member in ModelBuilderHelpers.MembersOf(classModel.Node))
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    foreach (var v in field.Declaration.Variables)
                        names.Add(v.Identifier.Text);
                    break;
                case PropertyDeclarationSyntax prop:
                    names.Add(prop.Identifier.Text);
                    break;
            }
        }
    }

    private static void AddLocalDeclarations(HashSet<string> names, SyntaxNode body)
    {
        foreach (var node in body.DescendantNodes())
        {
            switch (node)
            {
                case VariableDeclaratorSyntax v:
                    names.Add(v.Identifier.Text);
                    break;
                case SingleVariableDesignationSyntax d:
                    names.Add(d.Identifier.Text);
                    break;
                case ForEachStatementSyntax f:
                    names.Add(f.Identifier.Text);
                    break;
            }
        }
    }
}
