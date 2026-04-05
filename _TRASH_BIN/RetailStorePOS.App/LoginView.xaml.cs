using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RetailStorePOS.App;

public partial class LoginView : UserControl
{
    private const double AnimDuration = 220;  // ms — fast enough to feel snappy
    private const double SlideDistance = 40;  // px — subtle, not dramatic
    private const double CompactBreakpoint = 1120;
    private bool _isAnimating = false;
    private bool _isCompact;

    public LoginView()
    {
        InitializeComponent();
        PreviewKeyDown += LoginView_PreviewKeyDown;
    }

    public void ResetInputs()
    {
        PasswordBox.Clear();

        if (DataContext is LoginViewModel vm)
        {
            vm.ResetState();
            SetCashierMode(animate: false);
            ShowStaffList(animate: false);
        }

        Focus();
    }

    // ------------------------------------------------------------------
    // Loaded — set initial state and subscribe to ViewModel changes
    // ------------------------------------------------------------------
    private void LoginView_Loaded(object sender, RoutedEventArgs e)
    {
        Focus();
        UpdateResponsiveLayout();

        if (DataContext is not LoginViewModel vm) return;

        // Sync UI to whatever mode the ViewModel actually starts in
        if (vm.IsPasswordMode)
            SetAdminMode(animate: false);
        else
            SetCashierMode(animate: false);

        // Subscribe to future mode changes
        vm.PropertyChanged += Vm_PropertyChanged;
    }

    private void LoginView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout();

        if (!IsLoaded || DataContext is not LoginViewModel vm)
        {
            return;
        }

