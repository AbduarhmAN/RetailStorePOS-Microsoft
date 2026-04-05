using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace RetailStorePOS.App;

public partial class HeroUiView : UserControl
{
    private const string HeroUiHostName = "appassets.nexill";
    private static readonly TimeSpan SnapshotThrottle = TimeSpan.FromMilliseconds(70);

    private static readonly HashSet<string> SnapshotRelevantProperties = new(StringComparer.Ordinal)
    {
        nameof(CheckoutViewModel.SelectedProduct),
        nameof(CheckoutViewModel.TenderedCash),
        nameof(CheckoutViewModel.CurrencyCode),
        nameof(CheckoutViewModel.StoreName),
        nameof(CheckoutViewModel.StoreAddress),
        nameof(CheckoutViewModel.TaxEnabled),
        nameof(CheckoutViewModel.TaxRatePercent),
        nameof(CheckoutViewModel.Subtotal),
        nameof(CheckoutViewModel.Tax),
        nameof(CheckoutViewModel.Total),
        nameof(CheckoutViewModel.ChangeDue),
        nameof(CheckoutViewModel.Receipt),
        nameof(CheckoutViewModel.IsReceiptVisible),
        nameof(CheckoutViewModel.IsConfirmClearVisible),
        nameof(CheckoutViewModel.StatusMessage),
        nameof(CheckoutViewModel.CanCompleteSale),
        nameof(CheckoutViewModel.CanOverridePrice),
        nameof(CheckoutViewModel.CurrentCashierDisplay),
        nameof(CheckoutViewModel.QuickCash1),
        nameof(CheckoutViewModel.QuickCash2),
        nameof(CheckoutViewModel.QuickCash3)
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private bool _initializationStarted;
    private bool _navigationCompleted;
    private bool _isSuspended;
    private bool _frontendReady;
    private CheckoutViewModel? _checkoutViewModel;
    private readonly DispatcherTimer _snapshotTimer;
    private bool _snapshotQueued;

    public HeroUiView()
    {
        InitializeComponent();
        _snapshotTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = SnapshotThrottle
        };
        _snapshotTimer.Tick += SnapshotTimer_Tick;
        Loaded += HeroUiView_Loaded;
        IsVisibleChanged += HeroUiView_IsVisibleChanged;
        AppServices.Auth.LoginStateChanged += AppServices_LoginStateChanged;
    }

    private async void HeroUiView_Loaded(object sender, RoutedEventArgs e)
    {
        if (IsVisible)
        {
            await EnsureHeroUiReadyAsync();
        }
    }

    private async System.Threading.Tasks.Task EnsureHeroUiReadyAsync()
    {
        AttachCheckoutSubscriptions();

        if (_navigationCompleted && HeroHostBrowser.CoreWebView2 is CoreWebView2 existingWebView)
        {
            if (_isSuspended)
            {
                existingWebView.Resume();
                _isSuspended = false;
            }

            HeroHostBrowser.Visibility = Visibility.Visible;
            LoadingState.Visibility = _frontendReady ? Visibility.Collapsed : Visibility.Visible;
            QueueCheckoutSnapshot(immediate: true);
            return;
        }

        if (_initializationStarted)
        {
            return;
        }

        _initializationStarted = true;
        HeroHostBrowser.NavigationCompleted += HeroHostBrowser_NavigationCompleted;

        var bundleFolder = Path.Combine(AppContext.BaseDirectory, "HeroUi");
        var indexPath = Path.Combine(bundleFolder, "index.html");

        if (!File.Exists(indexPath))
        {
            ShowStatus(
                "HeroUI frontend build not found",
                "Run npm install and npm run build in UI/hero-ui-frontend, then rebuild the desktop app.");
            return;
        }

        try
        {
            var options = new CoreWebView2EnvironmentOptions(
                additionalBrowserArguments: "--renderer-process-limit=1 " +
                                            "--disable-features=AudioServiceOutOfProcess,NetworkServiceInProcess,Translate,OptimizationHints,MediaRouter " +
                                            "--disable-extensions " +
                                            "--disable-gpu-shader-disk-cache " +
                                            "--enable-unsafe-webgpu=false " +
                                            "--js-flags=\"--max-old-space-size=128\""
            );
            var environment = await WebView2EnvironmentFactory.CreateAsync("Checkout", options);
            await HeroHostBrowser.EnsureCoreWebView2Async(environment);

            if (HeroHostBrowser.CoreWebView2 is CoreWebView2 webView)
            {
                webView.WebMessageReceived -= HeroHostBrowser_WebMessageReceived;
                webView.WebMessageReceived += HeroHostBrowser_WebMessageReceived;
                webView.SetVirtualHostNameToFolderMapping(
                    HeroUiHostName,
                    bundleFolder,
                    CoreWebView2HostResourceAccessKind.Allow);
                webView.Settings.AreBrowserAcceleratorKeysEnabled = false;
                webView.Settings.AreDefaultContextMenusEnabled = false;
                webView.Settings.AreDevToolsEnabled = false;
                webView.Settings.IsStatusBarEnabled = false;
                webView.Settings.IsZoomControlEnabled = false;
            }

            var bundleVersion = File.GetLastWriteTimeUtc(indexPath).Ticks;
            HeroHostBrowser.Source = new Uri($"https://{HeroUiHostName}/index.html?mode=checkout&v={bundleVersion}");
        }
        catch (Exception ex)
        {
            _initializationStarted = false;
            ShowStatus(
                "Could not load the HeroUI workspace",
                ex.Message);
            AppServices.ReportException(ex, "HeroUiView.EnsureHeroUiReadyAsync");
        }
    }

