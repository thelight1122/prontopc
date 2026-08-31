using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProntoPC;

public partial class MainWindow : Window
{
    const string CloudRun = "https://pronto-pc-108655745293.us-central1.run.app";
    // The $29 one-time diagnostic + repair (same Stripe Payment Link the web funnel uses).
    const string CheckoutLink = "https://buy.stripe.com/28E4gA9bBfuY62ibfk9Ve02";
    string _token;
    string _payNonce = "";
    CancellationTokenSource? _pollCts;
    readonly List<(Dictionary<string, object?> action, CheckBox box)> _rows = new();
    readonly Orchestrator _orch;
    readonly bool _modelPresent;

    // ── Conversational Technician (Phase 2) — GATED CLOSED ────────────────────
    // The peer-facing conversation runs through ONE governance seam (Governance.cs).
    // The wired seam is PipelineGovernance — the bridge to the AEGIS module set
    // (ProntoPC.Governance.*) — constructed with enabled:false, so the §8 admission gate
    // is CLOSED: the entry stays hidden, every inbound is refused, the Canon/pipeline are
    // never touched, and the on-device model is never reached. Opening the Technician is a
    // deliberate, separate act — construct PipelineGovernance(enabled:true) with the exact
    // authorized Canon present (Tracey's §8 greenlight) — nothing else in this window changes.
    readonly IPeerGovernance _gov = new PipelineGovernance(enabled: false);
    CustodianChat? _chat;

