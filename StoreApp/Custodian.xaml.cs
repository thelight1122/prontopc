using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ProntoPC;

/// <summary>
/// The states the Custodian can be in. Each one is entered because something
/// real happened in the repair pipeline - never because a timer elapsed.
/// </summary>
public enum CustodianState
{
    Idle,
    Greeting,
    Scanning,
    Thinking,
    Presenting,
    Repairing,
    Complete,
    Blocked,
}

public partial class Custodian : UserControl
{
    private readonly SolidColorBrush _accent = new SolidColorBrush(Color.FromRgb(0xFF, 0xB9, 0x5F));
    private Storyboard? _running;
    private Storyboard? _ambient;
    private bool _stateSet;

    public Custodian()
    {
        InitializeComponent();
        StateRing.Stroke = _accent;
        StateLabel.Foreground = _accent;
        Loaded += (_, __) =>
        {
            _ambient = (Storyboard)FindResource("SbAmbient");
            _ambient.Begin(this, true);
            // WPF raises Loaded parent-first, so a host that set a state in its
            // own constructor or Loaded handler has already run. Don't stomp it.
            if (!_stateSet) SetState(CustodianState.Idle);
        };
    }

    public CustodianState State { get; private set; } = CustodianState.Idle;

    /// <summary>
    /// Enter a state. <paramref name="activity"/> is the specific, true thing
    /// currently happening - "Clearing 4,200 MB of temporary files", not
    /// "Optimising your PC". If you have nothing specific to say, pass null
    /// and the caption stays empty rather than filling with reassurance.
    /// </summary>
    public void SetState(CustodianState state, string? activity = null)
    {
        State = state;
        _stateSet = true;

        if (_running != null)
        {
            _running.Stop(this);
            _running = null;
        }

        string label;
        Color accent;
        string? storyboard;

        switch (state)
        {
            case CustodianState.Greeting:
                label = "READY";
                accent = Color.FromRgb(0xFF, 0xB9, 0x5F);
                storyboard = "SbBreath";
                break;
            case CustodianState.Scanning:
                label = "SCANNING";
                accent = Color.FromRgb(0x3E, 0xA8, 0xFF);
                storyboard = "SbScanning";
                break;
            case CustodianState.Thinking:
                label = "ANALYSING";
                accent = Color.FromRgb(0x91, 0xDB, 0x2A);
                storyboard = "SbThinking";
                break;
            case CustodianState.Presenting:
                label = "FINDINGS READY";
                accent = Color.FromRgb(0xFF, 0xB9, 0x5F);
                storyboard = "SbBreath";
                break;
            case CustodianState.Repairing:
                label = "REPAIRING";
                accent = Color.FromRgb(0x93, 0x85, 0xF2);
                storyboard = "SbRepairing";
                break;
            case CustodianState.Complete:
                label = "COMPLETE";
                accent = Color.FromRgb(0x91, 0xDB, 0x2A);
                storyboard = "SbComplete";
                break;
            case CustodianState.Blocked:
                label = "NEEDS YOU";
                accent = Color.FromRgb(0xFF, 0x8A, 0x5B);
                storyboard = "SbBlocked";
                break;
            default:
                label = "CUSTODIAN";
                accent = Color.FromRgb(0xFF, 0xB9, 0x5F);
                storyboard = "SbBreath";
                break;
        }

        StateLabel.Text = label;
        _accent.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(accent, TimeSpan.FromMilliseconds(450))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });

        if (storyboard != null)
        {
            _running = (Storyboard)FindResource(storyboard);
            _running.Begin(this, true);
        }

        SetActivity(activity ?? string.Empty);
    }

    /// <summary>Set the caption. Only ever the true, specific current action.</summary>
    public void SetActivity(string text) => ActivityLabel.Text = text ?? string.Empty;

    /// <summary>
    /// Fire one angular stroke. Call this when a repair action has ACTUALLY
    /// finished - the stroke is the visual record of a completed unit of work,
    /// so firing it on anything else would make the animation lie.
    /// </summary>
    public void ReportRepairComplete(string? actionName = null)
    {
        ((Storyboard)FindResource("SbStroke")).Begin(this, true);
        if (!string.IsNullOrWhiteSpace(actionName)) SetActivity(actionName);
    }
}
