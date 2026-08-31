// TaiwanAgri.Frontend/build/mdiSubsetPlugin.ts
// 職責：建置時把 Material Design Icons 的整包 CSS 裁成「本專案實際用得到的圖示」，
//       並讓 @font-face 只指向 woff2。
//
// 為什麼需要這支外掛：
//   `main.ts` 用 `import '@mdi/font/css/materialdesignicons.css'` 整包引入，這份 CSS
//   本身就有 418 KB（佔整包 CSS 的 88%），裡面是 MDI 提供的 7,448 個圖示的 content
//   對照；專案實際用到的是 74 個。同一份 @font-face 還宣告了 eot／woff／ttf／woff2
//   四種格式，Vite 會把四個檔全部搬進 dist（約 3.6 MB），但現代瀏覽器只會抓 woff2，
//   eot 是 IE8–11 專用、ttf 是舊 Android 用。
//
// 為什麼是建置時掃描，而不是產生一份 CSS 檔 commit 進版控：
//   產生檔會漂移——新增圖示後忘記重跑就是一個空白圖示，而 lint／vitest／vite build
//   全部不會攔（這正是本輪盤點反覆遇到的那類「跑得過但做錯了」的問題）。掃描放在
//   建置流程裡就不存在「忘記重跑」這件事。
//
// 圖示名稱有兩個來源，缺一不可：
//   ①前端原始碼裡的字面值（`class="mdi mdi-magnify"` 這種）。
//   ②**後端種子資料**：導覽列的圖示名稱存在 `NavModules.Icon` 資料表欄位，前端只有
//     ``:class="`mdi ${mod.icon}`"`` 這個動態組字串，名稱本身完全不出現在前端。
//     實測這一類有 21 個，其中 16 個在前端原始碼中一次都沒出現過——只掃 src/ 會讓
//     導覽列的 16 個圖示安靜地變成空白。所以這裡一併掃後端的種子檔。
//
// 掃描是「寧可多留、不可漏留」：多留一個圖示的代價是 CSS 多約 40 個位元組，
// 漏留一個的代價是畫面上一個看不出原因的空白。

import fs from 'node:fs'
import path from 'node:path'
import type { Plugin } from 'vite'

/** MDI 的圖示名稱樣式（含 .mdi-spin／.mdi-18px 這類輔助 class，多留無妨） */
const ICON_NAME = /mdi-[a-z0-9]+(?:-[a-z0-9]+)*/g

/** 單一圖示的字符規則：`.mdi-xxx::before { content: "\FXXXX"; }`。
 *  刻意比對到 content 與反斜線編碼為止，才不會把 `.mdi-spin:before`（單冒號、
 *  宣告的是 animation）這類輔助規則一起裁掉。 */
const GLYPH_RULE = /\.mdi-([a-z0-9-]+)::before\s*\{\s*content:\s*"\\[0-9A-Fa-f]+";?\s*\}\n*/g

function readFilesRecursive(dir: string, exts: string[], out: string[] = []): string[] {
  if (!fs.existsSync(dir)) return out
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) readFilesRecursive(full, exts, out)
    else if (exts.includes(path.extname(entry.name))) out.push(full)
  }
  return out
}

function collectNames(files: string[]): Set<string> {
  const names = new Set<string>()
  for (const file of files) {
    for (const match of fs.readFileSync(file, 'utf8').matchAll(ICON_NAME)) names.add(match[0])
  }
  return names
}

export function mdiSubset(): Plugin {
  /** 前端原始碼掃出來的名稱 */
  let fromSource = new Set<string>()
  /** 後端種子資料掃出來的名稱（導覽列用，前端看不到） */
  let fromSeed = new Set<string>()

  return {
    name: 'taiwanagri:mdi-subset',
    // 要在 Vite 自己的 CSS 處理之前拿到原始內容
    enforce: 'pre',

    buildStart() {
      const frontendRoot = path.resolve(import.meta.dirname, '..')
      const repoRoot = path.resolve(frontendRoot, '..')

      fromSource = collectNames(
        readFilesRecursive(path.join(frontendRoot, 'src'), ['.vue', '.ts', '.css', '.html']),
      )

      // 後端種子：DbInitializer 是正本，docs/sql 的兩支是後補模組的種子腳本。
      // 找不到就直接失敗——這裡如果靜靜地跳過，結果會是導覽列少了圖示卻沒有任何錯誤訊息。
      const seedFiles = [
        path.join(repoRoot, 'TaiwanAgri.Core', 'Infrastructure', 'DbInitializer.cs'),
        ...readFilesRecursive(path.join(repoRoot, 'docs', 'sql'), ['.sql']),
      ].filter(f => fs.existsSync(f))

      if (seedFiles.length === 0) {
        throw new Error(
          '[mdi-subset] 找不到後端的導覽列圖示種子檔（TaiwanAgri.Core/Infrastructure/DbInitializer.cs）。' +
            '導覽列的圖示名稱只存在資料庫，掃不到就會被裁掉並在畫面上變成空白，因此這裡直接中止建置。',
        )
      }
      fromSeed = collectNames(seedFiles)
    },

    transform(code, id) {
      if (!id.includes('materialdesignicons.css')) return null

      // ── 1. @font-face 只留 woff2 ──────────────────────────────────────
      const woff2Url = code.match(/url\("([^"]*materialdesignicons-webfont\.woff2[^"]*)"\)/)?.[1]
      if (!woff2Url) {
        throw new Error('[mdi-subset] 在 MDI 的 @font-face 裡找不到 woff2 來源，套件結構可能已變動。')
      }
      // 原始寫法是「一行 eot fallback + 一行四格式清單」兩個 src 宣告，一起換掉
      let out = code.replace(/src:[^;]+;\s*src:[^;]+;/, `src: url("${woff2Url}") format("woff2");`)

      // ── 2. 只保留用得到的字符規則 ─────────────────────────────────────
      const keep = new Set([...fromSource, ...fromSeed])
      let total = 0
      const kept = new Set<string>()

      out = out.replace(GLYPH_RULE, (rule, name: string) => {
        total++
        const fullName = `mdi-${name}`
        if (!keep.has(fullName)) return ''
        kept.add(fullName)
        return rule
      })

      // ── 3. 種子來源的名稱必須真的存在於 MDI，否則是拼錯或版本對不上 ──
      // 前端原始碼的名稱不做這個檢查：掃出來的集合裡混有 mdi-spin／mdi-18px 這類
      // 輔助 class，它們本來就不是字符規則。種子那 21 個則一定要是真圖示。
      const missing = [...fromSeed].filter(n => !kept.has(n))
      if (missing.length > 0) {
        throw new Error(
          `[mdi-subset] 後端種子指定的圖示在 MDI 裡不存在：${missing.join('、')}。` +
            '這些名稱會被存進 NavModules.Icon 並直接輸出到 class，拼錯的話畫面上是空白。',
        )
      }

      this.environment?.logger?.info?.(
        `[mdi-subset] 保留 ${kept.size} / ${total} 個圖示（其中 ${fromSeed.size} 個來自後端導覽列種子），字型格式只留 woff2`,
      )

      return { code: out, map: null }
    },
  }
}
