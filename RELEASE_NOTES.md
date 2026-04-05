# Yoink v0.8.0

## Added
- Onboarding wizard now includes capture saving settings (format, max size, save directory)
- About section with source, releases, issues, and license links
- Tray menu hotkey hints right-aligned in muted color
- Smoother drawing with point simplification for curved arrows and freehand
- Anti-aliased text on all overlay elements globally
- Perpendicular tick marks on ruler endpoints

## Changed
- Onboarding streamlined to 3 pages: Hotkeys, Capture & Saving, Done
- Removed recording and upload config from onboarding (use Settings)
- No overlap between install wizard and setup wizard options
- Constant line thickness for arrows (3.5px) and shapes (3px) instead of scaling with length
- Toolbar active/hover circles made more subtle
- All overlay borders slightly thicker (1.4px) for smoother edges on layered windows
- Version display uses 3-part format (v0.8.0 not v0.8.0.0)
- Registry entries include repo URL, help link, and full install size

## Fixed
- Curved arrow tip direction using stable tangent from further back along the curve
- Curved arrow line no longer pokes through the arrowhead
- Draw tool fills no longer invert (Winding fill mode)
- Dropdown search no longer skips the first typed character
- Step number badges auto-contrast text color based on badge brightness
- Installer no longer shows for dev builds running from Visual Studio

## Improved
- Split large source files into partial class segments (24 new files)
- Cached GDI objects for annotation shadow/stroke rendering
- Reusable point buffer for curved arrow offset calculations
