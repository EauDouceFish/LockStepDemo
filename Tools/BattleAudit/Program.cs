using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class Program
{
    private static readonly string[] BattlePathTokens =
    {
        $"{Path.DirectorySeparatorChar}Mugen{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}Lockstep{Path.DirectorySeparatorChar}",
    };

    private static readonly string[] IkemenCoreFiles =
    {
        "anim.go",
        "bytecode.go",
        "char.go",
        "compiler.go",
        "compiler_functions.go",
        "input.go",
        "system.go",
        "stage.go",
        "fight.go",
        "camera.go",
        "image.go",
        "sound.go",
    };

    private static int Main(string[] args)
    {
        string repoRoot = FindRepoRoot(Environment.CurrentDirectory);
        string logicRoot = GetArg(args, "--logic", Path.Combine(repoRoot, "Assets", "Logic"));
        string ikemenRoot = GetArg(args, "--ikemen", Path.Combine(repoRoot, "..", "MugenSource", "_reference", "Ikemen-GO", "src"));
        string outputRoot = GetArg(args, "--out", Path.Combine(repoRoot, "Docs", "Generated"));

        Directory.CreateDirectory(outputRoot);

        List<CSharpFunctionInfo> csharpFunctions = ScanCSharp(logicRoot, repoRoot);
        List<GoFunctionInfo> goFunctions = ScanGo(ikemenRoot);
        List<AuditRow> auditRows = BuildAudit(csharpFunctions, goFunctions);

        WriteCSharpList(Path.Combine(outputRoot, "battle_logic_functions_csharp.tsv"), csharpFunctions);
        WriteGoList(Path.Combine(outputRoot, "ikemen_battle_functions_go.tsv"), goFunctions);
        WriteAudit(Path.Combine(outputRoot, "battle_function_audit.md"), auditRows, logicRoot, ikemenRoot);
        WriteMissingIkemen(Path.Combine(outputRoot, "ikemen_functions_not_name_matched.md"), csharpFunctions, goFunctions);

        Console.WriteLine($"C# battle functions: {csharpFunctions.Count}");
        Console.WriteLine($"Ikemen GO functions: {goFunctions.Count}");
        Console.WriteLine($"Output: {outputRoot}");
        return 0;
    }

    private static string FindRepoRoot(string start)
    {
        DirectoryInfo? current = new DirectoryInfo(start);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return start;
    }

    private static string GetArg(string[] args, string name, string defaultValue)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(args[index], name))
            {
                return Path.GetFullPath(args[index + 1]);
            }
        }
        return Path.GetFullPath(defaultValue);
    }

    private static List<CSharpFunctionInfo> ScanCSharp(string logicRoot, string repoRoot)
    {
        List<CSharpFunctionInfo> result = new List<CSharpFunctionInfo>();
        foreach (string path in Directory.EnumerateFiles(logicRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(IsBattleLogicFile)
                     .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))
        {
            string text = File.ReadAllText(path);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text, path: path);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            foreach (BaseMethodDeclarationSyntax method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
            {
                FileLinePositionSpan span = tree.GetLineSpan(method.Span);
                string typeName = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "<global>";
                string namespaceName = method.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "";
                string methodName = MethodName(method);
                string signature = NormalizeWhitespace(method switch
                {
                    MethodDeclarationSyntax declaration => declaration.ReturnType + " " + declaration.Identifier.ValueText + declaration.ParameterList,
                    ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText + constructor.ParameterList,
                    _ => method.ToString().Split('{')[0].Trim()
                });
                string leadingTrivia = method.GetLeadingTrivia().ToFullString();
                string? explicitReference = ExtractReference(leadingTrivia);
                string status = explicitReference != null ? "annotated" : "needs-reference";

                result.Add(new CSharpFunctionInfo(
                    ToRepoPath(path, repoRoot),
                    span.StartLinePosition.Line + 1,
                    namespaceName,
                    typeName,
                    methodName,
                    signature,
                    explicitReference,
                    status));
            }
        }
        return result;
    }

    private static bool IsBattleLogicFile(string path)
    {
        string normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        if (normalized.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return BattlePathTokens.Any(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string MethodName(BaseMethodDeclarationSyntax method)
    {
        return method switch
        {
            MethodDeclarationSyntax declaration => declaration.Identifier.ValueText,
            ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
            DestructorDeclarationSyntax destructor => "~" + destructor.Identifier.ValueText,
            OperatorDeclarationSyntax operatorDeclaration => "operator " + operatorDeclaration.OperatorToken.ValueText,
            ConversionOperatorDeclarationSyntax conversion => "operator " + conversion.Type,
            _ => method.Kind().ToString()
        };
    }

    private static string? ExtractReference(string leadingTrivia)
    {
        Match ikemenReference = Regex.Match(leadingTrivia, @"Ikemen reference:\s*(?<value>[^\r\n]+)", RegexOptions.IgnoreCase);
        if (ikemenReference.Success)
        {
            return ikemenReference.Groups["value"].Value.Trim();
        }

        Match projectSpecific = Regex.Match(leadingTrivia, @"Project-specific[^:]*:\s*(?<value>[^\r\n]+)", RegexOptions.IgnoreCase);
        if (projectSpecific.Success)
        {
            return "Project-specific: " + projectSpecific.Groups["value"].Value.Trim();
        }

        Match projectSpecificLine = Regex.Match(leadingTrivia, @"Project-specific[^\r\n]+", RegexOptions.IgnoreCase);
        if (projectSpecificLine.Success)
        {
            return projectSpecificLine.Value.Trim();
        }

        Match source = Regex.Match(leadingTrivia, @"Source:\s*(?<value>[^\r\n]+)", RegexOptions.IgnoreCase);
        if (source.Success)
        {
            return source.Groups["value"].Value.Trim();
        }

        Match ported = Regex.Match(leadingTrivia, @"Ported from Ikemen GO.*?(?<file>src/[A-Za-z0-9_./-]+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (ported.Success)
        {
            return ported.Groups["file"].Value.Trim();
        }

        return null;
    }

    private static List<GoFunctionInfo> ScanGo(string ikemenRoot)
    {
        List<GoFunctionInfo> result = new List<GoFunctionInfo>();
        if (!Directory.Exists(ikemenRoot))
        {
            return result;
        }

        Regex functionRegex = new Regex(@"^\s*func\s+(?:(?<receiver>\([^)]+\))\s*)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<params>[^)]*)\)", RegexOptions.Compiled);
        HashSet<string> core = new HashSet<string>(IkemenCoreFiles, StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(ikemenRoot, "*.go", SearchOption.TopDirectoryOnly)
                     .Where(file => core.Contains(Path.GetFileName(file)))
                     .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))
        {
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                Match match = functionRegex.Match(lines[index]);
                if (!match.Success)
                {
                    continue;
                }

                string receiver = match.Groups["receiver"].Value.Trim();
                string name = match.Groups["name"].Value.Trim();
                string parameters = match.Groups["params"].Value.Trim();
                result.Add(new GoFunctionInfo(
                    Path.GetFileName(path),
                    index + 1,
                    receiver,
                    name,
                    NormalizeWhitespace($"func {receiver} {name}({parameters})")));
            }
        }
        return result;
    }

    private static List<AuditRow> BuildAudit(List<CSharpFunctionInfo> csharpFunctions, List<GoFunctionInfo> goFunctions)
    {
        Dictionary<string, List<GoFunctionInfo>> goByName = goFunctions
            .GroupBy(static item => NormalizeName(item.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        List<AuditRow> rows = new List<AuditRow>();
        foreach (CSharpFunctionInfo function in csharpFunctions)
        {
            List<GoFunctionInfo> candidates = new List<GoFunctionInfo>();
            string normalized = NormalizeName(function.MethodName);
            if (goByName.TryGetValue(normalized, out List<GoFunctionInfo>? exact))
            {
                candidates.AddRange(exact);
            }
            foreach (GoFunctionInfo candidate in goFunctions)
            {
                string goName = NormalizeName(candidate.Name);
                if (goName.Length >= 4 && (normalized.Contains(goName, StringComparison.OrdinalIgnoreCase)
                    || goName.Contains(normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!candidates.Contains(candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            string status;
            string reference;
            string note;
            if (!string.IsNullOrWhiteSpace(function.ExplicitReference))
            {
                status = "annotated-needs-line-check";
                reference = function.ExplicitReference!;
                note = "Has source comment; line precision still needs manual verification.";
            }
            else if (candidates.Count > 0)
            {
                status = "auto-candidate";
                reference = string.Join("; ", candidates.Take(3).Select(static item => $"{item.File}:{item.Line}:{item.DisplayName}"));
                note = "Matched by name only; implementation equivalence not confirmed.";
            }
            else
            {
                status = "project-specific-or-unmatched";
                reference = "";
                note = "No Ikemen function name candidate found; needs project-specific rationale or manual map.";
            }

            rows.Add(new AuditRow(function, reference, status, note));
        }
        return rows;
    }

    private static void WriteCSharpList(string outputPath, List<CSharpFunctionInfo> functions)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("path\tline\tnamespace\ttype\tmethod\tsignature\tstatus\texplicit_reference");
        foreach (CSharpFunctionInfo item in functions)
        {
            builder.AppendLine($"{item.Path}\t{item.Line}\t{item.Namespace}\t{item.TypeName}\t{item.MethodName}\t{item.Signature}\t{item.Status}\t{item.ExplicitReference}");
        }
        File.WriteAllText(outputPath, builder.ToString(), Encoding.UTF8);
    }

    private static void WriteGoList(string outputPath, List<GoFunctionInfo> functions)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("file\tline\treceiver\tname\tsignature");
        foreach (GoFunctionInfo item in functions)
        {
            builder.AppendLine($"{item.File}\t{item.Line}\t{item.Receiver}\t{item.Name}\t{item.Signature}");
        }
        File.WriteAllText(outputPath, builder.ToString(), Encoding.UTF8);
    }

    private static void WriteAudit(string outputPath, List<AuditRow> rows, string logicRoot, string ikemenRoot)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# Battle Function Audit");
        builder.AppendLine();
        builder.AppendLine($"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"C# root: `{logicRoot}`");
        builder.AppendLine($"Ikemen root: `{ikemenRoot}`");
        builder.AppendLine();
        builder.AppendLine("Status meanings:");
        builder.AppendLine("- `annotated-needs-line-check`: function already carries an Ikemen source note, but exact line range still needs manual verification.");
        builder.AppendLine("- `auto-candidate`: matched by name only; this is not an equivalence claim.");
        builder.AppendLine("- `project-specific-or-unmatched`: no automatic Ikemen function candidate; add rationale or a manual reference.");
        builder.AppendLine();
        builder.AppendLine("| Status | C# Function | C# Location | Ikemen Candidate / Reference | Note |");
        builder.AppendLine("|---|---|---:|---|---|");
        foreach (AuditRow row in rows)
        {
            CSharpFunctionInfo item = row.Function;
            builder.AppendLine($"| {Escape(row.Status)} | `{Escape(item.TypeName)}.{Escape(item.MethodName)}` | `{Escape(item.Path)}:{item.Line}` | {Escape(row.Reference)} | {Escape(row.Note)} |");
        }
        File.WriteAllText(outputPath, builder.ToString(), Encoding.UTF8);
    }

    private static void WriteMissingIkemen(string outputPath, List<CSharpFunctionInfo> csharpFunctions, List<GoFunctionInfo> goFunctions)
    {
        HashSet<string> csharpNames = csharpFunctions
            .Select(static item => NormalizeName(item.MethodName))
            .Where(static item => item.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<GoFunctionInfo> missing = goFunctions
            .Where(item => !csharpNames.Contains(NormalizeName(item.Name)))
            .OrderBy(static item => item.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Line)
            .ToList();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# Ikemen Functions Not Name-Matched");
        builder.AppendLine();
        builder.AppendLine("This is a name-level gap list generated from selected Ikemen core battle files.");
        builder.AppendLine("It is not proof that a function is logically absent: some C# implementations are intentionally renamed or split.");
        builder.AppendLine("Each row still requires manual mapping to one of: implemented-equivalent, implemented-renamed, presentation-only, out-of-scope, or missing.");
        builder.AppendLine();
        builder.AppendLine("| Ikemen Function | Location | Receiver | Suggested Review Area |");
        builder.AppendLine("|---|---:|---|---|");
        foreach (GoFunctionInfo item in missing)
        {
            builder.AppendLine($"| `{Escape(item.Name)}` | `{Escape(item.File)}:{item.Line}` | `{Escape(item.Receiver)}` | {Escape(ReviewArea(item.File, item.Name))} |");
        }
        File.WriteAllText(outputPath, builder.ToString(), Encoding.UTF8);
    }

    private static string ReviewArea(string file, string name)
    {
        string lowerFile = file.ToLowerInvariant();
        string lowerName = name.ToLowerInvariant();
        if (lowerFile == "char.go" && (lowerName.Contains("hit") || lowerName.Contains("guard") || lowerName.Contains("target") || lowerName.Contains("juggle")))
        {
            return "hit/guard/target/juggle";
        }
        if (lowerFile == "char.go" && (lowerName.Contains("state") || lowerName.Contains("action") || lowerName.Contains("ctrl")))
        {
            return "state machine";
        }
        if (lowerFile == "bytecode.go" || lowerFile.StartsWith("compiler", StringComparison.OrdinalIgnoreCase))
        {
            return "expression/controller compiler";
        }
        if (lowerFile == "anim.go")
        {
            return "animation/resource ownership";
        }
        if (lowerFile == "image.go")
        {
            return "palette/palfx/render resource";
        }
        if (lowerFile == "sound.go")
        {
            return "sound/presentation event";
        }
        if (lowerFile == "input.go")
        {
            return "command/input";
        }
        if (lowerFile == "system.go")
        {
            return "round/system/entity lifecycle";
        }
        return "manual review";
    }

    private static string ToRepoPath(string path, string repoRoot)
    {
        string relative = Path.GetRelativePath(repoRoot, path);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string NormalizeName(string value)
    {
        return Regex.Replace(value, @"[^A-Za-z0-9]", "").ToLowerInvariant();
    }

    private static string Escape(string? value)
    {
        return (value ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }
}

internal sealed record CSharpFunctionInfo(
    string Path,
    int Line,
    string Namespace,
    string TypeName,
    string MethodName,
    string Signature,
    string? ExplicitReference,
    string Status);

internal sealed record GoFunctionInfo(
    string File,
    int Line,
    string Receiver,
    string Name,
    string Signature)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Receiver) ? Name : $"{Receiver}.{Name}";
}

internal sealed record AuditRow(
    CSharpFunctionInfo Function,
    string Reference,
    string Status,
    string Note);
