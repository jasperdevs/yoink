# Yoink v0.8.4.0

## Changed
- Argos Translate is now installed only when requested from Settings -> OCR; the first-run installer no longer starts a Python/pip install.
- Release packaging now submits the generated winget manifests from the same workflow that publishes the GitHub release.
- Settings import now uses the same migration/defaulting path as normal app startup, so imported files pick up newer defaults instead of applying raw stale values.
- The updater now verifies the SHA-256 digest GitHub publishes for release assets before applying a downloaded update package.
- Upload failures now write diagnostic entries to the Yoink app log.

## Fixed
- Sticker saves and toast preview exports now use the same atomic write path as normal screenshot saves.
- Active-window capture now uses the same preferred window bounds logic as overlay window snapping, which reduces cropped shadows and frame mismatches.
- Tray icon rendering no longer leaks icon handles when switching between normal and recording states.
- Update downloads now clean up their temporary directory when a download fails instead of leaving partial packages behind.
- Self-update now closes lingering Yoink instances from the helper before copying the new build over the installed executable.
- The first-run installer now keeps Cancel available during file copy and local semantic-search preparation.
- Installer shutdown now attempts a graceful close before forcing a running Yoink process down.
- Settings write failures no longer silently clear the dirty state, and failed clipboard writes now leave a diagnostic log entry.
- Removed the unavailable public transfer.sh service from upload destination pickers and from rotating temporary-host uploads.
- Added tmpfiles.org to the rotating Temp Hosts upload mode.
