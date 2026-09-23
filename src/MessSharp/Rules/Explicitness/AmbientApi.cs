using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MessSharp.Rules.Explicitness;

/// <summary>
/// Well-known .NET members that read from or write to the world outside the
/// program: the clock, the environment, the console, the file system.
/// Matched by syntax as <c>Type.Member</c>, where Type is the rightmost name
/// of the receiver, so <c>System.Console.WriteLine</c> matches too.
/// </summary>
internal sealed class AmbientApi
{
    public static readonly AmbientApi Inputs = new(
    [
        "DateTime.Now", "DateTime.UtcNow", "DateTime.Today",
        "DateTimeOffset.Now", "DateTimeOffset.UtcNow",
        "Environment.TickCount", "Environment.TickCount64", "Environment.CurrentDirectory",
        "Environment.GetEnvironmentVariable", "Environment.GetEnvironmentVariables",
        "Environment.GetCommandLineArgs", "Environment.CommandLine",
        "Environment.MachineName", "Environment.UserName",
        "Stopwatch.GetTimestamp", "Stopwatch.StartNew",
        "Guid.NewGuid", "Random.Shared",
        "RandomNumberGenerator.GetBytes", "RandomNumberGenerator.GetInt32",
        "Console.Read", "Console.ReadLine", "Console.ReadKey", "Console.In", "Console.KeyAvailable",
        "File.Exists", "File.ReadAllText", "File.ReadAllLines", "File.ReadAllBytes", "File.ReadLines",
        "File.OpenRead", "File.OpenText", "File.ReadAllTextAsync", "File.ReadAllLinesAsync",
        "File.ReadAllBytesAsync", "File.GetLastWriteTime",
        "Directory.Exists", "Directory.GetFiles", "Directory.GetDirectories", "Directory.EnumerateFiles",
        "Directory.EnumerateDirectories", "Directory.EnumerateFileSystemEntries", "Directory.GetCurrentDirectory",
        "Process.GetCurrentProcess", "Process.GetProcesses",
    ]);

    public static readonly AmbientApi Outputs = new(
    [
        "Console.Write", "Console.WriteLine", "Console.Out", "Console.Error", "Console.Clear",
        "Console.SetOut", "Console.SetError", "Console.ForegroundColor", "Console.BackgroundColor",
        "Console.ResetColor",
        "Debug.Write", "Debug.WriteLine", "Debug.Print", "Debug.WriteIf", "Debug.WriteLineIf", "Debug.Fail",
        "Trace.Write", "Trace.WriteLine", "Trace.TraceInformation", "Trace.TraceWarning", "Trace.TraceError",
        "File.WriteAllText", "File.WriteAllLines", "File.WriteAllBytes", "File.WriteAllTextAsync",
        "File.WriteAllLinesAsync", "File.WriteAllBytesAsync", "File.AppendAllText", "File.AppendAllLines",
        "File.AppendAllTextAsync", "File.AppendText", "File.Create", "File.CreateText", "File.Delete",
        "File.Move", "File.Copy", "File.Replace", "File.OpenWrite", "File.SetAttributes",
        "Directory.CreateDirectory", "Directory.Delete", "Directory.Move", "Directory.SetCurrentDirectory",
        "Environment.SetEnvironmentVariable", "Environment.Exit", "Environment.FailFast", "Environment.ExitCode",
        "Process.Start",
    ]);

    private readonly HashSet<string> _members;

    private AmbientApi(IEnumerable<string> members) =>
        _members = new HashSet<string>(members, StringComparer.Ordinal);

    /// <summary>Each use of a listed member in the body, as the node and its "Type.Member" name.</summary>
    public IEnumerable<(SyntaxNode Site, string Name)> UsesIn(SyntaxNode body)
    {
        foreach (var access in body.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
        {
            if (RightmostName(access.Expression) is not { } type) continue;
            var name = type + "." + access.Name.Identifier.Text;
            if (_members.Contains(name))
                yield return (access, name);
        }
    }

    private static string? RightmostName(ExpressionSyntax expr) => expr switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
        AliasQualifiedNameSyntax aq => aq.Name.Identifier.Text,
        QualifiedNameSyntax qn => qn.Right.Identifier.Text,
        _ => null,
    };

    /// <summary>Each <c>new Random()</c> without a seed: its values come from the clock.</summary>
    public static IEnumerable<SyntaxNode> UnseededRandoms(SyntaxNode body) =>
        body.DescendantNodesAndSelf().OfType<ObjectCreationExpressionSyntax>()
            .Where(c => RightmostName(c.Type) == "Random" && (c.ArgumentList?.Arguments.Count ?? 0) == 0);
}
