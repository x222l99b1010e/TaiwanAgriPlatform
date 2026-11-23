import { describe, it, expect } from 'vitest'
import { parseStoredUser } from '../storedUser'

// 這段在 authStore 建立時就執行。原本直接 JSON.parse，存的內容壞掉時丟例外，
// 整個前端起不來、重新整理也一樣（2026-11 總檢查 3 號 C 類）
describe('parseStoredUser（讀回瀏覽器存的使用者資料）', () => {
  it('沒有存：沒登入，不算壞掉', () => {
    expect(parseStoredUser(null)).toEqual({ user: null, corrupted: false })
  })

  it('正常存下的資料讀得回來', () => {
    const user = { email: 'a@example.com', displayName: '測試農友', role: 'Guest' }
    expect(parseStoredUser(JSON.stringify(user))).toEqual({ user, corrupted: false })
  })

  it('不是合法的 JSON：不丟例外，回報壞掉', () => {
    expect(parseStoredUser('{壞掉的')).toEqual({ user: null, corrupted: true })
  })

  it('是合法的 JSON 但形狀不對：一樣回報壞掉', () => {
    for (const raw of ['123', '"字串"', '[]', 'null', '{"role":"Guest"}']) {
      expect(parseStoredUser(raw)).toEqual({ user: null, corrupted: true })
    }
  })
})
