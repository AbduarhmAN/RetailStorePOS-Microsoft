using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace RetailStorePOS.UI.Common.Views.Components;

/// <summary>
/// Reusable period selector for the Advanced Analytics pages. Emits
/// <see cref="PeriodChanged"/> whenever the active range changes (preset
/// click or custom-range edit).
///
/// Times are produced in UTC. <see cref="StartUtc"/> is the inclusive lower
/// bound; <see cref="EndUtc"/> is the exclusive upper bound — i.e. queries
/// should use <c>created_at &gt;= StartUtc AND created_at &lt; EndUtc</c>.
/// </summary>
public sealed partial class PeriodFilterControl : UserControl
{
    public enum PeriodPreset
    {
        Today,
        Last7Days,
        Last30Days,
        Last90Days,
        Custom
    }

    /// <summary>
    /// Raised every time the effective range changes for any reason.
    /// </summary>
    public event EventHandler<PeriodChangedEventArgs>? PeriodChanged;

    private bool _suppressEvents;
    private PeriodPreset _activePreset = PeriodPreset.Last7Days;

    public PeriodFilterControl()
    {
        InitializeComponent();

        // Initialize range from the default preset.
        ApplyPreset(PeriodPreset.Last7Days, raiseEvent: false);
    }

    public PeriodPreset ActivePreset => _activePreset;

    public DateTime StartUtc { get; private set; }

    public DateTime EndUtc { get; private set; }

    /// <summary>
    /// Returns the same range shifted backward by one period length, suitable
    /// for "vs previous period" comparisons.
    /// </summary>
    public (DateTime Start, DateTime End) GetPreviousPeriod()
    {
        var span = EndUtc - StartUtc;
        if (span <= TimeSpan.Zero)
        {
            span = TimeSpan.FromDays(1);
        }

        return (StartUtc - span, StartUtc);
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || button.Tag is not string tag)
        {
            return;
        }

        if (!Enum.TryParse<PeriodPreset>(tag, out var preset))
        {
            return;
        }

        ApplyPreset(preset, raiseEvent: true);
    }

    private void DatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_suppressEvents)
        {
            return;
        }

        if (_activePreset != PeriodPreset.Custom)
        {
            return;
        }

        var startLocal = StartDatePicker.Date;
        var endLocal = EndDatePicker.Date;
        if (startLocal is null || endLocal is null)
        {
            return;
        }

        var start = startLocal.Value.UtcDateTime.Date;
        // End-exclusive: queries use "< EndUtc", so the picked end day is fully included.
        var end = endLocal.Value.UtcDateTime.Date.AddDays(1);

        if (end <= start)
        {
            // Refuse inverted ranges silently; user will fix the second picker.
            return;
        }

        StartUtc = start;
        EndUtc = end;
        OnPeriodChanged();
    }

    private void ApplyPreset(PeriodPreset preset, bool raiseEvent)
    {
        _suppressEvents = true;
        try
        {
            _activePreset = preset;
            UpdatePresetButtonStates();

            var nowUtc = DateTime.UtcNow;
            var todayUtc = nowUtc.Date;

            switch (preset)
            {
                case PeriodPreset.Today:
                    StartUtc = todayUtc;
                    EndUtc = todayUtc.AddDays(1);
                    CustomRangePanel.Visibility = Visibility.Collapsed;
                    break;
                case PeriodPreset.Last7Days:
                    StartUtc = todayUtc.AddDays(-6);
                    EndUtc = todayUtc.AddDays(1);
                    CustomRangePanel.Visibility = Visibility.Collapsed;
                    break;
                case PeriodPreset.Last30Days:
                    StartUtc = todayUtc.AddDays(-29);
                    EndUtc = todayUtc.AddDays(1);
                    CustomRangePanel.Visibility = Visibility.Collapsed;
                    break;
                case PeriodPreset.Last90Days:
                    StartUtc = todayUtc.AddDays(-89);
                    EndUtc = todayUtc.AddDays(1);
                    CustomRangePanel.Visibility = Visibility.Collapsed;
                    break;
                case PeriodPreset.Custom:
                    CustomRangePanel.Visibility = Visibility.Visible;
                    if (StartDatePicker.Date is null)
                    {
                        StartDatePicker.Date = new DateTimeOffset(todayUtc.AddDays(-6), TimeSpan.Zero);
                    }
                    if (EndDatePicker.Date is null)
                    {
                        EndDatePicker.Date = new DateTimeOffset(todayUtc, TimeSpan.Zero);
                    }
                    StartUtc = StartDatePicker.Date!.Value.UtcDateTime.Date;
                    EndUtc = EndDatePicker.Date!.Value.UtcDateTime.Date.AddDays(1);
                    break;
            }
        }
        finally
        {
            _suppressEvents = false;
        }

        if (raiseEvent)
        {
            OnPeriodChanged();
        }
    }

    private void UpdatePresetButtonStates()
    {
        TodayButton.IsChecked = _activePreset == PeriodPreset.Today;
        Last7Button.IsChecked = _activePreset == PeriodPreset.Last7Days;
        Last30Button.IsChecked = _activePreset == PeriodPreset.Last30Days;
        Last90Button.IsChecked = _activePreset == PeriodPreset.Last90Days;
        CustomButton.IsChecked = _activePreset == PeriodPreset.Custom;
    }

    private void OnPeriodChanged()
    {
        PeriodChanged?.Invoke(this, new PeriodChangedEventArgs(_activePreset, StartUtc, EndUtc));
    }
}

public sealed class PeriodChangedEventArgs : EventArgs
{
    public PeriodChangedEventArgs(PeriodFilterControl.PeriodPreset preset, DateTime startUtc, DateTime endUtc)
    {
        Preset = preset;
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public PeriodFilterControl.PeriodPreset Preset { get; }
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }
}
