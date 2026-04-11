# Reliability Maintenance Log

## Scope
- Conservative maintenance pass focused on safe, high-confidence reliability, performance, and crash-prevention improvements.
- No UI/UX/design changes. No new features. No broad refactors.

## Initial inspection
- Repository mapped and scoped before edits.
- Primary focus areas identified: app lifecycle, capture pipeline, recording, clipboard, hotkeys, OCR, history/storage, startup/shutdown, and native interop.
- Validation approach: smallest relevant targeted tests and build checks after each meaningful change.

## Ranked plan
- 1. Fix hotkey registration/message-pump reliability so non-capture-only hotkey setups still work and bad registrations fail safely.
- 2. Dispose WinRT OCR imaging objects deterministically in `OcrService` to prevent native-memory growth across repeated OCR runs.
- 3. Add synchronization around shared DXGI staging-texture cache access to prevent race conditions during overlapping captures.
- 4. Clean up `VideoRecorder` process/device lifetime leaks in desktop-audio and audio-mux paths.
- 5. Validate each step with the smallest relevant test/build command before continuing.

## Step 1
- What I changed: Centralized hotkey registration in `HotkeyService` so every hotkey type attaches the message hook, unregisters any prior ID before re-registering, and cleanly treats `key == 0` as disabled without relying on the capture hotkey path.
- Why it was a problem: Non-capture-only hotkey configurations could register Windows hotkeys without ever wiring the WPF message pump, which left shortcuts inert. Re-registration was also less defensive if a hotkey was changed while the service instance remained alive.
- Files changed: `src/Yoink/Services/HotkeyService.cs`
- How I validated it: `lsp_diagnostics` reported zero errors for `HotkeyService.cs`. Standard `dotnet build`/`dotnet test` validation is currently blocked by existing workspace build artifacts and a running `Yoink.exe` locking the default debug output.
- Whether any risk remains: Low. This changes only registration plumbing, but end-to-end hotkey behavior still needs an unlocked app build or manual runtime check.

## Step 2
- What I changed: Added deterministic disposal for the WinRT random-access stream and `SoftwareBitmap` used during OCR conversion.
- Why it was a problem: The OCR path creates native-backed imaging objects on every recognition call. Leaving them for deferred cleanup risks native-memory growth during repeated OCR captures.
- Files changed: `src/Yoink/Services/OcrService.cs`
- How I validated it: `lsp_diagnostics` reported zero errors for `OcrService.cs`.
- Whether any risk remains: Very low. The change only tightens resource cleanup around existing OCR calls.

## Step 3
- What I changed: Serialized access to the cached DXGI device/context bundle during capture and warm-up.
- Why it was a problem: The cached D3D device context and staging textures were shared process-wide. Overlapping capture/warm-up work could race on native resources and crash or corrupt capture state.
- Files changed: `src/Yoink/Capture/DxgiScreenCapture.cs`
- How I validated it: `lsp_diagnostics` reported zero errors for `DxgiScreenCapture.cs`.
- Whether any risk remains: Low. The tradeoff is slightly less parallelism during DXGI use, but that is preferable to unsynchronized native access.

## Step 4
- What I changed: Disposed the temporary desktop-audio device enumerator immediately after use and wrapped the audio mux process in deterministic disposal.
- Why it was a problem: Repeated recordings could leak native/process handles in the desktop-audio and FFmpeg mux paths.
- Files changed: `src/Yoink/Capture/VideoRecorder.cs`
- How I validated it: `lsp_diagnostics` reported zero errors for `VideoRecorder.cs`.
- Whether any risk remains: Low. Recording behavior is unchanged; the fix is limited to process/device lifetime cleanup.

## Step 5
- What I changed: Reused the encoded PNG clipboard buffer when possible instead of always cloning it with `ToArray()`.
- Why it was a problem: Every image copy paid for an avoidable extra allocation and buffer copy before reaching the clipboard data object.
- Files changed: `src/Yoink/Services/ClipboardService.cs`
- How I validated it: `lsp_diagnostics` reported zero errors for `ClipboardService.cs`.
- Whether any risk remains: Very low. There is a fallback to the previous copied-buffer path if the stream buffer cannot be exposed.
