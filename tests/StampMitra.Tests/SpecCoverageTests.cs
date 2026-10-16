using System.Text.Json;
using System.Text.RegularExpressions;

namespace StampMitra.Tests;

/// <summary>
/// Fails when an operation in spec/openapi.json has no SDK method. Scans the SDK sources for the
/// METHOD + route literals passed to the transport. Also runnable via scripts/check-spec-coverage.sh.
/// </summary>
[Trait("Category", "SpecCoverage")]
public sealed class SpecCoverageTests
{
    private static readonly Regex Placeholder = new(@"\{[^}]+\}");

    public static IReadOnlyList<string> SpecOperations()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "spec", "openapi.json")));
        var ops = new List<string>();
        foreach (var path in doc.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var m in path.Value.EnumerateObject())
            {
                if (m.Name is "get" or "post" or "put" or "patch" or "delete")
                {
                    ops.Add(m.Name.ToUpperInvariant() + " " + Placeholder.Replace(path.Name, "{}"));
                }
            }
        }

        ops.Sort(StringComparer.Ordinal);
        return ops;
    }

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StampMitra.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    public static IReadOnlyList<string> SdkOperations()
    {
        var rx = new Regex(@"HttpMethod\.(Get|Post|Put|Patch|Delete),\s*""(/v1/[^""]*)""");
        var ops = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(RepoRoot(), "src", "StampMitra", "Resources"), "*.cs"))
        {
            foreach (Match m in rx.Matches(File.ReadAllText(file)))
            {
                ops.Add(m.Groups[1].Value.ToUpperInvariant() + " " + Placeholder.Replace(m.Groups[2].Value, "{}"));
            }
        }

        return ops.ToList();
    }

    [Fact]
    public void EverySpecOperationHasAnSdkMethod()
    {
        var spec = SpecOperations();
        Assert.Equal(31, spec.Count);
        var missing = spec.Except(SdkOperations()).ToList();
        Assert.True(missing.Count == 0, "Spec operations without an SDK method:\n  " + string.Join("\n  ", missing));
    }

    [Fact]
    public void SdkDoesNotCallRoutesOutsideTheSpec()
    {
        var extra = SdkOperations().Except(SpecOperations()).ToList();
        Assert.True(extra.Count == 0, "SDK routes not in the spec:\n  " + string.Join("\n  ", extra));
    }
}

/// <summary>Keeps README.md honest: every csharp block must appear verbatim in ReadmeSnippets.cs, which is compiled.</summary>
public sealed class ReadmeSnippetsTests
{
    private static string Norm(string s) => Regex.Replace(s, @"\s+", string.Empty);

    [Fact]
    public void EveryReadmeCodeBlockIsCompiled()
    {
        var root = SpecCoverageTests.RepoRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var snippets = Norm(File.ReadAllText(Path.Combine(root, "tests", "StampMitra.Tests", "ReadmeSnippets.cs")));
        var blocks = Regex.Matches(readme, "```csharp\n(.*?)```", RegexOptions.Singleline);
        Assert.True(blocks.Count >= 8);
        foreach (Match m in blocks)
        {
            var code = string.Join("\n", m.Groups[1].Value.Split('\n').Where(l => !Regex.IsMatch(l, @"^using StampMitra[\w.]*;\s*$")));
            Assert.True(snippets.Contains(Norm(code), StringComparison.Ordinal), "README block is not in ReadmeSnippets.cs:\n" + code);
        }
    }
}
