# Yoink v0.8.3.6

## Added
- New dedicated `Toast` settings tab with a visual button layout editor.
- Configurable toast action buttons for `Close`, `Pin`, `Save`, and optional `Delete`.
- Optional multi-monitor overlay capture setting so the region overlay can span all displays.

## Changed
- Moved toast-related settings out of `General` into the new `Toast` section.
- Toast editor now supports both click placement and drag placement, plus swapping buttons between slots.
- Hidden toast buttons now live in a shelf instead of separate show/hide toggles.
- Toast overlay buttons were made larger and their hover behavior was tightened up.

## Fixed
- Region capture overlay can now use the full virtual desktop instead of stopping at the active monitor when the new setting is enabled.
- Toast editor layout was cleaned up to avoid clipped drag feedback, mismatched widths, and inconsistent slot availability.
- Shelf drop feedback is now explicit instead of relying on a subtle border-only state.
- Tooltip behavior on toast overlay buttons was reduced so it no longer lingers awkwardly while hovering those controls.
