# ★ Black Star Hydra 360 Pro Audit

A production-grade hybrid code audit, auto-repair, and architecture refactoring tool.
Combines **Grep** (fast regex scanning) and **Roslyn** (AST semantic analysis) for deep,
accurate code analysis. Built on .NET 8 with WinForms.

## Features

### 🔍 Hybrid Audit Engine
- **Grep Scout Phase**: Fast regex-based scanning for 19+ audit rules across security, quality, error handling, and memory leak categories
- **Roslyn Semantic Phase**: AST-based analysis that ignores comments, understands type context, and catches issues regex cannot:
  - Confirmed hardcoded secrets in string literal assignments
  - Empty catch blocks (AST-verified, not regex)
  - IDisposable classes missing Dispose patterns
  - Missing GC.SuppressFinalize
  - Async void methods (fire-and-forget crash risk)
  - Public fields that should be properties
  - Orphan timers not stored for disposal

### 🔧 Automatic Repair & Rollback
- **Automatic Mode**: Fix all fixable issues with one click
- **Custom Mode**: Select specific issues to fix
- **Rollback**: All files are backed up before any modification — rollback restores originals
- Repairs include: redact hardcoded credentials, fix empty catch blocks, wrap Console.WriteLine in `#if DEBUG`, add GC.SuppressFinalize

### 🏗 Clean Architecture Refactoring
- Refactors C# MVVM projects into Core/Infrastructure/Presentation structure
- Migrates files from Models/Views/ViewModels to new hierarchy
- Rewrites namespaces in .cs and .xaml files
- Patches .csproj XML include paths
- Updates XAML ResourceDictionary source paths
- Dry-run (WhatIf) mode for safe preview
- Full backup before any changes

### 📊 Audit Modes
| Mode | Description |
|------|-------------|
| Fast | Grep-only quick scan |
| Secrets | Grep + Roslyn focused on credentials |
| Security | All security checks |
| Full | Complete audit: security, quality, memory leaks, best practices |
| Remediation | Full audit + automatic repair |

### 📄 Report Formats
TXT, JSON, HTML (dark-themed), and CSV

## Architecture
```
BlackStarHydra360/
├── Program.cs                    # Entry point with global exception handlers
├── App.config                     # Configuration
├── Logging/
│   └── AppLogger.cs              # Thread-safe file + UI event logger
├── Core/
│   ├── IssueModel.cs             # Issue data model with severity levels
│   ├── AuditMode.cs              # Audit mode and phase enums
│   ├── AuditResult.cs            # Audit result container with summary
│   ├── AuditEngine.cs            # Orchestrator: Grep + Roslyn coordination
│   └── ReportGenerator.cs        # TXT/JSON/HTML/CSV report generation
├── Auditors/
│   ├── GrepAuditor.cs            # Regex-based fast scanner (19 rules)
│   └── RoslynAuditor.cs          # AST-based semantic analyzer
├── Repair/
│   ├── BackupManager.cs          # File backup & rollback system
│   └── RepairEngine.cs           # Automatic repair with safe fixes
├── Refactor/
│   └── CleanArchitectureRefactor.cs  # Clean architecture refactoring
├── UI/
│   ├── DarkTheme.cs              # Dark theme palette and styling
│   ├── GlowPanel.cs              # Glow-bordered panel control
│   ├── AuditDataGridView.cs      # Issue display grid with severity colors
│   └── MainForm.cs               # Tabbed main interface
└── Properties/
    └── AssemblyInfo.cs
```

## Build
```bash
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

## Requirements
- .NET 8.0 SDK
- Windows OS (WinForms)
- Microsoft.CodeAnalysis.CSharp 4.8.0 (NuGet, auto-restored)

The project targets `net8.0-windows`, enables Windows targeting for CI or
non-Windows build hosts, and keeps the handwritten `Properties/AssemblyInfo.cs`
as the single assembly-metadata source.

## Logging
Logs: `%TEMP%/BlackStarHydra/logs/hydra_YYYYMMDD.log`
Backups: `%TEMP%/BlackStarHydra/backups/backup_YYYYMMDD_HHmmss/`

## Audit Rules (19 Grep + 7 Roslyn = 26 total)

### Grep Rules
| Rule ID | Category | Severity | Description |
|---------|----------|----------|-------------|
| SEC001 | Security | Critical | Hardcoded credential candidate |
| SEC002 | Security | Critical | Connection string with credential |
| SEC003 | Security | Critical | AWS access key |
| SEC004 | Security | Critical | Private key embedded |
| SEC005 | Security | Critical | JWT token |
| SEC013 | Security | Warning | Process execution |
| SEC014 | Security | Warning | Dynamic SQL |
| QUAL001-003 | Quality | Warning | TODO/FIXME/HACK markers |
| QUAL004 | Quality | Critical | NotImplementedException |
| QUAL005-006 | Debug | Warning/Info | Console/Debug.WriteLine |
| QUAL007-008 | Error Handling | Critical/Warning | Empty catch blocks |
| LEAK001-003 | Memory Leak | Warning | Event handlers, timers, bitmaps |

### Roslyn Rules
| Rule ID | Category | Severity | Description |
|---------|----------|----------|-------------|
| SEC101 | Security | Critical | Confirmed hardcoded secret (AST) |
| QUAL101 | Error Handling | Critical | Confirmed empty catch (AST) |
| LEAK101 | Memory Leak | Critical | IDisposable without Dispose |
| LEAK102 | Memory Leak | Warning | Missing GC.SuppressFinalize |
| LEAK103 | Memory Leak | Warning | Orphan timer |
| QUAL103 | Quality | Warning | Async void method |
| QUAL104 | Quality | Info | Public fields |
