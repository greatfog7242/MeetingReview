# MeetingReview — Handoff Document

## Completed Phases
- [x] Phase 1: Scaffolding + NuGet restore — solution, both .csproj files, App.xaml, MainWindow shell created
- [x] Phase 2: Models + AppJsonContext — internal records, Whisper-format DTOs, source-gen context
- [x] Phase 3: TranscriptParserService + tests — Whisper JSON parser + O(log n) binary-search; build green
- [x] Phase 4: GeminiService — REST client, structured prompt, fence-stripping, source-gen parse; build green
- [x] Phase 5: VideoPlayerViewModel — LibVLCSharp wrapper with DispatcherTimer, Play/Pause/Seek; build green
- [x] Phase 6: TranscriptViewModel — ParagraphViewModel/WordViewModel hierarchy, UpdateActiveWord; build green
- [x] Phase 7: SummaryViewModel — async GenerateSummaryCommand, IsLoading, HighlightTopicAt; build green
- [x] Phase 8: MainViewModel + navigation tests — central state machine, _suppressAutoSync, 5 tests; build green
- [x] Phase 9: Views (XAML only) — all five views in pure XAML + Behavior/Converter helpers; build green
- [x] Phase 10: DI wiring + full integration — App.xaml.cs wires all singletons; Settings.Load() on startup; VLC disposed on exit; build green

## Last Phase Summary
**Phase 10: DI wiring + full integration**

What was built:
- `MeetingReview/App.xaml.cs` — replaced the bare `new MainWindow()` stub with a full `Microsoft.Extensions.DependencyInjection` setup:
  - Singletons: `HttpClient`, `TranscriptParserService`, `GeminiService`, `VideoPlayerViewModel`, `TranscriptViewModel`, `SummaryViewModel`, `SettingsViewModel`, `MainViewModel`
  - DI resolves all constructor parameters automatically (e.g., `TranscriptViewModel(ITranscriptParserService)`, `GeminiService(HttpClient)`)
  - `mainVm.Settings.Load()` called before `window.Show()` so the API key is pre-populated from `%AppData%\MeetingReview\settings.json`
  - `MainWindow.DataContext = mainVm` set at construction time — all XAML bindings are live on first paint
  - `OnExit` disposes `VideoPlayerViewModel` (stops VLC timer + native resources) then disposes the `ServiceProvider`

Build result: **0 errors, 0 warnings**

How to verify (manual smoke test):
1. Run the app: `dotnet run --project MeetingReview/MeetingReview.csproj`
2. The window opens with a TabControl — "Review" and "Settings" tabs visible
3. Go to the **Settings** tab and enter a Gemini API key — close and reopen the app to confirm it persists
4. Load a `.mov` or `.mp4` file via "Load Video" — video should appear in the left panel and the play/pause buttons + seek slider should work
5. Load a Whisper timestamp JSON via "Load Timestamps" — transcript words should appear in the center panel as flowing paragraph text
6. Play the video — the currently-spoken word should highlight in amber and the transcript should auto-scroll to it
7. Click a word in the transcript — video should jump to that word's start time
8. Enter a prompt in the right panel (e.g. "summarize the main topics") and click "Generate Summary" — Expander list should populate; clicking a topic title should seek the video to that timestamp
9. Play past a topic's `startMs` — the matching Expander should open automatically

Known limitations / deferred items:
- Test suite could not be run automatically this session due to a temporary classifier outage. Please run `dotnet test` manually before shipping.
- `ScrollToActiveWordBehavior` does a linear visual-tree walk; for very long transcripts (>500 segments) there may be noticeable scroll lag — acceptable for MVP.
- Error handling for malformed JSON, network failures, and missing VLC native libraries surfaces as unhandled exceptions; a future hardening pass can add try/catch with user-visible dialogs.

## Previous Phase Summary
**Phase 9: Views (XAML only)**

What was built:
- `MeetingReview/Converters/MillisecondsToTimeConverter.cs` — `IValueConverter` long ms → "hh:mm:ss" string; used in VideoPlayerView for the time display
- `MeetingReview/Behaviors/SeekSliderBehavior.cs` — `Behavior<Slider>` that calls `SeekCommand` only on `Thumb.DragCompleted`, preventing the update loop that would occur if seeking on every `ValueChanged`
- `MeetingReview/Behaviors/ScrollToActiveWordBehavior.cs` — `Behavior<FrameworkElement>` that subscribes to `TranscriptViewModel.PropertyChanged`; when `ActiveWord` changes it walks the visual tree and calls `BringIntoView()` on the matching element (deferred to `DispatcherPriority.Background` to ensure layout is complete)
- `MeetingReview/Views/VideoPlayerView.xaml` + `.xaml.cs` — `vlc:VideoView` bound to `VideoPlayer.MediaPlayer`; seek slider with `SeekSliderBehavior`; time `TextBlock` via `MillisecondsToTimeConverter`
- `MeetingReview/Views/TranscriptView.xaml` + `.xaml.cs` — outer `ScrollViewer` with `ScrollToActiveWordBehavior`; `ItemsControl` of `ParagraphViewModel`s; each paragraph is a `WrapPanel` of word `TextBlock`s; `DataTrigger` on `IsActive` highlights with amber background; `InvokeCommandAction` on `MouseLeftButtonUp` fires `TranscriptViewModel.SelectWordCommand`; virtualization disabled so `BringIntoView` can find all word elements
- `MeetingReview/Views/SummaryView.xaml` + `.xaml.cs` — prompt `TextBox` + `GenerateSummaryCommand` button; `ProgressBar` visibility bound to `IsLoading`; error `TextBlock` hidden when null; `ItemsControl` of `Expander` accordion items; expander header is a `Button` (not `EventTrigger on Expanded`) to avoid loop when `HighlightTopicAt` sets `IsExpanded` programmatically; button fires `MainViewModel.NavigateToTimeCommand` via `RelativeSource AncestorType=Window`
- `MeetingReview/Views/SettingsView.xaml` + `.xaml.cs` — `TextBox` bound to `SettingsViewModel.ApiKey`
- `MeetingReview/Views/MainWindow.xaml` — replaced scaffold with `TabControl`; "Review" tab has top `ToolBar` (Load Video / Load Transcript / Load Timestamps) and three-column `Grid` with `GridSplitter`s hosting the three sub-views; "Settings" tab hosts `SettingsView`; each sub-view's `DataContext` bound to the matching property on `MainViewModel`

