// src/utils/loginForm.ts
// 職責：登入／註冊表單「還不能送出」的原因（純函式）。按鈕的停用與按 Enter 送出共用這一組條件

/**
 * 回傳還不能送出的原因，可以送出時回 null。
 *
 * 按鈕用 :disabled 擋，但欄位上的 @keyup.enter 是直接呼叫送出、不經過按鈕；
 * 兩邊原本各寫一份條件，Enter 那邊只擋了「兩次密碼不一致」，信箱或密碼空著按 Enter 照樣送出
 * （2026-11 總檢查 2、3 號 C 類）。抽成一份，兩邊不會再各說各話
 */
export function loginFormProblem(
  mode: 'login' | 'register',
  email: string,
  password: string,
  confirmPassword: string,
): string | null {
  if (!email || !password) return '請輸入電子信箱與密碼'
  if (mode === 'register') {
    if (!confirmPassword) return '請再輸入一次密碼'
    if (password !== confirmPassword) return '兩次輸入的密碼不一致，請重新確認'
  }
  return null
}
