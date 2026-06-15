using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingReview.Models;
using MeetingReview.Models.Dtos;
using MeetingReview.Services;

namespace MeetingReview.ViewModels;

public partial class SummaryViewModel : ObservableObject
{
    private readonly IGeminiService _gemini;
    private readonly IUsageService? _usageService;

    [ObservableProperty] private ObservableCollection<TopicSummary> _topics = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _prompt = string.Empty;
    [ObservableProperty] private bool _hasTopics;

    partial void OnTopicsChanged(ObservableCollection<TopicSummary> value)
        => HasTopics = value.Count > 0;

    internal string TranscriptText { get; set; } = string.Empty;
    internal string ApiKey         { get; set; } = string.Empty;
    internal string GeminiModel    { get; set; } = "gemini-2.5-flash";
    internal string? SavePath      { get; set; }

    public SummaryViewModel(IGeminiService gemini, IUsageService? usageService = null)
    {
        _gemini = gemini;
        _usageService = usageService;
    }

    [RelayCommand]
    private async Task GenerateSummaryAsync(CancellationToken ct)
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
            var result = await _gemini.GenerateSummaryAsync(
                TranscriptText, Prompt, ApiKey, GeminiModel, ct);

            Topics = new ObservableCollection<TopicSummary>(result.Topics);

            if (_usageService != null)
            {
                var cost = _usageService.CalculateCost(
                    result.ModelVersion, result.PromptTokens, result.CandidateTokens);

                await _usageService.SaveUsageAsync(new ApiUsageRecord
                {
                    CalledAt         = DateTime.UtcNow,
                    ModelVersion     = result.ModelVersion,
                    PromptTokens     = result.PromptTokens,
                    CandidateTokens  = result.CandidateTokens,
                    TotalTokens      = result.TotalTokens,
                    EstimatedCostUsd = cost
                }, ct);
            }

            _ = SaveAsync(ct);
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

    internal async Task TryLoadSavedAsync(CancellationToken ct = default)
    {
        if (SavePath == null || !File.Exists(SavePath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(SavePath, ct);
            var dto  = JsonSerializer.Deserialize(json, AppJsonContext.Default.SummarySaveDto);
            if (dto == null) return;

            Prompt = dto.Prompt;
            Topics = new ObservableCollection<TopicSummary>(dto.Topics.Select(d => new TopicSummary
            {
                Title           = d.Title,
                DetailedContent = d.DetailedContent,
                StartMs         = d.StartMs,
                EndMs           = d.EndMs
            }));
        }
        catch { /* silently ignore corrupt / incompatible save files */ }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        if (SavePath == null) return;
        try
        {
            var dto = new SummarySaveDto(
                Prompt,
                DateTime.UtcNow,
                Topics.Select(t => new TopicSummaryDto(t.Title, t.DetailedContent, t.StartMs, t.EndMs)).ToList()
            );
            var json = JsonSerializer.Serialize(dto, AppJsonContext.Default.SummarySaveDto);
            await File.WriteAllTextAsync(SavePath, json, ct);
        }
        catch { }
    }

    [RelayCommand]
    private void ExportMarkdown()
    {
        if (Topics.Count == 0) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Summary as Markdown",
            Filter = "Markdown files|*.md|All files|*.*",
            FileName = "summary.md"
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Meeting Summary");
        foreach (var topic in Topics)
        {
            sb.AppendLine();
            sb.AppendLine($"## {topic.Title}");
            sb.AppendLine();
            sb.AppendLine(topic.DetailedContent);
        }

        File.WriteAllText(dlg.FileName, sb.ToString());
    }

    public void HighlightTopicAt(long positionMs)
    {
        foreach (var topic in Topics)
            topic.IsExpanded = topic.StartMs <= positionMs && positionMs <= topic.EndMs;
    }
}
