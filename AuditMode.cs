namespace BlackStarHydra360.Core;

public enum AuditMode { Fast, Secrets, Security, Full, Remediation }
public enum AuditPhase { Scout, SemanticAnalysis, Remediation, Refactor, Complete }

public static class AuditModeExtensions
{
    public static string Description(this AuditMode mode) => mode switch
    {
        AuditMode.Fast => "Fast — Grep-only quick scan for patterns",
        AuditMode.Secrets => "Secrets — Grep + Roslyn focused on hardcoded credentials",
        AuditMode.Security => "Security — All security and vulnerability checks",
        AuditMode.Full => "Full — Complete audit: security, quality, memory leaks, and best practices",
        AuditMode.Remediation => "Remediation — Full audit + automatic repair with rollback",
        _ => mode.ToString()
    };
}
