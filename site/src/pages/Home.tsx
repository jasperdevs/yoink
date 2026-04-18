import { Link } from "react-router-dom";
import StarChart from "../components/StarChart";
import {
  AccordionGroup,
  AccordionItem,
  AccordionTrigger,
  AccordionContent,
} from "@/components/ui/accordion";

const features = [
  { name: "Region capture", desc: "rectangle, freeform, fullscreen, active window, and scrolling capture with delay timer and window detection" },
  { name: "Annotation tools", desc: "arrows, curved arrows, text, shapes, highlights, blur, freehand, step numbers, emoji, ruler, magnifier, and eraser with undo/redo" },
  { name: "OCR & Translate", desc: "extract text from your screen with Windows OCR, translate with Argos (offline) or Google Translate across 35+ languages" },
  { name: "Screen recording", desc: "record as GIF, MP4, WebM, or MKV with mic and desktop audio at 15/24/30/60 FPS" },
  { name: "Stickers", desc: "remove backgrounds with 5 local AI models or cloud providers, add shadow and stroke effects" },
  { name: "Color picker", desc: "pick colors from anywhere on screen with magnified preview, hex/RGB values, and color history" },
  { name: "QR/Barcode scanner", desc: "scan QR codes, Aztec, Data Matrix, PDF-417, CODE-128, EAN, UPC, and more" },
  { name: "Search history", desc: "find past screenshots by filename, OCR text, or AI-powered semantic similarity" },
  { name: "Upload anywhere", desc: "19 destinations including Imgur, S3, Dropbox, GitHub, OneDrive, and custom HTTP" },
  { name: "Hotkeys", desc: "fully configurable global hotkeys for every action with modifier key support" },
  { name: "Image formats", desc: "save as PNG, JPEG, or BMP with configurable quality and custom naming patterns" },
  { name: "After-capture actions", desc: "auto-copy, auto-save, auto-upload, auto-pin previews, or prompt for filename" },
  { name: "Settings import/export", desc: "save and load your settings as JSON, reset to defaults anytime" },
  { name: "Start with Windows", desc: "auto-launch on startup, runs quietly in the system tray" },
  { name: "Multiple monitors", desc: "full multi-monitor support for capture, recording, and color picking" },
  { name: "Auto-updates", desc: "background update checking keeps Yoink up to date" },
];

const faq = [
  { q: "what is yoink?", a: "yoink is a free, open-source screenshot and screen recording tool for windows. it replaces tools like sharex with a clean, modern interface." },
  { q: "is yoink free?", a: "yes, completely free and open source under the gpl-3.0 license. no ads, no tracking, no premium tiers." },
  { q: "does yoink work offline?", a: "yes. all capture, annotation, ocr, and recording features work fully offline. only uploads and google translate require internet." },
  { q: "what windows versions are supported?", a: "windows 10 (version 1903+) and windows 11. both x64 and arm64 are supported." },
  { q: "how does ocr work?", a: "yoink uses the windows built-in ocr engine. no downloads or setup needed. it supports all languages installed in your windows language settings." },
  { q: "can i upload screenshots automatically?", a: "yes. yoink supports auto-upload to 19 destinations: imgur, imgbb, catbox, litterbox, gyazo, file.io, uguu, transfer.sh, dropbox, google drive, onedrive, azure blob, github, immich, ftp, sftp, webdav, s3-compatible storage (aws, cloudflare r2, backblaze b2), and custom http endpoints." },
  { q: "where are screenshots saved?", a: "by default in your pictures/yoink folder. you can change this in settings along with the file format and naming pattern." },
  { q: "what recording formats are supported?", a: "gif, mp4, webm, and mkv. you can record with microphone audio, desktop audio, or both. frame rate and quality are configurable." },
  { q: "what translation services are supported?", a: "yoink supports argos translate (fully offline, no api key needed) and google translate (requires internet). both support 35+ languages." },
  { q: "what models does the sticker maker use?", a: "yoink supports local background removal with 5 ai models: bria rmbg (recommended), birefnet lite, isnet general use, u2net, and u2netp. you can also use cloud providers remove.bg and photoroom." },
  { q: "how is yoink different from sharex?", a: "yoink has a modern, clean interface with built-in sticker creation, semantic image search, and uses windows native ocr instead of tesseract. it focuses on being simple to use while still being powerful." },
  { q: "can i customize hotkeys?", a: "yes. every action has a configurable global hotkey. you can set hotkeys for screenshot, ocr, color picker, recording, stickers, and more in settings." },
  { q: "does yoink have a portable version?", a: "yes. the downloads page includes both a windows installer (recommended) and a portable zip." },
  { q: "how do i update yoink?", a: "installed builds can update through the app. you can also download the latest installer or portable build directly from the downloads page." },
  { q: "does yoink support multiple monitors?", a: "yes. yoink fully supports multi-monitor setups for capture, recording, and color picking. you can capture regions across monitors or target a specific screen." },
];

