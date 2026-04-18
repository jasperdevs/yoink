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

const options = [
  {
    title: "ko-fi",
    description: "buy me a coffee to support development.",
    url: "https://ko-fi.com/jasperdevs",
    buttonText: "donate on ko-fi",
  },
  {
    title: "paypal",
    description: "send a one-time donation via paypal.",
    url: "https://www.paypal.com/paypalme/9KGFX",
    buttonText: "donate on paypal",
  },
  {
    title: "github star",
    description: "star the repo to help others find yoink.",
    url: "https://github.com/jasperdevs/yoink",
    buttonText: "star on github",
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
