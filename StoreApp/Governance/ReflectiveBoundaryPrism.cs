using System;
using System.Collections.Generic;

namespace ProntoPC.Governance;

// RBC / Prism shapes expression only. Raw model expression remains available to the
// interaction ledger; this method never represents itself as a judgment of inference.
public static class ReflectiveBoundaryPrism
{
    public static PrismResult Apply(string response, IReadOnlyList<ForceArtifactObservation> outboundObservations)
    {
        response ??= string.Empty;
        if (outboundObservations is null || outboundObservations.Count == 0)
            return new PrismResult(response, response, Array.Empty<PrismAdjustment>());

        var adjustments = new List<PrismAdjustment>();
        foreach (var observation in outboundObservations)
            adjustments.Add(new PrismAdjustment(observation.Kind, "Peer-facing expression was reframed as an optional reflection."));

        const string reflection = "I may be reading this too strongly. I can offer a description of the options visible from your PC's information. You may pause, ask a question, or choose a different path.";
        return new PrismResult(response, reflection, adjustments);
    }
}
