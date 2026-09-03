using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ProntoPC.Governance;

// Proves the append-only Technician -> Communications relay: the Technician Orb posts findings once
// (immutable, hash-chained); the Communications Orb reads the latest for its session; tamper is
// illuminated. No model, no gate — a deterministic record proof.

int failures = 0;
void Check(bool condition, string label)
{
    Console.WriteLine((condition ? "PASS " : "FAIL ") + label);
    if (!condition) failures++;
}

static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

string temp = Path.Combine(Path.GetTempPath(), "prontopc-findings-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
string path = Path.Combine(temp, "findings-relay.jsonl");
try
{
    var clock = (Func<DateTimeOffset>)(() => new DateTimeOffset(2026, 9, 2, 22, 30, 0, TimeSpan.Zero));
    var relay = new AppendOnlyFindingsRelay(path, clock);

    string diag1 = Sha("{\"disk\":{\"free_gb\":11,\"percent_used\":91},\"temp_files\":{\"mb\":740}}");
    var r1 = relay.Append("session-1", "local-model", "gemma-3-1b-it",
        "Your C: drive is 91% full; clearing temporary files and the Recycle Bin can recover space.",
        new[] { new FindingsAction("clear_temp_files", "Clear temporary files", 740),
                new FindingsAction("empty_recycle_bin", "Empty the Recycle Bin", 1300) },
        diag1);

    // A re-scan on the same session appends a NEW record; the prior one is preserved (append-only).
    string diag2 = Sha("{\"disk\":{\"free_gb\":13,\"percent_used\":89},\"temp_files\":{\"mb\":120}}");
    var r2 = relay.Append("session-1", "local-model", "gemma-3-1b-it",
        "After the first pass, C: is 89% full; one more temp sweep is available.",
        new[] { new FindingsAction("clear_temp_files", "Clear temporary files", 120) }, diag2);

    // A different session's findings must not bleed across.
    var r3 = relay.Append("session-2", "local-model", "gemma-3-1b-it",
        "No urgent space issues; a driver review is suggested.",
        new[] { new FindingsAction("driver_report", "Review outdated drivers", 0) }, Sha("{\"drivers\":{\"outdated\":7}}"));

    FindingsRelayRecord[] all = relay.ReadAndVerify();
    Check(all.Length == 3, "three findings appended and the chain verifies");
    Check(all[0].PreviousHash is null, "genesis record has no predecessor");
    Check(all[1].PreviousHash == all[0].EntryHash && all[2].PreviousHash == all[1].EntryHash, "each record chains to its predecessor");
    Check(all.All(r => r.EntryHash.Length == 64), "every entry is hash-bound (SHA-256)");
    Check(all[0].Utc == new DateTimeOffset(2026, 9, 2, 22, 30, 0, TimeSpan.Zero), "records are timestamped");

    // The Communications Orb's read: latest verified findings for its session.
    var latest1 = relay.ReadLatestForSession("session-1");
    Check(latest1 is not null && latest1.RecordId == r2.RecordId, "Communications reads the LATEST findings for session-1 (the re-scan), not the first");
    Check(latest1!.PlanSummary.Contains("89% full") && latest1.Actions.Length == 1 && latest1.Actions[0].Id == "clear_temp_files",
        "the read carries the plan summary + actions the Communications Orb explains");
    Check(latest1.DiagnosticsSha256 == diag2, "the findings are bound to the exact scan (diagnostics hash)");
    var latest2 = relay.ReadLatestForSession("session-2");
    Check(latest2 is not null && latest2.RecordId == r3.RecordId, "session-2 reads its own findings");
    Check(relay.ReadLatestForSession("unknown-session") is null, "no findings for an unknown session (no cross-session bleed)");

    // Tamper 1: a malformed appended line is revealed.
    File.AppendAllText(path, "{\"bad\":true}\n");
    bool malformedRevealed = false;
    try { relay.ReadAndVerify(); } catch (InvalidDataException) { malformedRevealed = true; }
    Check(malformedRevealed, "a malformed appended record is revealed");

    // Tamper 2: altering a prior record's content breaks its hash.
    var lines = File.ReadAllLines(path).Where(l => !l.Contains("\"bad\":true")).ToArray();
    lines[0] = lines[0].Replace("91% full", "50% full");
    File.WriteAllLines(path, lines);
    bool contentTamperRevealed = false;
    try { relay.ReadAndVerify(); } catch (InvalidDataException) { contentTamperRevealed = true; }
    Check(contentTamperRevealed, "altering a prior finding's content is revealed (hash mismatch)");
}
finally
{
    if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
}

// ── head anchor (gap #3): tail-truncation / whole-file replacement, anchor in a SEPARATE domain ──
string atemp = Path.Combine(Path.GetTempPath(), "prontopc-findings-anchor-" + Guid.NewGuid().ToString("N"));
string adom = Path.Combine(Path.GetTempPath(), "prontopc-findings-anchordom-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(atemp); Directory.CreateDirectory(adom);
string apath = Path.Combine(atemp, "relay.jsonl");
string anchorPath = Path.Combine(adom, "relay.anchor.json"); // separate failure domain from the relay file
try
{
    var clk = (Func<DateTimeOffset>)(() => new DateTimeOffset(2026, 9, 3, 0, 20, 0, TimeSpan.Zero));
    var ar = new AppendOnlyFindingsRelay(apath, clk, anchorPath);
    string d = Sha("{\"disk\":{\"percent_used\":91}}");
    ar.Append("s", "local-model", "gemma", "one", new[] { new FindingsAction("clear_temp_files", "t", 1) }, d);
    ar.Append("s", "local-model", "gemma", "two", new[] { new FindingsAction("clear_temp_files", "t", 2) }, d);
    ar.Append("s", "local-model", "gemma", "three", new[] { new FindingsAction("clear_temp_files", "t", 3) }, d);
    Check(ar.ReadAndVerify().Length == 3, "anchor: three appended; chain + head anchor verify");

    // Tail truncation: drop the last record from the relay file; the anchor still attests count=3.
    File.WriteAllLines(apath, File.ReadAllLines(apath).Where(l => !string.IsNullOrWhiteSpace(l)).Take(2).ToArray());
    bool truncRevealed = false;
    try { ar.ReadAndVerify(); } catch (InvalidDataException) { truncRevealed = true; }
    Check(truncRevealed, "anchor: tail truncation is revealed (the intra-file chain alone would not catch it)");

    // Whole-file replacement by a DIFFERENT valid chain; the anchor's head/count no longer match.
    string scratch = Path.Combine(atemp, "scratch.jsonl");
    new AppendOnlyFindingsRelay(scratch, clk).Append("s", "local-model", "gemma", "forged",
        new[] { new FindingsAction("clear_temp_files", "t", 9) }, d);
    File.WriteAllLines(apath, File.ReadAllLines(scratch));
    bool replaceRevealed = false;
    try { ar.ReadAndVerify(); } catch (InvalidDataException) { replaceRevealed = true; }
    Check(replaceRevealed, "anchor: whole-file replacement by a foreign valid chain is revealed");
}
finally
{
    foreach (var dd in new[] { atemp, adom }) if (Directory.Exists(dd)) Directory.Delete(dd, recursive: true);
}

Console.WriteLine(failures == 0 ? "\nFINDINGS RELAY: ALL CHECKS PASS" : $"\n{failures} FAILURE(S)");
Environment.Exit(failures == 0 ? 0 : 1);
