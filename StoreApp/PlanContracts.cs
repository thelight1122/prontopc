using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProntoPC;

// The plan-author seam, kept in its own engine-free file so the Pod eval harness can
// compile Envelope + the authors without pulling the Windows-only repair engine.

// Result of one author attempt. A null return => decline; the Orchestrator tries the
// next author in provider order (local-model Satellite -> cloud broker).
sealed record PlanResult(JsonElement Plan, string Provider, string Model);

interface IPlanAuthor
{
    // Author a plan; the model may only choose from `admissible`. Return null to decline
    // (unavailable/unreachable) so the Orchestrator falls through to the next author.
    Task<PlanResult?> AuthorAsync(JsonElement diag, IReadOnlySet<string> admissible, CancellationToken ct);
}
