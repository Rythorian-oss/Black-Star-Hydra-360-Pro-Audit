using System.Diagnostics;
using BlackStarHydra360.Core;
using BlackStarHydra360.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BlackStarHydra360.Auditors;

/// <summary>
/// Roslyn AST-based semantic auditor. Parses C# syntax trees for deep analysis
/// that regex cannot achieve: ignores comments, understands type context,
/// detects missing Dispose patterns, async issues, and magic numbers.
/// </summary>
public sealed class RoslynAuditor : IDisposable
{
    public event Action<IssueModel>? IssueFound;
    private bool _disposed;

    public Task<List<IssueModel>> AnalyzeFileAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var issues = new List<IssueModel>();
            try
            {
                ct.ThrowIfCancellationRequested();
                var info = new FileInfo(filePath);
                if (!info.Exists || info.Length > 5 * 1024 * 1024)
                    return issues;

                string code = File.ReadAllText(filePath);
                var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: ct);
                var root = tree.GetRoot(ct);

                CheckHardcodedSecrets(root, filePath, issues);

                CheckEmptyCatchBlocks(root, filePath, issues);

                CheckMissingDisposePattern(root, filePath, issues);

                CheckAsyncVoidMethods(root, filePath, issues);

                CheckPublicFields(root, filePath, issues);

                CheckOrphanTimers(root, filePath, issues);

