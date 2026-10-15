using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace MessSharp.Rules.Explicitness;

/// <summary>One <see cref="RefAliases"/> per body, shared by every reader of that body.</summary>
internal static class RefAliasCache
{
    private static readonly ConditionalWeakTable<SyntaxNode, RefAliases> Aliases = new();

    public static RefAliases For(SyntaxNode body) => Aliases.GetValue(body, node => new RefAliases(node));
}
