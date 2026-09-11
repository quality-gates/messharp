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

    [Fact]
    public void Discover_DirectorySymlinkCycle_DoesNotDescendSymlink()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-symlink-");
        var link = Path.Combine(directory.FullName, "link");
        try
        {
            var file = Path.Combine(directory.FullName, "X.cs");
            File.WriteAllText(file, "class X { }");
            Directory.CreateSymbolicLink(link, directory.FullName);

            var discoverer = new PhysicalFileDiscoverer();
            var discovered = discoverer.Discover(
                new[] { directory.FullName },
                new[] { ".cs" },
                Array.Empty<string>(),
                ignoreTests: false);

            Assert.Single(discovered);
            Assert.Equal(Path.GetFullPath(file), Path.GetFullPath(discovered[0]));
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link, recursive: false);
            }
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void Discover_MutualDirectorySymlinkCycle_DoesNotDescendDirectorySymlink()
    {
        var root = Directory.CreateTempSubdirectory("messharp-mutual-symlink-");
        var dirA = Path.Combine(root.FullName, "dirA");
        var dirB = Path.Combine(root.FullName, "dirB");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);

        var fileA = Path.Combine(dirA, "A.cs");
        var fileB = Path.Combine(dirB, "B.cs");
        File.WriteAllText(fileA, "class A { }");
        File.WriteAllText(fileB, "class B { }");

        var linkToB = Path.Combine(dirA, "toB");
        var linkToA = Path.Combine(dirB, "toA");
        Directory.CreateSymbolicLink(linkToB, dirB);
        Directory.CreateSymbolicLink(linkToA, dirA);

        try
        {
            var discoverer = new PhysicalFileDiscoverer();
            var discovered = discoverer.Discover(
                new[] { root.FullName },
                new[] { ".cs" },
                Array.Empty<string>(),
                ignoreTests: false);

            Assert.Equal(2, discovered.Count);
            Assert.Contains(fileA, discovered);
            Assert.Contains(fileB, discovered);
        }
        finally
        {
            if (Directory.Exists(linkToB)) Directory.Delete(linkToB, recursive: false);
            if (Directory.Exists(linkToA)) Directory.Delete(linkToA, recursive: false);
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Fact]
    public void Discover_DirectDirectorySymlinkAsRoot_DiscoversContainedFiles()
    {
        var tempDir = Directory.CreateTempSubdirectory("messharp-symlink-root-");
        var realDir = Path.Combine(tempDir.FullName, "real");
        var linkDir = Path.Combine(tempDir.FullName, "link");
        Directory.CreateDirectory(realDir);

        var file = Path.Combine(realDir, "Target.cs");
        File.WriteAllText(file, "class Target { }");
        Directory.CreateSymbolicLink(linkDir, realDir);

        try
        {
            var discoverer = new PhysicalFileDiscoverer();
            var discovered = discoverer.Discover(
                new[] { linkDir },
                new[] { ".cs" },
                Array.Empty<string>(),
                ignoreTests: false);

            Assert.Single(discovered);
            Assert.Equal(Path.Combine(linkDir, "Target.cs"), discovered[0]);
        }
        finally
        {
            if (Directory.Exists(linkDir)) Directory.Delete(linkDir, recursive: false);
            Directory.Delete(tempDir.FullName, recursive: true);
        }
    }

    [Fact]
    public void Discover_BrokenSymlink_IgnoredGracefully()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-broken-symlink-");
        var brokenLink = Path.Combine(directory.FullName, "broken");
        try
        {
            var file = Path.Combine(directory.FullName, "Valid.cs");
            File.WriteAllText(file, "class Valid { }");
            Directory.CreateSymbolicLink(brokenLink, Path.Combine(directory.FullName, "nonexistent"));

            var discoverer = new PhysicalFileDiscoverer();
            var discovered = discoverer.Discover(
                new[] { directory.FullName },
                new[] { ".cs" },
                Array.Empty<string>(),
                ignoreTests: false);

            Assert.Single(discovered);
            Assert.Equal(Path.GetFullPath(file), Path.GetFullPath(discovered[0]));
        }
        finally
        {
            if (File.Exists(brokenLink) || Directory.Exists(brokenLink))
            {
                File.Delete(brokenLink);
            }
            Directory.Delete(directory.FullName, recursive: true);
        }
    }
}


