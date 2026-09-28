# IntentInk

IntentInk is a Windows desktop assistant that listens for text selection, shows a lightweight grammar suggestion, and applies a local AI correction without sending data to a cloud service.

It is designed for privacy-first writing help in apps like Notepad++, Word, Teams, Slack, browsers, and other Windows desktop editors.

## Why this exists

Most grammar tools either:
- require a cloud API,
- break when the user is working inside third-party apps,
- or steal focus while editing.

IntentInk solves that by using a selection-driven Windows pipeline:
- detect the selected text in the foreground app,
- show a floating non-activating suggestion button,
- ask a local Ollama model to correct the text,
- and replace the highlighted text in place with safe clipboard-based Ctrl+V input.

---

## High-level flow

```mermaid
flowchart LR
    A[User selects text in any app] --> B[GlobalSelectionHook detects selection]
    B --> C[OverlayPresenter shows floating fix button]
    C --> D[CorrectionCoordinator captures text]
    D --> E[OllamaClient calls local model]
    E --> F[CorrectionValidator checks result]
    F --> G[Result card shows diff / corrected text]
    G --> H[User clicks Replace]
    H --> I[InputHelper pastes corrected text back in-place]
    I --> J[Clipboard is restored to original state]
```

---

## Background architecture

The app runs mostly in the system tray and stays quiet until the user selects text.

Startup sequence:
1. The app creates a single-instance mutex so only one IntentInk process runs.
2. It loads the user settings from local app data.
3. It creates the tray icon and background services.
4. It installs low-level Win32 mouse and keyboard hooks.
5. It starts the selection pipeline and keeps the app alive in tray mode.

This means the software is acting in the background without blocking the user’s normal typing or requiring focus stealing UI.

---

## Core components

### App lifecycle
The entry point in [src/IntentInk.Desktop/App.xaml.cs](src/IntentInk.Desktop/App.xaml.cs) sets up the tray app, settings store, single-instance guard, and the correction pipeline.

### Global selection detection
The hook in [src/IntentInk.Desktop/Interop/GlobalSelectionHook.cs](src/IntentInk.Desktop/Interop/GlobalSelectionHook.cs) listens to low-level Windows mouse and keyboard events. It detects:
- mouse drag selection,
- double-click / triple-click word selection,
- Shift+Arrow selection,
- Ctrl+A selection,
- and clears the suggestion when the user keeps typing normally.

### Floating overlay
The overlay UI is a lightweight WPF window that is intentionally non-activating. It allows the user to click the fix action without stealing keyboard focus from the target app.

### Correction coordinator
The coordinator in [src/IntentInk.Desktop/Services/CorrectionCoordinator.cs](src/IntentInk.Desktop/Services/CorrectionCoordinator.cs) is the central background controller. It:
- receives selection events,
- shows the floating pill,
- captures selected text,
- calls the grammar client,
- and handles safe replacement with the active selection.

### Local AI layer
The local grammar client in [src/IntentInk.Desktop/Infrastructure/OllamaClient.cs](src/IntentInk.Desktop/Infrastructure/OllamaClient.cs) contacts the user’s local Ollama server at http://localhost:11434 and uses a strict JSON output contract.

### Validation layer
The validator in [src/IntentInk.Core/Services/CorrectionValidator.cs](src/IntentInk.Core/Services/CorrectionValidator.cs) rejects unsafe or noisy rewrites by checking:
- empty output,
- unchanged text,
- oversize edits,
- and protected tokens such as URLs, emails, and version numbers.

### Safe replacement
The clipboard-based replace logic in [src/IntentInk.Desktop/Interop/InputHelper.cs](src/IntentInk.Desktop/Interop/InputHelper.cs) snapshots the current clipboard, copies or pastes the corrected text with synthetic Ctrl+C/Ctrl+V events, and restores the previous clipboard state afterward.

---

## How the correction pipeline works in practice

1. The user selects text in a third-party app.
2. The low-level hook detects the selection and emits a selection event.
3. The overlay shows a small floating pill near the selection.
4. The user clicks the fix action.
5. IntentInk brings the target application to the foreground, copies the selected text using a safe clipboard snapshot, and reads it back.
6. The original text is sent to the local Ollama model with a structured grammar prompt.
7. The model returns a corrected version in JSON form.
8. The validator checks whether the change is safe and meaningful.
9. The result is displayed in the UI for review.
10. When the user confirms, the app injects the corrected text back into the current selection using Ctrl+V.
11. The original clipboard data is restored.

This preserves the user’s typing flow while keeping the correction process local and disconnected from external services.

---

## Quick start

### Prerequisites
- Windows 10 or 11
- .NET 9 SDK
- Ollama installed locally

### Install a model
```powershell
ollama pull llama3.2:3b
```

### Build and run
```powershell
dotnet build
dotnet run --project src/IntentInk.Desktop
```

### Run minimized to tray
```powershell
dotnet run --project src/IntentInk.Desktop -- --minimized
```

---

## Documentation

The detailed engineering docs are in the [docs](docs/) folder:

- [docs/architecture.md](docs/architecture.md) — architecture overview and selection pipeline
- [docs/replacement-pipeline.md](docs/replacement-pipeline.md) — capture and replacement details
- [docs/model-setup.md](docs/model-setup.md) — model setup and prompt contract
- [docs/configuration.md](docs/configuration.md) — settings and logging

---

## License

MIT License.

This project is built to be straightforward to run locally, easy to inspect, and reliable for privacy-first grammar assistance on Windows.
