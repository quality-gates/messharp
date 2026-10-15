using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

internal sealed class CallbackInvocation : IEquatable<CallbackInvocation>
{
    private readonly string _parameterName;
    private readonly InvocationExpressionSyntax _call;
    private readonly IReadOnlyList<StorageOrigin> _arguments;

    public CallbackInvocation(string parameterName, InvocationExpressionSyntax call, IReadOnlyList<StorageOrigin> arguments)
    {
        _parameterName = parameterName;
        _call = call;
        _arguments = arguments;
    }

    public string ParameterName => _parameterName;
    public InvocationExpressionSyntax Call => _call;
    public IReadOnlyList<StorageOrigin> Arguments => _arguments;

    public bool Equals(CallbackInvocation? other) => other is not null
        && _parameterName == other._parameterName && _call == other._call
        && _arguments.Count == other._arguments.Count
        && _arguments.Zip(other._arguments).All(pair => pair.First.SameRoots(pair.Second));

    public override bool Equals(object? obj) => obj is CallbackInvocation other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_parameterName, _call);
}
