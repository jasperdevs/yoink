function GitHubIcon() {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 24 24"
      fill="currentColor"
      className="w-10 h-10"
    >
      <path d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12" />
    </svg>
  );
}

const cards = [
  {
    title: "Ko-fi",
    description: "Buy me a coffee to support development.",
    url: "https://ko-fi.com/jasperdevs",
    buttonText: "Donate on Ko-fi",
    logo: (
      <img
        src="https://storage.ko-fi.com/cdn/brandasset/v2/kofi_s_logo_nolabel.png"
        alt="Ko-fi"
        className="w-10 h-10"
      />
    ),
  },
  {
    title: "PayPal",
    description: "Send a one-time donation via PayPal.",
    url: "https://www.paypal.com/paypalme/9KGFX",
    buttonText: "Donate on PayPal",
    logo: (
      <img
        src="https://www.paypalobjects.com/webstatic/icon/pp258.png"
        alt="PayPal"
        className="w-10 h-10 rounded"
      />
    ),
  },
  {
    title: "GitHub Star",
    description: "Star the repo to help others discover Yoink.",
    url: "https://github.com/jasperdevs/yoink",
    buttonText: "Star on GitHub",
    logo: <GitHubIcon />,
  },
];

export default function Donate() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Donate</h1>
        <p className="text-zinc-400 mt-2">
          Yoink is free and open source. If you find it useful, consider
          supporting the project.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {cards.map((card) => (
          <div
            key={card.title}
            className="rounded-lg border border-zinc-800 bg-zinc-900 p-5 flex flex-col items-center text-center space-y-4"
          >
            {card.logo}
            <div className="space-y-1">
              <h2 className="text-sm font-semibold">{card.title}</h2>
              <p className="text-sm text-zinc-400">{card.description}</p>
            </div>
            <a
              href={card.url}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center px-4 py-2 rounded-md border border-zinc-700 text-sm font-medium text-zinc-300 hover:bg-zinc-800 transition-colors mt-auto"
            >
              {card.buttonText}
            </a>
          </div>
        ))}
      </div>
    </div>
  );
}
