# Yoink v0.7.0

## Added
- OCR result window — text captures now open a dedicated window instead of copying straight to clipboard
- Translation support with Argos Translate (offline) and Google Translate API
- OCR settings panel with language selection, translation defaults, and model management
- Tessdata auto-download — selecting an OCR language installs its pack on the fly
- Stroke and shadow effects on all annotation tools (arrows, lines, shapes, freehand)
- Hotkey hints in the tray menu, right-aligned and muted
- Searchable dropdowns for language selectors and upload destination
- About section with links to source, releases, issues, and license
- Smooth cursor blink in text annotation mode
- Installer completion animation with logo scale effect
- Setup wizard page transitions with fade animations

## Changed
- Tray icon tooltip now shows "Click to capture, right-click for menu"
- Settings reorganized — General split into Sounds, Notifications, Search, System sections
- Overlay settings (crosshair, magnifier, annotation stroke) moved to Capture tab
- Upload destination combo uses tag-based selection instead of index
- Version display cleaned up to 3-part format (v0.7.0 not v0.7.0.0)
- Registry entries updated with proper publisher, repo links, and full install size
- Crosshair guides now 3px with shadow edges instead of 1px lines
- Installer skips for dev builds running from Visual Studio

## Fixed
- Draw tool inverting fills (switched GraphicsPath to Winding fill mode)
- Text cursor misaligned with drawn text (GDI vs GDI+ measurement mismatch)
- Curved arrow tips disappearing (direction now uses last few points, not distant lookback)
- Dropdown search skipping first typed character
- Non-Latin text (CJK, Arabic, Cyrillic) garbled in translation output
- Hardcoded dark-mode colors breaking light mode in several UI elements
- Old Windows checkbox style overriding modern template
- Setup wizard shadow and button border not adapting to light mode
- Duplicate LooksLikeBuildOutputPath between install and uninstall services
