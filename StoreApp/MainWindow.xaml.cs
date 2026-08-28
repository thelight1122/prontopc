using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
    bool _offline;

    public MainWindow(string token)
    {
        InitializeComponent();
        _token = token;
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

        _offline = false;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            string payload = JsonSerializer.Serialize(new { token = _token, diagnostics = diag });
            var resp = await http.PostAsync($"{CloudRun}/api/submit-diagnostics",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            string body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            string status = root.TryGetProperty("status", out var st) ? (st.GetString() ?? "") : "";
            // The genuine, model-authored plan.
            if (status == "ok" && root.TryGetProperty("plan", out var plan)) { ShowFindings(plan); return; }
            // Back-compat: an older server with no "status" field but a plan present.
            if (status == "" && resp.IsSuccessStatusCode && root.TryGetProperty("plan", out var legacyPlan))
            { ShowFindings(legacyPlan); return; }
        }
        catch { }

        // status:"unavailable" or the service was unreachable — the AI plan is genuinely
        // absent. Presenting a heuristic plan AS the AI plan is what failed Store
        // certification (10.1.2.10). Show it, but honestly labelled as an offline plan.
        ShowOfflinePlan(BuildLocalPlan(diag));
    }

    // ── Local fallback when backend is unreachable ───────────────────────────
    static JsonElement BuildLocalPlan(Dictionary<string, object?> diag)
    {
        try
        {
            var actions = new List<Dictionary<string, object?>>();
            var findings = new List<string>();

            int tempMb = SumSizeMb(diag, "temp_files");
            if (tempMb > 10)
            {
                actions.Add(PlanAction("clear_temp_files", "cleanup", "Clear temporary files", tempMb));
                findings.Add($"{tempMb} MB in temporary files");
            }

            int cacheMb = SumSizeMb(diag, "browser_caches");
            if (cacheMb > 10)
            {
                actions.Add(PlanAction("clear_all_browser_caches", "browser", "Clear browser caches", cacheMb));
                findings.Add($"{cacheMb} MB in browser caches");
            }

            actions.Add(PlanAction("clear_windows_caches", "cleanup", "Clear Windows thumbnail and icon caches", 0));

            int rbMb = DictSizeMb(diag, "recycle_bin");
            if (rbMb > 10)
            {
                actions.Add(PlanAction("empty_recycle_bin", "cleanup", "Empty Recycle Bin", rbMb));
                findings.Add($"{rbMb} MB in Recycle Bin");
            }

            actions.Add(PlanAction("flush_dns", "network", "Flush DNS cache", 0));

            string summary = findings.Count > 0
                ? $"Your system scan found {string.Join(", ", findings)} that can be cleaned up, along with other optimization opportunities."
                : "Your system scan completed. We found standard maintenance items to help keep your PC running smoothly.";

            return ToJsonElement(new Dictionary<string, object?> { ["summary"] = summary, ["actions"] = actions });
        }
        catch
        {
            return ToJsonElement(new Dictionary<string, object?>
            {
                ["summary"] = "Your system scan completed. We found maintenance items to help keep your PC running smoothly.",
                ["actions"] = new List<Dictionary<string, object?>>
                {
                    PlanAction("clear_temp_files", "cleanup", "Clear temporary files", 0),
                    PlanAction("clear_windows_caches", "cleanup", "Clear Windows caches", 0),
                    PlanAction("flush_dns", "network", "Flush DNS cache", 0),
                }
            });
        }
    }

    static Dictionary<string, object?> PlanAction(string id, string cat, string name, int mb) =>
        new() { ["id"] = id, ["category"] = cat, ["friendly_name"] = name, ["risk"] = "low", ["estimated_mb_recovered"] = mb };

    static JsonElement ToJsonElement(Dictionary<string, object?> plan)
    {
        string json = JsonSerializer.Serialize(plan);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    static int SumSizeMb(Dictionary<string, object?> diag, string key)
    {
        if (!diag.TryGetValue(key, out var val) || val is not IList<object> list) return 0;
        int total = 0;
        foreach (var item in list)
            if (item is Dictionary<string, object?> d && d.TryGetValue("size_mb", out var mb))
                try { total += (int)Math.Round(Convert.ToDouble(mb)); } catch { }
        return total;
    }

    static int DictSizeMb(Dictionary<string, object?> diag, string key)
    {
        if (!diag.TryGetValue(key, out var val) || val is not Dictionary<string, object?> d || !d.TryGetValue("size_mb", out var mb)) return 0;
        try { return (int)Math.Round(Convert.ToDouble(mb)); } catch { return 0; }
    }

    // ── Findings ─────────────────────────────────────────────────────────────
    void ShowOfflinePlan(JsonElement plan) { _offline = true; ShowFindings(plan); }

    void ShowFindings(JsonElement plan)
    {
        string summary = plan.TryGetProperty("summary", out var s) ? (s.GetString() ?? "") : "";
        FindingsSummary.Text = _offline
            ? "⚠ OFFLINE BASIC PLAN — our AI service was unavailable, so this is a basic rule-based cleanup, not your AI-personalised plan.\n\n" + summary
            : summary;
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
        var results = new List<Dictionary<string, object?>>();
        int i = 0, total = approved.Count;
        foreach (var action in approved)
        {
            i++;
            string name = action.GetValueOrDefault("friendly_name")?.ToString() ?? "";
            SetStatus("Repairing your PC…", total > 0 ? $"[{i}/{total}] {name}" : name);
            var r = await Task.Run(() => Repair.Execute(action));
            results.Add(r);
        }

        SetStatus("Finishing up…", "Saving your report.");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            string payload = JsonSerializer.Serialize(new { token = _token, results });
            await http.PostAsync($"{CloudRun}/api/submit-repair-results", new StringContent(payload, Encoding.UTF8, "application/json"));
        }
        catch { }

        int freed = results.Sum(r => Mb(r.GetValueOrDefault("mb_freed")));
        int done = results.Count(r => r.GetValueOrDefault("status")?.ToString() == "done");
        string msg = freed > 0 ? $"{done} repairs completed · {FormatMb(freed)} recovered." : $"{done} items completed.";
        if (Repair.NeedsRestart(results)) msg += "\n\nA restart is recommended to finish applying some repairs.";
        CompleteText.Text = msg;
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
