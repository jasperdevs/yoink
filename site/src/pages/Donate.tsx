function PrimaryBtn({ href, children }: { href: string; children: React.ReactNode }) {
  return (
    <a
      href={href}
      target="_blank"
      rel="noopener noreferrer"
      className="inline-flex items-center justify-center px-4 py-2 rounded-md bg-black text-white text-[13px] font-medium hover:bg-black/85 transition-colors"
    >
      {children}
    </a>
  );
}

function KofiLogo() {
  return (
    <svg viewBox="0 0 32 32" className="w-8 h-8 shrink-0" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
      <rect width="32" height="32" rx="6" fill="#FF5E5B" />
      <path
        fill="#FFFFFF"
        d="M23.3 9.5H8.4c-.7 0-1.2.6-1.2 1.2v7.9c0 3 2.4 5.4 5.4 5.4h4.6c3 0 5.4-2.4 5.4-5.4v-.6h.7c2.2 0 4-1.8 4-4s-1.8-4-4-4zm.4 6h-1.1v-3.8h1.1c1.1 0 1.9.8 1.9 1.9s-.8 1.9-1.9 1.9zM12 12.4c-1.2 0-2.2 1-2.2 2.2 0 1.7 2 3.3 3.6 4.5 1.7-1.2 3.6-2.8 3.6-4.5 0-1.2-1-2.2-2.2-2.2-.6 0-1.1.3-1.4.7-.3-.4-.8-.7-1.4-.7z"
      />
    </svg>
  );
}

function PayPalLogo() {
  return (
    <svg viewBox="0 0 32 32" className="w-8 h-8 shrink-0" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
      <rect width="32" height="32" rx="6" fill="#FFFFFF" stroke="#EBEBEB" />
      <path
        fill="#003087"
        d="M19.1 12.4c.1-1.8-1.3-3.2-3.8-3.2H10c-.3 0-.5.2-.5.5L8 21.6c0 .2.2.4.4.4h2.3l.6-3.8v.1c0-.3.3-.5.5-.5h1.1c2.1 0 3.8-.9 4.3-3.4 0-.1 0-.1 0-.2.7.4 1.2 1.1 1.3 2 0 .1 0 .1 0 .2-.5 2.5-2.1 3.4-4.3 3.4h-.6c-.3 0-.5.2-.5.5l-.3 2.2-.1.6c0 .2.1.4.4.4h2c.3 0 .5-.2.5-.4v-.1l.4-2.4v-.1c0-.2.3-.4.5-.4h.3c2.1 0 3.7-.9 4.2-3.3.2-1 .1-1.9-.4-2.5-.1-.2-.3-.4-.6-.5.1-.1.1-.3.1-.4.1 0 .1-.1.1-.2.1-.7.1-1.2-.2-1.8-.1-.2-.3-.4-.5-.5z"
      />
      <path
        fill="#0070E0"
        d="M19.1 12.4c.1-1.8-1.3-3.2-3.8-3.2H10c-.3 0-.5.2-.5.5L8 21.6c0 .2.2.4.4.4h2.3l.6-3.8.6-4c0-.3.3-.5.5-.5h1.8c2.5 0 4 .6 4.3 2.5 0 .1 0 .2 0 .3.2-.1.2-.1.3-.2.3-.1.6-.2.9-.3.4-.1.9-.1 1.4-.1-.1-.2 0-.4 0-.6 0-.1 0-.3 0-.4-.1-.3-.3-.5-.5-.7 0 0-.1 0-.5-.3z"
      />
    </svg>
  );
}

function GitHubLogo() {
  return (
    <svg viewBox="0 0 32 32" className="w-8 h-8 shrink-0" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
      <rect width="32" height="32" rx="6" fill="#000000" />
      <path
        fill="#FFFFFF"
        transform="translate(4 4)"
        d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12"
      />
    </svg>
  );
}

const options = [
  {
    title: "ko-fi",
    description: "buy me a coffee to support development.",
    url: "https://ko-fi.com/jasperdevs",
    buttonText: "donate on ko-fi",
    logo: <KofiLogo />,
  },
  {
    title: "paypal",
    description: "send a one-time donation via paypal.",
    url: "https://www.paypal.com/paypalme/9KGFX",
    buttonText: "donate on paypal",
    logo: <PayPalLogo />,
  },
  {
    title: "github star",
    description: "star the repo to help others find yoink.",
    url: "https://github.com/jasperdevs/yoink",
    buttonText: "star on github",
    logo: <GitHubLogo />,
  },
];

export default function Donate() {
  return (
    <div className="py-12">
      <div className="mb-8">
        <h1 className="text-[28px] font-bold text-black mb-2">donate</h1>
        <p className="text-black/70 leading-relaxed max-w-[60ch]">
          yoink is free and open source. if you find it useful, consider supporting the project.
        </p>
      </div>

      <div>
        {options.map((option) => (
          <div
            key={option.title}
            className="border-t border-[#EBEBEB] py-6 flex items-center gap-4 flex-wrap"
          >
            {option.logo}
            <div className="flex-1 min-w-0">
              <h2 className="text-[16px] font-bold text-black mb-1">{option.title}</h2>
              <p className="text-[14px] text-black/70">{option.description}</p>
            </div>
            <PrimaryBtn href={option.url}>{option.buttonText}</PrimaryBtn>
          </div>
        ))}
      </div>
    </div>
  );
}
