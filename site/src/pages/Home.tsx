import { Link } from "react-router-dom";
import { useStarCount } from "../hooks/useStarCount";

const features = [
  {
    name: "Region capture",
    description:
      "Rectangle, freeform, fullscreen, active window, and scrolling capture",
  },
  {
    name: "Annotation tools",
    description:
      "Arrows, text, shapes, blur, freehand, step numbers, emoji, and ruler",
  },
  {
    name: "OCR & Translate",
    description:
      "Extract text from your screen with Windows OCR, translate with Argos or Google",
  },
  {
    name: "Screen recording",
    description:
      "Record as GIF, MP4, WebM, or MKV with mic and desktop audio",
  },
  {
    name: "Stickers",
    description:
      "Remove backgrounds from captures with local or cloud providers",
  },
  {
    name: "Color picker",
    description:
      "Pick colors from anywhere on screen with hex/RGB values",
  },
  {
    name: "QR/Barcode scanner",
    description:
      "Scan QR codes and barcodes from screen regions",
  },
  {
    name: "Search history",
    description:
      "Find past screenshots by OCR text or semantic similarity",
  },
  {
    name: "Upload anywhere",
    description:
      "15+ services including Imgur, S3, Dropbox, GitHub, and custom HTTP",
  },
  {
    name: "Hotkeys",
    description:
      "Fully configurable global hotkeys for every action",
  },
];

export default function Home() {
  const stars = useStarCount();

  return (
    <div>
      {/* Hero */}
      <section className="text-center space-y-6 pt-8 pb-16">
        <h1 className="text-5xl font-bold tracking-tight">Yoink</h1>
        <p className="text-lg text-zinc-400 max-w-xl mx-auto leading-relaxed">
          Capture, annotate, OCR, translate, make stickers, record video, and
          upload. All in one open-source tool.
        </p>
        <div className="flex items-center justify-center gap-3">
          <Link
            to="/downloads"
            className="inline-flex items-center px-5 py-2.5 rounded-md bg-zinc-50 text-zinc-950 text-sm font-medium hover:bg-zinc-200 transition-colors"
          >
            Download
          </Link>
          <a
            href="https://github.com/jasperdevs/yoink"
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center px-5 py-2.5 rounded-md border border-zinc-800 text-sm font-medium text-zinc-300 hover:bg-zinc-800/50 transition-colors"
          >
            Source Code
          </a>
        </div>
      </section>

      <div className="border-t border-zinc-800" />

      {/* Features */}
      <section className="py-16 space-y-6">
        <h2 className="text-2xl font-bold tracking-tight">What is Yoink?</h2>
        <p className="text-zinc-400 leading-relaxed">
          Yoink is an open-source screen capture and productivity toolkit for
          Windows. It handles everything from quick screenshots to annotated
          recordings, OCR, translation, and one-click uploads.
        </p>
        <div className="space-y-3">
          {features.map((feature) => (
            <div key={feature.name} className="flex gap-3 text-sm leading-relaxed">
              <span className="text-zinc-500 shrink-0 font-mono">[*]</span>
              <span>
                <strong className="text-zinc-50">{feature.name}</strong>
                <span className="text-zinc-400"> - {feature.description}</span>
              </span>
            </div>
          ))}
        </div>
        <div className="pt-2">
          <Link
            to="/downloads"
            className="inline-flex items-center px-5 py-2.5 rounded-md bg-zinc-50 text-zinc-950 text-sm font-medium hover:bg-zinc-200 transition-colors"
          >
            Download
          </Link>
        </div>
      </section>

      <div className="border-t border-zinc-800" />

      {/* Star history */}
      <section className="py-16 space-y-6">
        <h2 className="text-2xl font-bold tracking-tight">Open source</h2>
        <p className="text-zinc-400">
          Yoink is free and open source, backed by{" "}
          <span className="text-zinc-50 font-medium">
            {stars !== null ? stars.toLocaleString() : "..."}
          </span>{" "}
          stars on GitHub.
        </p>
        <div className="rounded-lg border border-zinc-800 overflow-hidden">
          <img
            src="https://api.star-history.com/image?repos=jasperdevs/yoink&type=timeline&theme=dark&legend=top-left"
            alt="Star History Chart"
            className="w-full"
          />
        </div>
      </section>
    </div>
  );
}