    private void HeroHostBrowser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            ReportHostError(
                "Could not render the HeroUI workspace",
                $"WebView2 navigation failed with status: {e.WebErrorStatus}.");
            return;
        }

        _navigationCompleted = true;
        HeroHostBrowser.Visibility = Visibility.Collapsed;
        LoadingState.Visibility = Visibility.Visible;

        if (IsVisible)
        {
            QueueCheckoutSnapshot(immediate: true);
        }
        else
        {
            _ = SuspendWebViewAsync();
        }

        // Aggressively clean up the massive memory spike caused by XAML/WebView2 initialization
        Dispatcher.InvokeAsync(async () =>
        {
            await System.Threading.Tasks.Task.Delay(1000);
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        });
    }

    private async void HeroUiView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            await EnsureHeroUiReadyAsync();
            return;
        }

        _snapshotTimer.Stop();
        _snapshotQueued = false;
        DetachCheckoutSubscriptions();
        await SuspendWebViewAsync();
    }

    private async System.Threading.Tasks.Task SuspendWebViewAsync()
    {
        if (HeroHostBrowser.CoreWebView2 is not CoreWebView2 webView || _isSuspended)
        {
            HeroHostBrowser.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            await webView.TrySuspendAsync();
            _isSuspended = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to suspend HeroUI WebView: {ex.Message}");
            AppServices.ReportException(ex, "HeroUiView.SuspendWebViewAsync");
        }

        HeroHostBrowser.Visibility = Visibility.Collapsed;
    }

    private void ShowStatus(string title, string message)
    {
        HeroHostBrowser.Visibility = Visibility.Collapsed;
        LoadingState.Visibility = Visibility.Visible;
        LoadingTitleText.Text = title;
        LoadingMessageText.Text = message;
    }

    private void AppServices_LoginStateChanged(object? sender, EventArgs e)
    {
        if (!IsVisible)
        {
            return;
        }

        AttachCheckoutSubscriptions();
        Dispatcher.Invoke(() => QueueCheckoutSnapshot(immediate: true));
    }

    private void HeroHostBrowser_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var request = JsonSerializer.Deserialize<HeroUiRequest>(e.WebMessageAsJson, SerializerOptions);
            if (request == null || string.IsNullOrWhiteSpace(request.Type))
            {
                return;
            }

            HandleRequest(request);
        }
        catch (Exception ex)
        {
            ReportHostError("Unable to process HeroUI request", ex.Message);
            AppServices.ReportException(ex, "HeroUiView.HeroHostBrowser_WebMessageReceived");
        }
    }

    private void HandleRequest(HeroUiRequest request)
    {
        if (request.Type == "frontendReady")
        {
            _frontendReady = true;
            HeroHostBrowser.Visibility = Visibility.Visible;
            LoadingState.Visibility = Visibility.Collapsed;
            QueueCheckoutSnapshot(immediate: true);
            return;
        }

        if (request.Type == "frontendError")
        {
            ReportHostError("HeroUI workspace runtime error", request.Message ?? "Unknown frontend runtime error.");
            return;
        }

        AttachCheckoutSubscriptions();

        var checkout = GetCheckoutViewModel();
        if (checkout == null)
        {
            ReportHostError("Checkout view model unavailable", "The desktop checkout state is not ready yet.");
            return;
        }

        switch (request.Type)
        {
            case "requestCheckoutSnapshot":
                QueueCheckoutSnapshot(immediate: true);
                break;

            case "setCheckoutSearchTerm":
                checkout.SearchTerm = request.Query ?? string.Empty;
                break;

            case "selectCheckoutProduct":
                var selectedProduct = checkout.SearchResults.FirstOrDefault(item => item.Id == request.ProductId);
                if (selectedProduct != null)
                {
                    checkout.SelectedProduct = selectedProduct;
                }
                break;

            case "submitCheckoutQuery":
                checkout.BarcodeEntry = (request.Query ?? checkout.SearchTerm ?? string.Empty).Trim();
                checkout.AddBarcodeCommand.Execute(null);
                checkout.SearchTerm = string.Empty;
                break;

            case "addCheckoutSelected":
                checkout.AddSelectedCommand.Execute(null);
                break;

            case "addCheckoutProduct":
                var product = checkout.SearchResults.FirstOrDefault(item => item.Id == request.ProductId);
                if (product != null)
                {
                    checkout.SelectedProduct = product;
                    checkout.AddSelectedCommand.Execute(null);
                }
                break;

            case "overrideCheckoutPrice":
                var itemToOverride = FindCartItem(checkout, request.ProductId);
                if (itemToOverride != null)
                {
                    checkout.OverridePriceCommand.Execute(itemToOverride);
                }
                break;

            case "increaseCheckoutItem":
                var itemToIncrease = FindCartItem(checkout, request.ProductId);
                if (itemToIncrease != null)
                {
                    checkout.IncreaseQtyCommand.Execute(itemToIncrease);
                }
                break;

            case "decreaseCheckoutItem":
                var itemToDecrease = FindCartItem(checkout, request.ProductId);
                if (itemToDecrease != null)
                {
                    checkout.DecreaseQtyCommand.Execute(itemToDecrease);
                }
                break;

            case "removeCheckoutItem":
                var itemToRemove = FindCartItem(checkout, request.ProductId);
                if (itemToRemove != null)
                {
                    checkout.RemoveItemCommand.Execute(itemToRemove);
                }
                break;

            case "setCheckoutTendered":
                checkout.TenderedCash = NormalizeMoney(request.Amount);
                break;

            case "addCheckoutTenderAmount":
                checkout.TenderedCash = NormalizeMoney(checkout.TenderedCash + request.Amount.GetValueOrDefault());
                break;

            case "setCheckoutTenderExact":
                checkout.TenderExactCommand.Execute(null);
                break;

            case "showCheckoutClearCart":
                checkout.ClearCartCommand.Execute(null);
                break;

            case "cancelCheckoutClearCart":
                checkout.CancelClearCartCommand.Execute(null);
                break;

            case "confirmCheckoutClearCart":
                checkout.ConfirmClearCartCommand.Execute(null);
                break;

            case "completeCheckoutSale":
                checkout.CompleteSaleCommand.Execute(null);
                break;

            case "dismissCheckoutReceipt":
                checkout.DismissReceiptCommand.Execute(null);
                break;

            case "openCheckoutReceiptPdf":
                checkout.OpenReceiptPdfCommand.Execute(null);
                break;

            case "openCheckoutReceiptsFolder":
                checkout.OpenReceiptsFolderCommand.Execute(null);
                break;
        }
    }

    private void AttachCheckoutSubscriptions()
    {
        if (!IsVisible)
        {
            return;
        }

        var checkout = GetCheckoutViewModel();
        if (checkout == null || ReferenceEquals(_checkoutViewModel, checkout))
        {
            return;
        }

        DetachCheckoutSubscriptions();
        _checkoutViewModel = checkout;
        _checkoutViewModel.PropertyChanged += CheckoutViewModel_PropertyChanged;
        _checkoutViewModel.CartItems.CollectionChanged += CartItems_CollectionChanged;
        _checkoutViewModel.SearchResults.CollectionChanged += SearchResults_CollectionChanged;

        foreach (var item in _checkoutViewModel.CartItems)
        {
            item.PropertyChanged += CartItem_PropertyChanged;
        }
    }

    private void DetachCheckoutSubscriptions()
    {
        if (_checkoutViewModel == null)
        {
            return;
        }

        _checkoutViewModel.PropertyChanged -= CheckoutViewModel_PropertyChanged;
        _checkoutViewModel.CartItems.CollectionChanged -= CartItems_CollectionChanged;
        _checkoutViewModel.SearchResults.CollectionChanged -= SearchResults_CollectionChanged;

        foreach (var item in _checkoutViewModel.CartItems)
        {
            item.PropertyChanged -= CartItem_PropertyChanged;
        }

        _checkoutViewModel = null;
    }

    private void CheckoutViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || SnapshotRelevantProperties.Contains(e.PropertyName))
        {
            QueueCheckoutSnapshot();
        }
    }

    private void SearchResults_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueCheckoutSnapshot();
    }

    private void CartItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (CartItem item in e.NewItems)
            {
                item.PropertyChanged += CartItem_PropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (CartItem item in e.OldItems)
            {
                item.PropertyChanged -= CartItem_PropertyChanged;
            }
        }

        QueueCheckoutSnapshot();
    }

    private void CartItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        QueueCheckoutSnapshot();
    }

    private void QueueCheckoutSnapshot(bool immediate = false)
    {
        _snapshotQueued = true;

        if (immediate)
        {
            _snapshotTimer.Stop();
            FlushCheckoutSnapshot();
            return;
        }

        if (!_snapshotTimer.IsEnabled)
        {
            _snapshotTimer.Start();
        }
    }

    private void SnapshotTimer_Tick(object? sender, EventArgs e)
    {
        _snapshotTimer.Stop();
        FlushCheckoutSnapshot();
    }

    private void FlushCheckoutSnapshot()
    {
        if (!_snapshotQueued)
        {
            return;
        }

        _snapshotQueued = false;
        SendCheckoutSnapshot();
    }

    private void SendCheckoutSnapshot()
    {
        if (!IsVisible || _isSuspended || HeroHostBrowser.CoreWebView2 is not CoreWebView2 webView)
        {
            return;
        }

        var checkout = GetCheckoutViewModel();
        if (checkout == null)
        {
            return;
        }

        try
        {
            var snapshot = BuildCheckoutSnapshot(checkout);
            var envelope = new HeroUiEnvelope("checkoutSnapshot", snapshot);
            webView.PostWebMessageAsJson(JsonSerializer.Serialize(envelope, SerializerOptions));
        }
        catch (Exception ex)
        {
            ReportHostError("Unable to sync checkout state", ex.Message);
            AppServices.ReportException(ex, "HeroUiView.SendCheckoutSnapshot");
        }
    }

    private void ReportHostError(string title, string message)
    {
        ShowStatus(title, message);

        if (HeroHostBrowser.CoreWebView2 is not CoreWebView2 webView)
        {
            return;
        }

        var envelope = new HeroUiEnvelope("hostError", new HeroUiError(title, message));
        webView.PostWebMessageAsJson(JsonSerializer.Serialize(envelope, SerializerOptions));
    }

    private CheckoutViewModel? GetCheckoutViewModel()
    {
        return (Window.GetWindow(this)?.DataContext as MainViewModel)?.Checkout
            ?? (Application.Current?.MainWindow?.DataContext as MainViewModel)?.Checkout;
    }

    private static CartItem? FindCartItem(CheckoutViewModel checkout, long? productId)
    {
        if (productId == null)
        {
            return null;
        }

        return checkout.CartItems.FirstOrDefault(item => item.ProductId == productId.Value);
    }

    private static decimal NormalizeMoney(decimal? amount)
    {
        var value = amount.GetValueOrDefault();
        if (value < 0)
        {
            value = 0;
        }

        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static HeroUiCheckoutSnapshot BuildCheckoutSnapshot(CheckoutViewModel checkout)
    {
        var searchResults = checkout.SearchResults
            .Take(string.IsNullOrWhiteSpace(checkout.SearchTerm) ? 12 : 16)
            .Select(product => new HeroUiProductResult(
                product.Id,
                product.Name,
                product.Barcode ?? string.Empty,
                product.Price,
                product.TaxRatePercent))
            .ToList();

        var cartItems = checkout.CartItems
            .Select(item => new HeroUiCheckoutItem(
                item.ProductId,
                item.Name,
                item.Barcode ?? string.Empty,
                item.Quantity,
                item.Price,
                item.LineTotal,
                item.TaxAmount,
                item.IsPriceOverridden))
            .ToList();

        HeroUiCheckoutReceipt? receipt = null;
        if (checkout.IsReceiptVisible && checkout.Receipt != null)
        {
            var createdAt = checkout.Receipt.CreatedAt == default
                ? DateTime.UtcNow
                : checkout.Receipt.CreatedAt;

            var receiptItems = checkout.Receipt.Items
                .Select(item => new HeroUiCheckoutReceiptItem(
                    item.Name,
                    item.Quantity,
                    item.LineTotal))
                .ToList();

            receipt = new HeroUiCheckoutReceipt(
                checkout.Receipt.ReceiptNumber,
                createdAt,
                checkout.Receipt.Subtotal,
                checkout.Receipt.Tax,
                checkout.Receipt.Total,
                checkout.Receipt.Tendered,
                checkout.Receipt.Change,
                !string.IsNullOrWhiteSpace(checkout.Receipt.PdfPath) && File.Exists(checkout.Receipt.PdfPath),
                receiptItems);
        }

        return new HeroUiCheckoutSnapshot(
            checkout.SearchTerm,
            checkout.SelectedProduct?.Id,
            checkout.CurrentCashierDisplay,
            checkout.CurrencyCode,
            checkout.StoreName,
            checkout.StoreAddress,
            checkout.StatusMessage,
            checkout.TaxEnabled,
            checkout.TaxRatePercent,
            checkout.Subtotal,
            checkout.Tax,
            checkout.Total,
            checkout.TenderedCash,
            checkout.ChangeDue,
            checkout.CanCompleteSale,
            checkout.CanOverridePrice,
            checkout.IsConfirmClearVisible,
            new[] { checkout.QuickCash1, checkout.QuickCash2, checkout.QuickCash3 },
            cartItems,
            searchResults,
            checkout.SearchResultsCount,
            receipt);
    }
}

