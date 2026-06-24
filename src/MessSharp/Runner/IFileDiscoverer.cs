namespace MessSharp.Runner;

public interface IFileDiscoverer
{
    List<string> Discover(
        IReadOnlyList<string> paths,
        IReadOnlyList<string> suffixes,
        IReadOnlyList<string> exclude,
        bool ignoreTests);
}
