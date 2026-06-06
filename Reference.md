

## 1. Technology Stack & Architectural Strategy

To handle heavy video playback alongside dense JSON parsing efficiently on Windows, the following stack is selected:

* **Framework:** **WPF (.NET 8/9)** or **WinUI 3**. *Recommendation: WPF with CommunityToolkit.Mvvm.* It offers mature, high-performance UI thread synchronization and robust data-binding required for tri-directional navigation.
* **Video Engine:** **LibVLCSharp (VLC core)** or **MediaElement** (with standard Windows codecs installed). *Recommendation: LibVLCSharp* for native, high-precision frame-accurate scrubbing of high-bitrate `.mov` files.
* **Concurrency Model:** `async/await` heavily leveraged to ensure large timestamp JSON files ($>5\text{MB}$) do not block the UI thread during parsing or lookups.

---

## 2. Phase-by-Phase Development Plan

### Phase 1: Environment & Core Data Contracts

**Objective:** Define the boundaries and data models to prevent architectural drift during automated code generation.

* **Task 1.1:** Initialize the .NET desktop project. Structure the solution using strict MVVM separation (`Models/`, `ViewModels/`, `Views/`, `Services/`).
* **Task 1.2:** Define the Data Models:
* `WordTimestamp`: `{ string Word, long StartMs, long EndMs }`
* `TranscriptSegment`: `{ long StartMs, long EndMs, string Text, List<WordTimestamp> Words }`
* `TopicSummary`: `{ string Title, string DetailedContent, long StartMs, long EndMs, bool IsExpanded }`


* **Task 1.3:** Implement the JSON Parser Service using `System.Text.Json` (Source Generated for performance) to ingest the timestamp file and construct a fast lookup index (e.g., an interval tree or flat sorted list optimized for binary search).

### Phase 2: Video Engine & Transcript View (The Baseline UI)

**Objective:** Establish the foundation for media playback and text synchronization.

* **Task 2.1:** Integrate the video player component into the main view. Implement core transport controls (Play, Pause, Seek).
* **Task 2.2:** Build the Transcript View using a virtualized list control (e.g., `ListView` or `ItemsControl` wrapped in a `VirtualizingStackPanel`) to maintain 60 FPS scrolling even with hours of text.
* **Task 2.3:** Implement **Forward Synchronization (Video $\rightarrow$ Transcript)**:
* Subscribe to the video player's time-changed event (throttled to ~100ms intervals).
* Perform a binary search on the `TranscriptSegment` collection to find the active text block.
* Highlight the active word/sentence and auto-scroll the view using visual state triggers.



### Phase 3: Gemini API Integration & Structured Summary Generation

**Objective:** Fetch, process, and map the hierarchical summary from the Gemini API.

* **Task 3.1:** Create a `GeminiServiceClient` utilizing the official Google Gen AI SDK or direct REST endpoints using `HttpClient`.
* **Task 3.2:** Design the LLM Prompt strategy. The prompt must instruct Gemini to return a strictly formatted JSON array matching the `TopicSummary` schema, ensuring it injects the exact start and end timestamps extracted from the source data context.
* **Task 3.3:** Build the UI component for the summary: A hierarchical tree or an accordion-style `Expander` list mapped to the `TopicSummary` model.

### Phase 4: Bi-Directional Navigation Framework

**Objective:** Bind all three components (Video, Transcript, Summary) into a unified, reactive state machine.

* **Task 4.1:** Centralize state management inside the `MainViewModel`. Introduce a unified navigation command: `MapsToTimeCommand(long targetMs, object originSource)`.
* **Task 4.2:** Implement the Navigation Logic:
* **From Summary:** Clicking a topic or detail segment fires the command $\rightarrow$ updates Video Position $\rightarrow$ triggers Phase 2's Transcript auto-scroll.
* **From Transcript:** Clicking any word/sentence fires the command $\rightarrow$ updates Video Position $\rightarrow$ identifies the corresponding Topic Summary item and expands it.


* **Task 4.3:** Implement race-condition mitigation. When a user explicitly clicks to navigate, temporarily mute the automated video time-tracking event listener to prevent UI ping-ponging loops.

### Phase 5: Error Handling & Refinement

**Objective:** Solidify application stability.

* **Task 5.1:** Implement guardrails for missing or mismatched timestamp data (e.g., if the JSON duration exceeds the `.mov` actual duration).
* **Task 5.2:** Optimize memory footprints by ensuring video buffers are disposed of cleanly on file reload.
