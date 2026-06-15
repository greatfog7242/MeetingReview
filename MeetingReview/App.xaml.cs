using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MeetingReview.Services;
using MeetingReview.ViewModels;

namespace MeetingReview;

public partial class App : Application
{
    private ServiceProvider? _services;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        var sc = new ServiceCollection();

        sc.AddSingleton(new HttpClient { Timeout = TimeSpan.FromMinutes(10) });
        sc.AddSingleton<IUsageService, UsageService>();
        sc.AddSingleton<ITranscriptParserService, TranscriptParserService>();
        sc.AddSingleton<IGeminiService, GeminiService>();
        sc.AddSingleton<VideoPlayerViewModel>();
        sc.AddSingleton<TranscriptViewModel>();
        sc.AddSingleton<SummaryViewModel>();
        sc.AddSingleton<SettingsViewModel>();
        sc.AddSingleton<MainViewModel>();

        _services = sc.BuildServiceProvider();

        await _services.GetRequiredService<IUsageService>().InitAsync();

        var mainVm = _services.GetRequiredService<MainViewModel>();
        mainVm.Settings.Load();
        await mainVm.Settings.LoadSubViewModelsAsync();

        var window = new Views.MainWindow { DataContext = mainVm };
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
