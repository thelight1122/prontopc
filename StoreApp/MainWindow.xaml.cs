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

    // ── Technician → Communications relay seam (append-only, hash-bound) ───────
    // The inter-Orb seam made an APPEND-ONLY message rather than a mutable call. The
    // Computer Technician Orb PUBLISHES its validated, SystemAudit-bound findings here
    // ONCE (after AuthorPlanAsync); the Communications & Governance Orb — the Prism side —
    // READS only its own session's findings to explain what is already true. This opens no
    // gate and executes nothing (Manifest I2). Null only when the Action Envelope can't be
    // keyed — in which case the plan phase itself is already degraded, so there is nothing
    // valid to relay.
    readonly FindingsRelayIntegration? _findingsRelay;

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
        // sequences the phases. The bundled local-model Satellite is the only desktop
        // plan author: diagnostics are never sent to a cloud AI fallback. An unkeyed
        // envelope or a local decline is presented honestly and does not enable repairs.
        string modelDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "models", "gemma-3-1b-it");
        _modelPresent = System.IO.File.Exists(System.IO.Path.Combine(modelDir, "genai_config.json"));
        _orch = new Orchestrator(new IPlanAuthor[]
        {
            new GemmaLocalPlanner(OnnxGenAiInference.CreateOrAbsent(modelDir, "gemma-3-1b-it")),
        });
        _findingsRelay = BuildFindingsRelay();
        Loaded += async (_, __) => await Start();
    }

    // Compose the Technician→Communications relay. The relay file (Local app data) and its
    // head anchor (Roaming app data) sit in SEPARATE failure domains, so tail-truncation or
    // whole-file replacement is illuminated rather than silent (TOTEM-C-0110). Both are
    // per-user, admin-free paths. Returns null if the Action Envelope can't be loaded — the
    // app still runs; there is simply no relay, never one bound to a mismatched envelope.
    static FindingsRelayIntegration? BuildFindingsRelay()
    {
        try
        {
            var envelope = Envelope.Load();
            string relayPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProntoPC", "relay", "findings.jsonl");
            string anchorPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ProntoPC", "relay-anchor", "findings.head.json");
            var relay = new ProntoPC.Governance.AppendOnlyFindingsRelay(relayPath, clock: null, anchorPath: anchorPath);
            return new FindingsRelayIntegration(relay, envelope);
        }
        catch { return null; }
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

        // System Audit precondition (TOTEM-X-0074): the application (not the model)
        // validates the read-only diagnostics into an immutable, evidence-hashed audit.
        // Only a completed, valid audit may reach the model / findings / consent / repair;
        // an incomplete or failed audit is declined here with no model invocation.
        var audit = SystemAuditor.Assess(diag);

        // Plan phase: the bundled local model authors a plan from the validated audit and
        // the Action Envelope validates it before it reaches consent. No cloud AI fallback.
        var outcome = await _orch.AuthorPlanAsync(audit);
        if (outcome.IsOk)
        {
            var plan = ToElement(outcome.Plan!);
            if (await RecordNativePlanAsync(diag, plan, outcome))
            {
                // Technician Orb → the append-only relay: post the validated findings ONCE for
                // this session so the Communications Orb can later READ (never re-derive) them.
                PublishFindingsToRelay(audit, plan, outcome);
                ShowFindings(plan);
                return;
            }
            ShowError("We couldn't securely record this repair plan. No repairs have been run — please try again.");
            return;
        }

        ShowLocalPlanUnavailable(outcome.DeclineReason);
    }

    static JsonElement ToElement(JsonNode node) => JsonSerializer.SerializeToElement(node);

    // Technician Orb → the append-only relay. Publishes this session's findings ONCE: the
    // integration relays nothing unless the SystemAudit is valid and each action is Action-
    // Envelope admissible, and binds the record to the audit's own EvidenceHash. Best-effort by
    // design — the relay is a record for the peer-facing Communications Orb, which stays gated
    // CLOSED in shipped builds, so a relay hiccup (or a pre-existing tamper throw) must never
    // block the customer's already-paid repair flow. Opens no gate; executes nothing.
    void PublishFindingsToRelay(SystemAudit audit, JsonElement plan, PlanOutcome outcome)
    {
        try { _findingsRelay?.PublishFindings(_token, audit, plan, outcome.Provider, outcome.Model); }
        catch { /* append-only record for the gated Communications Orb — never disrupt the paid flow */ }
    }

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

    // ── Local plan declined → honest, local diagnostic ───────────────────────
    // A local-model failure must not invoke a cloud AI provider, nor claim that one
    // did. The stable code is safe to show and lets support distinguish runtime,
    // parsing, and Envelope validation without exposing model text or exception data.
    void ShowLocalPlanUnavailable(string reason)
    {
        string safeReason = string.IsNullOrWhiteSpace(reason) ? "local_author_declined" : reason;
        Cust.SetState(CustodianState.Blocked, "Your local AI plan needs attention.");
        MakeGoodText.Text = "Pronto PC couldn't validate a plan from the on-device model. "
            + "No diagnostics were sent to a cloud AI service, and no repairs have been run.\n\n"
            + "Local diagnostic: " + safeReason + "\n\n"
            + "Please contact support@prontopc.online with this code so we can correct the local model path.";
        MakeGoodCodeBox.Visibility = Visibility.Collapsed;
        ShowPanel(MakeGoodPanel);
    }

    // ── Findings ─────────────────────────────────────────────────────────────
    void ShowFindings(JsonElement plan)
    {
        Cust.SetState(CustodianState.Presenting);
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
                    Text = name,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xee, 0xf1, 0xff)),
                    FontSize = 14, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 10, 0)
                };
                var row = new DockPanel { LastChildFill = true };
                DockPanel.SetDock(box, Dock.Left);
                row.Children.Add(box);
                if (mb > 0)
                {
                    // Reclaim amount pinned to the right so the value the customer cares about
                    // reads clearly against the item name.
                    var amount = new TextBlock
                    {
                        Text = $"~{FormatMb(mb)}",
                        Foreground = new SolidColorBrush(Color.FromRgb(0x5b, 0xe3, 0xa0)),
                        FontSize = 13, FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 2, 0)
                    };
                    DockPanel.SetDock(amount, Dock.Right);
                    row.Children.Add(amount);
                }
                row.Children.Add(text);
                // Each repair is its own card so the list is clearly visible and every item
                // the app will run is legible at a glance (not faint text on the dark ground).
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x16, 0x19, 0x3a)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x2f, 0x35, 0x6a)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 0, 0, 8),
                    Child = row
                };
                box.Checked += (_, __) => UpdateTotal();
                box.Unchecked += (_, __) => UpdateTotal();
                ActionList.Children.Add(card);
                _rows.Add((action, box));
            }
        }
        UpdateTotal();

        // Grounded, templated narration of the COMPLETE findings — deterministic text
        // built from the validated plan (not the model's free-form summary, which can be
        // garbled or drift from what was actually measured). The action list below carries
        // the completeness-backstop additions (Envelope, TOTEM-C-0060), so this line and the
        // list together are the whole, honest picture.
        int foundCount = _rows.Count;
        int foundMb = _rows.Sum(r => Mb(r.action.GetValueOrDefault("estimated_mb_recovered")));
        // How many findings were surfaced by the completeness backstop (TOTEM-C-0060):
        // measured, admissible cleanups our scan added because the AI plan left them out.
        // Calling these out is an honest trust signal — the customer sees the list is complete.
        int addedCount = plan.TryGetProperty("completeness_added", out var added) && added.ValueKind == JsonValueKind.Array
            ? added.GetArrayLength() : 0;
        string addedNote = addedCount == 0 ? ""
            : addedCount == foundCount
                ? (addedCount == 1
                    ? " Our scan measured this one directly, to be sure nothing was missed."
                    : $" Our scan measured all {addedCount} directly, to be sure nothing was missed.")
                : $" {addedCount} of {(addedCount == 1 ? "them was" : "them were")} measured directly by our scan and added so nothing was missed.";
        FindingsSummary.Text = foundCount == 0
            ? "Good news — our read-only scan didn't find anything on this PC that needs repair. Nothing has been changed."
            : $"Our read-only scan is complete. We found {foundCount} item{(foundCount == 1 ? "" : "s")} worth repairing, listed below."
              + (foundMb > 0 ? $" We can safely reclaim about {FormatMb(foundMb)} of space." : "")
              + addedNote
              + " Nothing has been changed on this PC yet.";

        // Enter the X-0057 findings-stage question gate: present findings + the pause for
        // questions, with the separate repair-consent step hidden until the customer
        // continues. Reset each time findings are shown.
        FindingsQa.Visibility = Visibility.Collapsed;
        AskQuestionButton.Content = "I have a question";
        FindingsGate.Visibility = Visibility.Visible;
        FindingsConsent.Visibility = Visibility.Collapsed;

        // The Technician entry is shown ONLY when governance admits a peer-facing offering.
        // AbsentGovernance (the wired default) never admits, so the entry stays hidden.
        TechnicianEntry.Visibility = _gov.OfferingAllowed(out _) ? Visibility.Visible : Visibility.Collapsed;
        ShowPanel(FindingsPanel);
    }

    // ── X-0057 findings-stage question gate (scripted; no generative model) ─────
    // Toggle the service-only Q&A grounded in the presented findings. This is static
    // templated content — it does NOT invoke CustodianChat / the on-device model, so
    // the AEGIS §8 peer-facing gate stays closed.
    void ToggleQa_Click(object sender, RoutedEventArgs e)
    {
        bool show = FindingsQa.Visibility != Visibility.Visible;
        FindingsQa.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        AskQuestionButton.Content = show ? "Hide questions" : "I have a question";
    }

    // The customer has no (further) questions: advance from the question gate to the
    // separate repair-consent continuation. No repair runs here — the explicit consent
    // is still the "Perform Repairs" button in FindingsConsent.
    void GateContinue_Click(object sender, RoutedEventArgs e)
    {
        FindingsGate.Visibility = Visibility.Collapsed;
        FindingsConsent.Visibility = Visibility.Visible;
        Cust.SetState(CustodianState.Presenting, "Review the repairs below, then choose Perform Repairs when you're ready.");
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
        // The Custodian is seeded with the governance-supplied system prompt (Canon + RBC framing),
        // GROUNDED in this session's findings read back from the Technician Orb through the relay.
        // The read is the Communications Orb's side of the seam; the Prism (_gov: PIM→IRG→RBC→IDS)
        // still governs every turn — this only supplies the true subject matter to explain.
        _chat ??= new CustodianChat(OnnxGenAiInference.CreateOrAbsent(modelDir, "gemma-3-1b-it"), ComposeCommsSeed(_gov.SystemPrompt));
        ShowPanel(ChatPanel);
        ChatInput.Focus();
    }

    // Communications & Governance Orb → the append-only relay: fold THIS session's authorized
    // findings into the Custodian seed. Reads ONLY the session this window holds (held ==
    // requested == _token — the session-boundary authorization); a mismatch or an absent/
    // unverifiable record yields the ungrounded seed. Read-only, executes nothing, opens no gate.
    string ComposeCommsSeed(string systemPrompt)
    {
        try
        {
            var findings = _findingsRelay?.ReadAuthorizedFindings(_token, _token);
            if (findings is null) return systemPrompt;
            var sb = new StringBuilder();
            sb.Append("Findings already established by the read-only diagnostic (explain only these; invent nothing further): ");
            sb.Append(findings.PlanSummary);
            if (findings.Actions.Length > 0)
            {
                sb.Append(" Recommended repairs: ");
                sb.Append(string.Join("; ", findings.Actions.Select(a => a.Title)));
                sb.Append('.');
            }
            string grounded = sb.ToString();
            return string.IsNullOrWhiteSpace(systemPrompt) ? grounded : systemPrompt + "\n\n" + grounded;
        }
        catch { return systemPrompt; }
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
