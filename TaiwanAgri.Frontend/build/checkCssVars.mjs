// build/checkCssVars.mjs
// 職責：檢查有沒有用到「從來沒定義過的 CSS 變數」。
//
// 為什麼需要：`var(--typo)` 打錯字時，整條 CSS 宣告會靜靜失效（輸入框沒有邊框、
// 背景變透明），而 lint、vue-tsc、vite build 三者都不會攔——是那種「跑得過但做錯了」
// 的問題，只能靠專門的檢查抓。
//
// 定義側必須取三個來源的聯集，缺一就會誤報：
//   ①assets/*.css 的全域 token
//   ②元件自己 scoped style 裡的宣告（HintBox 用這種方式做變體開關，是刻意的寫法）
//   ③JS 端用 :style 綁定注入的（HomeView 的光點動畫每顆位置與時長）
// 只比對第一種的話，本專案現況會噴 9 個假陽性——一個對乾淨程式碼亮紅燈的檢查，
// 只會訓練出「紅了先忽略」的習慣，等於廢掉這個檢查。

import fs from 'node:fs'
import path from 'node:path'

const SRC = path.resolve(import.meta.dirname, '..', 'src')

function walk(dir, exts, out = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, e.name)
    if (e.isDirectory()) walk(full, exts, out)
    else if (exts.includes(path.extname(e.name))) out.push(full)
  }
  return out
}

const files = walk(SRC, ['.vue', '.ts', '.css'])
const used = new Map() // 變數名 → 第一次出現的位置
const defined = new Set()

for (const file of files) {
  const text = fs.readFileSync(file, 'utf8')
  const rel = path.relative(SRC, file)

  for (const m of text.matchAll(/var\((--[a-zA-Z0-9-]+)/g)) {
    if (!used.has(m[1])) used.set(m[1], rel)
  }
  // ①②：CSS 宣告。不錨行首——同一行寫多個宣告（--a: 1px; --b: 2px;）時
  // 錨行首只會抓到第一個，噴出一堆假陽性
  for (const m of text.matchAll(/(--[a-zA-Z0-9-]+)\s*:/g)) defined.add(m[1])
  // ③：JS 端 :style 綁定注入
  for (const m of text.matchAll(/['"](--[a-zA-Z0-9-]+)['"]\s*:/g)) defined.add(m[1])
}

const missing = [...used.entries()].filter(([name]) => !defined.has(name))

if (missing.length > 0) {
  console.error('[check-css-vars] 用到了沒有定義的 CSS 變數，這些宣告會整條靜靜失效：')
  for (const [name, where] of missing) console.error(`  ${name}  (首見於 ${where})`)
  process.exit(1)
}

console.log(`[check-css-vars] 使用 ${used.size} 個變數，定義 ${defined.size} 個，無未定義項目。`)
