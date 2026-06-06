using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingReview.Models;
using MeetingReview.Services;

namespace MeetingReview.ViewModels;

public partial class CostHistoryViewModel : ObservableObject
{
    private readonly IUsageService? _usageService;

    [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime _toDate = DateTime.Today;
    [ObservableProperty] private ObservableCollection<ApiUsageRecord> _records = new();
    [ObservableProperty] private double _totalCostUsd;
    [ObservableProperty] private string? _errorMessage;

    public CostHistoryViewModel(IUsageService? usageService = null) => _usageService = usageService;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        if (_usageService == null) return;
        ErrorMessage = null;
        try
        {
            var list = await _usageService.QueryUsageAsync(FromDate, ToDate.AddDays(1), ct);
            Records = new ObservableCollection<ApiUsageRecord>(list);
            TotalCostUsd = list.Sum(r => r.EstimatedCostUsd);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