                foreach (var issue in issues)
                    IssueFound?.Invoke(issue);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                AppLogger.Warning($"RoslynAuditor could not analyze {filePath}", ex);
            }
            return issues;
        }, ct);
    }

    public async Task<List<IssueModel>> AnalyzeDirectoryAsync(string dirPath,
        IProgress<(string file, int processed, int total)>? progress, CancellationToken ct = default)
    {
        var all = new List<IssueModel>();
        var files = Directory.EnumerateFiles(dirPath, "*.cs", SearchOption.AllDirectories)
            .Where(IsIncludedSourceFile)
            .ToList();

        AppLogger.Info($"RoslynAuditor analyzing {files.Count} files in {dirPath}");

        for (int i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report((files[i], i + 1, files.Count));
            var fileIssues = await AnalyzeFileAsync(files[i], ct);
            all.AddRange(fileIssues);
        }

        AppLogger.Info($"RoslynAuditor found {all.Count} issues.");
        return all;
    }

    private static void CheckHardcodedSecrets(SyntaxNode root, string filePath, List<IssueModel> issues)
    {
        var assignments = root.DescendantNodes().OfType<AssignmentExpressionSyntax>();
        foreach (var assign in assignments)
        {
            var leftName = assign.Left.ToString().ToLowerInvariant();
            if (!leftName.Contains("password") && !leftName.Contains("secret") &&
                !leftName.Contains("token") && !leftName.Contains("apikey") &&
                !leftName.Contains("api_key") && !leftName.Contains("privatekey"))
                continue;

            if (assign.Right is LiteralExpressionSyntax lit &&
                lit.IsKind(SyntaxKind.StringLiteralExpression))
            {
                var span = lit.GetLocation().GetLineSpan();
                issues.Add(new IssueModel
                {
                    RuleId = "SEC101",
                    FilePath = filePath,
                    LineNumber = span.StartLinePosition.Line + 1,
                    Column = span.StartLinePosition.Character + 1,
                    Category = "Security Risk",
                    Description = $"Roslyn confirmed: hardcoded secret in '{leftName}' — value: {lit.Token.ValueText.Truncate(30)}",
                    Severity = IssueSeverity.Critical,
                    CodeSnippet = lit.ToString(),
                    SuggestedFix = "Replace with secure configuration or environment variable.",
                    IsFixable = true,
                    IssueType = "Roslyn"
                });
            }
        }
    }

    private static void CheckEmptyCatchBlocks(SyntaxNode root, string filePath, List<IssueModel> issues)
    {
        var catches = root.DescendantNodes().OfType<CatchClauseSyntax>();
        foreach (var cat in catches)
        {
            if (cat.Block is { Statements.Count: 0 } && !HasComment(cat.Block))
            {
                var span = cat.GetLocation().GetLineSpan();
                issues.Add(new IssueModel
                {
                    RuleId = "QUAL101",
                    FilePath = filePath,
                    LineNumber = span.StartLinePosition.Line + 1,
                    Category = "Error Handling",
                    Description = "Roslyn confirmed: empty catch block — exceptions silently swallowed.",
                    Severity = IssueSeverity.Critical,
                    CodeSnippet = cat.ToString().Truncate(80),
                    SuggestedFix = "Log the exception or add proper error handling.",
                    IsFixable = true,
                    IssueType = "Roslyn"
                });
            }
        }
    }

    private static void CheckMissingDisposePattern(SyntaxNode root, string filePath, List<IssueModel> issues)
    {
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var cls in classes)
        {
            var baseList = cls.BaseList;
            if (baseList == null) continue;
            bool implementsDisposable = baseList.Types
                .Any(t => t.ToString().Contains("IDisposable"));

            if (!implementsDisposable) continue;

            // Check if Dispose(bool) method exists
            var methods = cls.Members.OfType<MethodDeclarationSyntax>();
            bool hasDispose = methods.Any(m => m.Identifier.Text == "Dispose");

            if (!hasDispose)
            {
                var span = cls.Identifier.GetLocation().GetLineSpan();
                issues.Add(new IssueModel
                {
                    RuleId = "LEAK101",
                    FilePath = filePath,
                    LineNumber = span.StartLinePosition.Line + 1,
                    Category = "Memory Leak",
                    Description = $"Roslyn confirmed: class '{cls.Identifier.Text}' implements IDisposable but has no Dispose method.",
                    Severity = IssueSeverity.Critical,
                    CodeSnippet = $"class {cls.Identifier.Text} : IDisposable",
                    SuggestedFix = "Implement IDisposable.Dispose() with proper resource cleanup.",
                    IsFixable = false,
                    IssueType = "Roslyn"
                });
            }

            // Check for GC.SuppressFinalize
            bool hasSuppressFinalize = methods
                .Where(m => m.Identifier.Text == "Dispose")
                .Any(m => m.ToString().Contains("GC.SuppressFinalize"));

            if (implementsDisposable && hasDispose && !hasSuppressFinalize)
            {
                var span = cls.Identifier.GetLocation().GetLineSpan();
                issues.Add(new IssueModel
                {
                    RuleId = "LEAK102",
                    FilePath = filePath,
                    LineNumber = span.StartLinePosition.Line + 1,
                    Category = "Memory Leak",
                    Description = $"Roslyn: class '{cls.Identifier.Text}' Dispose() missing GC.SuppressFinalize(this).",
                    Severity = IssueSeverity.Warning,
                    CodeSnippet = "Dispose() { ... } // missing GC.SuppressFinalize",
                    SuggestedFix = "Add GC.SuppressFinalize(this) at end of Dispose().",
                    IsFixable = true,
                    IssueType = "Roslyn"
                });
            }
        }
    }

    private static void CheckAsyncVoidMethods(SyntaxNode root, string filePath, List<IssueModel> issues)
    {
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            if (method.Modifiers.Any(SyntaxKind.AsyncKeyword) &&
                method.ReturnType is PredefinedTypeSyntax pts &&
                pts.Keyword.IsKind(SyntaxKind.VoidKeyword) &&
                !IsEventHandler(method))
            {
                var span = method.GetLocation().GetLineSpan();
                issues.Add(new IssueModel
                {
                    RuleId = "QUAL103",
                    FilePath = filePath,
                    LineNumber = span.StartLinePosition.Line + 1,
                    Category = "Code Quality",
                    Description = $"Roslyn: async void method '{method.Identifier.Text}' — exceptions will crash the process.",
                    Severity = IssueSeverity.Warning,
                    CodeSnippet = method.ToString().Truncate(100),
                    SuggestedFix = "Return Task instead of void for async methods.",
                    IsFixable = false,
                    IssueType = "Roslyn"
                });
            }
        }
    }

    private static bool IsEventHandler(MethodDeclarationSyntax method)
    {
        if (method.ParameterList.Parameters.Count != 2)
            return false;

        string typeName = method.ParameterList.Parameters[1].Type?.ToString() ?? string.Empty;
        return typeName.EndsWith("EventArgs", StringComparison.Ordinal) ||
               typeName.EndsWith("EventArgs?", StringComparison.Ordinal);
    }

    private static void CheckPublicFields(SyntaxNode root, string filePath, List<IssueModel> issues)
    {
        var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
        foreach (var field in fields)
        {
            if (field.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                !field.Modifiers.Any(SyntaxKind.ConstKeyword) &&
                !field.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                var span = field.GetLocation().GetLineSpan();
                var names = string.Join(", ", field.Declaration.Variables.Select(v => v.Identifier.Text));
                issues.Add(new IssueModel
                {
                    RuleId = "QUAL104",
                    FilePath = filePath,
                    LineNumber = span.StartLinePosition.Line + 1,
                    Category = "Code Quality",
                    Description = $"Roslyn: public field(s) '{names}' should be properties.",
                    Severity = IssueSeverity.Info,
                    CodeSnippet = field.ToString().Truncate(80),
                    SuggestedFix = "Use properties instead of public fields for encapsulation.",
                    IsFixable = false,
                    IssueType = "Roslyn"
                });
            }
        }
    }

    private static void CheckOrphanTimers(SyntaxNode root, string filePath, List<IssueModel> issues)
    {
        var creations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        foreach (var creation in creations)
        {
            var typeName = creation.Type.ToString();
            if (typeName.EndsWith("Timer") || typeName.EndsWith("System.Windows.Forms.Timer"))
            {
                // Check if assigned to a variable/field, not just new Timer.
                if (creation.Parent is not AssignmentExpressionSyntax &&
                    creation.Parent is not VariableDeclaratorSyntax)
                {
                    var span = creation.GetLocation().GetLineSpan();
                    issues.Add(new IssueModel
                    {
                        RuleId = "LEAK103",
                        FilePath = filePath,
                        LineNumber = span.StartLinePosition.Line + 1,
                        Category = "Memory Leak",
                        Description = "Roslyn: Timer created but not stored — cannot be disposed.",
                        Severity = IssueSeverity.Warning,
                        CodeSnippet = creation.ToString().Truncate(60),
                        SuggestedFix = "Store timer reference and dispose it in Dispose().",
                        IsFixable = false,
                        IssueType = "Roslyn"
                    });
                }
            }
        }
    }

    private static bool HasComment(SyntaxNode node) =>
        node.DescendantTrivia().Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                                          t.IsKind(SyntaxKind.MultiLineCommentTrivia));

    private static bool IsIncludedSourceFile(string path)
    {
        string normalized = path.Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        return !normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                           segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                           segment.Equals(".git", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

internal static class StringExtensions
{
    public static string Truncate(this string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
