import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  base: "/yoink/",
  build: {
    outDir: "../docs",
    emptyOutDir: true,
  },
});
