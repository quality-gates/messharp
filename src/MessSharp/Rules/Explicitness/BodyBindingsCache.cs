using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace MessSharp.Rules.Explicitness;

/// <summary>One <see cref="BodyBindings"/> per body, shared by every storage step in that body.</summary>
internal static class BodyBindingsCache
{
    private static readonly ConditionalWeakTable<SyntaxNode, BodyBindings> Bindings = new();

    public static BodyBindings For(SyntaxNode body) => Bindings.GetValue(body, node => new BodyBindings(node));
}
