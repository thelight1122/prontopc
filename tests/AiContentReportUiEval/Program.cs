using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ProntoPC;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Type reportType = args.Length > 1
            ? Assembly.LoadFrom(Path.GetFullPath(args[1])).GetType("ProntoPC.AiContentReportWindow", throwOnError: true)!
            : typeof(AiContentReportWindow);

        var app = new Application();
        var window = (Window)Activator.CreateInstance(reportType,
            "Example AI response â€” review and redact this before reporting.", reportType.Assembly.GetName().Version?.ToString() ?? "unknown")!;
        var scroll = (ScrollViewer)window.Content;
        var panel = (StackPanel)scroll.Content;
        WrapPanel? actions = null;
        bool destinationVisible = false;
        foreach (var child in panel.Children)
        {
            if (child is TextBlock label && label.Text.Contains("support@prontopc.online")) destinationVisible = true;
            if (child is WrapPanel buttons) actions = buttons;
        }
        if (!destinationVisible || actions is null) throw new Exception("Report destination must be visible.");
        if (((Button)actions.Children[0]).Content.ToString() != "Copy report" ||
            !((Button)actions.Children[1]).Content.ToString()!.StartsWith("Save report") ||
            ((Button)actions.Children[2]).Content.ToString() != "Open in your email app (if installed)")
            throw new Exception("Copy/Save must precede the explicitly optional email app action.");
        var response = (TextBox)reportType.GetField("_response", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        if (!response.Text.StartsWith("Example AI response") || response.IsReadOnly) throw new Exception("Selected reply must be editable.");
        response.Text = "";
        var method = reportType.GetMethod("ReviewedReport", BindingFlags.Instance | BindingFlags.NonPublic)!;
        if (method.Invoke(window, null) != null) throw new Exception("Empty content must not produce a report.");
        response.Text = "Only the user-reviewed content";
        if (!((string)method.Invoke(window, null)!).Contains(response.Text)) throw new Exception("Redaction not reflected in report.");
        scroll.Measure(new Size(544, 600)); scroll.Arrange(new Rect(0, 0, 544, 600)); scroll.UpdateLayout();
        var bitmap = new RenderTargetBitmap(544, 600, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(scroll);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(args[0])) png.Save(file);
        scroll.Measure(new Size(384, 310)); scroll.Arrange(new Rect(0, 0, 384, 310)); scroll.UpdateLayout();
        if (scroll.ExtentHeight <= scroll.ViewportHeight) throw new Exception("Small viewport must scroll to the reporting controls.");
        Console.WriteLine("PASS editable selected response, empty-report prevention, redaction reflected, small-window scrolling");
        Console.WriteLine("Rendered actual WPF reporting dialog: " + args[0]);
        return 0;
    }
}

