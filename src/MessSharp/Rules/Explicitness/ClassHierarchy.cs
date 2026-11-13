using MessSharp.Model;

namespace MessSharp.Rules.Explicitness;

/// <summary>A same-file class with every <c>partial</c> declaration it has, followed by the same-file classes it derives from.</summary>
internal static class ClassHierarchy
{
    /// <summary>Each declaration of <paramref name="cls"/> and of every same-file base class, nearest first.</summary>
    public static IEnumerable<ClassModel> Chain(ClassModel cls)
    {
        var seen = new HashSet<string>();
        for (var name = cls.Name; seen.Add(name);)
        {
            var parts = Parts(cls.File, name);
            foreach (var part in parts) yield return part;
            if (parts.SelectMany(part => part.BaseTypes).FirstOrDefault() is not { } baseType) yield break;
            name = TypeNames.Simple(baseType);
        }
    }

    private static List<ClassModel> Parts(SourceFile file, string name) => file.Classes.Where(candidate => candidate.Name == name).ToList();
}
