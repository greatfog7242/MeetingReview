# MeetingReview — Windows Desktop App: Development Plan

## Context

Build a WPF (.NET 8) Windows desktop application that loads meeting recordings (`.mov`), transcripts, and word-level timestamp JSON files, then calls the Gemini API to generate a structured, expandable topic summary. All three panels (video, transcript, summary) are bi-directionally linked via a centralized navigation state machine.

Three non-negotiable constraints govern code generation:
1. **Idempotency** — each service and ViewModel is generated and compilation-verified independently before UI assembly begins.
2. **Benchmarks** — JSON parsing and timestamp lookups must be O(log n); unit tests must prove this with large synthetic datasets.
3. **Strict MVVM** — zero business logic or event handlers in `.xaml.cs` code-behind files (only `InitializeComponent()` is permitted there).

---

## Technology Stack

| Concern | Choice | Rationale |
|---|---|---|
| Framework | WPF (.NET 8) | Mature data-binding, `VirtualizingStackPanel`, stable async/UI thread sync |
| MVVM toolkit | CommunityToolkit.Mvvm | Source-generated `[ObservableProperty]`, `[RelayCommand]`, no boilerplate |
| Video engine | LibVLCSharp.WPF | Frame-accurate scrubbing of high-bitrate `.mov`; codec-agnostic |
| JSON | `System.Text.Json` with source generation | Zero-reflection perf; required for large timestamp files |
| LLM API | Google Generative AI REST (`HttpClient`) | Direct REST avoids unstable SDK churn |
| Tests | xUnit + FluentAssertions | Standard .NET unit test stack |

---

## Solution Structure

```
MeetingReview.sln
├── MeetingReview/                        # WPF app
│   ├── Models/
│   │   ├── WordTimestamp.cs
│   │   ├── TranscriptSegment.cs
│   │   └── TopicSummary.cs
│   ├── Services/
│   │   ├── ITranscriptParserService.cs
│   │   ├── TranscriptParserService.cs    # binary-search index lives here
│   │   ├── IGeminiService.cs
│   │   └── GeminiService.cs
│   ├── ViewModels/
│   │   ├── MainViewModel.cs              # centralized NavigateToTimeCommand
│   │   ├── VideoPlayerViewModel.cs
│   │   ├── TranscriptViewModel.cs
│   │   ├── ParagraphViewModel.cs         # wraps TranscriptSegment
│   │   ├── WordViewModel.cs              # wraps WordTimestamp; exposes Text only (no timestamps)
│   │   ├── SummaryViewModel.cs
│   │   └── SettingsViewModel.cs          # API key storage/retrieval
│   ├── Views/
│   │   ├── MainWindow.xaml / .xaml.cs    # InitializeComponent() only; hosts TabControl
│   │   ├── VideoPlayerView.xaml / .xaml.cs
│   │   ├── TranscriptView.xaml / .xaml.cs
│   │   ├── SummaryView.xaml / .xaml.cs
│   │   └── SettingsView.xaml / .xaml.cs  # API key config page
│   └── App.xaml / App.xaml.cs
└── MeetingReview.Tests/                  # xUnit project
    ├── Services/
    │   ├── TranscriptParserServiceTests.cs
    │   └── BinarySearchBenchmarkTests.cs
    └── ViewModels/
        └── MainViewModelTests.cs
```

---

## Phase 1 — Environment & Data Contracts

**Goal:** Compile-clean skeleton; no UI yet.

### 1.1 Project initialization
- Create solution: `dotnet new sln -n MeetingReview`
- WPF app: `dotnet new wpf -n MeetingReview --framework net8.0-windows`
- Test project: `dotnet new xunit -n MeetingReview.Tests`
- NuGet packages for `MeetingReview`:
  - `CommunityToolkit.Mvvm`
  - `LibVLCSharp.WPF`
  - `VideoLAN.LibVLC.Windows` (native VLC runtime)
- NuGet packages for `MeetingReview.Tests`:
  - `FluentAssertions`
  - `NSubstitute` (mocking)

**Compilation gate:** `dotnet build` must succeed before Phase 2.

### 1.2 Data models

```csharp
// Models/WordTimestamp.cs
record WordTimestamp(string Word, long StartMs, long EndMs);

// Models/TranscriptSegment.cs
record TranscriptSegment(long StartMs, long EndMs, string Text, IReadOnlyList<WordTimestamp> Words);

// Models/TopicSummary.cs  — ObservableObject for IsExpanded binding
partial class TopicSummary : ObservableObject
{
    public string Title { get; init; }
    public string DetailedContent { get; init; }
    public long StartMs { get; init; }
    public long EndMs { get; init; }
    [ObservableProperty] bool _isExpanded;
}
```

### 1.3 JSON source-generation context

```csharp
[JsonSerializable(typeof(List<WordTimestamp>))]
[JsonSerializable(typeof(List<TranscriptSegment>))]
[JsonSerializable(typeof(List<TopicSummary>))]
internal partial class AppJsonContext : JsonSerializerContext { }
```

---

## Phase 2 — Services Layer (each service compiled & tested independently)

### 2.1 `TranscriptParserService`

