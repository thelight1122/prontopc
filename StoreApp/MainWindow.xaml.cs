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
    readonly string _token;
    readonly List<(Dictionary<string, object?> action, CheckBox box)> _rows = new();

    public MainWindow(string token)
    {
        InitializeComponent();
        _token = token;
        Loaded += async (_, __) => await Start();
    }

    async Task Start()
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            ShowError("No active session found. Please open ProntoPC from your purchase confirmation at prontopc.online.");
            return;
        }
        await RunScan();
    }

    // ── Scan ─────────────────────────────────────────────────────────────────
    async Task RunScan()
    {
        ShowWorking("Reading your system…", "This takes about a minute. Nothing is changed.");
        var diag = await Task.Run(() => Diagnostics.Collect());

        SetStatus("Analyzing…", "Building your personalized repair plan.");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            string payload = JsonSerializer.Serialize(new { token = _token, diagnostics = diag });
            var resp = await http.PostAsync($"{CloudRun}/api/submit-diagnostics",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) { ShowError("We couldn't analyze your scan just now. Please try again in a moment."); return; }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("plan", out var plan)) { ShowError("The repair plan came back empty. Please try again."); return; }
            ShowFindings(plan);
        }
        catch (Exception ex) { ShowError("Could not reach our service: " + ex.Message); }
    }

    // ── Findings ─────────────────────────────────────────────────────────────
    void ShowFindings(JsonElement plan)
    {
        FindingsSummary.Text = plan.TryGetProperty("summary", out var s) ? (s.GetString() ?? "") : "";
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
                    Foreground = new SolidColorBrush(Color.FromRgb(0xd4, 0xe4, 0xfa)),
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
        ShowWorking("Repairing your PC…", "Please wait — this can take a few minutes.");
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
    void ShowWorking(string title, string sub) { WorkingTitle.Text = title; WorkingSub.Text = sub; ShowPanel(WorkingPanel); }
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
