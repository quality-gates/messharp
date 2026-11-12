namespace MessSharp.Tests;

internal static class ExplicitnessExpectations
{
    internal static (string, string, int, string) Input(string owner, int line, string cause = "uses Console.Read") =>
        (owner, "ImplicitInput", line, $"The method {owner}() has an implicit input: {cause}.");

    internal static (string, string, int, string) ConstructorInput(string owner, int line) =>
        (owner, "ImplicitInput", line, $"The constructor {owner}() has an implicit input: uses Console.Read.");

    internal static (string, string, int, string) Output(string owner, int line, string cause) =>
        (owner, "ImplicitOutput", line, $"The method {owner}() has an implicit output: {cause}.");

    internal static (string, string, int, string) Unknown(string owner, int line, string cause) =>
        (owner, "UnknownEffect", line, $"The method {owner}() has an unknown effect: {cause}.");
}
