# Application Compatibility

> Expected behavior for the clipboard-based selection pipeline

## Compatibility requirements

IntentInk uses global mouse/keyboard hooks and standard clipboard shortcuts: `Ctrl+C` for capture and `Ctrl+V` for replacement. It does not require application-specific editor adapters.

A target application is compatible when it:

- supports standard text selection and clipboard shortcuts;
- is running at the same or lower Windows integrity level;
- keeps the selection active while the non-activating IntentInk overlay is open.

## Applications requiring smoke testing

| Application | Field/control type | Expected behavior | Current status |
|---|---|---|---|
| Notepad++ | Scintilla editor | Standard copy and paste | Re-test required |
| Microsoft Teams | Electron input fields | Standard copy and paste | Re-test required |
| Windows Notepad | Standard/RichEdit text | Standard copy and paste | Re-test required |
| Microsoft Word | Document body and tables | Standard copy and paste | Re-test required |
| Chrome / Edge | Inputs, textareas, contenteditable | Standard copy and paste | Re-test required |
| Slack / Discord | Electron input fields | Standard copy and paste | Re-test required |
| VS Code / Visual Studio | Editor controls | Standard copy and paste | Re-test required |

## Known limits and privacy

- Elevated, protected, remote, or custom controls may reject simulated shortcuts.
- Most standard password fields reject copying, but IntentInk cannot identify every custom password control. Exclude sensitive applications explicitly.
- Excluded process names prevent the selection overlay from appearing in configured applications.
- Clipboard formats are preserved in memory and restored after capture or replacement.
- Selected and corrected text is not written to diagnostic logs.
