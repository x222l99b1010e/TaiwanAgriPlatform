import { fileURLToPath } from 'node:url'
import { mergeConfig, defineConfig } from 'vitest/config'
import viteConfig from './vite.config'

// 沿用 vite.config 的 alias（@ → src）等設定；
// 第一批測試對象都是純函式，environment 用 node 即可，
// 之後要測元件再引入 jsdom/happy-dom
export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: 'node',
      root: fileURLToPath(new URL('./', import.meta.url)),
    },
  }),
)
