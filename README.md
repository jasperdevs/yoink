<p align="center">
  <img src="assets/banner.svg" alt="Yoink" width="100%"/>
</p>

<p align="center">
  <a href="https://github.com/jasperdevs/yoink/releases"><img src="https://img.shields.io/github/v/release/jasperdevs/yoink?style=flat-square&color=1962F4" alt="Release"/></a>
  <a href="https://github.com/jasperdevs/yoink/blob/main/LICENSE"><img src="https://img.shields.io/github/license/jasperdevs/yoink?style=flat-square" alt="License"/></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?style=flat-square" alt="Platform"/>
  <img src="https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square" alt=".NET 9"/>
</p>

---

Yoink is a free and open-source screenshot tool for Windows that lets you capture any region of your screen, annotate it with drawing tools, and copy it to your clipboard instantly. It runs in the system tray and works with a single hotkey.

## Features

**Capture**
- Region select with automatic window detection
- Freeform lasso selection
- Fullscreen capture
- OCR text extraction from any part of the screen
- Color picker with hex copy

**Annotate**
- Freehand draw
- Straight lines, arrows, and curved bezier arrows
- Text with font picker (15 fonts)
- Highlight marker
- Step numbers (auto-incrementing)
- Blur and pixelate regions
- Smart eraser
- Magnifier zoom
- Color emoji stamps with search
- Color palette
- Undo support for everything

**Workflow**
- Auto-copies to clipboard on capture
- Floating preview you can drag-and-drop into any app
- Saves to Pictures/Yoink
- Capture history with thumbnails
- OCR text and color history
- Customizable toolbar (show/hide any tool)
- Setup wizard on first launch
- System tray with customizable hotkeys
- Start with Windows

## Hotkeys

| Action | Default |
|--------|---------|
| Screenshot | `Alt + \`` |
| OCR Text Capture | `Alt + Shift + \`` |
| Color Picker | `Alt + C` |

All hotkeys can be changed in settings or during first-run setup.

## Get Yoink

Head to [**Releases**](https://github.com/jasperdevs/yoink/releases) and grab the latest version.

Requires [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) on Windows 10 or 11.

## Build from source

```
git clone https://github.com/jasperdevs/yoink.git
cd yoink
dotnet publish src/Yoink/Yoink.csproj -c Release -r win-x64 --self-contained false -o publish
```

## License

[MIT](LICENSE) - free and open-source.
