// src/utils/identityErrors.ts
// 職責：把後端（ASP.NET Core Identity）回的英文錯誤翻成中文，登入頁用。

export function translateIdentityError(msg: string): string {
  if (msg.includes('already taken')) return '此 Email 已被註冊，請直接登入或使用其他信箱'
  if (msg.includes('is invalid')) return 'Email 格式不正確'
  if (msg.includes('least one non alphanumeric')) return '密碼需包含至少一個特殊符號（如 !@#$）'
  if (msg.includes('least one digit')) return '密碼需包含至少一個數字'
  if (msg.includes('least one uppercase')) return '密碼需包含至少一個大寫字母'
  if (msg.includes('least one lowercase')) return '密碼需包含至少一個小寫字母'
  if (msg.includes('least') && msg.includes('characters')) return '密碼長度不足，請至少輸入 6 個字元'
  if (msg.includes('Invalid login attempt')) return '帳號或密碼錯誤，請重新確認'
  if (msg.includes('locked out')) return '帳號已被鎖定，請稍後再試'
  return msg  // 其他未知錯誤原樣顯示
}

/**
 * 後端註冊失敗時，把 Identity 每一條不合格規則的英文訊息用「, 」接成一句（每條以句點結尾）。
 * 要先切開再逐條翻譯：原本整句只比對出第一條，密碼同時缺三種字元時，要被擋三次才知道全部。
 * 只在句點後面的逗號切，訊息本身裡的逗號（"…is invalid, can only contain…"）不受影響
 */
export function translateIdentityErrors(raw: string): string[] {
  const parts = raw.split(/(?<=\.),\s*/).map(s => s.trim()).filter(Boolean)
  return [...new Set(parts.map(translateIdentityError))]
}