Responsibility: parse the timestamp JSON, build a sorted `List<TranscriptSegment>`, and expose a `FindSegmentAt(long positionMs)` binary-search lookup.

```csharp
public interface ITranscriptParserService
{
    Task<IReadOnlyList<TranscriptSegment>> ParseAsync(string jsonPath, CancellationToken ct = default);
    int FindSegmentIndex(IReadOnlyList<TranscriptSegment> segments, long positionMs); // O(log n)
}
```

Implementation notes:
- Use `JsonSerializer.DeserializeAsync` with `AppJsonContext` for streaming parse (no full-string load).
- After parsing, sort by `StartMs` (should already be sorted, but validate).
- `FindSegmentIndex` uses `List.BinarySearch` with a custom `IComparer<TranscriptSegment>`.

**Unit tests (`BinarySearchBenchmarkTests.cs`):**
- Generate synthetic `List<TranscriptSegment>` of **100,000 entries** (each 600ms apart).
- Assert lookup completes in under **1 ms** (Stopwatch-timed) for 10,000 random queries — proving O(log n) in practice.
- Edge cases: `positionMs` before first segment, after last segment, exactly on a boundary.

**Compilation gate:** `dotnet test --filter "Category=Services"` green before Phase 3.

### 2.2 `GeminiService`

Responsibility: send transcript text + user prompt to Gemini API; return `List<TopicSummary>`.

```csharp
public interface IGeminiService
{
    Task<List<TopicSummary>> GenerateSummaryAsync(
        string transcriptText,
        string userPrompt,
        string apiKey,
        CancellationToken ct = default);
}
```

Prompt engineering contract (enforced in service):
- System instruction tells Gemini to return a **JSON array only**, matching the `TopicSummary` schema.
- Each topic must include `startMs`/`endMs` extracted from the source transcript timestamps injected into the prompt context.
- Response is parsed via `AppJsonContext` — if Gemini wraps JSON in markdown fences, strip them before parse.

**Compilation gate:** Service file compiles in isolation (interfaces are dependency-injected; no concrete `MainViewModel` import).

---

## Phase 3 — ViewModels (each compiled & tested independently)

All ViewModels inherit `ObservableObject` (CommunityToolkit). No `using` references to View types.

### 3.1 `VideoPlayerViewModel`

- `[ObservableProperty] LibVLC _libVlc` / `MediaPlayer _mediaPlayer`
- `[RelayCommand] void Play()`, `void Pause()`
- `[RelayCommand] void Seek(long positionMs)`
- `TimeChangedEvent` — fires every ~100ms via VLC callback; raises `CurrentPositionMs` observable property.
- `LoadMedia(string filePath)` async method.

### 3.2 `TranscriptViewModel`

- `[ObservableProperty] ObservableCollection<ParagraphViewModel> Paragraphs` — each paragraph wraps a `TranscriptSegment` and exposes a flat `ObservableCollection<WordViewModel>` of its words.
- `[ObservableProperty] WordViewModel ActiveWord` — the currently playing word; drives highlight styling via `DataTrigger`.
- `UpdateActiveWord(long positionMs)` — binary-search to segment, then linear scan within the segment's words to find the active `WordViewModel`.
- `[RelayCommand] void SelectWord(WordViewModel word)` — fires `MainViewModel.NavigateToTimeCommand` with `word.StartMs`; no timestamp exposed in UI.
- Timestamps (`StartMs`/`EndMs`) are internal only — `WordViewModel` exposes only `Text` and the relay command to the View.

### 3.3 `SummaryViewModel`

- `[ObservableProperty] ObservableCollection<TopicSummary> Topics`
- `[ObservableProperty] bool IsLoading`
- `[RelayCommand] async Task GenerateSummaryAsync(string userPrompt)`
- `HighlightTopicAt(long positionMs)` — linear scan over `Topics` (small N ≤ ~50); expands matching topic.

### 3.4 `MainViewModel` (central state machine)

```csharp
// Single entry point for all navigation
[RelayCommand]
void NavigateToTime(long targetMs)
{
    _suppressAutoSync = true;          // race-condition guard
    VideoPlayer.Seek(targetMs);
    Transcript.UpdateActiveSegment(targetMs);
    Summary.HighlightTopicAt(targetMs);
    _suppressAutoSync = false;
}
```

- Owns `VideoPlayerViewModel`, `TranscriptViewModel`, `SummaryViewModel` as properties.
- Subscribes to `VideoPlayerViewModel.TimeChanged` (throttled 100ms via `DispatcherTimer`) and calls `Transcript.UpdateActiveWord` **only when `!_suppressAutoSync`**.
- File loading commands: `LoadVideoCommand`, `LoadTranscriptCommand`, `LoadJsonCommand` — each uses `ITranscriptParserService`.

**Unit tests (`MainViewModelTests.cs`):**
- Mock `ITranscriptParserService` and `IGeminiService` via NSubstitute.
- Verify `NavigateToTime` updates all three child VM properties.
- Verify `_suppressAutoSync` prevents re-entrant navigation loop.

**Compilation gate:** `dotnet test --filter "Category=ViewModels"` green before Phase 4.

---

