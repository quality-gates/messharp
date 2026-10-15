using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal static class StorageCollectionEdits
{
    public static StorageMutation? Find(InvocationExpressionSyntax call, string name,
        Func<ExpressionSyntax, StorageOrigin> origin) => name switch
        {
            "Remove" when call.ArgumentList.Arguments.Count == 1 =>
                new StorageMutation.Remove(origin(call.ArgumentList.Arguments[0].Expression)),
            "RemoveAt" => new StorageMutation.RemoveRange(Integer(call, 0), 1),
            "RemoveRange" => new StorageMutation.RemoveRange(Integer(call, 0), Integer(call, 1)),
            "Reverse" => new StorageMutation.Reverse(call.ArgumentList.Arguments.Count == 0 ? 0 : Integer(call, 0),
                Integer(call, 1), call.ArgumentList.Arguments.Count == 0),
            _ => null,
        };

    private static int? Integer(InvocationExpressionSyntax call, int index) =>
        call.ArgumentList.Arguments.ElementAtOrDefault(index)?.Expression is LiteralExpressionSyntax literal
            ? literal.Token.Value as int? : null;
}
