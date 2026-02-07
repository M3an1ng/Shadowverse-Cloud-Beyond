import { defineConfig } from 'vite';

// https://vitejs.dev/config/
export default defineConfig({
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    watch: {
      // Prevent frequent full reloads caused by backend/log file writes under the monorepo root.
      ignored: [
        '**/*.log',
        '**/Server/Logs/**',
        '**/Server/bin/**',
        '**/Server/obj/**',
        '**/Server/publish/**',
        '**/Server.Tests/bin/**',
        '**/Server.Tests/obj/**',
        '**/openspec/**',
      ],
    },
    proxy: {
      // 将所有以 /api 开头的请求代理到后端 API 服务器
      '/api': {
        target: 'http://localhost:5100', // 修正：指向实际运行的 HTTP 后端地址
        changeOrigin: true, // 建议开启，用于虚拟主机站点
        // secure: false, // 目标是 HTTP，不再需要此选项
        // 如果你的后端只在 HTTP 上运行 (例如 http://localhost:5100)，则使用:
        // target: 'http://localhost:5100', // 保留注释作为参考
        // changeOrigin: true,
        
        // 可选：重写路径 (如果前端路径与后端路径不完全匹配)
        // rewrite: (path) => path.replace(/^\/api/, '') 
      },
      // 添加 SignalR Hub (/gamehub) 的代理规则
      '/gamehub': {
        target: 'http://localhost:5100', // 同样指向后端 HTTP 地址
        ws: true, // 必须开启，让 Vite 代理 WebSocket 连接
        changeOrigin: true,
      },
      '/loghub': {
        target: 'http://localhost:5100',
        ws: true,
        changeOrigin: true,
      }
      // 如果还有其他需要代理的路径，可以在这里继续添加
      // '/hubs': { // 保留旧注释作为参考
      //   target: 'https://localhost:7097', // SignalR Hubs 可能也需要代理
      //   ws: true, // 重要：为 WebSocket 连接启用代理
      //   changeOrigin: true,
      //   secure: false, 
      // }
    }
  }
});
