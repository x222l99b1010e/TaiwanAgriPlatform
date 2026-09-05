import { describe, it, expect } from 'vitest'
import { GLYPH_RULE } from '../mdiSubsetPlugin'

/**
 * GLYPH_RULE 是把 MDI 整包 CSS 裁成子集的那條規則，它唯一有實質風險的地方是
 * **必須裁掉字符規則、但不能裁掉輔助規則**——`.mdi-spin::before` 那一類宣告的是
 * animation，被裁掉的話圖示會停止旋轉，而畫面不會報錯、lint 與 build 也都不會攔。
 *
 * 它同時負責把 content 的碼位捕捉出來給字型子集化用，所以捕獲群組也要一起釘住：
 * 少捕一個碼位＝那個圖示會從字型檔裡消失，畫面上變成空白方框。
 */
describe('GLYPH_RULE', () => {
  /** 每次比對前重置 lastIndex：正規表示式帶 g 旗標時是有狀態的 */
  function matchAll(css: string) {
    GLYPH_RULE.lastIndex = 0
    return [...css.matchAll(GLYPH_RULE)]
  }

  it('比對得到字符規則，並同時捕捉名稱與碼位', () => {
    const m = matchAll('.mdi-magnify::before { content: "\\F0349"; }')
    expect(m).toHaveLength(1)
    expect(m[0][1]).toBe('magnify')
    expect(m[0][2]).toBe('F0349')
  })

  it('圖示名稱含連字號時也比對得到', () => {
    const m = matchAll('.mdi-map-marker-radius::before { content: "\\F0C59"; }')
    expect(m[0][1]).toBe('map-marker-radius')
  })

  it('不會誤裁單冒號的輔助規則（.mdi-spin:before 宣告的是 animation）', () => {
    const css = '.mdi-spin:before { animation: mdi-spin 2s infinite linear; }'
    expect(matchAll(css)).toHaveLength(0)
  })

  it('不會誤裁沒有 content 的其他規則', () => {
    const css = '.mdi-18px.mdi-set, .mdi-18px.mdi:before { font-size: 18px; }'
    expect(matchAll(css)).toHaveLength(0)
  })

  it('整段 CSS 裡只挑出字符規則', () => {
    const css = [
      '.mdi-spin:before { animation: mdi-spin 2s infinite linear; }',
      '.mdi-magnify::before { content: "\\F0349"; }',
      '.mdi-paw::before { content: "\\F0E08"; }',
      '.mdi-rotate-45:before { transform: rotate(45deg); }',
    ].join('\n')
    const names = matchAll(css).map(m => m[1])
    expect(names).toEqual(['magnify', 'paw'])
  })
})
