using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ProntoPC;

public partial class MainWindow : Window
{
    string _sessionId = Guid.NewGuid().ToString("N");
    readonly string _repairRequestPath;
    readonly List<(Dictionary<string, object?> action, CheckBox box)> _rows = new();
    readonly Orchestrator _orch;
    readonly ILocalInference _localInference;
    readonly bool _modelPresent;
    Task<ModelWarmupResult>? _modelWarmup;
    readonly CancellationTokenSource _warmupCancellation = new();

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
    // (ProntoPC.Governance.*). OPENED under Tracey's explicit §8 greenlight (TOTEM-C-0142):
    // enabled:true admits the live Technician. The Canon is the model's embedded formed
    // disposition (in-model:formed-disposition; CanonBoundarySeed.Resolve is always Available,
    // TOTEM-C-0143), so no runtime Canon file is required — it works on every install. Every
    // inbound is still PIM-screened and every reply passes RBC/Prism before it is shown.
    readonly IPeerGovernance _gov = new PipelineGovernance(enabled: true);
    CustodianChat? _chat;

    // Remembered across a plan-authoring retry so the Retry screen can re-attempt WITHOUT
    // re-running the multi-minute diagnostic, and so the make-good / refund routes keep context.
    Dictionary<string, object?>? _lastDiag;
    JsonElement? _lastDiagnostics;
    JsonElement? _lastPlan;
    string _lastHostname = "";
    string? _reportPath;   // set when the repair report is saved; opened by the Open Report button
    SystemAudit? _lastAudit;
    string? _lastDecline;
    StoreUpdateChecker? _updateChecker;

