# Yoink v0.5.2

## Highlights
- Added a `Show cursor in captures` setting so screenshots, GIFs, videos, and scroll captures can include or hide the cursor consistently.
- Split the large settings window into smaller partial files so the UI code stays modular and easier to maintain.
- Added a `Show in folder` action to history items that have a real file location.
- Cleaned up the capture and overlay flow after removing the experimental window-elements detection path.
- Improved release packaging so GitHub releases include both direct `.exe` assets and portable `.zip` assets.

## Notes
- ZIP assets remain the format used for winget and portable installs.
- The direct EXE assets are included for users who want a simple download from the GitHub release page.
