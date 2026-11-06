import { defineConfig } from 'vite';
// Matches the HTTP endpoint in backend/Properties/launchSettings.json.
export default defineConfig({
  server: {
    port: 5173,
    strictPort: true,
    proxy: { '/api': 'http://127.0.0.1:65421' },
  },
});
