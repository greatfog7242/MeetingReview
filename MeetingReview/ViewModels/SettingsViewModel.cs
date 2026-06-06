using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MeetingReview.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeetingReview");
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    [ObservableProperty] private string _apiKey = string.Empty;

    partial void OnApiKeyChanged(string value) => _ = PersistAsync();

    public void Load()
    {
        if (!File.Exists(SettingsFile)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsFile));
            ApiKey = doc.RootElement.GetProperty("apiKey").GetString() ?? string.Empty;
        }
        catch { }
    }

    private async Task PersistAsync()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            await File.WriteAllTextAsync(SettingsFile,
                JsonSerializer.Serialize(new { apiKey = ApiKey }));
        }
        catch { }
    }
}