    public MainWindow(string repairRequestPath = "")
    {
        InitializeComponent();
        Closed += (_, __) => _warmupCancellation.Cancel();
        _repairRequestPath = repairRequestPath;
        // The Orchestrator holds the Action Envelope, keys the plan Umbilical to it, and
        // sequences the phases. The bundled local-model Satellite is the only desktop
        // plan author: diagnostics are never sent to a cloud AI fallback. An unkeyed
        // envelope or a local decline is presented honestly and does not enable repairs.
        string modelDir = ProductModel.DirectoryPath;
        _modelPresent = ProductModel.IsPresentAndBound();
        _localInference = OnnxGenAiInference.CreateOrAbsent(modelDir, ProductModel.Name);
        _orch = new Orchestrator(new IPlanAuthor[]
        {
            new GemmaLocalPlanner(_localInference),
        });
        _findingsRelay = BuildFindingsRelay();
        Loaded += async (_, __) =>
        {
            Cust.SetState(CustodianState.Scanning, "Reading your system… nothing on your PC is changed.");
            CollapseAllPanels();
            ShowWorkingArt("pronto-auditor.png");
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            _ = CheckForUpdatesAfterFirstPaintAsync();
            await Start();
        };
    }


    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Certification devices can have smaller work areas or higher display scaling.
        // Keep the window inside the visible desktop so the title bar and bottom action
        // buttons remain reachable, and let terminal panels scroll when content is taller.
        var maxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 32);
        MaxHeight = maxHeight;
        if (Height > maxHeight) Height = maxHeight;
        if (Top < SystemParameters.WorkArea.Top) Top = SystemParameters.WorkArea.Top;
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

    // Entry: Store acquisition owns payment. Once installed, the app is usable without
    // a runtime account, token, checkout, or server round-trip.
    async Task Start()
    {
        if (!string.IsNullOrWhiteSpace(_repairRequestPath))
        {
            await ResumeElevatedRepair(_repairRequestPath);
            return;
        }
        await RunScan();
    }

    async Task CheckForUpdatesOnLaunchAsync()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            _updateChecker = new StoreUpdateChecker(handle);
            var result = await _updateChecker.CheckAsync();
            if (!result.IsUpdateAvailable)
                return;

            UpdateBannerText.Text = result.Message;
            UpdateBanner.Visibility = Visibility.Visible;
        }
        catch
        {
            // Update checks are informational. Scan and repair must remain available offline.
        }
    }

    async Task CheckForUpdatesAfterFirstPaintAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            await Dispatcher.InvokeAsync(
                () => CheckForUpdatesOnLaunchAsync(),
                DispatcherPriority.Background).Task.Unwrap();
        }
        catch
        {
            // Update checks are informational. Scan and repair must remain available offline.
        }
    }

    async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_updateChecker is null)
            return;

        InstallUpdateButton.IsEnabled = false;
        InstallUpdateButton.Content = "Updating...";
        try
        {
            UpdateBannerText.Text = await _updateChecker.RequestInstallAsync();
        }
        catch
        {
            UpdateBannerText.Text = "Update could not be started right now.";
        }
        finally
        {
            InstallUpdateButton.Content = "Update";
            InstallUpdateButton.IsEnabled = true;
        }
    }

    // ── Scan ─────────────────────────────────────────────────────────────────
    async Task RunScan()
    {
        Cust.SetState(CustodianState.Scanning, "Reading your system… nothing on your PC is changed.");
        CollapseAllPanels();
        ShowWorkingArt("pronto-auditor.png");   // auditor present through scan + plan authoring
        StartModelWarmupDuringScan();
        var diag = await Task.Run(() => Diagnostics.Collect());

        // System Audit precondition (TOTEM-X-0074): the application (not the model)
        // validates the read-only diagnostics into an immutable, evidence-hashed audit.
        // Only a completed, valid audit may reach the model / findings / consent / repair;
        // an incomplete or failed audit is declined here with no model invocation.
        var audit = SystemAuditor.Assess(diag);

        // Keep the validated audit + diagnostics so the customer can re-attempt plan authoring
        // from the Retry screen WITHOUT re-running the multi-minute diagnostic.
        _lastDiag = diag;
        _lastAudit = audit;
        _lastDiagnostics = audit.Diagnostics.Clone();
        _lastHostname = HostnameFromDiagnostics(diag);

        await AuthorPlanAndProceed();
    }

    void StartModelWarmupDuringScan()
    {
        if (_modelWarmup is not null) return;
        _modelWarmup = ModelWarmup.PrepareAsync(_localInference, null, _warmupCancellation.Token);
    }

    // Plan phase (retryable): the bundled local model authors a plan from the validated audit and
    // the Action Envelope validates it before it reaches consent. No cloud AI fallback. Runs on the
    // first pass and again when the customer taps "Yes, Attempt Again" on the Retry screen.
    async Task AuthorPlanAndProceed()
    {
        if (_lastAudit is null || _lastDiag is null)
        {
            ShowError("Something went wrong before the plan step. Please run Pronto PC again.");
            return;
        }

        CollapseAllPanels();
        StartModelWarmupDuringScan();
        ModelWarmupResult ready;
        try { ready = await _modelWarmup!; }
        catch (OperationCanceledException) { return; }
        if (_warmupCancellation.IsCancellationRequested) return;
        if (!ready.IsReady)
        {
            ShowError(ready.Reason);
            return;
        }
        Cust.SetState(CustodianState.Thinking, "AI ready. Building your personalized repair plan… The AI runs entirely on your PC.");
        ShowWorkingArt("pronto-auditor.png");

        var outcome = await _orch.AuthorPlanAsync(_lastAudit);
        if (outcome.IsOk)
        {
            var plan = ToElement(outcome.Plan!);
            _lastPlan = plan.Clone();
            // Technician Orb → the append-only relay: post the validated findings ONCE for
            // this session so the Communications Orb can later READ (never re-derive) them.
            PublishFindingsToRelay(_lastAudit, plan, outcome);
            ShowFindings(plan);
            return;
        }

        // The orchestrator already auto-retried the on-device author; a miss here is rarer.
        // Offer an explicit Retry screen (with self-help tips) before the make-good / refund routes.
        _lastDecline = outcome.DeclineReason;
        RecordPlanDecline(outcome.DeclineReason);
        ShowRetry();
    }


    void RecordPlanDecline(string reason)
    {
        try
        {
            var logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProntoPC", "logs");
            System.IO.Directory.CreateDirectory(logDir);
            var version = typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "unknown";
            var line = $"{DateTimeOffset.UtcNow:O}|version={version}|reason={(reason ?? "unknown").Replace("\r", " ").Replace("\n", " ")}|model_present={System.IO.File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "ProntoPlan", "genai_config.json"))}";
            System.IO.File.AppendAllText(System.IO.Path.Combine(logDir, "plan-authoring.log"), line + Environment.NewLine);
        }
        catch { }
    }
    void ShowRetry()
    {
        Cust.SetState(CustodianState.Blocked, "The repair plan did not build that time.");
        ShowPanel(RetryPanel);
    }

    async void RetryAuthor_Click(object sender, RoutedEventArgs e) => await AuthorPlanAndProceed();

    // "Still stuck?" — take the customer to honest support/refund guidance. No backend
    // coupon minting exists in the one-time Store model.
    async void GiveUpToMakeGood_Click(object sender, RoutedEventArgs e)
    {
        await Task.Yield();
        ShowMakeGood();
    }

    // Store purchases are handled by Microsoft. The app gives clear recourse without
    // calling any ProntoPC backend to issue refunds.
    async void RequestRefund_Click(object sender, RoutedEventArgs e) => await RequestRefundAsync();

    async Task RequestRefundAsync()
    {
        await Task.Yield();
        ShowRefundOutcome();
    }

    void ShowRefundOutcome()
    {
        RefundTitle.Text = "About refunds";
        RefundText.Text = "ProntoPC is now purchased through Microsoft Store. If you need help or believe the app did not deliver what you paid for, contact support@prontopc.online and use Microsoft's Store purchase/refund options for the transaction.";
        Cust.SetState(CustodianState.Blocked, "Refund guidance shown.");
        ShowPanel(RefundPanel);
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
        try { _findingsRelay?.PublishFindings(_sessionId, audit, plan, outcome.Provider, outcome.Model); }
        catch { /* append-only record for the gated Communications Orb — never disrupt the paid flow */ }
    }

    // ── Local plan declined → honest, local diagnostic ───────────────────────
    // A local-model failure must not invoke a cloud AI provider, nor claim that one
    // did. The stable code is safe to show and lets support distinguish runtime,
    // parsing, and Envelope validation without exposing model text or exception data.
    // After a validated audit, if the on-device author cannot produce a plan, ProntoPC
    // never substitutes a non-AI plan under the AI claim. It shows a local support code
    // so the customer has a clear next step and the model/runtime failure is visible.
    void ShowMakeGood()
    {
        Cust.SetState(CustodianState.Blocked, "Your AI plan didn't run this time.");
        string reason = !string.IsNullOrWhiteSpace(_lastDecline)
            ? _lastDecline!
            : (_modelPresent ? "local_author_declined" : "no_local_model");
        MakeGoodText.Text = "Your repair is AI-powered, and this time the on-device AI could not build a valid repair plan. "
            + $"Nothing on your PC has changed. Please contact support@prontopc.online and include this code: {reason}.";
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
                // The Store-cache reset needs the Microsoft Store app; don't offer it where it's absent.
                if (string.Equals(action.GetValueOrDefault("id")?.ToString(), "clear_windows_store_cache", StringComparison.OrdinalIgnoreCase)
                    && !Repair.StoreAppInstalled())
                    continue;
                // The row reads from the deterministic FindingCatalog keyed by action id, never the
                // model-authored friendly_name. The title is written back into the action so the
                // repair progress line and the report use the same name the customer approved.
                string id = action.GetValueOrDefault("id")?.ToString() ?? "";
                var entry = FindingCatalog.For(id);
                action["friendly_name"] = entry.Title;
                string evidence = _lastDiagnostics is JsonElement diagEl ? FindingCatalog.Evidence(id, diagEl, action) : "";
                action["finding_evidence"] = evidence;
                int mb = Mb(action.GetValueOrDefault("estimated_mb_recovered"));

                var box = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
                var text = new StackPanel { Margin = new Thickness(10, 0, 10, 0) };
                text.Children.Add(new TextBlock
                {
                    Text = entry.Title,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xee, 0xf1, 0xff)),
                    FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap
                });
                text.Children.Add(new TextBlock
                {
                    Text = entry.What,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xc3, 0xc8, 0xe6)),
                    FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0)
                });
                if (evidence.Length > 0)
                    text.Children.Add(new TextBlock
                    {
                        Text = evidence,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x8f, 0xd8, 0xb8)),
                        FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0)
                    });
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

        // §8 OPEN (TOTEM-C-0142): the findings step IS the live on-device Technician. Seed the
        // Canon-formed chat grounded in THIS session's findings (via the relay), then post a
        // factual grounded opening (no model call). The customer can ask real questions before
        // choosing Perform Repairs / Skip Repair; model inference happens only on an actual question,
        // and every turn is PIM-screened inbound and RBC/Prism-shaped outbound.
        ChatLog.Children.Clear();
        _chat = new CustodianChat(
            OnnxGenAiInference.CreateOrAbsent(ProductModel.DirectoryPath, ProductModel.Name),
            ComposeCommsSeed(_gov.SystemPrompt));
        AppendChat("Technician", FindingsSummary.Text
            + " Ask me anything about what I found, or choose Perform Repairs to run the checked items — or Skip Repair.");
        ShowPanel(FindingsPanel);
    }

    // Skip Repair: the customer declines the repairs. Honest — nothing on the PC is changed and
    // no repair runs; goes to the completion screen marked "no repairs performed".
    void SkipRepair_Click(object sender, RoutedEventArgs e)
    {
        Cust.SetState(CustodianState.Complete, "No repairs were performed.");
        CompleteText.Text = "You chose to skip the repairs, so nothing on this PC was changed. "
            + "Your read-only diagnostic is complete — you can run Pronto PC again anytime.";
        _reportPath = null;
        OpenReportButton.Visibility = Visibility.Collapsed;   // no repair ran — no report to open
        ShowPanel(CompletePanel);
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
        if (approved.Count > 0 && !IsAdmin())
        {
            if (RequestElevatedRepair(approved))
            {
                Application.Current.Shutdown();
                return;
            }
            ProceedButton.IsEnabled = true;
            ShowError("Windows did not allow ProntoPC to start the repair step as administrator. No repairs have been run. Choose Perform Repairs to attempt it again.");
            return;
        }

        await RunRepair(approved);
    }

    async Task ResumeElevatedRepair(string requestPath)
    {
        if (!IsAdmin())
        {
            ShowError("ProntoPC needs administrator approval before it can perform the selected repairs. Please choose Perform Repairs again and approve the Windows prompt.");
            return;
        }
        ElevatedRepairRequest? request;
        try
        {
            string json = await System.IO.File.ReadAllTextAsync(requestPath);
            using var doc = JsonDocument.Parse(json);
            request = ReadElevatedRequest(doc.RootElement);
        }
        catch
        {
            ShowError("ProntoPC could not read the approved repair list. No repairs have been run — please return to the app and attempt it again.");
            return;
        }
        finally
        {
            try { System.IO.File.Delete(requestPath); } catch { }
        }

        if (request is null)
        {
            ShowError("ProntoPC could not verify the approved repair list. No repairs have been run — please return to the app and attempt it again.");
            return;
        }

        _sessionId = request.SessionId;
        _lastDiagnostics = request.Diagnostics;
        _lastPlan = request.RepairPlan;
        _lastHostname = request.Hostname;

        if (request.ApprovedActions.Count == 0)
        {
            SkipRepair_Click(this, new RoutedEventArgs());
            return;
        }

        Cust.SetState(CustodianState.Repairing, "Starting your approved repairs…");
        CollapseAllPanels();
        ShowWorkingArt("pronto-repair.png");
        await RunRepair(request.ApprovedActions);
    }

    bool RequestElevatedRepair(List<Dictionary<string, object?>> approved)
    {
        try
        {
            if (_lastDiagnostics is not JsonElement diagnostics || _lastPlan is not JsonElement repairPlan)
                return false;
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProntoPC", "repair-requests");
            System.IO.Directory.CreateDirectory(dir);
            string requestPath = System.IO.Path.Combine(dir, "repair-" + Guid.NewGuid().ToString("N") + ".json");
            string json = JsonSerializer.Serialize(new
            {
                sessionId = _sessionId,
                diagnostics,
                repairPlan,
                approvedActions = approved,
                hostname = _lastHostname
            });
            System.IO.File.WriteAllText(requestPath, json, Encoding.UTF8);

            string exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName!;
            string args = "--repair-request=" + QuoteArg(requestPath);
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                Verb = "runas",
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static string QuoteArg(string value) => "\"" + Uri.EscapeDataString(value).Replace("\"", "%22") + "\"";

    static bool IsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    // ── Repair ───────────────────────────────────────────────────────────────
    async Task RunRepair(List<Dictionary<string, object?>> approved)
    {
        Cust.SetState(CustodianState.Repairing, "Starting repairs…");
        CollapseAllPanels();
        ShowWorkingArt("pronto-repair.png");     // repair tech present while repairs run

        // Repair + verify phases: the Orchestrator invokes the engine and summarizes.
        // Each real progress tick fires ONE angular stroke on the Custodian — the visual
        // record of a unit of work actually being done, never decoration on a timer.
        // Detail ticks (folder / tool, observe-only) update the live detail box without firing a
        // stroke, so a stroke still means one repair action started.
        RepairDetailBox.Visibility = Visibility.Visible;
        RepairDetailTitle.Text = "Starting repairs…";
        RepairDetailText.Text = "";
        var progress = new Progress<RepairProgress>(p =>
        {
            string label = p.Total > 0 ? $"[{p.Index}/{p.Total}] {p.Name}" : p.Name;
            if (p.Detail is null)
            {
                Cust.ReportRepairComplete(label);
                RepairDetailTitle.Text = label;
                RepairDetailText.Text = FindingCatalog.For(approved.ElementAtOrDefault(p.Index - 1)?.GetValueOrDefault("id")?.ToString()).What;
            }
            else
            {
                RepairDetailTitle.Text = label;
                RepairDetailText.Text = p.Detail;
            }
        });
        var outcome = await _orch.RunRepairAsync(approved, progress);
        RepairDetailBox.Visibility = Visibility.Collapsed;

        // Complete only after the selected local actions finish and the on-device
        // report has been atomically saved to the customer's Desktop.
        Cust.SetActivity("Saving your report…");
        string? reportPath = await SubmitResultsAndSaveReportAsync(outcome.Results);
        if (reportPath is null)
        {
            ShowError("Your repair actions finished, but ProntoPC could not save the local report. Please contact support@prontopc.online.");
            return;
        }

        Cust.SetState(CustodianState.Complete, "Your PC has been serviced.");
        CompleteText.Text = $"{outcome.Message}\n\nRepair report saved to:\n{reportPath}";
        _reportPath = reportPath;
        OpenReportButton.Visibility = Visibility.Visible;   // a report exists — offer to open it
        ShowPanel(CompletePanel);
    }

    async Task<string?> SubmitResultsAndSaveReportAsync(List<Dictionary<string, object?>> results)
    {
        try
        {
            if (_lastDiagnostics is not JsonElement diagnostics || _lastPlan is not JsonElement repairPlan)
                return null;
            JsonElement repairResults = JsonSerializer.SerializeToElement(results);
            string reportHtml = LocalReport.Generate(
                diagnostics,
                repairPlan,
                repairResults,
                _lastHostname);
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
    // Every error screen offers Close; when the customer came from a screen they can return to
    // (e.g. the completion screen), it also offers Back to that screen.
    UIElement? _errorBackTarget;
    void ShowError(string msg, UIElement? backTo = null)
    {
        Cust.SetState(CustodianState.Blocked, msg);
        ErrorText.Text = msg;
        _errorBackTarget = backTo;
        ErrorBackButton.Visibility = backTo is null ? Visibility.Collapsed : Visibility.Visible;
        ShowPanel(ErrorPanel);
    }

    void ErrorBack_Click(object sender, RoutedEventArgs e)
    {
        if (_errorBackTarget is null) return;
        if (_errorBackTarget == CompletePanel) Cust.SetState(CustodianState.Complete, "Your PC has been serviced.");
        ShowPanel(_errorBackTarget);
    }

    void CollapseAllPanels()
    {
        FindingsPanel.Visibility = Visibility.Collapsed;
        CompletePanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        MakeGoodPanel.Visibility = Visibility.Collapsed;
        RetryPanel.Visibility = Visibility.Collapsed;
        RefundPanel.Visibility = Visibility.Collapsed;
        WorkingArt.Visibility = Visibility.Collapsed;
        RepairDetailBox.Visibility = Visibility.Collapsed;
    }

    // Working-phase character art. Decoded once at a capped height (the source PNGs
    // are large), cached, and frozen so the same image is reused across phases.
    readonly Dictionary<string, BitmapImage> _artCache = new();
    void ShowWorkingArt(string file)
    {
        if (!_artCache.TryGetValue(file, out var art))
        {
            art = new BitmapImage();
            art.BeginInit();
            art.UriSource = new Uri($"pack://application:,,,/Assets/{file}");
            art.DecodePixelHeight = 720;              // displayed at MaxHeight 360
            art.CacheOption = BitmapCacheOption.OnLoad;
            art.EndInit();
            art.Freeze();
            _artCache[file] = art;
        }
        WorkingArt.Source = art;
        WorkingArt.Visibility = Visibility.Visible;   // set AFTER any CollapseAllPanels
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

    // ── Technician conversation (§8 OPEN) — every path runs through _gov ───────
    // The chat is seeded in ShowFindings (the findings step IS the live Technician). Each turn is
    // PIM-screened inbound (ScreenInbound) and RBC/Prism-shaped outbound (ApplyBoundary); nothing
    // here executes an action — Perform Repairs remains the explicit consent (Manifest I2).

    // Communications & Governance Orb → the append-only relay: fold THIS local session's
    // authorized findings into the Custodian seed. Reads ONLY the session this window holds;
    // a mismatch or an absent/
    // unverifiable record yields the ungrounded seed. Read-only, executes nothing, opens no gate.
    string ComposeCommsSeed(string systemPrompt)
    {
        try
        {
            var findings = _findingsRelay?.ReadAuthorizedFindings(_sessionId, _sessionId);
            if (findings is null) return systemPrompt;
            var sb = new StringBuilder();
            sb.Append("Findings already established by the read-only diagnostic (explain only these; invent nothing further): ");
            sb.Append(findings.PlanSummary);
            // Per-item facts from the deterministic FindingCatalog: the same title, purpose and
            // measured evidence the customer sees in each row, so a question like "what is the
            // Windows item?" is answered from what was measured, not guessed.
            if (_rows.Count > 0)
            {
                sb.Append(" How to read the list below: each [title] names a repair and what it DOES, not a problem that was found."
                    + " Only the sentences starting with 'Measured:' are findings from the scan."
                    + " The scan does not rate severity, so do not call any item critical or urgent; if asked, say what was measured.");
                sb.Append(" Recommended repairs, each with what it does and what the scan measured:");
                foreach (var (action, _) in _rows)
                {
                    var entry = FindingCatalog.For(action.GetValueOrDefault("id")?.ToString());
                    string ev = action.GetValueOrDefault("finding_evidence")?.ToString() ?? "";
                    sb.Append($" [{entry.Title}] {entry.What}{(ev.Length > 0 ? " " + ev : "")}");
                }
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

    // Show the "Enter your question here…" hint only while the box is empty.
    void ChatInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ChatPlaceholder != null)
            ChatPlaceholder.Visibility = string.IsNullOrEmpty(ChatInput.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    // Open the locally generated repair report saved to the Desktop.
    void OpenReport_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_reportPath)) return;
        try { Process.Start(new ProcessStartInfo(_reportPath) { UseShellExecute = true }); return; }
        catch { /* no default HTML handler — fall back to showing the file in File Explorer */ }
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_reportPath}\"") { UseShellExecute = true });
            return;
        }
        catch { }
        ShowError($"The report couldn't be opened automatically. It is saved on your Desktop:\n{_reportPath}", backTo: CompletePanel);
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
            AppendChat("Technician", reply, reportable: true);
        }
        finally { ChatSendButton.IsEnabled = true; }
    }


    void OpenAiAegis_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(AiContentReport.ExplanationUrl) { UseShellExecute = true }); }
        catch
        {
            MessageBox.Show(this, "Open this address in your browser:\n" + AiContentReport.ExplanationUrl,
                "About AI & AEGIS", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    void ReportAiContent_Click(object sender, RoutedEventArgs e) => ShowAiContentReport("");

    void ReportRepairPlan_Click(object sender, RoutedEventArgs e) => ShowAiContentReport(FindingsSummary.Text ?? "");

    void ShowAiContentReport(string response) => new AiContentReportWindow(response,
        typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "unknown") { Owner = this }.ShowDialog();

    void AppendChat(string who, string text, bool reportable = false)
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
        if (reportable)
        {
            var report = new TextBlock { FontSize = 12, Margin = new Thickness(0, 0, 0, 12) };
            var link = new System.Windows.Documents.Hyperlink
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xc4, 0xb5, 0xfd))
            };
            link.Inlines.Add("Report this response");
            link.Click += (_, _) => ShowAiContentReport(text);
            report.Inlines.Add(link);
            ChatLog.Children.Add(report);
        }
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

    static string HostnameFromDiagnostics(Dictionary<string, object?> diag)
    {
        if (diag.TryGetValue("os", out var osValue) && osValue is Dictionary<string, object?> os)
        {
            string? host = os.GetValueOrDefault("hostname")?.ToString();
            if (!string.IsNullOrWhiteSpace(host)) return host!;
        }
        return Environment.MachineName;
    }

    static ElevatedRepairRequest? ReadElevatedRequest(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty("diagnostics", out var diagnostics)) return null;
        if (!root.TryGetProperty("repairPlan", out var repairPlan)) return null;
        if (!root.TryGetProperty("approvedActions", out var actions) || actions.ValueKind != JsonValueKind.Array) return null;

        var approved = actions.EnumerateArray()
            .Select(JsonToDict)
            .Where(a => !string.IsNullOrWhiteSpace(a.GetValueOrDefault("id")?.ToString()))
            .ToList();
        string sessionId = root.TryGetProperty("sessionId", out var sid) && sid.ValueKind == JsonValueKind.String
            ? sid.GetString() ?? Guid.NewGuid().ToString("N")
            : Guid.NewGuid().ToString("N");
        string hostname = root.TryGetProperty("hostname", out var host) && host.ValueKind == JsonValueKind.String
            ? host.GetString() ?? Environment.MachineName
            : Environment.MachineName;

        return new ElevatedRepairRequest(sessionId, diagnostics.Clone(), repairPlan.Clone(), approved, hostname);
    }

    sealed record ElevatedRepairRequest(
        string SessionId,
        JsonElement Diagnostics,
        JsonElement RepairPlan,
        List<Dictionary<string, object?>> ApprovedActions,
        string Hostname);
}
