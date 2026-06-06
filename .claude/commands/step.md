# /step — Implement the next plan phase, build, and hand off

You are implementing the AudioPen Android project one phase at a time. Each invocation of /step does exactly one phase, verifies it compiles and works, writes a handoff document, then stops and waits for the user to approve before the next phase begins.

## Workflow

### 1. Determine which phase is next

- Read `PLAN.md` in the project root to get the full implementation order.
- Read `HANDOFF.md` in the project root (if it exists) to see which phases are already complete.
- The next phase is the first one in PLAN.md's "Implementation Order" section that is NOT marked complete in HANDOFF.md.
- If all phases are complete, report that the project is finished and stop.

### 2. Implement the phase

- Follow the plan exactly. Do not implement anything from future phases.
- Reuse existing patterns from the Diary app at `C:\Users\Xiaozhong Chen\AndroidStudioProjects\Diary` whenever the plan calls for it (preferences, DI, Room, SettingsScreen patterns, etc.).
- Write clean, minimal Kotlin code — no speculative abstractions, no comments explaining what the code does.

### 3. Build and verify

Run the debug build from the project root:

```
./gradlew :app:assembleDebug
```

- If the build fails, fix all errors and warnings before proceeding. Re-run until it is clean.
- If the phase involves UI (RecordScreen, PlaybackScreen, etc.), note what manual testing steps the user should perform on a device/emulator (you cannot run the app yourself).

### 4. Write the handoff document

Update `HANDOFF.md` in the project root with this structure:

```markdown
# AudioPen — Handoff Document

## Completed Phases
- [x] Phase 1: <title> — <one-line summary of what was built>
- [x] Phase 2: <title> — <one-line summary>
- [ ] Phase 3: <title> — (next)
...

## Last Phase Summary
**Phase N: <title>**

What was built:
- <bullet list of files created/modified>

How to verify (manual steps on device/emulator):
- <step-by-step instructions>

Known limitations / deferred items:
- <anything intentionally left out or noted as a gotcha>

## Next Phase Preview
**Phase N+1: <title>**
<2–3 sentence description of what will be implemented next>
```

### 5. Stop and request approval

After writing HANDOFF.md, output a short summary to the user:

- Which phase was just completed
- The build status (pass/fail, any notable warnings)
- The manual verification steps they should run
- A one-line preview of the next phase

Then **stop**. Do not begin the next phase until the user explicitly says to continue (e.g., "continue", "next step", "/step", or similar approval).
