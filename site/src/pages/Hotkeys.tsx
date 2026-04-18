const globalHotkeys = [
  { keys: "Alt + `", action: "capture (rectangle select)" },
  { keys: "Alt + Shift + `", action: "ocr text extraction" },
  { keys: "Alt + C", action: "color picker" },
  { keys: "customizable", action: "qr/barcode scanner" },
  { keys: "customizable", action: "sticker maker" },
  { keys: "customizable", action: "ruler" },
  { keys: "customizable", action: "gif/video recording" },
  { keys: "customizable", action: "fullscreen capture" },
  { keys: "customizable", action: "active window capture" },
  { keys: "customizable", action: "scrolling capture" },
];

const annotationHotkeys = [
  { keys: "1", action: "select tool" },
  { keys: "2", action: "arrow" },
  { keys: "3", action: "curved arrow" },
  { keys: "4", action: "text" },
  { keys: "5", action: "highlight" },
  { keys: "6", action: "blur" },
  { keys: "7", action: "step number" },
  { keys: "8", action: "freehand draw" },
  { keys: "9", action: "line" },
  { keys: "0", action: "ruler" },
  { keys: "-", action: "rectangle shape" },
  { keys: "=", action: "circle shape" },
  { keys: "[", action: "emoji" },
  { keys: "]", action: "eraser" },
];

const captureHotkeys = [
  { keys: "Ctrl + Z", action: "undo last annotation" },
  { keys: "Delete", action: "delete selected annotation" },
  { keys: "Escape", action: "cancel capture or close popup" },
  { keys: "Enter", action: "confirm text input" },
  { keys: "Tab", action: "reopen emoji picker (while placing)" },
  { keys: "Shift + drag", action: "constrain to square or straight line" },
];

function HotkeyList({ rows }: { rows: { keys: string; action: string }[] }) {
  return (
    <div className="flex flex-col">
      {rows.map((row) => (
        <div
          key={row.keys + row.action}
          className="flex items-center gap-4 py-2 border-b border-[#EBEBEB] last:border-b-0"
        >
          <kbd className="inline-flex items-center px-2 py-0.5 rounded border border-[#EBEBEB] bg-[#EBEBEB] text-black text-[12px] font-mono shrink-0 min-w-[120px] justify-center">
            {row.keys}
          </kbd>
          <span className="text-[14px] text-black/80">{row.action}</span>
        </div>
      ))}
    </div>
  );
}

function Section({ title, desc, rows }: { title: string; desc: string; rows: { keys: string; action: string }[] }) {
  return (
    <section className="border-t border-[#EBEBEB] pt-10 pb-4">
      <h2 className="text-[18px] font-bold mb-2 text-black">{title}</h2>
      <p className="text-black/70 leading-relaxed mb-5 max-w-[70ch] text-[14px]">{desc}</p>
      <HotkeyList rows={rows} />
    </section>
  );
}

export default function Hotkeys() {
  return (
    <div className="py-12 space-y-2">
      <section className="pb-6">
        <h1 className="text-[28px] font-bold text-black mb-2">hotkeys</h1>
        <p className="text-black/70 leading-relaxed max-w-[60ch]">
          all hotkeys are fully customizable in settings. these are the defaults.
        </p>
      </section>

      <Section
        title="global hotkeys"
        desc="work system-wide, even when yoink is in the background. supports ctrl, alt, shift, and win modifiers."
        rows={globalHotkeys}
      />

      <Section
        title="annotation tools"
        desc="switch between tools during capture using number keys."
        rows={annotationHotkeys}
      />

      <Section
        title="during capture"
        desc="shortcuts available while the capture overlay is active."
        rows={captureHotkeys}
      />
    </div>
  );
}
