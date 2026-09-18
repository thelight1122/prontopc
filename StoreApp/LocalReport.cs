using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ProntoPC;

// LocalReport — generates the completion report HTML entirely ON-DEVICE.
//
// One-time paid Store app pivot (docs/PROPOSAL-ONE-TIME-PAID-APP.md; consented C-0171 / X-0116):
// the app no longer reaches a backend, so the report that used to come from
// GET /api/session/{token}/report is now built locally. This is a faithful C# port of the
// server's generateReportHtml() (pronto-server-cloudrun.mjs) — same structure, same styling,
// same sections — so the customer's report is identical in quality with no network at all.
//
// Contract (so Codex can wire it at the completion seam without coupling to this file):
//   string html = LocalReport.Generate(diagnostics, repairPlan, repairResults, hostname,
//                                       verification, couponCode, couponExpiresAtIso, logoDataUri);
// All structured inputs are JsonElement (navigate-only, read-only); the app already holds these
// as JSON (diagnostics collected on-device, the validated plan, and the repair results). Pure
// function: JSON in, HTML string out — no I/O, no app-type coupling. The caller writes the
// returned string to disk (e.g. the customer's Desktop) and opens it.
//
// Improvement over the server port: every diagnostic-derived value is HTML-escaped (Esc). The
// server interpolated raw; escaping here is defense-in-depth so a hostile device name / event
// message can never inject markup into the report.
public static class LocalReport
{
    public static string Generate(
        JsonElement diagnostics,
        JsonElement repairPlan,
        JsonElement repairResults,
        string hostname,
        JsonElement? verification = null,
        string? couponCode = null,
        string? couponExpiresAtIso = null,
        string? logoDataUri = null)
    {
        JsonElement diag = diagnostics;
        JsonElement plan = repairPlan;
        var results = (repairResults.ValueKind == JsonValueKind.Array
            ? repairResults.EnumerateArray().ToList()
            : new List<JsonElement>());

        string host = string.IsNullOrWhiteSpace(hostname) ? "Your PC" : hostname;
        string now = DateTime.Now.ToString("MMMM d, yyyy, hh:mm tt ", CultureInfo.InvariantCulture)
                     + TimeZoneInfo.Local.StandardName;

        bool hasRepairs = results.Any(r => S(r, "status") == "done");
        string badgeText = hasRepairs ? "Repair Complete" : "Diagnostic Report";
        string badgeColor = hasRepairs ? "#7C5CFF" : "#3B82F6";

        string summary = S(plan, "summary", "Your PC was scanned. See the details below.");
        string hwNotes = S(plan, "hardware_notes");
        string secAssess = S(plan, "security_assessment");

        JsonElement mem = G(diag, "memory");
        int totalFreed = results.Sum(r => I(r, "mb_freed"));
        int completedCount = results.Count(r => S(r, "status") == "done");

        var sec = new StringBuilder();

        // ── Post-restart verification (only when the reconnect flow ran) ──────────────────
        if (verification is JsonElement v && v.ValueKind == JsonValueKind.Object)
        {
            bool healthy = B(v, "healthy") == true;
            string vColor = healthy ? "#2A7A4A" : "#7C5CFF";
            string vLabel = healthy
                ? "System restarted cleanly — the repairs are holding."
                : "Restarted — a few services were still starting up.";
            var stopped = A(v, "stopped_critical").Select(x => S(x)).Where(s => s.Length > 0).ToList();
            string critical = stopped.Count > 0
                ? $"<span style=\"color:#C0392B\">{Esc(string.Join(", ", stopped))} not running</span>"
                : "<span style=\"color:#2A7A4A\">All running</span>";
            bool? nce = null; int nceVal = 0;
            var nceEl = G(v, "new_critical_errors");
            if (nceEl.ValueKind == JsonValueKind.Number) { nce = true; nceVal = nceEl.GetInt32(); }
            string newErrors = nce == null ? "—"
                : nceVal == 0 ? "<span style=\"color:#2A7A4A\">None</span>"
                : $"<span style=\"color:#7C5CFF\">{nceVal}</span>";
            var tempEl = G(v, "temp_mb");
            string tempAfter = tempEl.ValueKind == JsonValueKind.Number ? $"{tempEl.GetInt32()} MB" : "—";
            sec.Append($"<div class=\"section\" style=\"border-color:{(healthy ? "rgba(42,122,74,.3)" : "rgba(124,92,255,.3)")}\">")
               .Append("<div class=\"section-head\"><div class=\"dot\" style=\"background:").Append(vColor).Append("\"></div><h3>Post-Restart Verification</h3></div>")
               .Append($"<div style=\"padding:12px 20px\"><p style=\"font-weight:700;color:{vColor};margin:0 0 10px\">{vLabel}</p><table><tr><th>Check</th><th>Result</th></tr>")
               .Append($"<tr><td>Critical services</td><td>{critical}</td></tr>")
               .Append($"<tr><td>New critical errors since reboot</td><td>{newErrors}</td></tr>")
               .Append($"<tr><td>Temp files after restart</td><td>{tempAfter}</td></tr>")
               .Append("</table></div></div>");
        }

        // ── Repair actions ────────────────────────────────────────────────────────────────
        if (results.Count > 0)
        {
            // Map each action id -> kind (maintenance/repair/report) from the validated plan
            // (the envelope stamps "kind"). Lets the report scope the guarantee to true repairs.
            var kindById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (plan.ValueKind == JsonValueKind.Object && plan.TryGetProperty("actions", out var planActs)
                && planActs.ValueKind == JsonValueKind.Array)
                foreach (var pa in planActs.EnumerateArray())
                    if (pa.ValueKind == JsonValueKind.Object && pa.TryGetProperty("id", out var pid)
                        && pid.ValueKind == JsonValueKind.String)
                        kindById[pid.GetString()!] = pa.TryGetProperty("kind", out var pk)
                            && pk.ValueKind == JsonValueKind.String ? pk.GetString()! : "repair";

            bool anyMaintenance = false;
            var rows = new StringBuilder();
            foreach (var a in results)
            {
                string name = S(a, "name"); if (name.Length == 0) name = S(a, "action");
                string status = S(a, "status");
                int freed = I(a, "mb_freed");
                string detail = S(a, "detail"); if (detail.Length == 0) detail = "—";
                if (detail.Length > 120) detail = detail.Substring(0, 120);
                string kind = kindById.TryGetValue(S(a, "action"), out var k) ? k : "repair";
                if (kind == "maintenance") anyMaintenance = true;
                string typeLabel = kind == "maintenance" ? "Maintenance" : kind == "report" ? "Report" : "Repair";
                string typeColor = kind == "maintenance" ? "#6B6560" : kind == "report" ? "#4F7DFF" : "#2A7A4A";
                rows.Append($"<tr><td>{Esc(name)}</td><td style=\"color:{typeColor};font-weight:600\">{typeLabel}</td>")
                    .Append($"<td style=\"color:{StatusColor(status)};font-weight:600\">{StatusLabel(status)}</td>")
                    .Append($"<td style=\"color:#2A7A4A;font-family:'Courier New',monospace\">{(freed > 0 ? Esc(FmtMb(freed)) + " recovered" : "")}</td>")
                    .Append($"<td style=\"color:#6B6560;font-size:.85em\">{Esc(detail)}</td></tr>");
            }
            sec.Append("<div class=\"section\"><div class=\"section-head\"><div class=\"dot\"></div><h3>Repair Actions</h3></div>")
               .Append("<table><tr><th>Action</th><th>Type</th><th>Result</th><th>Space</th><th>Notes</th></tr>")
               .Append(rows).Append("</table>");
            if (anyMaintenance)
                sec.Append("<div style=\"padding:0 20px 12px\"><p style=\"font-size:.82rem;color:#6B6560;margin:6px 0 0\">"
                    + "<strong>Maintenance</strong> items — temporary files, caches and similar — reclaim space now but naturally build back up with normal use, so their return is expected. The 30-day repair guarantee covers genuine <strong>repairs</strong>, not routine cleanup.</p></div>");
            sec.Append("</div>");
        }

        // ── Drive health ────────────────────────────────────────────────────────────────
        var disks = A(diag, "disk").ToList();
        if (disks.Count > 0)
        {
            var rows = new StringBuilder();
            foreach (var d in disks)
            {
                int pct = I(d, "percent_used");
                string color = pct > 90 ? "#C0392B" : pct > 70 ? "#7C5CFF" : "#2A7A4A";
                rows.Append($"<tr><td>{Esc(S(d, "device"))}</td><td>{Esc(S(d, "total_gb"))} GB</td><td>{Esc(S(d, "used_gb"))} GB</td>")
                    .Append($"<td>{Esc(S(d, "free_gb"))} GB</td><td style=\"color:{color};font-weight:600\">{pct}%</td></tr>");
            }
            sec.Append("<div class=\"section\"><div class=\"section-head\"><div class=\"dot\"></div><h3>Drive Health</h3></div>")
               .Append("<table><tr><th>Drive</th><th>Total</th><th>Used</th><th>Free</th><th>Usage</th></tr>")
               .Append(rows).Append("</table></div>");
        }

        // ── Driver audit (+ device errors, BSOD) ───────────────────────────────────────────
        JsonElement drv = G(diag, "drivers");
        if (I(drv, "total") > 0)
        {
            var content = new StringBuilder();
            int unsignedCount = A(drv, "unsigned").Count();
            int outdatedCount = A(drv, "outdated").Count();
            content.Append("<div style=\"padding:16px 20px\"><div class=\"stats\" style=\"margin-bottom:12px\">")
                   .Append($"<div class=\"stat\"><div class=\"num\">{I(drv, "total")}</div><div class=\"lbl\">total drivers</div></div>")
                   .Append($"<div class=\"stat\"><div class=\"num\">{I(drv, "signed")}</div><div class=\"lbl\">signed</div></div>")
                   .Append($"<div class=\"stat\"><div class=\"num\">{unsignedCount}</div><div class=\"lbl\">unsigned</div></div>")
                   .Append($"<div class=\"stat\"><div class=\"num\">{outdatedCount}</div><div class=\"lbl\">outdated</div></div></div></div>");
            if (unsignedCount > 0)
            {
                content.Append("<table><tr><th>Unsigned Driver</th><th>Manufacturer</th><th>Version</th></tr>");
                foreach (var d in A(drv, "unsigned"))
                    content.Append($"<tr><td style=\"color:#C0392B\">{Esc(S(d, "device"))}</td><td>{Esc(S2(d, "manufacturer"))}</td><td>{Esc(S2(d, "version"))}</td></tr>");
                content.Append("</table>");
            }
            if (outdatedCount > 0)
            {
                content.Append("<div style=\"padding:0 20px\"><p style=\"font-size:.82rem;color:#6B6560;margin:6px 0 0\">These drivers are over two years old. Updating them is optional and free — click <strong>Find latest</strong> to reach the manufacturer's download page and install it yourself. ProntoPC never installs or changes drivers automatically.</p></div>");
                content.Append("<table><tr><th>Outdated Driver</th><th>Version</th><th>Date</th><th>Age</th><th>Recommended</th></tr>");
                foreach (var d in A(drv, "outdated"))
                {
                    string q = Uri.EscapeDataString($"{S(d, "provider")} {S(d, "device")} driver download".Trim());
                    content.Append($"<tr><td>{Esc(S(d, "device"))}</td><td>{Esc(S2(d, "version"))}</td><td>{Esc(S2(d, "date"))}</td>")
                           .Append($"<td style=\"color:#7C5CFF\">{Esc(S(d, "age_days"))}d</td>")
                           .Append($"<td><a href=\"https://www.google.com/search?q={q}\" target=\"_blank\" rel=\"noopener\" style=\"color:#1A6DC8;text-decoration:none;font-weight:600\">Find latest &rarr;</a></td></tr>");
                }
                content.Append("</table>");
            }
            var devErrors = A(diag, "device_errors").ToList();
            if (devErrors.Count > 0)
            {
                content.Append("<table><tr><th>Problem Device</th><th>Code</th><th>Description</th></tr>");
                foreach (var d in devErrors)
                    content.Append($"<tr><td style=\"color:#C0392B\">{Esc(S(d, "device"))}</td><td>{Esc(S(d, "error_code"))}</td><td>{Esc(S(d, "error_description"))}</td></tr>");
                content.Append("</table>");
            }
            var bsod = A(diag, "bsod_history").ToList();
            if (bsod.Count > 0)
            {
                content.Append("<table><tr><th>Crash Date</th><th>Details</th></tr>");
                foreach (var b in bsod)
                {
                    string date = S(b, "date"); if (date.Length > 19) date = date.Substring(0, 19);
                    string det = S(b, "file"); if (det.Length == 0) det = S(b, "message"); if (det.Length == 0) det = "—";
                    content.Append($"<tr><td>{Esc(date)}</td><td>{Esc(det)}</td></tr>");
                }
                content.Append("</table>");
            }
            sec.Append("<div class=\"section\"><div class=\"section-head\"><div class=\"dot\"></div><h3>Driver Audit</h3></div>")
               .Append(content).Append("</div>");
        }

        // ── Security ────────────────────────────────────────────────────────────────────
        JsonElement security = G(diag, "security");
        JsonElement def = G(security, "defender");
        JsonElement fw = G(security, "firewall");
        JsonElement act = G(diag, "activation");
        bool hasDefender = G(def, "real_time_protection").ValueKind is JsonValueKind.True or JsonValueKind.False;
        if (hasDefender || A(fw, "profiles").Any() || S(act, "status").Length > 0)
        {
            var content = new StringBuilder("<div style=\"padding:16px 20px\">");
            if (hasDefender)
            {
                content.Append("<div style=\"margin-bottom:12px\"><strong style=\"font-size:.82rem;text-transform:uppercase;color:#6B6560\">Windows Defender</strong>")
                       .Append("<div style=\"display:grid;grid-template-columns:1fr 1fr;gap:6px;margin-top:6px\">")
                       .Append($"<div>Real-time Protection: {BoolBadge(B(def, "real_time_protection"))}</div>")
                       .Append($"<div>Antivirus: {BoolBadge(B(def, "antivirus_enabled"))}</div>")
                       .Append($"<div>Antispyware: {BoolBadge(B(def, "antispyware_enabled"))}</div>")
                       .Append($"<div>Behavior Monitor: {BoolBadge(B(def, "behavior_monitor"))}</div></div></div>");
            }
            if (A(fw, "profiles").Any())
            {
                var profs = A(fw, "profiles").Select(p => $"{Esc(S(p, "name"))}: {BoolBadge(B(p, "enabled"), "ON", "OFF")}");
                content.Append("<div style=\"margin-bottom:12px\"><strong style=\"font-size:.82rem;text-transform:uppercase;color:#6B6560\">Firewall</strong>")
                       .Append("<div style=\"margin-top:6px\">").Append(string.Join(" &nbsp;·&nbsp; ", profs)).Append("</div></div>");
            }
            if (S(act, "status").Length > 0)
            {
                content.Append("<div><strong style=\"font-size:.82rem;text-transform:uppercase;color:#6B6560\">Activation</strong>")
                       .Append($"<div style=\"margin-top:6px\">{BoolBadge(B(act, "is_activated"), "Licensed", Esc(S(act, "status")))} — {Esc(S(act, "product"))}</div></div>");
            }
            content.Append("</div>");
            sec.Append("<div class=\"section\"><div class=\"section-head\"><div class=\"dot\"></div><h3>Security Status</h3></div>")
               .Append(content).Append("</div>");
        }

        // ── Network ─────────────────────────────────────────────────────────────────────
        JsonElement net = G(diag, "network");
        JsonElement conn = G(net, "connectivity");
        if (A(net, "adapters").Any() || conn.ValueKind == JsonValueKind.Object)
        {
            var content = new StringBuilder();
            if (A(net, "adapters").Any())
            {
                content.Append("<table><tr><th>Adapter</th><th>Description</th><th>Speed</th></tr>");
                foreach (var a in A(net, "adapters"))
                    content.Append($"<tr><td>{Esc(S(a, "name"))}</td><td>{Esc(S2(a, "description"))}</td><td>{Esc(S2(a, "speed"))}</td></tr>");
                content.Append("</table>");
            }
            if (A(conn, "interfaces").Any())
            {
                content.Append("<table><tr><th>Interface</th><th>IPv4</th><th>Gateway</th><th>DNS</th></tr>");
                foreach (var i in A(conn, "interfaces"))
                    content.Append($"<tr><td>{Esc(S(i, "name"))}</td><td>{Esc(S(i, "ipv4"))}</td><td>{Esc(S(i, "gateway"))}</td><td style=\"font-size:.82em\">{Esc(S(i, "dns_servers"))}</td></tr>");
                content.Append("</table>");
            }
            var inet = B(conn, "internet_reachable");
            if (inet != null)
                content.Append($"<div style=\"padding:12px 20px\">Internet: {BoolBadge(inet, "Connected", "UNREACHABLE")}</div>");
            sec.Append("<div class=\"section\"><div class=\"section-head\"><div class=\"dot\"></div><h3>Network Health</h3></div>")
               .Append(content).Append("</div>");
        }

        // ── Services ────────────────────────────────────────────────────────────────────
        JsonElement svc = G(diag, "services");
        if (A(svc, "stopped_critical").Any() || A(svc, "failed").Any())
        {
            var content = new StringBuilder();
            if (A(svc, "stopped_critical").Any())
            {
                content.Append("<table><tr><th>Service</th><th>Display Name</th><th>Status</th></tr>");
                foreach (var s in A(svc, "stopped_critical"))
                    content.Append($"<tr><td style=\"color:#C0392B\">{Esc(S(s, "name"))}</td><td>{Esc(S(s, "display_name"))}</td><td>{Esc(S(s, "status"))}</td></tr>");
                content.Append("</table>");
            }
            if (A(svc, "failed").Any())
            {
                content.Append("<table><tr><th>Time</th><th>Message</th></tr>");
                foreach (var f in A(svc, "failed"))
                {
                    string t = S(f, "time"); if (t.Length > 19) t = t.Substring(0, 19);
                    string m = S(f, "message"); if (m.Length > 150) m = m.Substring(0, 150);
                    content.Append($"<tr><td style=\"font-size:.82em\">{Esc(t)}</td><td style=\"font-size:.82em\">{Esc(m)}</td></tr>");
                }
                content.Append("</table>");
            }
            sec.Append("<div class=\"section\"><div class=\"section-head\"><div class=\"dot\"></div><h3>Services Audit</h3></div>")
               .Append(content).Append("</div>");
        }

        // ── Windows event errors ──────────────────────────────────────────────────────────
        var eventErrors = A(diag, "event_errors").ToList();
        if (eventErrors.Count > 0)
        {
            var rows = new StringBuilder();
            foreach (var e in eventErrors.Take(10))
            {
                string t = S(e, "time"); if (t.Length > 19) t = t.Substring(0, 19);
                string m = S(e, "message"); if (m.Length > 120) m = m.Substring(0, 120);
                rows.Append($"<tr><td style=\"font-size:.82em;color:#6B6560\">{Esc(t)}</td><td style=\"font-weight:500\">{Esc(S(e, "source"))}</td><td style=\"font-size:.82em\">{Esc(m)}</td></tr>");
            }
            sec.Append("<div class=\"section\"><div class=\"section-head\"><div class=\"dot\"></div><h3>Windows Event Errors</h3></div>")
               .Append("<table><tr><th>Time</th><th>Source</th><th>Message</th></tr>").Append(rows).Append("</table></div>");
        }

        // ── System information ────────────────────────────────────────────────────────────
        JsonElement osInfo = G(diag, "os");
        JsonElement uptimeInfo = G(diag, "uptime");
        JsonElement cpuInfo = G(diag, "cpu");
        JsonElement ppInfo = G(diag, "power_plan");
        string cpuName = S(cpuInfo, "processor_name"); if (cpuName.Length == 0) cpuName = S(osInfo, "processor"); if (cpuName.Length == 0) cpuName = "—";
        string rebootNote = B(uptimeInfo, "needs_reboot") == true ? " <span style=\"color:#C0392B\">(reboot recommended)</span>" : "";
        sec.Append("<div class=\"section\"><div class=\"section-head\"><div class=\"dot\"></div><h3>System Information</h3></div><div class=\"sysinfo\">")
           .Append($"<div class=\"sysinfo-row\"><span class=\"key\">Computer</span><span class=\"val\">{Esc(host)}</span></div>")
           .Append($"<div class=\"sysinfo-row\"><span class=\"key\">Windows</span><span class=\"val\">{Esc(S(osInfo, "edition"))} (Build {Esc(S(osInfo, "build"))})</span></div>")
           .Append($"<div class=\"sysinfo-row\"><span class=\"key\">CPU</span><span class=\"val\">{Esc(cpuName)}</span></div>")
           .Append($"<div class=\"sysinfo-row\"><span class=\"key\">Memory</span><span class=\"val\">{Esc(S2(mem, "total_gb"))} GB ({I(mem, "percent_used")}% used)</span></div>")
           .Append($"<div class=\"sysinfo-row\"><span class=\"key\">Uptime</span><span class=\"val\">{I(uptimeInfo, "uptime_days")} days{rebootNote}</span></div>")
           .Append($"<div class=\"sysinfo-row\"><span class=\"key\">Power Plan</span><span class=\"val\">{Esc(S2(ppInfo, "active_plan"))}</span></div>")
           .Append("</div></div>");

        // ── Header logo (optional; self-contained data URI if supplied) ────────────────────
        string logo = string.IsNullOrEmpty(logoDataUri)
            ? ""
            : $"<img src=\"{logoDataUri}\" width=\"46\" height=\"46\" alt=\"\" style=\"border-radius:8px;flex-shrink:0\">";

        // ── Optional loyalty coupon (retained if the app chooses to present one) ────────────
        string couponBlock = "";
        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            string expires = "";
            if (!string.IsNullOrWhiteSpace(couponExpiresAtIso) && DateTime.TryParse(couponExpiresAtIso, out var exp))
                expires = exp.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
            couponBlock =
                "<div class=\"section\" style=\"border-color:rgba(124,92,255,.25);background:linear-gradient(135deg,#FFFDF8,#FFF8ED)\"><div style=\"padding:24px;text-align:center\">"
                + "<h3 style=\"font-size:1.1rem;font-weight:700;color:#1A1917;margin-bottom:8px\">A thank-you: 50% off your next repair</h3>"
                + "<p style=\"color:#6B6560;font-size:.9rem;max-width:440px;margin:0 auto 16px\">Keep this code. If your PC ever needs us again, it's half price — good for six months.</p>"
                + "<div style=\"display:inline-block;background:#fff;border:1px dashed #7C5CFF;border-radius:10px;padding:16px 28px;margin-bottom:12px\">"
                + "<div style=\"font-size:.72rem;color:#9B9590;text-transform:uppercase;letter-spacing:.08em;margin-bottom:4px\">Your coupon code</div>"
                + $"<div style=\"font-size:1.5rem;font-weight:800;color:#1A1917;font-family:'Courier New',monospace;letter-spacing:.05em\">{Esc(couponCode)}</div></div>"
                + (expires.Length > 0 ? $"<p style=\"font-size:.78rem;color:#9B9590;margin-bottom:16px\">50% off · valid through {expires}</p>" : "")
                + "</div></div>";
        }

