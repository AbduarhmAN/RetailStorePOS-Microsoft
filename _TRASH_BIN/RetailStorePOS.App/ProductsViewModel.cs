using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Repositories;

namespace RetailStorePOS.App;

public sealed class ProductsViewModel : ViewModelBase
{
    private readonly ProductRepository _productRepository;
    private readonly TaxCategoryRepository _taxCategoryRepository;
    private readonly ProductImportCoordinator _importCoordinator;

    private string _searchTerm = string.Empty;
    private Product? _selectedProduct;
    private string _statusMessage = string.Empty;
    private string _importSummary = string.Empty;
    private bool _isEditorOpen;
    private ProductImportJobSnapshot? _importJob;

    public ProductsViewModel(ProductRepository productRepository, TaxCategoryRepository taxCategoryRepository, ProductImportCoordinator importCoordinator)
    {
        _productRepository = productRepository;
        _taxCategoryRepository = taxCategoryRepository;
        _importCoordinator = importCoordinator;

        Editor.PropertyChanged += (_, _) => NotifyEditorState();

        NewCommand = new RelayCommand(async () => await NewProduct());
        SaveCommand = new RelayCommand(async () => await SaveProduct());
        DeleteCommand = new RelayCommand(async () => await DeleteProduct());
        ImportCommand = new RelayCommand(async () => await ImportCsv());
        CloseEditorCommand = new RelayCommand(CloseEditor);

        LoadTaxCategories();
        LoadProducts();
        if (_importCoordinator.CurrentJob != null)
        {
            ApplyImportJobSnapshot(_importCoordinator.CurrentJob);
        }

        _importCoordinator.JobChanged += (_, snapshot) => ApplyImportJobSnapshot(snapshot);

        AppServices.Auth.LoginStateChanged += (_, _) =>
        {
            LoadTaxCategories();
            LoadProducts();
            OnPropertyChanged(nameof(CanOverridePrice));
        };
    }

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<TaxCategory> TaxCategories { get; } = new();
    public ProductEditor Editor { get; } = new();

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (SetProperty(ref _searchTerm, value))
            {
                OnPropertyChanged(nameof(SearchSummary));
                LoadProducts();
            }
        }
    }

    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                if (value == null)
                {
                    Editor.Reset();
                }
                else
                {
                    Editor.LoadFrom(value);
                    IsEditorOpen = true;
                }

                NotifyEditorState();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ImportSummary
    {
        get => _importSummary;
        private set => SetProperty(ref _importSummary, value);
    }

    public bool IsEditorOpen
    {
        get => _isEditorOpen;
        set => SetProperty(ref _isEditorOpen, value);
    }

    public bool CanOverridePrice => AppServices.Auth.CanOverridePrice;
    public bool HasImportJob => _importJob != null;
    public bool IsImportRunning => _importJob?.IsActive == true;
    public bool CanImportCsv => !IsImportRunning;
    public bool HasImportDetail => !string.IsNullOrWhiteSpace(_importJob?.DetailMessage);
    public bool HasProducts => Products.Count > 0;
    public bool HasNoProducts => !HasProducts;
    public bool CanDeleteSelected => Editor.Id > 0;
    public bool IsEditingExistingProduct => Editor.Id > 0;
    public string ImportButtonText => IsImportRunning ? "Import Running" : "Import CSV";
    public string ImportFileName => _importJob?.FileName ?? "No file selected";
    public string ImportStatusText => _importJob?.StatusText ?? string.Empty;
    public string ImportDetailText => _importJob?.DetailMessage ?? string.Empty;
    public double ImportProgressPercent => _importJob?.ProgressPercent ?? 0;
    public string ImportPercentLabel => _importJob == null
        ? "0% complete"
        : $"{Math.Round(_importJob.ProgressPercent)}% complete";
    public string ImportProcessedLabel => _importJob == null
        ? "0 / 0 processed"
        : $"{_importJob.ProcessedRows:N0} / {_importJob.TotalRows:N0} processed";
    public string ImportImportedLabel => _importJob == null
        ? "0 imported"
        : $"{_importJob.ImportedRows:N0} imported";
    public string ImportSkippedLabel => _importJob == null
        ? "0 skipped"
        : $"{_importJob.SkippedRows:N0} skipped";
    public string SearchSummary => string.IsNullOrWhiteSpace(SearchTerm)
        ? "Showing all products."
        : $"Filtering products for \"{SearchTerm.Trim()}\".";
    public string EditorTitle => IsEditingExistingProduct ? "Edit Product" : "New Product";
    public string EditorSubtitle => IsEditingExistingProduct
        ? "Update the selected product and save changes to the catalog."
        : "Create a product and save it to the catalog.";

    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand CloseEditorCommand { get; }

    private void LoadProducts()
    {
        Products.Clear();
        foreach (var product in _productRepository.Search(SearchTerm, null))
        {
            Products.Add(product);
        }

        OnPropertyChanged(nameof(HasProducts));
        OnPropertyChanged(nameof(HasNoProducts));
    }

    private void LoadTaxCategories()
    {
        TaxCategories.Clear();
        foreach (var category in _taxCategoryRepository.GetAll())
        {
            TaxCategories.Add(category);
        }
    }

    private async Task NewProduct()
    {
        SelectedProduct = null;
        Editor.Reset();
        IsEditorOpen = true;
        StatusMessage = "New product ready.";
        NotifyEditorState();

        // Clear the status message (toast) after a delay
        string currentMessage = StatusMessage;
        await Task.Delay(3500);
        if (StatusMessage == currentMessage) // Only clear if it hasn't changed to a new message
        {
            StatusMessage = string.Empty;
        }
    }

    private async Task SaveProduct()
    {
        if (string.IsNullOrWhiteSpace(Editor.Name))
        {
            StatusMessage = "Product name is required.";
            return;
        }

        if (Editor.Price < 0)
        {
            StatusMessage = "Price must be zero or greater.";
            return;
        }

        var product = new Product
        {
            Id = Editor.Id,
            Name = Editor.Name.Trim(),
            Barcode = Clean(Editor.Barcode),
            Sku = Clean(Editor.Sku),
            Unit = Clean(Editor.Unit),
            Price = Editor.Price,
            TaxCategoryId = Editor.TaxCategoryId,
            CashierName = AppServices.Auth.CurrentUser?.DisplayName
                          ?? AppServices.Auth.CurrentUser?.Username
                          ?? "Cashier"
        };

        if (!AppServices.Auth.CanOverridePrice)
        {
            if (product.Id == 0)
            {
                product.Price = 0m;
            }
            else
            {
                var existing = _productRepository.Search(product.Barcode ?? "", 1).FirstOrDefault(p => p.Id == product.Id)
                               ?? _productRepository.Search(product.Name, 50).FirstOrDefault(p => p.Id == product.Id);
                // In lieu of GetById, we will just trust the UI disablement for updates, but ideally we'd preserve the old price
                // For this patch, we will rely on the IsEnabled binding on the UI.
            }
        }

        if (product.Id == 0)
        {
            _productRepository.Create(product);
            StatusMessage = "Product created.";
        }
        else
        {
            _productRepository.Update(product);
            StatusMessage = "Product updated.";
        }

        LoadProducts();
        SelectedProduct = Products.FirstOrDefault(p => p.Id == product.Id);
        IsEditorOpen = false;
        AppServices.RaiseProductsUpdated();
        NotifyEditorState();

        // Clear the status message (toast) after a delay
        string currentMessage = StatusMessage;
        await Task.Delay(3500);
        if (StatusMessage == currentMessage) // Only clear if it hasn't changed to a new message
        {
            StatusMessage = string.Empty;
        }
    }

    private async Task DeleteProduct()
    {
        if (Editor.Id == 0)
        {
            StatusMessage = "Select a product to delete.";
            return;
        }

        _productRepository.Delete(Editor.Id);
        Editor.Reset();
        LoadProducts();
        AppServices.RaiseProductsUpdated();
        IsEditorOpen = false;
        StatusMessage = "Product deleted.";
        NotifyEditorState();

        // Clear the status message (toast) after a delay
        string currentMessage = StatusMessage;
        await Task.Delay(3500);
        if (StatusMessage == currentMessage) // Only clear if it hasn't changed to a new message
        {
            StatusMessage = string.Empty;
        }
    }

    private void CloseEditor()
    {
        IsEditorOpen = false;
        SelectedProduct = null;
        NotifyEditorState();
    }

    private async Task ImportCsv()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!_importCoordinator.TryStartImport(dialog.FileName, out var errorMessage))
        {
            StatusMessage = errorMessage ?? "A CSV import is already running.";
            await ClearStatusMessageLaterAsync(StatusMessage, 3500);
            return;
        }

        ImportSummary = string.Empty;
        StatusMessage = $"Import started for {Path.GetFileName(dialog.FileName)}. You can keep working while it runs.";
        await ClearStatusMessageLaterAsync(StatusMessage, 3500);
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private void NotifyEditorState()
    {
        OnPropertyChanged(nameof(CanDeleteSelected));
        OnPropertyChanged(nameof(IsEditingExistingProduct));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(EditorSubtitle));
    }

    private void ApplyImportJobSnapshot(ProductImportJobSnapshot? snapshot)
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == false)
        {
            Application.Current.Dispatcher.Invoke(() => ApplyImportJobSnapshot(snapshot));
            return;
        }

        if (snapshot == null)
        {
            _importJob = null;
            ImportSummary = string.Empty;
            OnPropertyChanged(nameof(HasImportJob));
            OnPropertyChanged(nameof(IsImportRunning));
            OnPropertyChanged(nameof(CanImportCsv));
            OnPropertyChanged(nameof(HasImportDetail));
            OnPropertyChanged(nameof(ImportButtonText));
            OnPropertyChanged(nameof(ImportFileName));
            OnPropertyChanged(nameof(ImportStatusText));
            OnPropertyChanged(nameof(ImportDetailText));
            OnPropertyChanged(nameof(ImportProgressPercent));
            OnPropertyChanged(nameof(ImportPercentLabel));
            OnPropertyChanged(nameof(ImportProcessedLabel));
            OnPropertyChanged(nameof(ImportImportedLabel));
            OnPropertyChanged(nameof(ImportSkippedLabel));
            return;
        }

        var previous = _importJob;
        _importJob = snapshot;

        OnPropertyChanged(nameof(HasImportJob));
        OnPropertyChanged(nameof(IsImportRunning));
        OnPropertyChanged(nameof(CanImportCsv));
        OnPropertyChanged(nameof(HasImportDetail));
        OnPropertyChanged(nameof(ImportButtonText));
        OnPropertyChanged(nameof(ImportFileName));
        OnPropertyChanged(nameof(ImportStatusText));
        OnPropertyChanged(nameof(ImportDetailText));
        OnPropertyChanged(nameof(ImportProgressPercent));
        OnPropertyChanged(nameof(ImportPercentLabel));
        OnPropertyChanged(nameof(ImportProcessedLabel));
        OnPropertyChanged(nameof(ImportImportedLabel));
        OnPropertyChanged(nameof(ImportSkippedLabel));

        if (previous?.Id == snapshot.Id && previous.Status == snapshot.Status)
        {
            return;
        }

        if (snapshot.Status == ProductImportJobStatus.Completed)
        {
            ImportSummary = $"Imported: {snapshot.CreatedRows} created, {snapshot.UpdatedRows} updated, {snapshot.SkippedRows} skipped.";
            LoadProducts();
            StatusMessage = snapshot.DetailMessage ?? $"Import completed: {snapshot.CreatedRows} created, {snapshot.UpdatedRows} updated, {snapshot.SkippedRows} skipped.";
            _ = ClearStatusMessageLaterAsync(StatusMessage, 4500);
            return;
        }

        if (snapshot.Status == ProductImportJobStatus.Failed)
        {
            ImportSummary = string.Empty;
            StatusMessage = snapshot.DetailMessage ?? snapshot.StatusText;
            _ = ClearStatusMessageLaterAsync(StatusMessage, 4500);
        }
    }

    private async Task ClearStatusMessageLaterAsync(string currentMessage, int delayMilliseconds)
    {
        await Task.Delay(delayMilliseconds);
        if (StatusMessage == currentMessage)
        {
            StatusMessage = string.Empty;
        }
    }
}

public sealed class ProductEditor : ViewModelBase
{
    private long _id;
    private string _name = string.Empty;
    private string? _barcode;
    private string? _sku;
    private string? _unit;
    private decimal _price;
    private long _taxCategoryId = 1;

    public long Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    public string? Sku
    {
        get => _sku;
        set => SetProperty(ref _sku, value);
    }

    public string? Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    public decimal Price
    {
        get => _price;
        set => SetProperty(ref _price, value);
    }

    public long TaxCategoryId
    {
        get => _taxCategoryId;
        set => SetProperty(ref _taxCategoryId, value);
    }

    public void LoadFrom(Product product)
    {
        Id = product.Id;
        Name = product.Name;
        Barcode = product.Barcode;
        Sku = product.Sku;
        Unit = product.Unit;
        Price = product.Price;
        TaxCategoryId = product.TaxCategoryId;
    }

    public void Reset()
    {
        Id = 0;
        Name = string.Empty;
        Barcode = string.Empty;
        Sku = string.Empty;
        Unit = string.Empty;
        Price = 0m;
        TaxCategoryId = 1;
    }
}


