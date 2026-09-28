# Configuration & Logging Reference

> **Settings Schema, Unified Diagnostics Logging, and Windows Startup**

---

## Settings File Location

Application settings are persisted as JSON in the user's local application data directory:

```
%LOCALAPPDATA%\IntentInk\settings.json
```
*(e.g., `C:\Users\<username>\AppData\Local\IntentInk\settings.json`)*

---

## Settings Schema

```json
{
  "baseUrl": "http://localhost:11434/",
  "modelName": "llama3.2:3b",
  "textLimit": 1500,
  "runAtSignIn": false,
  "isPaused": false,
  "excludedProcessNames": [
    "keepass",
    "1password"
  ]
}
```

### Property Reference

| Property | Type | Default | Description |
|---|---|---|---|
| `baseUrl` | `string` | `"http://localhost:11434/"` | The local loopback URL where the Ollama server is listening. |
| `modelName` | `string` | `""` | The name of the installed model to invoke (e.g. `llama3.2:3b`, `qwen2.5:3b`). |
| `textLimit` | `integer` | `1500` | Maximum character length of text selection to send to Ollama. |
| `runAtSignIn` | `boolean` | `false` | When enabled, IntentInk launches automatically in the background at Windows sign-in. |
| `isPaused` | `boolean` | `false` | When `true`, IntentInk suspends all selection hooks and floating button overlays. |
| `excludedProcessNames` | `string[]` | `[]` | List of process executable names (without `.exe`) where IntentInk will not display floating buttons. |

---

## Unified Diagnostics Logging

All runtime diagnostic events, hook installations, model discoveries, inference timings, and replacement results are written to a single consolidated log:

```
<ProjectRoot>\logs\diagnostics.log
```
*(or `%LOCALAPPDATA%\IntentInk\diagnostics.log` when installed)*

### Log Format
```
[HH:mm:ss.fff] [LEVEL] [Category] Message (elapsed_ms)
```

Example Log Entries:
```
[01:21:08.117] [OK] [AI] Correction generated (322 characters). (29257ms)
[01:21:32.285] [INFO] [UI] User clicked 'Replace' button. Executing replacement...
[01:21:32.454] [OK] [Replace] Replaced selection via Ctrl+V (322 chars).
```

### Live Log Viewer Window
The user can open the real-time log viewer anytime by right-clicking the system tray icon and selecting **View Logs**.

---

## Run at Sign-in Registry Integration

When **Start IntentInk automatically when I sign in to Windows** is enabled, IntentInk writes to:

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

- **Name**: `IntentInk`
- **Value**: `"C:\Path\To\IntentInk.exe" --minimized`

Because this targets `HKEY_CURRENT_USER`, it does not require administrator privileges or UAC elevation.