function Btn({
  to,
  href,
  variant = "primary",
  children,
}: {
  to?: string;
  href?: string;
  variant?: "primary" | "outline";
  children: React.ReactNode;
}) {
  const cls =
    variant === "primary"
      ? "inline-flex items-center justify-center px-5 py-2.5 rounded-md bg-black text-white text-[14px] font-medium hover:bg-black/85 transition-colors"
      : "inline-flex items-center justify-center px-5 py-2.5 rounded-md border border-black text-black text-[14px] font-medium hover:bg-[#EBEBEB] transition-colors";

  if (href) {
    return (
      <a href={href} target="_blank" rel="noopener noreferrer" className={cls}>
        {children}
      </a>
    );
  }
  return (
    <Link to={to ?? "/"} className={cls}>
      {children}
    </Link>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="border-t border-[#EBEBEB] pt-10 pb-4">
      <h2 className="text-[18px] font-bold mb-3 text-black">{title}</h2>
      {children}
    </section>
  );
}

export default function Home() {
  const base = import.meta.env.BASE_URL;

  return (
    <div className="py-12 space-y-2">
      <section className="pb-10">
        <img src={base + "banner.svg"} alt="yoink" className="w-64 mb-8" />
        <h1 className="text-[28px] font-bold mb-4 text-black">yoink</h1>
        <p className="text-black/70 leading-relaxed mb-8 max-w-[60ch]">
          capture, annotate, ocr, translate, make stickers, record video, and upload. all in one open-source tool for windows.
        </p>
        <div className="flex flex-wrap items-center gap-3">
          <Btn to="/downloads" variant="primary">download for windows</Btn>
          <Btn href="https://github.com/jasperdevs/yoink" variant="outline">source code</Btn>
        </div>
      </section>

      <Section title="what is yoink?">
        <p className="text-black/70 leading-relaxed mb-5 max-w-[70ch]">
          yoink is an open-source screen capture and productivity toolkit for windows. it handles everything from quick screenshots to annotated recordings, ocr, translation, and one-click uploads.
        </p>
        <div className="space-y-2">
          {features.map((f) => (
            <p key={f.name} className="text-black/70 leading-relaxed max-w-[75ch]">
              <strong className="text-black font-semibold">{f.name}</strong>{" "}
              <span>{f.desc}</span>
            </p>
          ))}
        </div>
        <div className="mt-6">
          <Btn to="/downloads" variant="outline">download &rarr;</Btn>
        </div>
      </Section>

      <Section title="powerful annotation tools">
        <p className="text-black/70 leading-relaxed mb-5 max-w-[70ch]">
          arrows, text, shapes, blur, highlights, freehand drawing, step numbers, emoji, ruler, and more. everything you need to mark up screenshots before sharing.
        </p>
        <img loading="lazy" src={base + "annotations.png"} alt="annotation tools" className="w-full rounded border border-[#EBEBEB]" />
      </Section>

      <Section title="built-in sticker maker">
        <p className="text-black/70 leading-relaxed mb-5 max-w-[70ch]">
          turn any screenshot into a sticker by removing the background, then save, copy, or upload it like a normal image.
        </p>
        <img loading="lazy" src={base + "sticker-showcase.png"} alt="sticker showcase" className="w-full rounded border border-[#EBEBEB]" />
      </Section>

      <Section title="ocr and translate">
        <p className="text-black/70 leading-relaxed mb-5 max-w-[70ch]">
          extract text from any region of your screen. results open in a dedicated window where you can edit, copy, or translate the text instantly.
        </p>
        <img loading="lazy" src={base + "ocr-screenshot.png"} alt="ocr result window" className="w-full rounded border border-[#EBEBEB]" />
      </Section>

      <Section title="search your history">
        <p className="text-black/70 leading-relaxed mb-5 max-w-[70ch]">
          search your image history by filename, ocr text, and semantic matching, so you can find screenshots by what they say or by what they show.
        </p>
        <img loading="lazy" src={base + "search-screenshot.png"} alt="search history" className="w-full rounded border border-[#EBEBEB]" style={{ marginBottom: "-20%", clipPath: "inset(0 0 20% 0)" }} />
      </Section>

      <Section title="color picker">
        <p className="text-black/70 leading-relaxed mb-5 max-w-[70ch]">
          pick any color from your screen with a magnified preview. copies hex and rgb values to your clipboard instantly.
        </p>
        <img loading="lazy" src={base + "color-picker.png"} alt="color picker" className="w-full rounded border border-[#EBEBEB]" />
      </Section>

      <Section title="screen recording">
        <p className="text-black/70 leading-relaxed mb-5 max-w-[70ch]">
          record your screen as gif, mp4, webm, or mkv. capture microphone and desktop audio simultaneously with configurable frame rate and quality.
        </p>
        <img loading="lazy" src={base + "recording.png"} alt="screen recording" className="w-full rounded border border-[#EBEBEB]" />
      </Section>

      <Section title="built for privacy">
        <p className="text-black/70 leading-relaxed max-w-[70ch]">
          yoink runs entirely on your machine. no accounts, no telemetry, no cloud dependencies. your screenshots never leave your computer unless you choose to upload them.
        </p>
      </Section>

      <Section title="faq">
        <AccordionGroup type="single" collapsible className="w-full max-w-full">
          {faq.map((item, i) => (
            <AccordionItem key={item.q} value={item.q} index={i}>
              <AccordionTrigger>{item.q}</AccordionTrigger>
              <AccordionContent>{item.a}</AccordionContent>
            </AccordionItem>
          ))}
        </AccordionGroup>
      </Section>

      <Section title="open source">
        <p className="text-black/70 leading-relaxed mb-5 max-w-[70ch]">
          free and open source, licensed under gpl-3.0.
        </p>
        <StarChart />
      </Section>
    </div>
  );
}
