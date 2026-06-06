using CommunityToolkit.Mvvm.ComponentModel;

namespace MeetingReview.Models;

public partial class ModelRate : ObservableObject
{
    [ObservableProperty] private string _modelPattern = string.Empty;
    [ObservableProperty] private double _inputRatePerMillion;
    [ObservableProperty] private double _outputRatePerMillion;
}