        // ── Shell ─────────────────────────────────────────────────────────────────────────
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>\n<html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Pronto PC — ")
          .Append(badgeText).Append("</title>\n<style>")
          .Append("*{margin:0;padding:0;box-sizing:border-box}body{font-family:'Segoe UI',Arial,sans-serif;background:#FDFAF4;color:#1A1917;font-size:15px;line-height:1.6}.page{max-width:860px;margin:0 auto;padding:40px 32px 60px}.header{background:#1A1917;color:#fff;border-radius:12px;padding:28px 32px;margin-bottom:28px;display:flex;justify-content:space-between;align-items:flex-start}.header-left h1{font-size:1.6rem;font-weight:700;letter-spacing:-.5px;margin-bottom:4px}.header-left h1 span{color:#7C5CFF}.header-left p{color:rgba(255,255,255,.6);font-size:.85rem}.header-right{text-align:right}.header-right .badge{color:#fff;font-size:.72rem;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;padding:.3rem .7rem;border-radius:6px;display:inline-block;margin-bottom:6px}.header-right p{color:rgba(255,255,255,.5);font-size:.78rem}.summary-box{background:#fff;border:1px solid #E2DBD0;border-radius:10px;padding:20px 24px;margin-bottom:20px}.summary-box h2{font-size:1rem;font-weight:700;margin-bottom:8px}.summary-box p{color:#3A3835;line-height:1.7}.hw-notes{background:#FFF5F5;border:1px solid #FCA5A5;border-radius:10px;padding:16px 20px;margin-bottom:20px}.hw-notes strong{color:#C0392B}.sec-notes{background:#F0F9FF;border:1px solid #93C5FD;border-radius:10px;padding:16px 20px;margin-bottom:20px}.sec-notes strong{color:#1D4ED8}.stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(120px,1fr));gap:14px;margin-bottom:20px}.stat{background:#fff;border:1px solid #E2DBD0;border-radius:10px;padding:16px 20px;text-align:center}.stat .num{font-size:1.8rem;font-weight:700;color:#7C5CFF;font-family:Georgia,serif;line-height:1}.stat .lbl{font-size:.78rem;color:#6B6560;margin-top:4px;font-weight:500}.section{background:#fff;border:1px solid #E2DBD0;border-radius:10px;margin-bottom:20px;overflow:hidden}.section-head{background:#F5F0E8;padding:14px 20px;border-bottom:1px solid #E2DBD0;display:flex;align-items:center;gap:10px}.section-head h3{font-size:.9rem;font-weight:700;letter-spacing:.3px;text-transform:uppercase;color:#1A1917}.section-head .dot{width:10px;height:10px;border-radius:50%;background:#7C5CFF;flex-shrink:0}table{width:100%;border-collapse:collapse}th{text-align:left;font-size:.78rem;font-weight:700;letter-spacing:.8px;text-transform:uppercase;color:#6B6560;padding:10px 20px;border-bottom:1px solid #E2DBD0;background:#FDFAF4}td{padding:11px 20px;border-bottom:1px solid #F0EBE4;font-size:.88rem;vertical-align:top}tr:last-child td{border-bottom:none}tr:hover td{background:#FDFAF4}.sysinfo{padding:16px 20px;display:grid;grid-template-columns:1fr 1fr;gap:8px 24px}.sysinfo-row{display:flex;flex-direction:column}.sysinfo-row .key{font-size:.72rem;font-weight:700;letter-spacing:.8px;text-transform:uppercase;color:#9B9590}.sysinfo-row .val{font-size:.88rem;color:#1A1917;font-weight:500}.footer{margin-top:32px;padding-top:20px;border-top:1px solid #E2DBD0;display:flex;justify-content:space-between;align-items:center}.footer p{font-size:.78rem;color:#9B9590}.footer strong{color:#1A1917}.print-btn{margin-top:8px;background:#7C5CFF;color:#fff;border:none;font-weight:700;font-size:.75rem;padding:7px 14px;border-radius:6px;cursor:pointer}.print-btn:hover{background:#6A4FE0}@media print{body{background:#fff}.page{padding:20px}.header{border-radius:0}.no-print{display:none!important}}@media(max-width:600px){.stats{grid-template-columns:1fr 1fr}.header{flex-direction:column;gap:12px}.header-right{text-align:left}.sysinfo{grid-template-columns:1fr}}")
          .Append("</style>\n</head><body><div class=\"page\">\n")
          .Append("<div class=\"header\"><div class=\"header-left\" style=\"display:flex;align-items:center;gap:14px\">").Append(logo)
          .Append($"<div><h1>Pronto <span>PC</span> — {badgeText}</h1><p>Computer: {Esc(host)} · {Esc(now)}</p></div></div>")
          .Append($"<div class=\"header-right\"><div class=\"badge\" style=\"background:{badgeColor}\">{badgeText}</div><p>Custodian v2.0</p><button class=\"print-btn no-print\" onclick=\"window.print()\">Print / Save PDF</button></div></div>\n")
          .Append($"<div class=\"summary-box\"><h2>Summary</h2><p>{Esc(summary)}</p></div>\n")
          .Append(hwNotes.Length > 0 ? $"<div class=\"hw-notes\"><strong>Hardware Notes:</strong> {Esc(hwNotes)}</div>\n" : "")
          .Append(secAssess.Length > 0 ? $"<div class=\"sec-notes\"><strong>Security:</strong> {Esc(secAssess)}</div>\n" : "")
          .Append("<div class=\"stats\">")
          .Append($"<div class=\"stat\"><div class=\"num\">{(totalFreed > 0 ? Esc(FmtMb(totalFreed)) : "—")}</div><div class=\"lbl\">space recovered</div></div>")
          .Append($"<div class=\"stat\"><div class=\"num\">{(completedCount > 0 ? completedCount.ToString() : "—")}</div><div class=\"lbl\">repairs{(hasRepairs ? " completed" : " recommended")}</div></div>")
          .Append($"<div class=\"stat\"><div class=\"num\">{(I(mem, "percent_used") > 0 ? I(mem, "percent_used") + "%" : "—")}</div><div class=\"lbl\">memory in use</div></div></div>\n")
          .Append(sec)
          .Append(couponBlock)
          .Append("<div class=\"footer\"><p><strong>Pronto PC</strong> · prontopc.online · support@prontopc.online</p><p>5D Service Solutions Global LLC · 30-day repair guarantee</p></div>\n")
          .Append("</div></body></html>");
        return sb.ToString();
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────
    static JsonElement G(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v : default;

    static string S(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString() ?? "",
        JsonValueKind.Number => e.ToString(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => ""
    };

    static string S(JsonElement e, string name, string dflt = "")
    {
        var v = G(e, name);
        string s = S(v);
        return s.Length > 0 ? s : dflt;
    }

    // Like S but returns "—" placeholder default (mirrors the server's `|| "—"`).
    static string S2(JsonElement e, string name) => S(e, name, "—");

    static int I(JsonElement e, string name, int dflt = 0)
    {
        var v = G(e, name);
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : dflt;
    }

    static bool? B(JsonElement e, string name) => G(e, name).ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };

    static IEnumerable<JsonElement> A(JsonElement e, string name)
    {
        var v = G(e, name);
        return v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : Enumerable.Empty<JsonElement>();
    }

    static string FmtMb(int mb) => mb >= 1024
        ? $"{(mb / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)} GB"
        : $"{mb} MB";

    static string StatusLabel(string s) => s switch
    {
        "done" => "✓ Completed",
        "noted" => "✓ Logged",
        "skipped" => "— Skipped",
        "error" => "✗ Error",
        _ => Esc(s)
    };

    static string StatusColor(string s) => s switch
    {
        "done" => "#2A7A4A",
        "noted" => "#2A7A4A",
        "skipped" => "#6B6560",
        "error" => "#C0392B",
        _ => "#1A1917"
    };

    static string BoolBadge(bool? val, string trueText = "Enabled", string falseText = "Disabled")
        => val == true
            ? $"<span style=\"color:#2A7A4A;font-weight:600\">{trueText}</span>"
            : $"<span style=\"color:#C0392B;font-weight:600\">{falseText}</span>";

    // HTML-escape every diagnostic-derived value (defense-in-depth; the server port did not).
    static string Esc(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => c.ToString()
            });
        return sb.ToString();
    }
}
