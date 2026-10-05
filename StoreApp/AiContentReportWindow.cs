using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ProntoPC;

public sealed class AiContentReportWindow : Window
{
    readonly TextBox _response;
    readonly TextBox _concern;
    readonly TextBlock _status;
    readonly string _version;

    public AiContentReportWindow(string response, string version)
    {
        _version = version;
        Title = "Report inappropriate AI content";
        Width = 560; Height = 640; MinWidth = 400; MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x1c));
        Foreground = Brushes.WhiteSmoke;
        var panel = new StackPanel { Margin = new Thickness(22) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(Label("Report inappropriate AI content", 20, bold: true));
        panel.Children.Add(Label("Review and remove private information before sharing. Only the text below and the app version are included. No logs, diagnostic reports, or files are attached automatically."));
        panel.Children.Add(Label("AI response to report (you can edit or redact it)", bold: true));
        _response = Input(response, 145);
        _response.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "AI response to report");
        panel.Children.Add(_response);
        panel.Children.Add(Label("What concerns you?", bold: true));
        _concern = Input("", 85);
        _concern.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "What concerns you");
        panel.Children.Add(_concern);
        panel.Children.Add(Label("Copy this report, then send it to " + AiContentReport.SupportEmail + ". You can also save the report and attach it to an email. Opening your email app does not send the report; you review and send it yourself."));
        var actions = new WrapPanel();
        AddButton(actions, "Copy report", CopyReport);
        AddButton(actions, "Save report…", SaveReport);
        AddButton(actions, "Open in your email app (if installed)", OpenEmail);
        AddButton(actions, "Close", (_, _) => Close());
        panel.Children.Add(actions);
        _status = Label("");
        _status.SetValue(System.Windows.Automation.AutomationProperties.LiveSettingProperty,
            System.Windows.Automation.AutomationLiveSetting.Polite);
        panel.Children.Add(_status);
    }

    static TextBlock Label(string text, double size = 13, bool bold = false) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, FontSize = size,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, 0, 0, 12)
    };

    static TextBox Input(string text, double height) => new()
    {
        Text = text, Height = height, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8),
        Margin = new Thickness(0, 0, 0, 14)
    };

    static void AddButton(Panel panel, string text, RoutedEventHandler action)
    {
        var button = new Button { Content = text, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 8, 8) };
        button.Click += action;
        panel.Children.Add(button);
    }

    string? ReviewedReport()
    {
        if (string.IsNullOrWhiteSpace(_response.Text))
        {
            _status.Text = "Paste or enter the AI response you want to report first.";
            _response.Focus();
            return null;
        }
        return AiContentReport.Compose(_version, _response.Text, _concern.Text);
    }

    void OpenEmail(object sender, RoutedEventArgs e)
    {
        string? body = ReviewedReport();
        if (body is null) return;
        string? uri = AiContentReport.EmailDraftUri(body);
        if (uri is null)
        {
            _status.Text = "This report is too long for an email link. Copy or save the full report, then send it to " + AiContentReport.SupportEmail + ". Nothing has been sent.";
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            _status.Text = "Email draft requested. Review and send it in your email app. If no draft opened, copy or save the report and email " + AiContentReport.SupportEmail + ".";
        }
        catch
        {
            _status.Text = "No email draft could be opened. Copy or save the report and email " + AiContentReport.SupportEmail + ". Nothing has been sent.";
        }
    }

    void CopyReport(object sender, RoutedEventArgs e)
    {
        string? report = ReviewedReport();
        if (report is null) return;
        try { Clipboard.SetText(report); _status.Text = "Report copied. Paste it into an email to " + AiContentReport.SupportEmail + ". Nothing has been sent."; }
        catch { _status.Text = "Clipboard unavailable. Try Save report instead. Nothing has been sent."; }
    }

    void SaveReport(object sender, RoutedEventArgs e)
    {
        string? report = ReviewedReport();
        if (report is null) return;
        var picker = new SaveFileDialog { FileName = "ProntoPC-AI-content-report.txt", Filter = "Text files (*.txt)|*.txt", DefaultExt = ".txt" };
        if (picker.ShowDialog(this) != true) return;
        try { File.WriteAllText(picker.FileName, report); _status.Text = "Report saved. Send it to " + AiContentReport.SupportEmail + ". Nothing has been sent."; }
        catch { _status.Text = "The report could not be saved. Choose another location or try Copy report."; }
    }
}