## Phase 4 — Views (XAML-only; zero logic in code-behind)

**Rule enforced:** Each `.xaml.cs` contains only:
```csharp
public partial class FooView : UserControl
{
    public FooView() => InitializeComponent();
}
```

All behavior is expressed via `{Binding}`, `DataTrigger`, `Converter`, `Command`, and `Behavior` (from `Microsoft.Xaml.Behaviors.Wpf`).

### 4.1 `MainWindow.xaml`
- Top-level `TabControl` with two tabs: **Main** and **Settings**.
- **Main tab:** Three-column `Grid`: `VideoPlayerView` | `TranscriptView` | `SummaryView`, separated by `GridSplitter` dividers.
- `DataContext` bound to `MainViewModel` (set in `App.xaml.cs` via DI).
- File open buttons in a top toolbar inside the Main tab; bound to `MainViewModel` load commands.
- User prompt `TextBox` + "Generate Summary" button in the Summary column header area.
- **Settings tab:** Contains `SettingsView` with API key field and other preferences.

### 4.2 `SettingsView.xaml`
- `TextBox` for Gemini API key (masked with `PasswordBox` or toggleable visibility).
- API key stored/retrieved via `SettingsViewModel` — persisted to `%AppData%\MeetingReview\settings.json`, never exposed in the main UI.

### 4.3 `VideoPlayerView.xaml`
- `vlc:VideoView` bound to `VideoPlayerViewModel.MediaPlayer`.
- Play/Pause/Seek slider all bound via commands and `IValueConverter` for `TimeSpan ↔ long`.
- No raw millisecond values displayed; time shown as `HH:mm:ss` format via converter.

### 4.4 `TranscriptView.xaml`
- Displays the transcript as **flowing paragraph text**, not a per-word list.
- Rendered as a `RichTextBox` (read-only) or `ItemsControl` of paragraph `TextBlock`s with inline `Run` elements — one `Run` per word.
- Each `Run` is bound to a `WordViewModel` wrapping `WordTimestamp`; clicking a `Run` fires `MainViewModel.NavigateToTimeCommand` with the word's `StartMs`.
- **No timestamp values are displayed** anywhere in the transcript panel.
- As the video plays, the `Run` corresponding to the current word is highlighted (bold or background color) via `ActiveWordIndex` observable property on `TranscriptViewModel`.
- The view auto-scrolls to keep the highlighted word visible via a `ScrollToActiveWordBehavior` attached behavior.
- Paragraph boundaries are derived from `TranscriptSegment` groupings.

### 4.5 `SummaryView.xaml`
- `ItemsControl` binding to `SummaryViewModel.Topics`.
- Each item is an `Expander` with `IsExpanded` two-way bound to `TopicSummary.IsExpanded`.
- Clicking the `Expander` header fires `MainViewModel.NavigateToTimeCommand` via `InvokeCommandAction` behavior.
- Loading spinner overlay bound to `SummaryViewModel.IsLoading`.
- No timestamp values displayed in topic headers or detail text.

---

## Phase 5 — Integration & Hardening

### 5.1 Error handling
- If JSON duration exceeds `.mov` duration: clamp `positionMs` lookups and show a non-blocking `InfoBar` notification.
- Gemini API failures: surface error message in `SummaryViewModel.ErrorMessage` observable; bind to `TextBlock` in UI.
- File reload: dispose `MediaPlayer` and clear collections before reloading (prevent memory leak from VLC buffers).

### 5.2 Race-condition mitigation (already architected in 3.4)
- `_suppressAutoSync` bool flag in `MainViewModel`.
- Video time-change subscription throttled to 100ms via `DispatcherTimer` (not raw VLC callback on non-UI thread).

### 5.3 Dependency injection wiring (`App.xaml.cs`)
```csharp
services.AddSingleton<ITranscriptParserService, TranscriptParserService>();
services.AddSingleton<IGeminiService, GeminiService>();
services.AddTransient<MainViewModel>();
```

---

## Verification

| Step | Command / Action |
|---|---|
| Phase 1 gate | `dotnet build MeetingReview.sln` — zero errors |
| Phase 2 gate | `dotnet test --filter "Category=Services"` — all pass, benchmark assertions green |
| Phase 3 gate | `dotnet test --filter "Category=ViewModels"` — all pass |
| Full test suite | `dotnet test` |
| Manual smoke test | Load sample `.mov` + transcript + JSON; click a word → video seeks; click a topic → video seeks + word highlights |
| Gemini integration | Enter API key + prompt → summary loads, topics expand, clicking navigates video |

---

## Implementation Order (idempotent steps)

1. `dotnet new` scaffolding + NuGet restore → **build gate**
2. Models + `AppJsonContext` → **build gate**
3. `TranscriptParserService` + tests → **test gate**
4. `GeminiService` (no UI dependency) → **build gate**
5. `VideoPlayerViewModel` → **build gate**
6. `TranscriptViewModel` → **build gate**
7. `SummaryViewModel` → **build gate**
8. `MainViewModel` + navigation tests → **test gate**
9. Views (XAML only) → **build gate**
10. DI wiring + full integration → **full test gate + manual smoke test**