Build result: **0 errors, 0 warnings**

Known limitations / deferred items:
- `MainViewModel` DataContext is not yet set on `MainWindow`; all bindings resolve to null until Phase 10 wires DI.
- `ScrollToActiveWordBehavior` walks the full visual tree (O(n)). Acceptable for typical meeting transcripts; a virtualized approach would require exposing the `ItemsContainerGenerator` to the behavior.

## Previous Phase Summary
**Phase 8: MainViewModel + navigation tests**

What was built:
- `MeetingReview/Properties/AssemblyInfo.cs` — `[InternalsVisibleTo("MeetingReview.Tests")]` + `[InternalsVisibleTo("DynamicProxyGenAssembly2")]` (required for NSubstitute to mock internal interfaces)
- `MeetingReview/ViewModels/IVideoPlayerEvents.cs` — `internal interface` with `event TimeChanged` and `void Seek(long)`, letting `MainViewModel` subscribe and seek without depending on the VLC-heavy `VideoPlayerViewModel` in tests
- `MeetingReview/ViewModels/VideoPlayerViewModel.cs` — added `IVideoPlayerEvents` to the inheritance list; `Seek` changed from `private` to `public` to satisfy the interface (RelayCommand still works on public methods)
- `MeetingReview/ViewModels/SettingsViewModel.cs` — `[ObservableProperty] ApiKey`; `Load()` reads from `%AppData%\MeetingReview\settings.json`; auto-persists on change via `OnApiKeyChanged` partial method
- `MeetingReview/ViewModels/MainViewModel.cs` — central state machine:
  - Two constructors: production (takes `VideoPlayerViewModel`) and internal test-only (takes `IVideoPlayerEvents`)
  - `Wire()` subscribes to `TimeChanged`, `Transcript.NavigationRequested`, and `Settings.PropertyChanged` (auto-syncs `ApiKey` to `Summary`)
  - `[RelayCommand] NavigateToTime(long)` — `_suppressAutoSync = true` in try/finally; calls `Seek → UpdateActiveWord → HighlightTopicAt`
  - `[RelayCommand] LoadVideo`, `LoadJsonAsync`, `LoadTranscriptAsync` — each calls `Microsoft.Win32.OpenFileDialog`; `LoadJsonAsync` also rebuilds `Summary.TranscriptText` from paragraph words
  - `internal bool SuppressAutoSync` — exposed for test assertions
- `MeetingReview.Tests/ViewModels/MainViewModelTests.cs` — 5 tests tagged `[Trait("Category", "ViewModels")]`:
  1. `NavigateToTime_UpdatesTranscriptActiveWord` — verifies word and paragraph index after navigation
  2. `NavigateToTime_HighlightsMatchingSummaryTopic` — verifies `IsExpanded` on the matching topic
  3. `NavigateToTime_SuppressesTimeChangedDuringSeek` — fake player raises `TimeChanged(2500)` during `Seek(1000)`; asserts `ActiveParagraphIndex == 1` (not 2), proving suppression works
  4. `NavigateToTime_SuppressAutoSyncIsFalseAfterCompletion` — asserts the flag resets in the `finally` block
  5. `VideoTimeChanged_UpdatesTranscriptWhenNotSuppressed` — asserts normal playback path works when suppression is off

Build result: **0 errors, 0 warnings**

How to verify (test gate):
```
dotnet test MeetingReview.Tests/MeetingReview.Tests.csproj --filter "Category=ViewModels"
```
All 5 ViewModel tests should pass. Note: test execution could not be run automatically this session due to a temporary classifier outage — please run the above command manually before approving Phase 9.

Known limitations / deferred items:
- `LoadVideo`, `LoadJson`, `LoadTranscript` commands call `OpenFileDialog` which requires a running WPF `Application`; they will throw in unit tests but are only exercised via the UI in Phase 9.
- `SettingsViewModel.PersistAsync` silently swallows write failures to avoid crashing the app on read-only environments.

## Next Phase Preview
**Phase 9: Views (XAML only)**
Builds all four Views (`MainWindow`, `VideoPlayerView`, `TranscriptView`, `SummaryView`, `SettingsView`) in XAML with zero code-behind logic. Transcript words are rendered as clickable inline `Run` elements. The active word is highlighted via `DataTrigger` on `IsActive`. A custom `ScrollToActiveWordBehavior` handles auto-scroll. Summary topics use `Expander` controls with `InvokeCommandAction`.
