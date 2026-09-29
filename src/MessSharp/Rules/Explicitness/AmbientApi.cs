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

    /// <summary>The "Type.Member" name when the node uses a listed member, otherwise null.</summary>
    public string? UseAt(SyntaxNode node)
    {
        if (node is not MemberAccessExpressionSyntax access || RightmostName(access.Expression) is not { } type)
            return null;
        var name = type + "." + access.Name.Identifier.Text;
        return _members.Contains(name) ? name : null;
    }

    private static string? RightmostName(ExpressionSyntax expr) => expr switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
        AliasQualifiedNameSyntax aq => aq.Name.Identifier.Text,
        QualifiedNameSyntax qn => qn.Right.Identifier.Text,
        _ => null,
    };

    /// <summary>
    /// True for <c>new Random()</c> without a seed, including the target-typed
    /// <c>Random r = new();</c>: its values come from the clock.
    /// </summary>
    public static bool IsUnseededRandom(SyntaxNode node) =>
        node is BaseObjectCreationExpressionSyntax creation
        && CreatedType(creation) is { } type
        && RightmostName(type) == "Random"
        && (creation.ArgumentList?.Arguments.Count ?? 0) == 0;

    private static TypeSyntax? CreatedType(BaseObjectCreationExpressionSyntax creation) => creation switch
    {
        ObjectCreationExpressionSyntax explicitType => explicitType.Type,
        ImplicitObjectCreationExpressionSyntax implicitType => DeclaredType(implicitType),
        _ => null,
    };

    /// <summary>The declared type a target-typed <c>new()</c> initializes, when syntax shows it.</summary>
    private static TypeSyntax? DeclaredType(ImplicitObjectCreationExpressionSyntax creation) =>
        creation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } }
            ? WithoutNullable(declaration.Type)
            : null;

    private static TypeSyntax WithoutNullable(TypeSyntax type) =>
        type is NullableTypeSyntax nullable ? nullable.ElementType : type;
}
