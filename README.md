# Yoink

A fast, lightweight screenshot tool for Windows. Capture, annotate, and share in seconds.

## Features

**Capture**
- Region select with window auto-detection
- Freeform selection
- Fullscreen capture
- OCR text extraction (Alt+Shift+`)
- Color picker (Alt+C)

**Annotate**
- Draw (freehand)
- Straight lines
- Arrows (straight and curved)
- Text with font picker (Ctrl+F to change font)
- Highlight marker
- Step numbers (auto-incrementing)
- Blur/pixelate regions
- Smart eraser
- Magnifier (click to place zoomed views)
- Emoji stamps (real color emoji, searchable)
- Color palette

**Workflow**
- Copies to clipboard automatically
- Floating preview with drag-to-drop into any app
- Save to file (Pictures/Yoink)
- Capture history with thumbnails
- OCR and color history
- Capture sound effect

## Hotkeys

| Action | Default |
|--------|---------|
| Screenshot | `Alt + \`` |
| OCR Text Capture | `Alt + Shift + \`` |
| Color Picker | `Alt + C` |

All hotkeys are customizable in settings.

## Install

Download `Yoink.zip` from [Releases](https://github.com/jasperdevs/yoink/releases), extract, and run `Yoink.exe`. The app lives in your system tray.

Requires .NET 9 Desktop Runtime on Windows 10/11.

## Build

```
dotnet publish src/Yoink/Yoink.csproj -c Release -r win-x64 --self-contained false -o publish
```

## License

MIT
