import { useReleases } from "../hooks/useReleases";

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

function renderMarkdown(body: string): string {
  let html = body;

  // Escape HTML
  html = html.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

  // Headers: ### h3, ## h2, # h1
  html = html.replace(/^### (.+)$/gm, '<h4 class="text-sm font-semibold mt-4 mb-1">$1</h4>');
  html = html.replace(/^## (.+)$/gm, '<h3 class="text-base font-semibold mt-5 mb-2">$1</h3>');
  html = html.replace(/^# (.+)$/gm, '<h2 class="text-lg font-semibold mt-6 mb-2">$1</h2>');

  // Bold
  html = html.replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");

  // Inline code
  html = html.replace(/`([^`]+)`/g, '<code class="px-1 py-0.5 rounded bg-zinc-800 text-sm font-mono">$1</code>');

  // Unordered list items
  html = html.replace(/^[*-] (.+)$/gm, '<li class="ml-4 list-disc text-sm text-zinc-300">$1</li>');

  // Wrap consecutive <li> in <ul>
  html = html.replace(
    /(<li[^>]*>.*<\/li>\n?)+/g,
    (match) => `<ul class="space-y-1 my-2">${match}</ul>`
  );

  // Links: [text](url)
  html = html.replace(
    /\[([^\]]+)\]\(([^)]+)\)/g,
    '<a href="$2" target="_blank" rel="noopener noreferrer" class="text-blue-400 hover:underline">$1</a>'
  );

  // Paragraphs: wrap non-tag lines
  html = html
    .split("\n")
    .map((line) => {
      const trimmed = line.trim();
      if (
        !trimmed ||
        trimmed.startsWith("<h") ||
        trimmed.startsWith("<ul") ||
        trimmed.startsWith("</ul") ||
        trimmed.startsWith("<li") ||
        trimmed.startsWith("<a")
      ) {
        return line;
      }
      return `<p class="text-sm text-zinc-400 my-1">${trimmed}</p>`;
    })
    .join("\n");

  return html;
}

export default function Changelog() {
  const { releases, loading } = useReleases();

  if (loading) {
    return (
      <div className="text-center py-20 text-zinc-500">
        Loading changelog...
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Changelog</h1>
        <p className="text-zinc-400 mt-2">
          Release notes for every version of Yoink.
        </p>
      </div>

      <div className="space-y-4">
        {releases.map((release) => (
          <div
            key={release.id}
            className="rounded-lg border border-zinc-800 bg-zinc-900 p-5 space-y-3"
          >
            <div className="flex items-center gap-3">
              <h2 className="text-base font-semibold">{release.tag_name}</h2>
              <span className="text-sm text-zinc-500">
                {formatDate(release.published_at)}
              </span>
            </div>
            {release.body && (
              <div
                className="prose-invert max-w-none"
                dangerouslySetInnerHTML={{
                  __html: renderMarkdown(release.body),
                }}
              />
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
