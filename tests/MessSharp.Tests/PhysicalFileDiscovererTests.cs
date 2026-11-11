using MessSharp.Runner;
using Xunit;

namespace MessSharp.Tests;

public class PhysicalFileDiscovererTests
{
    [Theory]
    [InlineData("TeamTests", "parent")]
    [InlineData("TeamTests", "test")]
    [InlineData("tEaMtEsTs", "nested")]
    [InlineData("Tests", "file")]
    public void IgnoreTests_SkipsFilesUnderTestDirectoryForAnyInput(string testDirectory, string inputKind)
    {
        var root = Directory.CreateTempSubdirectory("messharp-ancestor-");
        try
        {
            var test = Directory.CreateDirectory(Path.Combine(root.FullName, testDirectory));
            var nested = Directory.CreateDirectory(Path.Combine(test.FullName, "Nested"));
            var file = Path.Combine(nested.FullName, "Helper.cs");
            File.WriteAllText(file, "class Helper { }");
            var input = inputKind switch
            {
                "parent" => root.FullName,
                "test" => test.FullName,
                "nested" => nested.FullName,
                _ => file,
            };
            var discoverer = new PhysicalFileDiscoverer();

            Assert.Empty(discoverer.Discover(new[] { input }, new[] { ".cs" }, Array.Empty<string>(), true));
            Assert.Single(discoverer.Discover(new[] { input }, new[] { ".cs" }, Array.Empty<string>(), false));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Fact]
    public void IgnoreTests_PreservesFiltersDuplicatesAndMissingPaths()
    {
        var root = Directory.CreateTempSubdirectory("messharp-ordinary-");
        try
        {
            var ordinary = Directory.CreateDirectory(Path.Combine(root.FullName, "TeamTestsExtra"));
            var test = Directory.CreateDirectory(Path.Combine(root.FullName, "TeamTests"));
            var file = Path.Combine(ordinary.FullName, "Helper.cs");
            var excluded = Path.Combine(ordinary.FullName, "Excluded.cs");
            File.WriteAllText(file, "class Helper { }");
            File.WriteAllText(excluded, "class Excluded { }");
            File.WriteAllText(Path.Combine(ordinary.FullName, "HelperTests.cs"), "class HelperTests { }");
            File.WriteAllText(Path.Combine(ordinary.FullName, "Other.txt"), "text");
            var discoverer = new PhysicalFileDiscoverer();
            var suffixes = new[] { ".cs" };
            var exclude = new[] { "Excluded.cs" };

            Assert.Equal(new[] { file }, discoverer.Discover(new[] { ordinary.FullName, file }, suffixes, exclude, true));
            Assert.Empty(discoverer.Discover(new[] { file }, new[] { ".txt" }, exclude, true));
            Assert.Empty(discoverer.Discover(new[] { excluded }, suffixes, exclude, true));
            var missing = Path.Combine(test.FullName, "Missing.cs");
            Assert.Throws<FileNotFoundException>(() => discoverer.Discover(new[] { missing }, suffixes, exclude, true));
            Assert.Throws<FileNotFoundException>(() => discoverer.Discover(new[] { file, missing }, suffixes, exclude, true));
            Assert.Throws<FileNotFoundException>(() => discoverer.Discover(new[] { missing, file }, suffixes, exclude, true));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Fact]
    public void IgnoreTests_UsesLexicalSymlinkNameForExplicitRoots()
    {
        var root = Directory.CreateTempSubdirectory("messharp-lexical-");
        try
        {
            var target = Directory.CreateDirectory(Path.Combine(root.FullName, "TargetTests"));
            var ordinary = Directory.CreateDirectory(Path.Combine(root.FullName, "Ordinary"));
            File.WriteAllText(Path.Combine(target.FullName, "Helper.cs"), "class Helper { }");
            File.WriteAllText(Path.Combine(ordinary.FullName, "Helper.cs"), "class Helper { }");
            var ordinaryLink = Path.Combine(root.FullName, "Link");
            var testLink = Path.Combine(root.FullName, "LinkTests");
            Directory.CreateSymbolicLink(ordinaryLink, target.FullName);
            Directory.CreateSymbolicLink(testLink, ordinary.FullName);
            var discoverer = new PhysicalFileDiscoverer();
            var suffixes = new[] { ".cs" };

            Assert.Equal(new[] { Path.Combine(ordinaryLink, "Helper.cs") },
                discoverer.Discover(new[] { ordinaryLink }, suffixes, Array.Empty<string>(), true));
            Assert.Empty(discoverer.Discover(new[] { testLink }, suffixes, Array.Empty<string>(), true));
            Assert.Single(discoverer.Discover(new[] { testLink }, suffixes, Array.Empty<string>(), false));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

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

    [Fact]
    public void Discover_CaseDifferentiatedFiles_BothDiscoveredOnCaseSensitiveFilesystem()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-case-");
        try
        {
            var fileUpper = Path.Combine(directory.FullName, "Foo.cs");
            var fileLower = Path.Combine(directory.FullName, "foo.cs");
            File.WriteAllText(fileUpper, "class Aa { }");
            File.WriteAllText(fileLower, "class Bb { }");

            // Verify the filesystem is case-sensitive:
            if (File.ReadAllText(fileUpper) != "class Aa { }")
            {
                // Filesystem is case-insensitive; skip assertion for this environment
                return;
            }

            var discoverer = new PhysicalFileDiscoverer();
            var discovered = discoverer.Discover(
                new[] { directory.FullName },
                new[] { ".cs" },
                Array.Empty<string>(),
                ignoreTests: false);

            Assert.Equal(2, discovered.Count);
            Assert.Contains(fileUpper, discovered);
            Assert.Contains(fileLower, discovered);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void Discover_ExplicitCaseDifferentiatedFiles_BothDiscoveredOnCaseSensitiveFilesystem()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-case-explicit-");
        try
        {
            var fileUpper = Path.Combine(directory.FullName, "Foo.cs");
            var fileLower = Path.Combine(directory.FullName, "foo.cs");
            File.WriteAllText(fileUpper, "class Aa { }");
            File.WriteAllText(fileLower, "class Bb { }");

            if (File.ReadAllText(fileUpper) != "class Aa { }")
            {
                return;
            }

            var discoverer = new PhysicalFileDiscoverer();
            var discovered = discoverer.Discover(
                new[] { fileUpper, fileLower },
                new[] { ".cs" },
                Array.Empty<string>(),
                ignoreTests: false);

            Assert.Equal(2, discovered.Count);
            Assert.Contains(fileUpper, discovered);
            Assert.Contains(fileLower, discovered);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void Discover_DuplicateExplicitPaths_Deduplicated()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-dedupe-");
        try
        {
            var file = Path.Combine(directory.FullName, "File.cs");
            File.WriteAllText(file, "class File { }");

            var discoverer = new PhysicalFileDiscoverer();
            var discovered = discoverer.Discover(
                new[] { file, file, Path.Combine(directory.FullName, ".", "File.cs") },
                new[] { ".cs" },
                Array.Empty<string>(),
                ignoreTests: false);

            Assert.Single(discovered);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void Discover_CaseInsensitiveComparer_DedupesCaseDifferences()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-case-ignore-");
        try
        {
            var fileUpper = Path.Combine(directory.FullName, "Foo.cs");
            var fileLower = Path.Combine(directory.FullName, "foo.cs");
            File.WriteAllText(fileUpper, "class Aa { }");
            File.WriteAllText(fileLower, "class Bb { }");

            if (File.ReadAllText(fileUpper) != "class Aa { }")
            {
                return;
            }

            var discoverer = new PhysicalFileDiscoverer(StringComparer.OrdinalIgnoreCase);
            var discovered = discoverer.Discover(
                new[] { fileUpper, fileLower },
                new[] { ".cs" },
                Array.Empty<string>(),
                ignoreTests: false);

            Assert.Single(discovered);
            Assert.Equal(fileUpper, discovered[0]);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void Discover_CaseSensitiveComparer_PreservesCaseDifferences()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-case-preserve-");
        try
        {
            var fileUpper = Path.Combine(directory.FullName, "Foo.cs");
            var fileLower = Path.Combine(directory.FullName, "foo.cs");
            File.WriteAllText(fileUpper, "class Aa { }");
            File.WriteAllText(fileLower, "class Bb { }");

            if (File.ReadAllText(fileUpper) != "class Aa { }")
            {
                return;
            }

            var discoverer = new PhysicalFileDiscoverer(StringComparer.Ordinal);
            var discovered = discoverer.Discover(
                new[] { fileUpper, fileLower },
                new[] { ".cs" },
                Array.Empty<string>(),
                ignoreTests: false);

            Assert.Equal(2, discovered.Count);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void Discover_SortsFilesDeterministicallyWhenDifferingOnlyInCase()
    {
        var directory = Directory.CreateTempSubdirectory("messharp-case-sort-");
        try
        {
            var fileUpper = Path.Combine(directory.FullName, "Foo.cs");
            var fileLower = Path.Combine(directory.FullName, "foo.cs");
            File.WriteAllText(fileUpper, "class Aa { }");
            File.WriteAllText(fileLower, "class Bb { }");

            if (File.ReadAllText(fileUpper) != "class Aa { }")
            {
                return;
            }

            var discoverer = new PhysicalFileDiscoverer(StringComparer.Ordinal);
            var discovered = discoverer.Discover(
                new[] { fileLower, fileUpper },
                new[] { ".cs" },
                Array.Empty<string>(),
                ignoreTests: false);

            Assert.Equal(2, discovered.Count);
            // Ordinal: 'F' (70) < 'f' (102), so Foo.cs is ordered before foo.cs
            Assert.Equal(fileUpper, discovered[0]);
            Assert.Equal(fileLower, discovered[1]);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }
}


