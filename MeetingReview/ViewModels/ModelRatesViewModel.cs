using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingReview.Models;
using MeetingReview.Services;

namespace MeetingReview.ViewModels;

public partial class ModelRatesViewModel : ObservableObject
{
    private readonly IUsageService? _usageService;

    [ObservableProperty] private ObservableCollection<ModelRate> _rates = new();
    [ObservableProperty] private string? _statusMessage;

    public ModelRatesViewModel(IUsageService? usageService = null) => _usageService = usageService;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (_usageService == null) return;
        var list = await _usageService.GetRatesAsync(ct);
        Rates = new ObservableCollection<ModelRate>(list);
    }

    [RelayCommand]
    private void AddRate() =>
        Rates.Add(new ModelRate { ModelPattern = "new-model", InputRatePerMillion = 0, OutputRatePerMillion = 0 });

    [RelayCommand]
    private void DeleteRate(ModelRate? rate)
    {
        if (rate != null) Rates.Remove(rate);
    }

    [RelayCommand]
    private async Task SaveAllRatesAsync(CancellationToken ct)
    {
        if (_usageService == null) return;
        try
        {
            await _usageService.ReplaceAllRatesAsync(Rates, ct);
            StatusMessage = "Saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
}
