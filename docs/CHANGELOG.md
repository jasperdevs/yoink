# OddSnap v0.8.30

## Changed
- restore bundled SVG icon rendering for stable Windows 10/11 toolbar icons.
- lower the desktop app target to Windows 10 19041.
- pin WinUI package versions used by the experimental shell.

## Fixed
- fix broken icon-font glyphs showing as wrong shapes on some systems.
- fix DXGI region capture to copy only the selected monitor overlap.
- speed up GIF recording by streaming raw frames to FFmpeg.
- reduce selection, toolbar, magnifier, color picker, and history redraw work.
- avoid temp files for PNG saves and DeepAI upscale uploads.
- fix OCR and color history search cache churn.
