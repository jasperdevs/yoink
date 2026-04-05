import { Link } from "react-router-dom";
import { useStarCount } from "../hooks/useStarCount";

const features = [
  {
    title: "Region & Window Capture",
    description:
      "Capture any region of your screen or snap a full window with pixel-perfect accuracy.",
  },
  {
    title: "Annotation Tools",
    description:
      "Draw, highlight, add text, arrows, and shapes directly on your captures.",
  },
  {
    title: "OCR & Translate",
    description:
      "Extract text from any capture and translate it into other languages instantly.",
  },
  {
    title: "Screen Recording",
    description:
      "Record your screen as video with audio support and flexible region selection.",
  },
  {
    title: "Stickers",
    description:
      "Remove backgrounds and turn any capture into a sticker with one click.",
  },
  {
    title: "Upload Anywhere",
    description:
      "Upload captures to Imgur, custom hosts, or copy straight to your clipboard.",
  },
];

export default function Home() {
  const stars = useStarCount();

  return (
    <div className="space-y-16">
      {/* Hero */}
      <section className="text-center space-y-6 pt-8">
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

      <hr className="border-zinc-800" />

      {/* Features */}
      <section>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {features.map((feature) => (
            <div
              key={feature.title}
              className="rounded-lg border border-zinc-800 bg-zinc-900 p-5 space-y-2"
            >
              <h3 className="text-sm font-semibold">{feature.title}</h3>
              <p className="text-sm text-zinc-400 leading-relaxed">
                {feature.description}
              </p>
            </div>
          ))}
        </div>
      </section>

      <hr className="border-zinc-800" />

      {/* Star history */}
      <section className="space-y-6">
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
