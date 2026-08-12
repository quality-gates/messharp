using System.Reflection;

namespace MessSharp;

/// <summary>Exposes build metadata through the public application seam.</summary>
public static class BuildInfo
{
    public static string Version { get; } =
        typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";
}
