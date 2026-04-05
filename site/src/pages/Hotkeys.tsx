const globalHotkeys = [
  { keys: "Alt + `", action: "Capture (rectangle select)" },
  { keys: "Alt + Shift + `", action: "OCR text extraction" },
  { keys: "Alt + C", action: "Color picker" },
  { keys: "Customizable", action: "QR/Barcode scanner" },
  { keys: "Customizable", action: "Sticker maker" },
  { keys: "Customizable", action: "Ruler" },
  { keys: "Customizable", action: "GIF/Video recording" },
  { keys: "Customizable", action: "Fullscreen capture" },
  { keys: "Customizable", action: "Active window capture" },
  { keys: "Customizable", action: "Scrolling capture" },
];

const annotationHotkeys = [
  { keys: "1", action: "Select tool" },
  { keys: "2", action: "Arrow" },
  { keys: "3", action: "Curved arrow" },
  { keys: "4", action: "Text" },
  { keys: "5", action: "Highlight" },
  { keys: "6", action: "Blur" },
  { keys: "7", action: "Step number" },
  { keys: "8", action: "Freehand draw" },
  { keys: "9", action: "Line" },
  { keys: "0", action: "Ruler" },
  { keys: "-", action: "Rectangle shape" },
  { keys: "=", action: "Circle shape" },
  { keys: "[", action: "Emoji" },
  { keys: "]", action: "Eraser" },
];

const captureHotkeys = [
  { keys: "Ctrl + Z", action: "Undo last annotation" },
  { keys: "Delete", action: "Delete selected annotation" },
  { keys: "Escape", action: "Cancel capture or close popup" },
  { keys: "Enter", action: "Confirm text input" },
  { keys: "Tab", action: "Reopen emoji picker (while placing)" },
  { keys: "Shift + drag", action: "Constrain to square or straight line" },
];

function HotkeyTable({ rows }: { rows: { keys: string; action: string }[] }) {
  return (
    <div className="rounded-lg border border-[#2a2a28] overflow-hidden">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-[#2a2a28] bg-[#1a1a18]">
            <th className="text-left px-4 py-3 text-[#8a8a80] font-medium w-48">Shortcut</th>
            <th className="text-left px-4 py-3 text-[#8a8a80] font-medium">Action</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.action} className="border-b border-[#2a2a28] last:border-b-0">
              <td className="px-4 py-2.5">
                <kbd className="inline-flex items-center px-2 py-0.5 rounded bg-[#222220] border border-[#333330] text-[#e8e6e3] text-xs font-mono">
                  {row.keys}
                </kbd>
              </td>
              <td className="px-4 py-2.5 text-[#d0cec8]">{row.action}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default function Hotkeys() {
  return (
    <div className="px-6 py-10 space-y-10">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Hotkey Reference</h1>
        <p className="text-[#8a8a80] mt-2">
          All hotkeys are fully customizable in Settings. These are the defaults.
        </p>
      </div>

      <div className="space-y-3">
        <h2 className="font-bold text-lg text-[#e8e6e3]">Global hotkeys</h2>
        <p className="text-[#8a8a80] text-sm">
          Work system-wide, even when Yoink is in the background. Supports Ctrl, Alt, Shift, and Win modifiers.
        </p>
        <HotkeyTable rows={globalHotkeys} />
      </div>

      <div className="space-y-3">
        <h2 className="font-bold text-lg text-[#e8e6e3]">Annotation tools</h2>
        <p className="text-[#8a8a80] text-sm">
          Switch between tools during capture using number keys.
        </p>
        <HotkeyTable rows={annotationHotkeys} />
      </div>

      <div className="space-y-3">
        <h2 className="font-bold text-lg text-[#e8e6e3]">During capture</h2>
        <p className="text-[#8a8a80] text-sm">
          Shortcuts available while the capture overlay is active.
        </p>
        <HotkeyTable rows={captureHotkeys} />
      </div>
    </div>
  );
}
