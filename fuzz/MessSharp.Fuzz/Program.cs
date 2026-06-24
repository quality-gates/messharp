using System.Text;
using System.Xml;
using MessSharp.Model;
using MessSharp.Rule;
using MessSharp.RuleSet;
using SharpFuzz;
using RuleSetType = MessSharp.Rule.RuleSet;

namespace MessSharp.Fuzz;

public static class Program
{
    private static readonly Lazy<IReadOnlyList<RuleSetType>> CSharpRules = new(
        () => new Loader { MaxPriority = 1 }.Load("csharp"));

    public static void Main(string[] args)
    {
        var target = args.Length > 0 ? args[0] : "source";

        switch (target)
        {
            case "source":
                Fuzzer.OutOfProcess.Run(FuzzSource);
                break;
            case "ruleset":
                Fuzzer.OutOfProcess.Run(FuzzRuleset);
                break;
            default:
                Console.Error.WriteLine("usage: MessSharp.Fuzz <source|ruleset>");
                Environment.ExitCode = 1;
                break;
        }
    }

    private static void FuzzSource(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        var source = reader.ReadToEnd();

        if (source.Length > 128_000)
        {
            return;
        }

        var file = ModelBuilder.Parse("fuzz.cs", source);
        Engine.Analyze(file, CSharpRules.Value);
    }

    private static void FuzzRuleset(Stream stream)
    {
        var path = Path.Combine(Path.GetTempPath(), $"messharp-ruleset-fuzz-{Environment.ProcessId}.xml");

        try
        {
            using (var file = File.Create(path))
            {
                stream.CopyTo(file);
            }

            new Loader { MaxPriority = 1 }.Load(path);
        }
        catch (XmlException)
        {
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (PathTooLongException)
        {
        }
        finally
        {
            File.Delete(path);
        }
    }
}