internal sealed record HeroUiEnvelope(string Type, object Payload);

internal sealed record HeroUiRequest(
    string Type,
    string? Query = null,
    long? ProductId = null,
    decimal? Amount = null,
    string? Message = null);

internal sealed record HeroUiError(string Title, string Message);

internal sealed record HeroUiCheckoutSnapshot(
    string SearchTerm,
    long? SelectedProductId,
    string CurrentCashier,
    string CurrencyCode,
    string StoreName,
    string StoreAddress,
    string StatusMessage,
    bool TaxEnabled,
    decimal TaxRatePercent,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    decimal TenderedCash,
    decimal ChangeDue,
    bool CanCompleteSale,
    bool CanOverridePrice,
    bool IsConfirmClearVisible,
    IReadOnlyList<decimal> QuickCashAmounts,
    IReadOnlyList<HeroUiCheckoutItem> CartItems,
    IReadOnlyList<HeroUiProductResult> SearchResults,
    int SearchResultsCount,
    HeroUiCheckoutReceipt? Receipt);

internal sealed record HeroUiCheckoutItem(
    long ProductId,
    string Name,
    string Barcode,
    decimal Quantity,
    decimal Price,
    decimal LineTotal,
    decimal TaxAmount,
    bool IsPriceOverridden);

internal sealed record HeroUiProductResult(
    long Id,
    string Name,
    string Barcode,
    decimal Price,
    decimal TaxRatePercent);

internal sealed record HeroUiCheckoutReceipt(
    long ReceiptNumber,
    DateTime CreatedAt,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    decimal Tendered,
    decimal Change,
    bool PdfAvailable,
    IReadOnlyList<HeroUiCheckoutReceiptItem> Items);

internal sealed record HeroUiCheckoutReceiptItem(
    string Name,
    decimal Quantity,
    decimal LineTotal);
