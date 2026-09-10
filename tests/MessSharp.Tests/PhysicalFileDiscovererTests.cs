using MessSharp.Runner;
using Xunit;

namespace MessSharp.Tests;

public class PhysicalFileDiscovererTests
{
    [Fact]
    public void ExplicitFile_UsesSameFiltersAsDirectoryWalk()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-discoverer-");
        var file = Path.Combine(directory.FullName, "FixtureTests.cs");
        File.WriteAllText(file, "public class FixtureTests { }");

        try
        {
            var discoverer = new PhysicalFileDiscoverer();
            var cases = new[]
            {
                (suffixes: new[] { ".xaml" }, exclude: Array.Empty<string>(), ignoreTests: false),
                (suffixes: new[] { ".cs" }, exclude: new[] { "FixtureTests.cs" }, ignoreTests: false),
                (suffixes: new[] { ".cs" }, exclude: Array.Empty<string>(), ignoreTests: true),
            };

            foreach (var filterCase in cases)
            {
                var walked = discoverer.Discover(
                    new[] { directory.FullName },
                    filterCase.suffixes,
                    filterCase.exclude,
                    filterCase.ignoreTests);
                var explicitFile = discoverer.Discover(
                    new[] { file },
                    filterCase.suffixes,
                    filterCase.exclude,
                    filterCase.ignoreTests);

                Assert.Empty(walked);
                Assert.Equal(walked, explicitFile);
            }
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }
}
