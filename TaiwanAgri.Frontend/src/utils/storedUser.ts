// src/utils/storedUser.ts
// 職責：把瀏覽器存的使用者資料（localStorage 裡的 JSON 字串）讀回物件；讀不出來時明講「壞掉了」（純函式）
import type { AuthResponseDto } from '@/api/auth'

export type StoredUser = Omit<AuthResponseDto, 'token'>

/**
 * - 沒有存（`null`）：`{ user: null, corrupted: false }`，就是沒登入
 * - 是物件、而且有 email 字串：`{ user, corrupted: false }`
 * - 不是合法的 JSON，或形狀不對（數字、陣列、`null`、少了 email）：`{ user: null, corrupted: true }`
 *
 * 原本直接 `JSON.parse`，而這段在 authStore 建立時就執行：內容壞掉（被手動改過、別的版本寫的格式）時丟例外，
 * 整個前端起不來，重新整理也一樣，只能自己去清瀏覽器資料（2026-11 總檢查 3 號 C 類）
 */
export function parseStoredUser(raw: string | null): { user: StoredUser | null; corrupted: boolean } {
  if (raw === null) return { user: null, corrupted: false }
  try {
    const parsed: unknown = JSON.parse(raw)
    if (
      parsed !== null &&
      typeof parsed === 'object' &&
      !Array.isArray(parsed) &&
      typeof (parsed as { email?: unknown }).email === 'string'
    ) {
      return { user: parsed as StoredUser, corrupted: false }
    }
  } catch {
    // 不是合法的 JSON：跟形狀不對一樣，往下當成壞掉
  }
  return { user: null, corrupted: true }
}
