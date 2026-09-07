import { fileURLToPath } from 'node:url'
import { mergeConfig, defineConfig } from 'vitest/config'
import viteConfig from './vite.config'

// 沿用 vite.config 的 alias（@ → src）等設定。
// environment 用 node：純函式測試不需要 DOM，元件測試也不需要——
// 走 vue/server-renderer 把元件算成 HTML 字串做結構斷言（見 components/layouts/__tests__），
// 零額外依賴。要驗互動行為（hover 陰影、地圖點選）才需要 jsdom/happy-dom，屆時再引入。
export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: 'node',
      root: fileURLToPath(new URL('./', import.meta.url)),
    },
  }),
)
