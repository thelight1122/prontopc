using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProntoPC;

// The plan-author seam, kept in its own engine-free file so the Pod eval harness can
// compile Envelope + the authors without pulling the Windows-only repair engine.

// Result of one author attempt. A null return means the local author declined. The
// caller must report that result honestly; it must never substitute a cloud response.
sealed record PlanResult(JsonElement Plan, string Provider, string Model);

interface IPlanAuthor
{
    // Author a plan from a COMPLETED, VALIDATED System Audit (TOTEM-X-0074). The model-
    // facing path never receives raw diagnostics — only the audit report, whose evidence
    // is minted solely by SystemAuditor.Assess. The author must not invoke the model for
    // an invalid/incomplete audit. The model may only choose from `admissible`. Return
    // null to decline.
    Task<PlanResult?> AuthorAsync(SystemAudit audit, IReadOnlySet<string> admissible, CancellationToken ct);
}

// An optional, non-sensitive reason that can be shown locally when a local author
// declines. It deliberately contains a stable code, not raw model output or an
// exception message.
interface IPlanAuthorDiagnostics
{
    string LastDeclineReason { get; }
}
