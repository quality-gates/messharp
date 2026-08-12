using System.Reflection;

namespace MessSharp;

public static class BuildInfo
{
    public static string Version { get; } =
        typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";
}
