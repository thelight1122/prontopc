using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace CustodianPrototype
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? _demo;

        public MainWindow()
        {
            InitializeComponent();
            // --state=Scanning starts the harness directly in one state, so a
            // given look can be reproduced and captured without clicking.
            foreach (var a in Environment.GetCommandLineArgs())
                if (a.StartsWith("--state=", StringComparison.OrdinalIgnoreCase)
                    && Enum.TryParse<CustodianState>(a.Substring(8), true, out var st))
                    Tech.SetState(st, DemoCaption(st));
        }

        private static string DemoCaption(CustodianState s) => s switch
        {
            CustodianState.Greeting   => "Session verified. Ready when you are.",
            CustodianState.Scanning   => "Reading disk health on C:",
            CustodianState.Thinking   => "Building your repair plan from 35 diagnostic readings",
            CustodianState.Presenting => "6 items found. Nothing runs until you approve it.",
            CustodianState.Repairing  => "Clearing 4,200 MB of temporary files",
            CustodianState.Complete   => "6 of 6 repairs finished. Report saved to your Desktop.",
            CustodianState.Blocked    => "Windows needs your permission to continue.",
            _ => "",
        };

        private void Greeting_Click(object s, RoutedEventArgs e) => Stop(() =>
            Tech.SetState(CustodianState.Greeting, "Session verified. Ready when you are."));

        private void Scanning_Click(object s, RoutedEventArgs e) => Stop(() =>
            Tech.SetState(CustodianState.Scanning, "Reading disk health on C:"));

        private void Thinking_Click(object s, RoutedEventArgs e) => Stop(() =>
            Tech.SetState(CustodianState.Thinking, "Building your repair plan from 35 diagnostic readings"));

        private void Presenting_Click(object s, RoutedEventArgs e) => Stop(() =>
            Tech.SetState(CustodianState.Presenting, "6 items found. Nothing runs until you approve it."));

        private void Repairing_Click(object s, RoutedEventArgs e) => Stop(() =>
            Tech.SetState(CustodianState.Repairing, "Clearing 4,200 MB of temporary files"));

        private void Complete_Click(object s, RoutedEventArgs e) => Stop(() =>
            Tech.SetState(CustodianState.Complete, "6 of 6 repairs finished. Report saved to your Desktop."));

        private void Blocked_Click(object s, RoutedEventArgs e) => Stop(() =>
            Tech.SetState(CustodianState.Blocked, "Windows needs your permission to continue."));

        private void Stroke_Click(object s, RoutedEventArgs e) =>
            Tech.ReportRepairComplete();

        private void Stop(Action then)
        {
            _demo?.Stop();
            _demo = null;
            then();
        }

        /// <summary>
        /// DEMO ONLY. This is the one place a timer is allowed, because here the
        /// timer IS the event source. Inside ProntoPC every SetState call is made
        /// by the code that actually did the work.
        /// </summary>
        private void Sequence_Click(object sender, RoutedEventArgs e)
        {
            _demo?.Stop();
            var steps = new List<(int ms, Action act)>
            {
                (0,    () => Tech.SetState(CustodianState.Greeting,  "Session verified. Ready when you are.")),
                (2200, () => Tech.SetState(CustodianState.Scanning,  "Reading disk health on C:")),
                (1500, () => Tech.SetActivity("Checking memory pressure")),
                (1500, () => Tech.SetActivity("Inspecting 19 startup programs")),
                (1500, () => Tech.SetActivity("Reviewing driver signatures")),
                (1600, () => Tech.SetState(CustodianState.Thinking,  "Building your repair plan from 35 diagnostic readings")),
                (2600, () => Tech.SetState(CustodianState.Presenting,"6 items found. Nothing runs until you approve it.")),
                (2600, () => Tech.SetState(CustodianState.Repairing, "Clearing 4,200 MB of temporary files")),
                (1800, () => Tech.ReportRepairComplete("Removing Delivery Optimization cache")),
                (1800, () => Tech.ReportRepairComplete("Disabling 4 unnecessary startup programs")),
                (1800, () => Tech.ReportRepairComplete("Repairing Windows Update components")),
                (1800, () => Tech.SetState(CustodianState.Complete,  "6 of 6 repairs finished. Report saved to your Desktop.")),
            };

            int i = 0;
            _demo = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1) };
            _demo.Tick += (_, __) =>
            {
                if (i >= steps.Count) { _demo!.Stop(); return; }
                var step = steps[i++];
                step.act();
                if (i < steps.Count)
                    _demo!.Interval = TimeSpan.FromMilliseconds(steps[i].ms);
                else
                    _demo!.Stop();
            };
            _demo.Start();
        }
    }
}
