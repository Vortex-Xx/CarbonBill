import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.ico', 'apple-touch-icon.png'],
      manifest: {
        name: 'CarbonBill - SME Carbon Accounting',
        short_name: 'CarbonBill',
        description: 'Bangla-first SME Carbon Accounting & Defensible Buyer Reporting',
        theme_color: '#0f766e',
        background_color: '#f8fafc',
        display: 'standalone',
        orientation: 'portrait'
      }
    })
  ],
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('FloorStaffView')) {
            return 'floor';
          }
          if (id.includes('chart.js') || id.includes('react-chartjs-2')) {
            return 'charts';
          }
          if (id.includes('node_modules')) {
            return 'vendor';
          }
        }
      }
    },
    chunkSizeWarningLimit: 300
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5120',
        changeOrigin: true
      }
    }
  }
});
