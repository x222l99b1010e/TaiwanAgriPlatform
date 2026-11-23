import { describe, it, expect } from 'vitest'
import { isHttpUrl } from '../url'

describe('isHttpUrl（外部資料的欄位是不是網址）', () => {
  it('http 與 https 開頭的是網址，不分大小寫', () => {
    expect(isHttpUrl('https://data.moa.gov.tw/x')).toBe(true)
    expect(isHttpUrl('http://example.com')).toBe(true)
    expect(isHttpUrl('HTTPS://EXAMPLE.COM')).toBe(true)
  })

  it('一般文字、空字串、null 不是網址', () => {
    expect(isHttpUrl('臺北市第一果菜批發市場')).toBe(false)
    expect(isHttpUrl('')).toBe(false)
    expect(isHttpUrl(null)).toBe(false)
    expect(isHttpUrl(undefined)).toBe(false)
  })

  it('其他協定（例如 javascript:）不會變成可以點的連結', () => {
    expect(isHttpUrl('javascript:alert(1)')).toBe(false)
    expect(isHttpUrl('ftp://example.com')).toBe(false)
  })
})
