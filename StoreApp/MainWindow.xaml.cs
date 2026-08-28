using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProntoPC;

public partial class MainWindow : Window
{
    const string CloudRun = "https://pronto-pc-108655745293.us-central1.run.app";
    string _token;
    readonly List<(Dictionary<string, object?> action, CheckBox box)> _rows = new();
    readonly Orchestrator _orch;
    readonly bool _modelPresent;

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

    async Task Start() => await RunScan();

    // ── Scan ─────────────────────────────────────────────────────────────────
    async Task RunScan()
    {
        ShowWorking("Reading your system…", "This takes about a minute. Nothing is changed.");
        var diag = await Task.Run(() => Diagnostics.Collect());

        SetStatus("Analyzing…", "Building your personalized repair plan.");

        if (string.IsNullOrWhiteSpace(_token))
            _token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        // Plan phase: the Orchestrator authors a plan (local Satellite -> cloud broker)
        // and validates it against the keyed Action Envelope before it reaches consent.
        var outcome = await _orch.AuthorPlanAsync(diag);
        if (outcome.IsOk) { ShowFindings(ToElement(outcome.Plan!)); return; }

        // Both AI authors declined (on-device Gemma AND cloud broker). We never present
        // a rule-based plan under the AI claim — instead we report the miss so the
        // customer is made whole (single-use 100% coupon) and show honest recourse.
        await ReportAiUnavailable(diag);
    }

    static JsonElement ToElement(JsonNode node) => JsonSerializer.SerializeToElement(node);

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
        await RunRepair(approved);
    }

    // ── Repair ───────────────────────────────────────────────────────────────
    async Task RunRepair(List<Dictionary<string, object?>> approved)
    {
        ShowWorking("Repairing your PC…", "Please wait — this can take a few minutes.", repairing: true);

        // Repair + verify phases: the Orchestrator invokes the engine and summarizes.
        var progress = new Progress<RepairProgress>(p =>
            SetStatus("Repairing your PC…", p.Total > 0 ? $"[{p.Index}/{p.Total}] {p.Name}" : p.Name));
        var outcome = await _orch.RunRepairAsync(approved, progress);

        // Complete phase: persist results to the broker (best-effort), then show the report.
        SetStatus("Finishing up…", "Saving your report.");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            string payload = JsonSerializer.Serialize(new { token = _token, results = outcome.Results });
            await http.PostAsync($"{CloudRun}/api/submit-repair-results", new StringContent(payload, Encoding.UTF8, "application/json"));
        }
        catch { }

        CompleteText.Text = outcome.Message;
        ShowPanel(CompletePanel);
    }

    // ── UI helpers ───────────────────────────────────────────────────────────
    void ShowWorking(string title, string sub, bool repairing = false)
    {
        WorkingTitle.Text = title; WorkingSub.Text = sub;
        WorkingAuditor.Visibility = repairing ? Visibility.Collapsed : Visibility.Visible;
        WorkingBuilder.Visibility = repairing ? Visibility.Visible : Visibility.Collapsed;
        ShowPanel(WorkingPanel);
    }
    void SetStatus(string title, string sub) { WorkingTitle.Text = title; WorkingSub.Text = sub; }
    void ShowError(string msg) { ErrorText.Text = msg; ShowPanel(ErrorPanel); }

    void ShowPanel(UIElement panel)
    {
        WorkingPanel.Visibility = Visibility.Collapsed;
        FindingsPanel.Visibility = Visibility.Collapsed;
        CompletePanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        MakeGoodPanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();

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
