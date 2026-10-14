using MessSharp.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Executable members that the shared model leaves out: expression-bodied properties,
/// indexers and event accessors. Each one owns its own effects.
/// </summary>
internal static class MemberFormOwners
{
    public static IEnumerable<CallableOwner> Gather(SourceFile file) => file.Classes.SelectMany(cls =>
        cls.Node.ChildNodes().OfType<MemberDeclarationSyntax>().SelectMany(member => Owners(file, cls, member)));

    private static IEnumerable<CallableOwner> Owners(SourceFile file, ClassModel cls, MemberDeclarationSyntax member) =>
        member switch
        {
            PropertyDeclarationSyntax { ExpressionBody: { } arrow } property =>
                [Owner(file, cls, "get_" + property.Identifier.Text, property, arrow.Expression, [], property.Type)],
            IndexerDeclarationSyntax { ExpressionBody: { } arrow } indexer =>
                [Owner(file, cls, "get_Item", indexer, arrow.Expression, MemberFormParameters.Declared(indexer.ParameterList), indexer.Type)],
            IndexerDeclarationSyntax { AccessorList: { } list } indexer =>
                Accessors(file, cls, list, "Item", MemberFormParameters.Declared(indexer.ParameterList), indexer.Type),
            EventDeclarationSyntax { AccessorList: { } list } declared =>
                Accessors(file, cls, list, declared.Identifier.Text, [], declared.Type),
            _ => [],
        };

    private static IEnumerable<CallableOwner> Accessors(SourceFile file, ClassModel cls, AccessorListSyntax list,
        string name, List<ParameterModel> parameters, TypeSyntax type) =>
        list.Accessors.Where(accessor => accessor.Body is not null || accessor.ExpressionBody is not null).Select(accessor =>
        {
            var getter = accessor.Keyword.IsKind(SyntaxKind.GetKeyword);
            var all = getter ? parameters : [.. parameters, MemberFormParameters.Value(type, accessor)];
            return Owner(file, cls, accessor.Keyword.Text + "_" + name, accessor,
                (SyntaxNode?)accessor.Body ?? accessor.ExpressionBody!.Expression, all, getter ? type : null);
        });

    private static CallableOwner Owner(SourceFile file, ClassModel cls, string name, SyntaxNode node, SyntaxNode body,
        List<ParameterModel> parameters, TypeSyntax? returnType)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new CallableOwner(new MethodModel
        {
            Name = name,
            Line = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1,
            Parameters = parameters,
            ReturnType = returnType?.ToString() ?? "void",
            Class = cls,
            Node = node,
            Body = body as BlockSyntax,
            File = file,
        }, body);
    }
}
