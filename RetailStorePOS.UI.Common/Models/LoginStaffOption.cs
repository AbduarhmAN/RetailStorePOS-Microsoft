using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using RetailStorePOS.Data.Models;
using RetailStorePOS.UI.Common;

namespace RetailStorePOS.UI.Common.Models;

public sealed partial class LoginStaffOption : ObservableObject
{
    private static readonly SolidColorBrush SelectedCardBackgroundBrush = new(ColorHelper.FromArgb(255, 255, 255, 255));
    private static readonly SolidColorBrush IdleCardBackgroundBrush = new(ColorHelper.FromArgb(255, 255, 255, 255));
    private static readonly SolidColorBrush SelectedCardBorderBrush = new(ColorHelper.FromArgb(255, 11, 99, 246));
    private static readonly SolidColorBrush IdleCardBorderBrush = new(ColorHelper.FromArgb(255, 226, 232, 240));
    private static readonly SolidColorBrush SelectedBadgeBackgroundBrush = new(ColorHelper.FromArgb(255, 190, 205, 245));
    private static readonly SolidColorBrush IdleBadgeBackgroundBrush = new(ColorHelper.FromArgb(255, 203, 218, 255));
    private static readonly SolidColorBrush SelectedInitialForegroundBrush = new(ColorHelper.FromArgb(255, 15, 23, 42));
    private static readonly SolidColorBrush IdleInitialForegroundBrush = new(ColorHelper.FromArgb(255, 15, 23, 42));
    private static readonly SolidColorBrush PinReadyBackgroundBrush = new(ColorHelper.FromArgb(255, 220, 252, 231));
    private static readonly SolidColorBrush PinReadyForegroundBrush = new(ColorHelper.FromArgb(255, 22, 101, 52));
    private static readonly SolidColorBrush PinMissingBackgroundBrush = new(ColorHelper.FromArgb(255, 255, 237, 213));
    private static readonly SolidColorBrush PinMissingForegroundBrush = new(ColorHelper.FromArgb(255, 154, 52, 18));
    private static readonly SolidColorBrush NameForegroundBrushValue = new(ColorHelper.FromArgb(255, 15, 23, 42));
    private bool _isSelected;

    public LoginStaffOption(User user)
    {
        User = user;
        DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
        Initial = GetInitial(DisplayName, user.Username);
    }

    public User User { get; }

    public string DisplayName { get; }

    public string Initial { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(CardBackgroundBrush));
                OnPropertyChanged(nameof(CardBorderBrush));
                OnPropertyChanged(nameof(BadgeBackgroundBrush));
                OnPropertyChanged(nameof(InitialForegroundBrush));
                OnPropertyChanged(nameof(SelectionGlyphVisibility));
            }
        }
    }

    public Brush CardBackgroundBrush => IsSelected ? SelectedCardBackgroundBrush : IdleCardBackgroundBrush;

    public Brush CardBorderBrush => IsSelected ? SelectedCardBorderBrush : IdleCardBorderBrush;

    public Brush BadgeBackgroundBrush => IsSelected ? SelectedBadgeBackgroundBrush : IdleBadgeBackgroundBrush;

    public Brush InitialForegroundBrush => IsSelected ? SelectedInitialForegroundBrush : IdleInitialForegroundBrush;

    public Brush NameForegroundBrush => NameForegroundBrushValue;

    public bool HasPin => !string.IsNullOrWhiteSpace(User.PinHash);

    public Brush PinStatusBackgroundBrush => HasPin ? PinReadyBackgroundBrush : PinMissingBackgroundBrush;

    public Brush PinStatusForegroundBrush => HasPin ? PinReadyForegroundBrush : PinMissingForegroundBrush;

    public Visibility SelectionGlyphVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    public string PinStatusText => HasPin ? "PIN set" : "Needs PIN setup";

    public string RoleLabel => User.IsAdmin
        ? LocalizationHelper.GetString("Users_Role_AdminLabel")
        : LocalizationHelper.GetString("Users_Role_CashierLabel");

    private static string GetInitial(string displayName, string username)
    {
        var source = !string.IsNullOrWhiteSpace(displayName) ? displayName : username;
        return string.IsNullOrWhiteSpace(source)
            ? "?"
            : source.Trim()[0].ToString().ToUpperInvariant();
    }
}