    public MainWindow(string token)
    {
        InitializeComponent();
        _token = token;
        // The Orchestrator holds the Action Envelope, keys the plan Umbilical to it, and
        // sequences the phases. Provider order: local-model Satellite (on-device, offline)
        // -> cloud broker. The Satellite runs only when a model is bundled (Stage 2b);
        // until then AbsentInference makes it decline and the cloud broker authors,
        // unchanged. An unkeyed envelope degrades only the plan phase (labelled offline).
        // The Satellite uses the bundled Gemma if present (Stage 2b), else AbsentInference
        // (declines -> cloud). CreateOrAbsent keys off genai_config.json, so dropping the
        // model into Assets/models/gemma-3-1b-it activates it with no code change.
        string modelDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "models", "gemma-3-1b-it");
        _modelPresent = System.IO.File.Exists(System.IO.Path.Combine(modelDir, "genai_config.json"));
        _orch = new Orchestrator(new IPlanAuthor[]
        {
            new GemmaLocalPlanner(OnnxGenAiInference.CreateOrAbsent(modelDir, "gemma-3-1b-it")),
            new CloudPlanAuthor(CloudRun, () => _token),
        });
        Loaded += async (_, __) => await Start();
    }

    // Entry: the pay-per-run gate stands in front of the scan. A run is unlocked only
    // by a fulfilled Stripe checkout — there is no free path.
    //  • Arrived via prontopc://run?token=… → re-verify the token names a live paid
    //    session, then scan.
    //  • No/expired/invalid token (e.g. a Start-menu launch) → present the checkout gate.
    async Task Start()
    {
        if (!string.IsNullOrWhiteSpace(_token) && await TokenIsPaid(_token)) { await RunScan(); return; }
        ShowGate();
    }

    // ── Pay-per-run gate ──────────────────────────────────────────────────────
    void ShowGate()
    {
        Cust.SetState(CustodianState.Greeting);
        GateStart.Visibility = Visibility.Visible;
        GateWaiting.Visibility = Visibility.Collapsed;
        ShowPanel(GatePanel);
    }

    // Open the Stripe Payment Link tagged with a per-launch nonce, then poll the backend
    // until that checkout is fulfilled. The customer pays in the browser and this window
    // continues on its own — no deep-link, so no second instance and no second UAC.
    void GateBuy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_payNonce))
            _payNonce = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"); // 64 hex

        try { Process.Start(new ProcessStartInfo($"{CheckoutLink}?client_reference_id={_payNonce}") { UseShellExecute = true }); }
        catch { /* no browser — the customer can still retry; polling continues */ }

        GateStart.Visibility = Visibility.Collapsed;
        GateWaiting.Visibility = Visibility.Visible;
        ShowPanel(GatePanel);

        _pollCts?.Cancel();
        _pollCts = new CancellationTokenSource();
        _ = PollForPayment(_payNonce, _pollCts.Token);
    }

    void GateCancel_Click(object sender, RoutedEventArgs e)
    {
        _pollCts?.Cancel();
        ShowGate();
    }

    async Task PollForPayment(string nonce, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var resp = await http.GetAsync($"{CloudRun}/api/await-payment?ref={nonce}", ct);
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("paid", out var p) && p.ValueKind == JsonValueKind.True
                    && doc.RootElement.TryGetProperty("token", out var t) && t.ValueKind == JsonValueKind.String)
                {
                    _token = t.GetString()!;
                    _payNonce = "";
                    await RunScan();
                    return;
                }
            }
            catch (OperationCanceledException) { return; }
            catch { /* transient — keep polling */ }
            try { await Task.Delay(3000, ct); } catch { return; }
        }
    }

    // A live, unexpired session for this token means it came from a fulfilled checkout.
    // Fail-closed: any error (offline, unknown token) is treated as unpaid → the gate.
    async Task<bool> TokenIsPaid(string token)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var resp = await http.GetAsync($"{CloudRun}/api/verify-token?token={Uri.EscapeDataString(token)}");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.TryGetProperty("paid", out var p) && p.ValueKind == JsonValueKind.True;
        }
        catch { return false; }
    }

    // ── Scan ─────────────────────────────────────────────────────────────────
    async Task RunScan()
    {
        Cust.SetState(CustodianState.Scanning, "Reading your system… nothing on your PC is changed.");
        CollapseAllPanels();
        var diag = await Task.Run(() => Diagnostics.Collect());

        Cust.SetState(CustodianState.Thinking, "Building your personalized repair plan…");

        // Plan phase: the Orchestrator authors a plan (local Satellite -> cloud broker)
        // and validates it against the keyed Action Envelope before it reaches consent.
        var outcome = await _orch.AuthorPlanAsync(diag);
        if (outcome.IsOk)
        {
            var plan = ToElement(outcome.Plan!);
            if (await RecordNativePlanAsync(diag, plan, outcome)) { ShowFindings(plan); return; }
            ShowError("We couldn't securely record this repair plan. No repairs have been run — please try again.");
            return;
        }

        // Both AI authors declined (on-device Gemma AND cloud broker). We never present
        // a rule-based plan under the AI claim — instead we report the miss so the
        // customer is made whole (single-use 100% coupon) and show honest recourse.
        await ReportAiUnavailable(diag);
    }

    static JsonElement ToElement(JsonNode node) => JsonSerializer.SerializeToElement(node);

    // A local model may author the plan, but the paid session is the source of truth
    // for consent, completion, browser confirmation, and the report.  Do not offer
    // repair checkboxes until the backend has recorded the validated native plan.
    async Task<bool> RecordNativePlanAsync(Dictionary<string, object?> diagnostics, JsonElement plan, PlanOutcome outcome)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
            string payload = JsonSerializer.Serialize(new
            {
                token = _token,
                diagnostics,
                plan,
                provenance = new { provider = outcome.Provider, model = outcome.Model, dropped = outcome.Dropped }
            });
            var resp = await http.PostAsync($"{CloudRun}/api/native-plan",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode) return false;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True;
        }
        catch { return false; }
    }

    // ── AI unavailable → honest recourse (never a rule-based plan) ────────────
    // Both AI authors declined. Report the miss to the backend, which issues a
    // single-use 100% coupon (that customer, that machine) and notifies Support,
    // then show the customer an honest make-good — we never dress a non-AI plan
    // up as the AI-powered plan they paid for.
    async Task ReportAiUnavailable(Dictionary<string, object?> diag)
    {
        var (machineId, machineName) = MachineFingerprint(diag);
        string reason = _modelPresent ? "local_and_cloud_unavailable" : "no_local_model_and_cloud_unavailable";
        string? coupon = null;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            string payload = JsonSerializer.Serialize(new { token = _token, machineId, machineName, reason });
            var resp = await http.PostAsync($"{CloudRun}/api/report-ai-unavailable",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            string bodyText = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(bodyText);
            if (doc.RootElement.TryGetProperty("couponCode", out var c) && c.ValueKind == JsonValueKind.String)
                coupon = c.GetString();
        }
        catch { /* offline / backend unreachable — still show honest recourse below */ }

        ShowMakeGood(coupon);
    }

    void ShowMakeGood(string? coupon)
    {
        Cust.SetState(CustodianState.Blocked, "Your AI plan didn't run this time.");
        if (!string.IsNullOrWhiteSpace(coupon))
        {
            MakeGoodText.Text = "Your repair is AI-powered, and this time our AI couldn't build your plan. "
                + "You paid for that and didn't get it — so this one's on us. We've emailed you a 100% coupon "
                + "(a free repair on this PC) and flagged it to our team. Run Pronto PC again anytime with the code below.";
            MakeGoodCode.Text = coupon;
            MakeGoodCodeBox.Visibility = Visibility.Visible;
        }
        else
        {
            MakeGoodText.Text = "Your repair is AI-powered, and this time our AI couldn't build your plan. "
                + "You paid for that and didn't get the full benefit — so we'll make it right. We've flagged this to "
                + "our team at support@prontopc.online, and you'll receive a 100% coupon for a free repair on this PC.";
            MakeGoodCodeBox.Visibility = Visibility.Collapsed;
        }
        ShowPanel(MakeGoodPanel);
    }

    // A stable, non-identifying fingerprint of THIS machine, so the make-good is
    // scoped to the PC that missed its plan. Derived from already-collected
    // diagnostics (no extra probing): hostname + processor + OS build, hashed.
    static (string machineId, string machineName) MachineFingerprint(Dictionary<string, object?> diag)
    {
        string host = "", proc = "", build = "";
        if (diag.TryGetValue("os", out var osv) && osv is Dictionary<string, object?> os)
        {
            host = os.GetValueOrDefault("hostname")?.ToString() ?? "";
            proc = os.GetValueOrDefault("processor")?.ToString() ?? "";
            build = os.GetValueOrDefault("build")?.ToString() ?? "";
        }
        string basis = $"{host}|{proc}|{build}";
        string id;
        using (var sha = System.Security.Cryptography.SHA256.Create())
            id = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(basis))).Substring(0, 16);
        return (id, string.IsNullOrWhiteSpace(host) ? "unknown-pc" : host);
    }

    // ── Findings ─────────────────────────────────────────────────────────────
    void ShowFindings(JsonElement plan)
    {
        Cust.SetState(CustodianState.Presenting);
        string summary = plan.TryGetProperty("summary", out var s) ? (s.GetString() ?? "") : "";
        FindingsSummary.Text = summary;
        ActionList.Children.Clear();
        _rows.Clear();

        if (plan.TryGetProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in actions.EnumerateArray())
            {
                var action = JsonToDict(el);
                string name = action.GetValueOrDefault("friendly_name")?.ToString() ?? action.GetValueOrDefault("id")?.ToString() ?? "Repair";
                int mb = Mb(action.GetValueOrDefault("estimated_mb_recovered"));

                var box = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
                var text = new TextBlock
                {
                    Text = mb > 0 ? $"{name}   (~{FormatMb(mb)})" : name,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xee, 0xf1, 0xff)),
                    FontSize = 14, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0)
                };
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
                DockPanel.SetDock(box, Dock.Left);
                row.Children.Add(box);
                row.Children.Add(text);
                box.Checked += (_, __) => UpdateTotal();
                box.Unchecked += (_, __) => UpdateTotal();
                ActionList.Children.Add(row);
                _rows.Add((action, box));
            }
        }
        UpdateTotal();
        // The Technician entry is shown ONLY when governance admits a peer-facing offering.
        // AbsentGovernance (the wired default) never admits, so the entry stays hidden.
        TechnicianEntry.Visibility = _gov.OfferingAllowed(out _) ? Visibility.Visible : Visibility.Collapsed;
        ShowPanel(FindingsPanel);
    }

    void UpdateTotal()
    {
        int total = 0, count = 0;
        foreach (var (a, box) in _rows)
            if (box.IsChecked == true) { count++; total += Mb(a.GetValueOrDefault("estimated_mb_recovered")); }
        FindingsTotal.Text = count == 0 ? "Nothing selected — diagnostic only." : $"{count} selected · about {FormatMb(total)} to recover";
        ProceedButton.Content = count == 0 ? "Finish (diagnostic only)" : "Perform Repairs";
    }

    async void Proceed_Click(object sender, RoutedEventArgs e)
    {
        var approved = _rows.Where(r => r.box.IsChecked == true).Select(r => r.action).ToList();
        ProceedButton.IsEnabled = false;
        var authoritative = await RecordConsentAndStartAsync(approved);
        if (authoritative is null)
        {
            ProceedButton.IsEnabled = true;
            ShowError("We couldn't record your approval. No repairs have been run — please try again.");
            return;
        }
        await RunRepair(authoritative);
    }

    // Persist explicit per-action approval, then request the exact server-authorized
    // action list.  The desktop engine never runs a locally selected action that the
    // paid session did not acknowledge.
    async Task<List<Dictionary<string, object?>>?> RecordConsentAndStartAsync(List<Dictionary<string, object?>> approved)
    {
        try
        {
            var selectedIds = approved.Select(a => a.GetValueOrDefault("id")?.ToString() ?? "").ToArray();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
            var consent = await http.PostAsync($"{CloudRun}/api/session/{Uri.EscapeDataString(_token)}/consent",
                new StringContent(JsonSerializer.Serialize(new { selectedIds }), Encoding.UTF8, "application/json"));
            if (!consent.IsSuccessStatusCode) return null;
            using var consentDoc = JsonDocument.Parse(await consent.Content.ReadAsStringAsync());
            if (!consentDoc.RootElement.TryGetProperty("ok", out var consentOk) || consentOk.ValueKind != JsonValueKind.True) return null;

            var start = await http.PostAsync($"{CloudRun}/api/session/{Uri.EscapeDataString(_token)}/repair-start",
                new StringContent("{}", Encoding.UTF8, "application/json"));
            if (!start.IsSuccessStatusCode) return null;
            using var startDoc = JsonDocument.Parse(await start.Content.ReadAsStringAsync());
            if (!startDoc.RootElement.TryGetProperty("ok", out var startOk) || startOk.ValueKind != JsonValueKind.True
                || !startDoc.RootElement.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array) return null;

            var authoritative = actions.EnumerateArray().Select(JsonToDict).ToList();
            var authoritativeIds = authoritative.Select(a => a.GetValueOrDefault("id")?.ToString() ?? "").OrderBy(id => id);
            if (!authoritativeIds.SequenceEqual(selectedIds.OrderBy(id => id))) return null;
            return authoritative;
        }
        catch { return null; }
    }

    // ── Repair ───────────────────────────────────────────────────────────────
    async Task RunRepair(List<Dictionary<string, object?>> approved)
    {
        Cust.SetState(CustodianState.Repairing, "Starting repairs…");
        CollapseAllPanels();

        // Repair + verify phases: the Orchestrator invokes the engine and summarizes.
        // Each real progress tick fires ONE angular stroke on the Custodian — the visual
        // record of a unit of work actually being done, never decoration on a timer.
        var progress = new Progress<RepairProgress>(p =>
            Cust.ReportRepairComplete(p.Total > 0 ? $"[{p.Index}/{p.Total}] {p.Name}" : p.Name));
        var outcome = await _orch.RunRepairAsync(approved, progress);

        // Complete only after the same paid session accepts the result set and its
        // server-rendered report has been atomically saved to the customer's Desktop.
        Cust.SetActivity("Saving your report…");
        string? reportPath = await SubmitResultsAndSaveReportAsync(outcome.Results);
        if (reportPath is null)
        {
            ShowError("Your repair results could not be confirmed and saved. The actions already run are shown only after the connection is restored and the report is saved.");
            return;
        }

        Cust.SetState(CustodianState.Complete, "Your PC has been serviced.");
        CompleteText.Text = $"{outcome.Message}\n\nConfirmed repair report saved to:\n{reportPath}";
        ShowPanel(CompletePanel);
    }

    async Task<string?> SubmitResultsAndSaveReportAsync(List<Dictionary<string, object?>> results)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            string payload = JsonSerializer.Serialize(new { token = _token, results });
            var submitted = await http.PostAsync($"{CloudRun}/api/submit-repair-results",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            if (!submitted.IsSuccessStatusCode) return null;
            using var resultDoc = JsonDocument.Parse(await submitted.Content.ReadAsStringAsync());
            if (!resultDoc.RootElement.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True) return null;

            var reportResponse = await http.GetAsync($"{CloudRun}/api/session/{Uri.EscapeDataString(_token)}/report");
            if (!reportResponse.IsSuccessStatusCode) return null;
            string reportHtml = await reportResponse.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(reportHtml)) return null;

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktop)) return null;
            System.IO.Directory.CreateDirectory(desktop);
            string name = $"ProntoPC_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html";
            string path = System.IO.Path.Combine(desktop, name);
            string temporary = path + ".tmp";
            await System.IO.File.WriteAllTextAsync(temporary, reportHtml, Encoding.UTF8);
            System.IO.File.Move(temporary, path, overwrite: false);
            return path;
        }
        catch { return null; }
    }

    // ── UI helpers ───────────────────────────────────────────────────────────
    // The Custodian (the Actuator) is the persistent visual across every phase; the
    // content panels below it carry the interactive controls for the current phase.
    void ShowError(string msg) { Cust.SetState(CustodianState.Blocked, msg); ErrorText.Text = msg; ShowPanel(ErrorPanel); }

    void CollapseAllPanels()
    {
        GatePanel.Visibility = Visibility.Collapsed;
        FindingsPanel.Visibility = Visibility.Collapsed;
        CompletePanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        MakeGoodPanel.Visibility = Visibility.Collapsed;
        ChatPanel.Visibility = Visibility.Collapsed;
    }

    void ShowPanel(UIElement panel) { CollapseAllPanels(); panel.Visibility = Visibility.Visible; }

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    // Open the bundled AI-model + third-party license notices (Gemma Terms of Use +
    // Prohibited Use Policy). Shipped as Content, so present in every install.
    void OpenLicenses_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "licenses", "GEMMA-NOTICE.txt");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { /* viewer unavailable — the file still ships in the install folder */ }
    }

    // ── Technician conversation (Phase 2) — every path runs through _gov ───────
    // While _gov is AbsentGovernance the entry is hidden, so none of these run in a
    // shipped build. They exist so the whole conversation drops in the moment an OPEN
    // IPeerGovernance is wired — and, as defense in depth, each re-checks the gate so a
    // closed governance can never reach the model.
    void OpenChat_Click(object sender, RoutedEventArgs e)
    {
        // Never open the conversation unless governance admits the offering.
        if (!_gov.OfferingAllowed(out _)) return;
        string modelDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "models", "gemma-3-1b-it");
        // The Custodian is seeded with the governance-supplied system prompt (Canon + RBC framing).
        _chat ??= new CustodianChat(OnnxGenAiInference.CreateOrAbsent(modelDir, "gemma-3-1b-it"), _gov.SystemPrompt);
        ShowPanel(ChatPanel);
        ChatInput.Focus();
    }

    void ChatInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; ChatSend_Click(sender, e); }
    }

    async void ChatSend_Click(object sender, RoutedEventArgs e)
    {
        string text = ChatInput.Text?.Trim() ?? "";
        if (text.Length == 0) return;
        AppendChat("You", text);
        ChatInput.Clear();

        // PIM inbound: screen the customer's text BEFORE it can reach the model. A non-null
        // return blocks it — AbsentGovernance refuses everything, so nothing is sent while gated.
        string? refusal = _gov.ScreenInbound(text);
        if (refusal != null) { AppendChat("Technician", refusal); return; }
        if (_chat is null || !_chat.Available) { AppendChat("Technician", "The Technician isn't available right now."); return; }

        _gov.LogTurn("user", text);
        ChatSendButton.IsEnabled = false;
        try
        {
            string? reply = await _chat.ReplyAsync(text, CancellationToken.None);
            if (string.IsNullOrWhiteSpace(reply))
            {
                AppendChat("Technician", "I couldn't put that into words just now — could you ask again?");
                return;
            }
            // RBC / Prism outbound: apply Reflective Boundary Conditions before the reply is shown.
            reply = _gov.ApplyBoundary(reply);
            _gov.LogTurn("model", reply);
            AppendChat("Technician", reply);
        }
        finally { ChatSendButton.IsEnabled = true; }
    }

    void ChatBack_Click(object sender, RoutedEventArgs e) => ShowPanel(FindingsPanel);

    void AppendChat(string who, string text)
    {
        var block = new TextBlock
        {
            Foreground = new SolidColorBrush(who == "You"
                ? Color.FromRgb(0xee, 0xf1, 0xff) : Color.FromRgb(0xa7, 0x8b, 0xfa)),
            TextWrapping = TextWrapping.Wrap, FontSize = 13.5, Margin = new Thickness(0, 0, 0, 10)
        };
        block.Inlines.Add(new System.Windows.Documents.Run(who + ": ") { FontWeight = FontWeights.Bold });
        block.Inlines.Add(new System.Windows.Documents.Run(text));
        ChatLog.Children.Add(block);
        ChatScroll.ScrollToEnd();
    }

    static int Mb(object? v) { int m = 0; int.TryParse(v?.ToString(), out m); return m; }
    static string FormatMb(int mb) => mb >= 1024 ? $"{mb / 1024.0:0.0} GB" : $"{mb} MB";

    static Dictionary<string, object?> JsonToDict(JsonElement el)
    {
        var d = new Dictionary<string, object?>();
        if (el.ValueKind == JsonValueKind.Object)
            foreach (var p in el.EnumerateObject())
                d[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Number => p.Value.TryGetInt64(out var l) ? (object)l : p.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => p.Value.ToString()
                };
        return d;
    }
}
