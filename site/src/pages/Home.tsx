import { useEffect, useRef, useState } from "react";
import StarChart from "../components/StarChart";
import { useReleases } from "../hooks/useReleases";
import {
  AccordionGroup,
  AccordionItem,
  AccordionTrigger,
  AccordionContent,
} from "@/components/ui/accordion";

const features = [
  "region capture",
  "annotation tools",
  "ocr & translate",
  "screen recording",
  "sticker maker",
  "color picker",
  "qr/barcode scanner",
  "search history",
  "19 upload destinations",
  "global hotkeys",
  "png, jpeg, bmp",
  "multi-monitor",
  "start with windows",
  "auto-updates",
  "gpl-3.0 licensed",
];

const showcase = [
  { title: "annotate", desc: "arrows, text, shapes, blur, highlights, freehand, step numbers, emoji, ruler.", img: "annotations.png" },
  { title: "make stickers", desc: "remove backgrounds locally with 5 ai models, save as transparent png.", img: "sticker-showcase.png" },
  { title: "ocr & translate", desc: "extract text from any region and translate across 35+ languages.", img: "ocr-screenshot.png" },
  { title: "search history", desc: "find screenshots by filename, ocr text, or ai semantic match.", img: "search-screenshot.png" },
  { title: "color picker", desc: "pick any color on screen with a magnified preview. hex and rgb.", img: "color-picker.png" },
  { title: "record", desc: "save as gif, mp4, webm, or mkv. microphone and desktop audio.", img: "recording.png" },
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
  { q: "how is yoink different from sharex?", a: "yoink has a modern, clean interface with built-in sticker creation, semantic image search, and uses windows native ocr instead of tesseract. it focuses on being simple to use while still being powerful." },
  { q: "can i customize hotkeys?", a: "yes. every action has a configurable global hotkey. you can set hotkeys for screenshot, ocr, color picker, recording, stickers, and more in settings." },
  { q: "does yoink have a portable version?", a: "yes. the downloads page includes both a windows installer (recommended) and a portable zip." },
  { q: "how do i update yoink?", a: "installed builds can update through the app. you can also download the latest installer or portable build directly from the downloads page." },
  { q: "does yoink support multiple monitors?", a: "yes. yoink fully supports multi-monitor setups for capture, recording, and color picking. you can capture regions across monitors or target a specific screen." },
];

function detectArch(): "arm64" | "x64" {
  const ua = navigator.userAgent.toLowerCase();
  if (ua.includes("arm64") || ua.includes("aarch64")) return "arm64";
  return "x64";
}

function pickInstaller(release: { assets: { name: string; browser_download_url: string }[] }, arch: "arm64" | "x64") {
  const exes = release.assets.filter((a) => a.name.toLowerCase().endsWith(".exe"));
  const match = exes.find((a) => a.name.toLowerCase().includes(arch));
  const installer = exes.find((a) => a.name.toLowerCase().includes("setup"));
  return (match ?? installer ?? exes[0])?.browser_download_url ?? null;
}

