namespace MessSharp.Model;

public sealed class RoslynSourceFileParser : ISourceFileParser
{
    public SourceFile ParseFile(string path) => ModelBuilder.ParseFile(path);
}
