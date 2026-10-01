import { defineConfig } from "vite";
export default defineConfig({
  base: "./",
  build: {
    chunkSizeWarningLimit: 750,
    rollupOptions: { output: { manualChunks: { three: ["three"] } } },
  },
  server: { port: 4173, strictPort: true },
});