        AnimateTabIndicator(vm.IsPinMode, animate: false);
    }

    private void UpdateResponsiveLayout()
    {
        var shouldBeCompact = ActualWidth < CompactBreakpoint;
        if (_isCompact == shouldBeCompact)
        {
            return;
        }

        _isCompact = shouldBeCompact;

        if (_isCompact)
        {
            LeftBrandPanel.Visibility = Visibility.Collapsed;
            LeftColumnDef.Width = new GridLength(0);
            RightLoginPanel.CornerRadius = new CornerRadius(28);
        }
        else
        {
            LeftBrandPanel.Visibility = Visibility.Visible;
            LeftColumnDef.Width = new GridLength(1.05, GridUnitType.Star);
            RightLoginPanel.CornerRadius = new CornerRadius(0, 28, 28, 0);
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (
            nameof(LoginViewModel.IsPasswordMode) or
            nameof(LoginViewModel.IsPinMode) or
            nameof(LoginViewModel.IsUserSelected)))
            return;

        if (DataContext is not LoginViewModel vm) return;

        if (e.PropertyName == nameof(LoginViewModel.IsUserSelected))
        {
            if (vm.IsUserSelected)
                ShowPinPad(animate: true);
            else
                ShowStaffList(animate: true);
            return;
        }

        if (vm.IsPasswordMode)
            SetAdminMode(animate: true);
        else
            SetCashierMode(animate: true);
    }


    // ------------------------------------------------------------------
    // Mode Transitions
    // ------------------------------------------------------------------
    private void SetAdminMode(bool animate)
    {
        if (animate && _isAnimating) return;

        // Tab indicator snaps/slides to left 
        AnimateTabIndicator(toRight: false, animate);

        // Update tab button appearance
        AdminTabBtn.Foreground = (Brush)FindResource("AppTextPrimary");
        CashierTabBtn.Foreground = (Brush)FindResource("AppMuted");

        if (!animate)
        {
            // Instant — show Admin, hide PIN (keep both in visual tree)
            PasswordPanel.Opacity = 1;
            PasswordPanel.IsHitTestVisible = true;
            PasswordPanelTranslate.X = 0;

            PinPanel.Opacity = 0;
            PinPanel.IsHitTestVisible = false;
            PinPanelTranslate.X = SlideDistance;
            return;
        }

        // Animate: PIN slides out right, Admin slides in from left
        PinPanel.IsHitTestVisible = false;
        SlideOut(PinPanel, PinPanelTranslate, toX: SlideDistance, onComplete: () =>
        {
            PasswordPanel.IsHitTestVisible = true;
            SlideIn(PasswordPanel, PasswordPanelTranslate, fromX: -SlideDistance, onComplete: () => _isAnimating = false);
        });
    }

    private void SetCashierMode(bool animate)
    {
        if (animate && _isAnimating) return;

        // Tab indicator slides to right
        AnimateTabIndicator(toRight: true, animate);

        // Update tab button appearance
        AdminTabBtn.Foreground = (Brush)FindResource("AppMuted");
        CashierTabBtn.Foreground = (Brush)FindResource("AppTextPrimary");

        // Always reset sub-panels to staff-list state
        StaffListTranslate.X = 0;
        StaffListPanel.Opacity = 1;
        StaffListPanel.IsHitTestVisible = true;
        PinEntryTranslate.X = SlideDistance;
        PinEntryPanel.Opacity = 0;
        PinEntryPanel.IsHitTestVisible = false;

        if (!animate)
        {
            PasswordPanel.Opacity = 0;
            PasswordPanel.IsHitTestVisible = false;
            PasswordPanelTranslate.X = -SlideDistance;

            PinPanel.Opacity = 1;
            PinPanel.IsHitTestVisible = true;
            PinPanelTranslate.X = 0;
            return;
        }

        // Animate: Admin slides out left, PIN slides in from right
        PasswordPanel.IsHitTestVisible = false;
        SlideOut(PasswordPanel, PasswordPanelTranslate, toX: -SlideDistance, onComplete: () =>
        {
            PinPanel.IsHitTestVisible = true;
            SlideIn(PinPanel, PinPanelTranslate, fromX: SlideDistance, onComplete: () => _isAnimating = false);
        });
    }

    // ------------------------------------------------------------------
    // Staff List ↔ PIN Pad transitions (within Cashier panel)
    // ------------------------------------------------------------------
    private void ShowPinPad(bool animate)
    {
        if (!animate)
        {
            StaffListPanel.Opacity = 0;
            StaffListPanel.IsHitTestVisible = false;
            StaffListTranslate.X = -SlideDistance;

            PinEntryPanel.Opacity = 1;
            PinEntryPanel.IsHitTestVisible = true;
            PinEntryTranslate.X = 0;
            return;
        }

        // Staff list slides out left, PIN pad slides in from right
        StaffListPanel.IsHitTestVisible = false;
        SlideOut(StaffListPanel, StaffListTranslate, toX: -SlideDistance, onComplete: () =>
        {
            PinEntryPanel.IsHitTestVisible = true;
            SlideIn(PinEntryPanel, PinEntryTranslate, fromX: SlideDistance,
                onComplete: () => _isAnimating = false);
        });
    }

    private void ShowStaffList(bool animate)
    {
        if (!animate)
        {
            PinEntryPanel.Opacity = 0;
            PinEntryPanel.IsHitTestVisible = false;
            PinEntryTranslate.X = SlideDistance;

            StaffListPanel.Opacity = 1;
            StaffListPanel.IsHitTestVisible = true;
            StaffListTranslate.X = 0;
            return;
        }

        // PIN pad slides out right, staff list slides in from left
        PinEntryPanel.IsHitTestVisible = false;
        SlideOut(PinEntryPanel, PinEntryTranslate, toX: SlideDistance, onComplete: () =>
        {
            StaffListPanel.IsHitTestVisible = true;
            SlideIn(StaffListPanel, StaffListTranslate, fromX: -SlideDistance,
                onComplete: () => _isAnimating = false);
        });
    }


    // ------------------------------------------------------------------
    // Animation Helpers
    // ------------------------------------------------------------------
    private void SlideOut(FrameworkElement panel, TranslateTransform translate, double toX, Action onComplete)
    {
        _isAnimating = true;
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var duration = TimeSpan.FromMilliseconds(AnimDuration);

        var sb = new Storyboard();

        var slideAnim = new DoubleAnimation(translate.X, toX, duration) { EasingFunction = easing };
        Storyboard.SetTarget(slideAnim, panel);
        Storyboard.SetTargetProperty(slideAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

        var fadeAnim = new DoubleAnimation(1, 0, duration) { EasingFunction = easing };
        Storyboard.SetTarget(fadeAnim, panel);
        Storyboard.SetTargetProperty(fadeAnim, new PropertyPath(UIElement.OpacityProperty));

        sb.Children.Add(slideAnim);
        sb.Children.Add(fadeAnim);
        sb.Completed += (_, _) => onComplete();
        sb.Begin();
    }

    private void SlideIn(FrameworkElement panel, TranslateTransform translate, double fromX, Action onComplete)
    {
        translate.X = fromX;
        panel.Opacity = 0;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(AnimDuration);

        var sb = new Storyboard();

        var slideAnim = new DoubleAnimation(fromX, 0, duration) { EasingFunction = easing };
        Storyboard.SetTarget(slideAnim, panel);
        Storyboard.SetTargetProperty(slideAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

        var fadeAnim = new DoubleAnimation(0, 1, duration) { EasingFunction = easing };
        Storyboard.SetTarget(fadeAnim, panel);
        Storyboard.SetTargetProperty(fadeAnim, new PropertyPath(UIElement.OpacityProperty));

        sb.Children.Add(slideAnim);
        sb.Children.Add(fadeAnim);
        sb.Completed += (_, _) => onComplete();
        sb.Begin();
    }

    private void AnimateTabIndicator(bool toRight, bool animate)
    {
        // Recompute the selected tab's real X position so the indicator stays aligned
        // after resizes, DPI rounding, or subtle layout changes.
        Dispatcher.InvokeAsync(() =>
        {
            if (SegmentHostGrid is null)
            {
                return;
            }

            var selectedButton = toRight ? CashierTabBtn : AdminTabBtn;
            double targetX = selectedButton.TranslatePoint(new Point(0, 0), SegmentHostGrid).X;

            if (!animate)
            {
                TabIndicatorTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                TabIndicatorTranslate.X = targetX;
                return;
            }

            var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var anim = new DoubleAnimation(TabIndicatorTranslate.X, targetX,
                TimeSpan.FromMilliseconds(AnimDuration + 30))
            { EasingFunction = easing };

            TabIndicatorTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ------------------------------------------------------------------
    // Keyboard handling (unchanged)
    // ------------------------------------------------------------------
    private void LoginView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not LoginViewModel vm)
            return;

        if (vm.IsPinMode)
        {
            switch (e.Key)
            {
                case Key.D0 or Key.NumPad0: vm.PinDigitCommand.Execute("0"); e.Handled = true; break;
                case Key.D1 or Key.NumPad1: vm.PinDigitCommand.Execute("1"); e.Handled = true; break;
                case Key.D2 or Key.NumPad2: vm.PinDigitCommand.Execute("2"); e.Handled = true; break;
                case Key.D3 or Key.NumPad3: vm.PinDigitCommand.Execute("3"); e.Handled = true; break;
                case Key.D4 or Key.NumPad4: vm.PinDigitCommand.Execute("4"); e.Handled = true; break;
                case Key.D5 or Key.NumPad5: vm.PinDigitCommand.Execute("5"); e.Handled = true; break;
                case Key.D6 or Key.NumPad6: vm.PinDigitCommand.Execute("6"); e.Handled = true; break;
                case Key.D7 or Key.NumPad7: vm.PinDigitCommand.Execute("7"); e.Handled = true; break;
                case Key.D8 or Key.NumPad8: vm.PinDigitCommand.Execute("8"); e.Handled = true; break;
                case Key.D9 or Key.NumPad9: vm.PinDigitCommand.Execute("9"); e.Handled = true; break;
                case Key.Back: vm.BackspacePinCommand.Execute(null); e.Handled = true; break;
                case Key.C or Key.Escape: vm.ClearPinCommand.Execute(null); e.Handled = true; break;
            }
        }

        if (vm.IsPasswordMode && e.Key == Key.Enter)
        {
            vm.LoginPasswordCommand.Execute(PasswordBox);
            e.Handled = true;
        }
    }
}
