using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Model;

/// <summary>
/// Groups the parts of each <c>partial</c> type across all analysed files, so
/// rules can resolve member usage in parts declared in other files. Parts are
/// matched syntactically by namespace, containing types, name and arity.
/// </summary>
public sealed class PartialTypeIndex
{
    public static readonly PartialTypeIndex Empty = new(Array.Empty<SourceFile>());

    private readonly Dictionary<string, List<ClassModel>> _parts = new(StringComparer.Ordinal);

    public PartialTypeIndex(IEnumerable<SourceFile> files)
    {
        foreach (var cls in files.SelectMany(f => f.Classes))
        {
            if (!IsPartial(cls.Node)) continue;
            var key = KeyOf(cls);
            if (!_parts.TryGetValue(key, out var parts))
                _parts[key] = parts = new List<ClassModel>();
            parts.Add(cls);
        }
    }

    /// <summary>
    /// Parts of the same partial type as <paramref name="cls"/> that live in
    /// other files. Empty for non-partial types and single-file partials.
    /// </summary>
    public IEnumerable<ClassModel> PartsInOtherFiles(ClassModel cls)
    {
        if (!IsPartial(cls.Node)) return Enumerable.Empty<ClassModel>();
        if (!_parts.TryGetValue(KeyOf(cls), out var parts)) return Enumerable.Empty<ClassModel>();
        return parts.Where(p => !ReferenceEquals(p.File, cls.File));
    }

    private static bool IsPartial(TypeDeclarationSyntax node) =>
        node.Modifiers.Any(SyntaxKind.PartialKeyword);

    private static string KeyOf(ClassModel cls)
    {
        var types = cls.Node.AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .Select(t => $"{t.Identifier.Text}`{t.Arity}")
            .Reverse();
        return cls.Namespace + "::" + string.Join(".", types);
    }
}
