using System.Windows;
using System.Windows.Input;
using RetailStorePOS.Data.Repositories;

namespace RetailStorePOS.App
{
    public class CloseDayViewModel : ViewModelBase
    {
        private readonly SaleRepository _saleRepository;
        private DailySummary? _summary;
        private string _statusMessage = string.Empty;

        public CloseDayViewModel(SaleRepository saleRepository)
        {
            _saleRepository = saleRepository;
            LoadSummaryCommand = new RelayCommand(LoadSummary);
            CloseShiftCommand = new RelayCommand(CloseShift, CanClose);
            PrintZReportCommand = new RelayCommand(PrintZReport);

            // Load initial data
            LoadSummary();
        }

        public DailySummary? Summary
        {
            get => _summary;
            set
            {
                _summary = value;
                OnPropertyChanged(nameof(Summary));
                OnPropertyChanged(nameof(TotalRevenue));
                OnPropertyChanged(nameof(TotalTax));
                OnPropertyChanged(nameof(TransactionCount));
                OnPropertyChanged(nameof(CashTotal));
                OnPropertyChanged(nameof(CardTotal));
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public decimal TotalRevenue => Summary?.TotalRevenue ?? 0;
        public decimal TotalTax => Summary?.TotalTax ?? 0;
        public int TransactionCount => Summary?.TransactionCount ?? 0;
        public decimal CashTotal => Summary?.CashTotal ?? 0;
        public decimal CardTotal => Summary?.CardTotal ?? 0;

        public ICommand LoadSummaryCommand { get; }
        public ICommand CloseShiftCommand { get; }
        public ICommand PrintZReportCommand { get; }

        public void LoadSummary()
        {
            try
            {
                Summary = _saleRepository.GetDailySummary(DateTime.Now);
                StatusMessage = $"Summary loaded for {DateTime.Now:d}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading summary: {ex.Message}";
                AppServices.ReportException(ex, "CloseDayViewModel.LoadSummary");
            }
        }

        private bool CanClose()
        {
            return Summary != null && Summary.TransactionCount > 0;
        }

        private void CloseShift()
        {
            var result = MessageBox.Show(
                $"Are you sure you want to close the shift for {DateTime.Now:d}?\n\n" +
                $"Total Revenue: {TotalRevenue:C}\n" +
                $"Cash in Drawer: {CashTotal:C}",
                "Confirm Close Shift",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // TODO: Implement actual session closing logic (e.g., set flag in DB)
                StatusMessage = "Shift closed successfully. Z-Report printed.";
                PrintZReport(); // Auto-print on close
            }
        }

        private void PrintZReport()
        {
            // TODO: Integrate with Printer Service
            StatusMessage = "Z-Report sent to printer (Simulation)...";
        }
    }
}
