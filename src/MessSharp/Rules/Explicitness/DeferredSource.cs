using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>One way a variable, parameter, field or property can hold the value read at a site.</summary>
internal abstract record DeferredSource
{
    /// <summary>An expression evaluated by <paramref name="Owner"/>, or by the reading owner when null.</summary>
    internal sealed record Value(ExpressionSyntax Expression, CallableOwner? Owner = null) : DeferredSource;

    /// <summary>The value a parameter of the reading owner holds on entry.</summary>
    internal sealed record Entry(string Parameter) : DeferredSource;

    /// <summary>One element of <paramref name="Collection"/>, read by the reading owner.</summary>
    internal sealed record Element(ExpressionSyntax Collection) : DeferredSource;

    /// <summary>A value the callee writes through an <c>out</c> or <c>ref</c> argument.</summary>
    internal sealed record Written(ArgumentSyntax Argument) : DeferredSource;

    /// <summary>A writer the analysis cannot follow.</summary>
    internal sealed record Opaque : DeferredSource;
}
