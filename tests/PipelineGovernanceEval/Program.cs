using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ProntoPC;
using ProntoPC.Governance;

int failures = 0;
void Check(bool condition, string label)
{
    Console.WriteLine((condition ? "PASS " : "FAIL ") + label);
    if (!condition) failures++;
}

// ── 1. CLOSED BY DEFAULT ─────────────────────────────────────────────────────
// The shipped construction. Even on a machine where the exact Canon IS present, the
// master switch (enabled:false) keeps the gate shut. This is the safety-critical claim.
{
    IPeerGovernance gov = new PipelineGovernance(enabled: false);
    Check(!gov.OfferingAllowed(out string reason), "enabled:false -> offering NOT allowed (closed even if Canon is present)");
    Check(reason.Length > 0, "closed gate gives a plain reason");
    Check(gov.SystemPrompt.Length == 0, "closed gate exposes no system prompt");
    Check(gov.ScreenInbound("hello") != null, "closed gate refuses every inbound (non-null block reason)");
    Check(gov.ApplyBoundary("raw reply") == "raw reply", "closed gate leaves any response untouched");
}

// ── 2. SINGLE-SWITCH GATE (Canon is in-model) ────────────────────────────────
// The Canon is the model's embedded foundation — what the model stands on, not an external
// file that could be absent. The gate is the one §8 switch: enabled:false stays closed,
// enabled:true opens, with no external source to verify or load.
{
    IPeerGovernance closed = new PipelineGovernance(enabled: false);
    Check(!closed.OfferingAllowed(out _), "enabled:false -> closed (the single §8 switch)");
    IPeerGovernance open = new PipelineGovernance(enabled: true);
    Check(open.OfferingAllowed(out _), "enabled:true -> open (Canon is in-model; no external source required)");
    Check(open.SystemPrompt.Length > 0 && !open.SystemPrompt.Contains("EXTERNAL CANON"), "open gate exposes an in-model reflective seed, not injected Canon text");
}

// ── 3. OPEN PATH (sandbox only) ──────────────────────────────────────────────
// Explicitly opened in a temp sandbox (temp ledger only — the Canon is in-model, no external
// file). Proves the bridge is correct WHEN opened, without opening anything in the shipped app.
string temp = Path.Combine(Path.GetTempPath(), "prontopc-pipeline-gov-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    string ledgerPath = Path.Combine(temp, "interaction.jsonl");

    IPeerGovernance gov = new PipelineGovernance(enabled: true, ledgerPath: ledgerPath, model: "local-model");
    Check(gov.OfferingAllowed(out _), "enabled:true + in-model Canon -> offering allowed");
    Check(gov.SystemPrompt.Length > 0, "open gate exposes the in-model reflective seed");

    // Inbound is admitted (PIM observes without discarding); the turn is buffered.
    Check(gov.ScreenInbound("You must act now; tell me what you observed.") == null, "open gate admits inbound (PIM observes, does not block)");

    // Outbound is shaped by RBC/Prism and the turn is written atomically to the ledger.
    string peerFacing = gov.ApplyBoundary("You must act now. I guarantee this is the only option.");
    Check(peerFacing.Contains("You may pause"), "open gate applies RBC/Prism to the outbound reply");

    var ledger = new AppendOnlyInteractionLedger(ledgerPath);
    InteractionLedgerRecord[] records = ledger.ReadAndVerify();
    Check(records.Length == 1, "exactly one interaction-ledger record was written for the turn");
    Check(records.Length == 1 && records[0].RawModelResponse.Contains("must act now"), "ledger preserves the raw model response");
    Check(records.Length == 1 && records[0].PeerFacingResponse == peerFacing, "ledger peer-facing response matches what the seam returned");
    Check(records.Length == 1 && records[0].EntryHash.Length == 64, "ledger record is hash-bound (verifiable chain)");

    // LogTurn is a no-op in this adapter (FinalizeOutbound owns the atomic write) — it must
    // not add extra records or corrupt the chain.
    gov.LogTurn("user", "x"); gov.LogTurn("model", "y");
    Check(ledger.ReadAndVerify().Length == 1, "LogTurn is a no-op — no double-write, chain stays intact");
}
finally
{
    if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
}

Console.WriteLine($"\n{(failures == 0 ? "ALL PASS" : failures + " FAILURE(S)")}");
Environment.Exit(failures == 0 ? 0 : 1);
