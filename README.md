# ZoneId Cleaner

**English** | [日本語](README.ja.md)

A Windows app that removes the Mark of the Web (the `Zone.Identifier` alternate data stream, or "ZoneId") that is automatically attached to files downloaded from the internet, in bulk.

---

## Features

- **Drag & drop**: Drop files or folders onto either of the following. You can drop multiple items at once.
  - The `ZoneId-Cleaner.exe` icon (the app starts and processes them right away)
  - The window of the running app (anywhere in the window is accepted)
- **Long path support**: Uses extended-length paths (`\\?\`), so paths of 260 characters or more and UNC paths such as `\\server\share\...` also work.
- **Unicode support**: Paths containing characters outside the Shift_JIS (cp932) range are handled correctly.
- **High DPI support**: The window follows the display scaling. Text and window size adapt when you move it between monitors with different scaling.
- **Multilingual**: The UI is shown in Japanese or English, following the Windows display language. Any other display language falls back to English.
- **Lightweight**: Built on .NET Framework; starts and runs fast.

---

## Usage

1. Get `ZoneId-Cleaner.exe` (see below). No installation is required.
2. Specify the files or folders (multiple allowed) whose ZoneId you want to remove, in any of these ways:
   - Drop them onto the `ZoneId-Cleaner.exe` icon
   - Start `ZoneId-Cleaner.exe` and drop them onto its window
   - Start `ZoneId-Cleaner.exe` and use the "Select files..." / "Select folders..." buttons
3. The results are shown in the list area as follows:
   - 🟩 `[Removed]` : The file had a ZoneId and it was removed successfully
   - 🟥 `[Failed]` : The ZoneId could not be removed (for example, insufficient permissions)
   - ⬜ `[Not needed]` : The file had no ZoneId to begin with

   While a large number of files is being processed, you can stop with the "Cancel" button.

---

## Requirements

- OS: Windows 10 (version 1803 or later) / Windows 11
- .NET Framework 4.7.2 or later (included with Windows 11)

### Notes

- You cannot drag & drop from a normal (non-elevated) Explorer onto an app running as administrator (a Windows restriction). Start the app without elevated privileges.
- The executable is not code-signed, so Windows SmartScreen may show a warning depending on your environment.

---

## Building

The build uses the C# compiler (`csc.exe`) that ships with Windows. No additional tools are needed.

```
build.bat
```

This produces `ZoneId-Cleaner.exe`. The build uses these files:

| File | Description |
| --- | --- |
| `ZoneId-Cleaner.cs` | Application source |
| `ZoneId-Cleaner.Strings.cs` | Per-language table of UI strings (Japanese and English) |
| `ZoneId-Cleaner.ico` | Application icon |
| `ZoneId-Cleaner.manifest` | High DPI declaration |
| `build.bat` | Build script |
| `make-icon.cs` | Source for regenerating the icon (`.ico`); not needed for a normal build |

### Adding a language

In `ZoneId-Cleaner.Strings.cs`, add a dictionary with the same keys as the English one (`En`), and add one `case` for the language in `Select()`. Any missing key is shown in English.

---

## License

[MIT License](LICENSE)
