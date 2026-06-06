using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingReview.Models;
using MeetingReview.Services;

namespace MeetingReview.ViewModels;

public partial class SummaryViewModel : ObservableObject
{
    private readonly IGeminiService _gemini;

    [ObservableProperty] private ObservableCollection<TopicSummary> _topics = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    internal string TranscriptText { get; set; } = string.Empty;
    internal string ApiKey { get; set; } = string.Empty;

    public SummaryViewModel(IGeminiService gemini) => _gemini = gemini;

    [RelayCommand]
    private async Task GenerateSummaryAsync(string? userPrompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            ErrorMessage = "Please configure your Gemini API key in Settings.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TranscriptText))
        {
            ErrorMessage = "Load a transcript before generating a summary.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var topics = await _gemini.GenerateSummaryAsync(
                TranscriptText, userPrompt ?? string.Empty, ApiKey, ct);
            Topics = new ObservableCollection<TopicSummary>(topics);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void HighlightTopicAt(long positionMs)
    {
        foreach (var topic in Topics)
            topic.IsExpanded = topic.StartMs <= positionMs && positionMs <= topic.EndMs;
    }
}
