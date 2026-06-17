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
    private static readonly string PromptsFile =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "MeetingReview", "prompts.json");

    private readonly IGeminiService _gemini;
    private readonly IUsageService? _usageService;

    // ── Template list ─────────────────────────────────────────────────────
    public ObservableCollection<PromptTemplate> Templates { get; } = new();
    public static IReadOnlyList<PromptFormat> AllFormats { get; } = Enum.GetValues<PromptFormat>();

    [ObservableProperty] private PromptTemplate? _selectedTemplate;

    // ── Inline editor fields ──────────────────────────────────────────────
    [ObservableProperty] private string _editName   = string.Empty;
    [ObservableProperty] private string _editPrompt = string.Empty;
    [ObservableProperty] private PromptFormat _editFormat = PromptFormat.Dropdown;

    // ── Result state ──────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<TopicSummary> _topics = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _hasContent;
    [ObservableProperty] private PromptFormat _currentFormat = PromptFormat.Dropdown;
    [ObservableProperty] private string _responseText = string.Empty;

    // ── Recording context ─────────────────────────────────────────────────
    // Path prefix like "C:\recordings\transcript" (no extension, no template name).
    // MainViewModel sets this when a recording is loaded.
    [ObservableProperty] private string? _baseRecordingPath;

    internal string TranscriptText { get; set; } = string.Empty;
    internal string ApiKey         { get; set; } = string.Empty;
    internal string GeminiModel    { get; set; } = "gemini-2.5-flash";

    // Save path is derived from BaseRecordingPath + sanitized template name.
    private string? SavePath =>
        BaseRecordingPath == null || string.IsNullOrWhiteSpace(EditName)
            ? null
            : $"{BaseRecordingPath}.{SanitizeName(EditName)}.summary.json";

    // Keep backward-compat: callers that read HasTopics still work
    public bool HasTopics => HasContent;

    public SummaryViewModel(IGeminiService gemini, IUsageService? usageService = null)
    {
        _gemini = gemini;
        _usageService = usageService;
    }

    // ── Reactive: recording or template changed ───────────────────────────

    partial void OnBaseRecordingPathChanged(string? value)
    {
        ClearResults();
        if (SelectedTemplate != null)
            _ = TryLoadSavedAsync();
    }

    partial void OnSelectedTemplateChanged(PromptTemplate? value)
    {
        if (value == null) return;
        EditName   = value.Name;
        EditPrompt = value.Prompt;
        EditFormat = value.Format;
        ClearResults();
        if (BaseRecordingPath != null)
            _ = TryLoadSavedAsync();
    }

    private void ClearResults()
    {
        Topics       = new ObservableCollection<TopicSummary>();
        ResponseText = string.Empty;
        ErrorMessage = null;
    }

    partial void OnTopicsChanged(ObservableCollection<TopicSummary> value)
        => RefreshHasContent();

    partial void OnResponseTextChanged(string value)
        => RefreshHasContent();

    private void RefreshHasContent()
        => HasContent = Topics.Count > 0 || !string.IsNullOrEmpty(ResponseText);

    // ── Template CRUD ─────────────────────────────────────────────────────

    [RelayCommand]
    private void NewTemplate()
    {
        SelectedTemplate = null;
        EditName         = string.Empty;
        EditPrompt       = string.Empty;
        EditFormat       = PromptFormat.Dropdown;
    }

    [RelayCommand]
    private async Task SaveTemplateAsync(CancellationToken ct)
    {
        var name   = EditName.Trim();
        var prompt = EditPrompt.Trim();
        if (string.IsNullOrEmpty(name)) return;

        if (SelectedTemplate != null)
        {
            SelectedTemplate.Name   = name;
            SelectedTemplate.Prompt = prompt;
            SelectedTemplate.Format = EditFormat;
        }
        else
        {
            var t = new PromptTemplate { Name = name, Prompt = prompt, Format = EditFormat };
            Templates.Add(t);
            SelectedTemplate = t;
        }

        await PersistTemplatesAsync(ct);
    }

    [RelayCommand]
    private async Task DeleteTemplateAsync(CancellationToken ct)
    {
        if (SelectedTemplate == null) return;
        Templates.Remove(SelectedTemplate);
        SelectedTemplate = null;
        EditName         = string.Empty;
        EditPrompt       = string.Empty;
        EditFormat       = PromptFormat.Dropdown;
        await PersistTemplatesAsync(ct);
    }

    // ── Template persistence ──────────────────────────────────────────────

    public async Task LoadTemplatesAsync(CancellationToken ct = default)
    {
        if (File.Exists(PromptsFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(PromptsFile, ct);
                var dtos = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListPromptTemplateDto);
                if (dtos != null)
                {
                    foreach (var d in dtos)
                        Templates.Add(new PromptTemplate
                        {
                            Name   = d.Name,
                            Prompt = d.Prompt,
                            Format = (PromptFormat)d.Format
                        });
                    return;
                }
            }
            catch { }
        }

        // Seed defaults on first run
        Templates.Add(new PromptTemplate
        {
            Name   = "Topic Breakdown",
            Prompt = "identify all distinct topics discussed and provide detailed notes for each",
            Format = PromptFormat.Dropdown
        });
        Templates.Add(new PromptTemplate
        {
            Name   = "Meeting Notes",
            Prompt = "write detailed meeting notes with key decisions, action items, and next steps",
            Format = PromptFormat.Markdown
        });
        Templates.Add(new PromptTemplate
        {
            Name   = "Action Items",
            Prompt = "list all action items, commitments, and follow-ups mentioned",
            Format = PromptFormat.Text
        });

        await PersistTemplatesAsync(ct);
    }

    private async Task PersistTemplatesAsync(CancellationToken ct = default)
    {
        try
        {
            var dir = Path.GetDirectoryName(PromptsFile)!;
            Directory.CreateDirectory(dir);
            var dtos = Templates.Select(t => new PromptTemplateDto(t.Name, t.Prompt, (int)t.Format)).ToList();
            var json = JsonSerializer.Serialize(dtos, AppJsonContext.Default.ListPromptTemplateDto);
            await File.WriteAllTextAsync(PromptsFile, json, ct);
        }
        catch { }
    }

    // ── Summary generation ────────────────────────────────────────────────

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
        CurrentFormat = EditFormat;

        try
        {
            if (EditFormat == PromptFormat.Dropdown)
            {
                var result = await _gemini.GenerateSummaryAsync(
                    TranscriptText, EditPrompt, ApiKey, GeminiModel, ct);

                Topics       = new ObservableCollection<TopicSummary>(result.Topics);
                ResponseText = string.Empty;

                await RecordUsageAsync(result.ModelVersion, result.PromptTokens,
                                       result.CandidateTokens, result.TotalTokens, ct);
            }
            else
            {
                var result = await _gemini.GenerateTextAsync(
                    TranscriptText, EditPrompt, ApiKey, GeminiModel, ct);

                Topics       = new ObservableCollection<TopicSummary>();
                ResponseText = result.Text;

                await RecordUsageAsync(result.ModelVersion, result.PromptTokens,
                                       result.CandidateTokens, result.TotalTokens, ct);
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

    private async Task RecordUsageAsync(
        string modelVersion, int promptTokens, int candidateTokens, int totalTokens,
        CancellationToken ct)
    {
        if (_usageService == null) return;
        var cost = _usageService.CalculateCost(modelVersion, promptTokens, candidateTokens);
        await _usageService.SaveUsageAsync(new ApiUsageRecord
        {
            CalledAt         = DateTime.UtcNow,
            ModelVersion     = modelVersion,
            PromptTokens     = promptTokens,
            CandidateTokens  = candidateTokens,
            TotalTokens      = totalTokens,
            EstimatedCostUsd = cost
        }, ct);
    }

    // ── Save / Load per-recording per-template ────────────────────────────

    internal async Task TryLoadSavedAsync(CancellationToken ct = default)
    {
        var path = SavePath;
        if (path == null || !File.Exists(path)) return;
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            var dto  = JsonSerializer.Deserialize(json, AppJsonContext.Default.SummarySaveDto);
            if (dto == null) return;

            // Prompt/Name/Format come from the live template definition.
            // Only restore the generated results and the format they were rendered in.
            CurrentFormat = (PromptFormat)dto.Format;
            ResponseText  = dto.ResponseText ?? string.Empty;
            Topics = new ObservableCollection<TopicSummary>(dto.Topics.Select(d => new TopicSummary
            {
                Title           = d.Title,
                DetailedContent = d.DetailedContent,
                StartMs         = d.StartMs,
                EndMs           = d.EndMs
            }));
        }
        catch { }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        var path = SavePath;
        if (path == null) return;
        try
        {
            var dto = new SummarySaveDto(
                EditPrompt,
                DateTime.UtcNow,
                Topics.Select(t => new TopicSummaryDto(t.Title, t.DetailedContent, t.StartMs, t.EndMs)).ToList(),
                (int)CurrentFormat,
                string.IsNullOrEmpty(ResponseText) ? null : ResponseText
            );
            var json = JsonSerializer.Serialize(dto, AppJsonContext.Default.SummarySaveDto);
            await File.WriteAllTextAsync(path, json, ct);
        }
        catch { }
    }

    // ── Export ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ExportMarkdown()
    {
        if (!HasContent) return;

        var suggestedName = string.IsNullOrWhiteSpace(EditName) ? "summary" : EditName;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Summary as Markdown",
            Filter = "Markdown files|*.md|All files|*.*",
            FileName = $"{suggestedName}.md"
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Meeting Summary");
        sb.AppendLine();
        sb.AppendLine("## Prompt Used");
        sb.AppendLine();
        sb.AppendLine(_gemini.BuildExportablePrompt(EditPrompt, CurrentFormat));
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        if (CurrentFormat == PromptFormat.Dropdown)
        {
            foreach (var topic in Topics)
            {
                sb.AppendLine($"## {topic.Title}");
                sb.AppendLine();
                sb.AppendLine(topic.DetailedContent);
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine(ResponseText);
        }

        File.WriteAllText(dlg.FileName, sb.ToString());
    }

    // ── Video sync ────────────────────────────────────────────────────────

    public void HighlightTopicAt(long positionMs)
    {
        foreach (var topic in Topics)
            topic.IsExpanded = topic.StartMs <= positionMs && positionMs <= topic.EndMs;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string SanitizeName(string name)
    {
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
        var result  = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim('_', ' ');
        return string.IsNullOrEmpty(result) ? "_" : result;
    }
}
