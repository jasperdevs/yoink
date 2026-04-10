# Yoink v0.8.4.1

## Highlights
- Redesigned the capture dock with support for top, bottom, left, and right placement.
- Refreshed the capture, toast, and settings surfaces with a full Streamline-based icon set.
- Added a drag-and-drop toast button layout editor with configurable close, pin, save, and delete placement.
- Expanded local history and image search with better storage, migration, diagnostics, and runtime preparation.

## Added
- Streamline icon rendering helpers for both WinForms and WPF UI surfaces.
- New embedded icon assets for capture tools, toast actions, and settings UI.
- Persistent background runtime job tracking for long-running setup tasks.
- Bundled local semantic-search runtime assets and setup flow for CLIP-based search.
- SQLite-backed history storage for screenshot, OCR, and color history.
- Settings infrastructure for media caching plus reorganized settings/history/media code paths.

## Changed
- The capture dock now supports configurable placement on all four screen edges.
- Annotation interactions were refined with better text editing behavior, larger handles, and cleaner repainting while dragging or resizing.
- Toast previews now support richer layout options, auto-pin behavior, and configurable overlay buttons.
- Capture preferences now include dock placement and all-monitor overlay behavior.
- Image-search preferences now expose visibility, source selection, exact-match mode, and diagnostics.
- Startup, shutdown, capture, upload, and settings code were reorganized into more focused partial files and folders.

## Improved
- Multi-monitor dock anchoring behaves more reliably across mixed monitor layouts, including negative-coordinate displays.
- Overlay hit targets and picker layout were tightened up for easier interaction.
- Settings persistence now buffers disk writes, keeps a process cache, and applies migrations/defaults more safely.
- Legacy history data can be migrated into the new storage layout automatically.
- Semantic-search preparation is surfaced more clearly through runtime status and settings workflows.
- Upload settings migration now normalizes older AI chat and temporary-host configurations more safely.

## Fixed
- History no longer depends on the old JSON-only AppData layout; screenshots, OCR history, and color history migrate into the newer Pictures-based store.
- Temporary-host uploads now support `tmpfiles.org` as a first-class destination and normalize older host settings correctly.
- Hotkey registration is more reliable when only secondary hotkeys are enabled or when hotkeys are re-registered from changed settings.
- OCR cleanup now disposes WinRT imaging objects deterministically during recognition.
- Shared DXGI capture resources are guarded more safely during warm-up and capture.
- Video recording cleans up desktop-audio and mux-process resources more deterministically.
- Clipboard PNG copy avoids an unnecessary extra buffer clone in the common image-copy path.
- Installer payload selection now copies the full published app instead of an incomplete exe-only subset when installing from a published build.

## Internal
- Release packaging continues to produce per-architecture portable `.exe` and `.zip` artifacts for GitHub releases and winget.
- Tests cover toolbar placement, toast specs, history location, upload normalization, and settings migrations tied to this release.
