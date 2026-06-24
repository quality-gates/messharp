namespace MessSharp.Model;

public interface ISourceFileParser
{
    SourceFile ParseFile(string path);
}
