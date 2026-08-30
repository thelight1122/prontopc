using System;
using System.Collections.Generic;

namespace ProntoPC.Governance;

// The seven-virtue surface is intentionally binary in shape but not scored. AEGIS core
// has not defined per-virtue scoring, so this module records that boundary rather than inventing it.
public static class SevenVirtueScreen
{
    static readonly string[] Virtues = { "Honesty", "Respect", "Attention", "Affection", "Loyalty", "Trust", "Communication" };

    public static IntegrityScreenResult Evaluate(string response)
    {
        var results = new List<VirtueScreenResult>();
        foreach (string virtue in Virtues)
            results.Add(new VirtueScreenResult(virtue, VirtueScreenState.CoreGated,
                "Per-virtue scoring is core-gated and no score is claimed by ProntoPC."));
        return new IntegrityScreenResult(results, "core-gated: binary per-virtue scoring is undesigned in AEGIS core");
    }
}