function Showcase() {
  const base = import.meta.env.BASE_URL;
  const scrollerRef = useRef<HTMLDivElement>(null);
  const [active, setActive] = useState(0);

  const scrollTo = (i: number) => {
    const el = scrollerRef.current;
    if (!el) return;
    const card = el.children[i] as HTMLElement | undefined;
    if (card) el.scrollTo({ left: card.offsetLeft - el.offsetLeft, behavior: "smooth" });
  };

  useEffect(() => {
    const el = scrollerRef.current;
    if (!el) return;
    const onScroll = () => {
      const children = Array.from(el.children) as HTMLElement[];
      if (children.length === 0) return;
      const centerX = el.scrollLeft + el.clientWidth / 2;
      let closest = 0;
      let closestDist = Infinity;
      children.forEach((child, i) => {
        const mid = child.offsetLeft + child.offsetWidth / 2 - el.offsetLeft;
        const dist = Math.abs(mid - centerX);
        if (dist < closestDist) {
          closestDist = dist;
          closest = i;
        }
      });
      setActive(closest);
    };
    el.addEventListener("scroll", onScroll, { passive: true });
    return () => el.removeEventListener("scroll", onScroll);
  }, []);

  return (
    <div>
      <div
        ref={scrollerRef}
        className="no-scrollbar flex overflow-x-auto snap-x snap-mandatory gap-4 -mx-6 px-6 sm:-mx-8 sm:px-8 pb-1"
        style={{ scrollSnapType: "x mandatory" }}
      >
        {showcase.map((s) => (
          <div
            key={s.title}
            className="snap-center shrink-0 w-full"
          >
            <div className="rounded-md overflow-hidden border border-[#EBEBEB] bg-white">
              <img
                loading="lazy"
                src={base + s.img}
                alt={s.title}
                className="w-full h-auto block"
              />
            </div>
            <div className="mt-3">
              <h3 className="text-[15px] font-bold text-black">{s.title}</h3>
              <p className="text-[14px] text-black/70 leading-relaxed mt-1 max-w-[60ch]">{s.desc}</p>
            </div>
          </div>
        ))}
      </div>

      <div className="mt-5 flex items-center justify-between gap-4">
        <div className="flex items-center gap-1.5">
          {showcase.map((s, i) => (
            <button
              key={s.title}
              onClick={() => scrollTo(i)}
              aria-label={`go to ${s.title}`}
              className={`h-1.5 rounded-full transition-all ${
                active === i ? "w-6 bg-black" : "w-1.5 bg-black/20 hover:bg-black/40"
              }`}
            />
          ))}
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => scrollTo(Math.max(0, active - 1))}
            aria-label="previous"
            className="w-8 h-8 inline-flex items-center justify-center rounded-md border border-[#EBEBEB] text-black/70 hover:text-black hover:bg-[#EBEBEB] transition-colors"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M15 18l-6-6 6-6" /></svg>
          </button>
          <button
            onClick={() => scrollTo(Math.min(showcase.length - 1, active + 1))}
            aria-label="next"
            className="w-8 h-8 inline-flex items-center justify-center rounded-md border border-[#EBEBEB] text-black/70 hover:text-black hover:bg-[#EBEBEB] transition-colors"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M9 18l6-6-6-6" /></svg>
          </button>
        </div>
      </div>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="pt-10 pb-4">
      <h2 className="text-[18px] font-bold mb-3 text-black">{title}</h2>
      {children}
    </section>
  );
}

export default function Home() {
  const base = import.meta.env.BASE_URL;
  const { releases } = useReleases();
  const [downloadUrl, setDownloadUrl] = useState<string | null>(null);

  useEffect(() => {
    if (releases.length === 0) return;
    const arch = detectArch();
    const latest = releases[0];
    setDownloadUrl(pickInstaller(latest, arch));
  }, [releases]);

  return (
    <div className="py-12 space-y-2">
      <section className="pb-10 flex flex-col items-center text-center">
        <img src={base + "banner.svg"} alt="yoink" className="w-60 mb-6" />
        <p className="text-black/70 leading-relaxed mb-8 max-w-[55ch] text-[15px]">
          capture, annotate, ocr, translate, make stickers, record video, and upload. all in one open-source tool for windows.
        </p>
        <div className="flex flex-wrap items-center justify-center gap-3">
          {downloadUrl ? (
            <a
              href={downloadUrl}
              className="inline-flex items-center justify-center px-5 py-2.5 rounded-md bg-black text-white text-[14px] font-medium hover:bg-black/85 transition-colors"
            >
              download
            </a>
          ) : (
            <span className="inline-flex items-center justify-center px-5 py-2.5 rounded-md bg-black/40 text-white text-[14px] font-medium cursor-wait">
              loading...
            </span>
          )}
          <a
            href="https://github.com/jasperdevs/yoink"
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center justify-center px-5 py-2.5 rounded-md border border-black text-black text-[14px] font-medium hover:bg-[#EBEBEB] transition-colors"
          >
            source code
          </a>
        </div>
      </section>

      <Section title="everything in one tool">
        <Showcase />
      </Section>

      <Section title="also included">
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-x-4 gap-y-2">
          {features.map((f) => (
            <span key={f} className="text-[14px] text-black/70 leading-snug">
              {f}
            </span>
          ))}
        </div>
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
