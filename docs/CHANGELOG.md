# Yoink v0.8.23

## Added
- add AI Redirect toast button defaults for Google Lens with the free/no-setup upload filter.
- add provider icons for AI redirects, upload hosts, sticker providers, and upscale providers.
- add uninstall actions for local rembg and upscale runtimes.

## Changed
- replace Lucide with embedded Fluent UI icon data across overlay, toast, tray, and settings surfaces.
- use the shared WinForms menu renderer for tray and More Tools menus.
- restore built-in WPF dropdown controls for app windows after custom dropdown regressions.
- move sticker and upscale runtime/model actions into the Local Models cards.
- remove file.io from automatic free/no-setup and Google Lens fallback uploads.

## Removed
- remove Lucide font and legacy embedded PNG icon resources.
- remove the older U2Net sticker model option from settings.
- remove gradient/shimmer styling from loading and result surfaces.

## Fixed
- fix text and annotation handle dragging, resizing, and repaint smearing in the capture overlay.
- fix More Tools hotkeys, cursor state, outside-click handling, and hover handoff behavior.
- fix recording and scrolling capture docks to use the shared dock styling.
- fix bare Space being accepted as a global hotkey.
- fix toast button slot conflicts hiding the close button.
- fix settings upload dropdown sizing, missing provider icons, and AI Redirect default selection.
- fix missing embedded Azure icon resource that prevented Settings from opening.
