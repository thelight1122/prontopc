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
            adjustments.Add(new PrismAdjustment(observation.Kind, "Peer-facing expression kept the answer and added an optional reflection."));

        const string reflection = "I may be reading part of this strongly, so please treat the answer above as descriptive rather than a decision for you. You may pause, ask a question, or choose a different path.";
        string emitted = response.TrimEnd();
        if (emitted.Length == 0)
            emitted = reflection;
        else
            emitted += Environment.NewLine + Environment.NewLine + reflection;
        return new PrismResult(response, emitted, adjustments);
    }
}
