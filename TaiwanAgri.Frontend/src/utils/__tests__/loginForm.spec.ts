import { describe, it, expect } from 'vitest'
import { loginFormProblem } from '../loginForm'

// 按鈕的停用與按 Enter 送出共用這一組條件。原本 Enter 那邊只擋「兩次密碼不一致」，
// 信箱或密碼空著按 Enter 照樣送出（2026-11 總檢查 2、3 號 C 類）
describe('loginFormProblem（登入／註冊表單還不能送出的原因）', () => {
  it('登入：信箱與密碼都有就可以送出', () => {
    expect(loginFormProblem('login', 'a@example.com', 'P@ss1', '')).toBeNull()
  })

  it('登入：信箱或密碼空著不能送出', () => {
    expect(loginFormProblem('login', '', 'P@ss1', '')).toBe('請輸入電子信箱與密碼')
    expect(loginFormProblem('login', 'a@example.com', '', '')).toBe('請輸入電子信箱與密碼')
  })

  it('登入：確認密碼欄不參與判斷', () => {
    expect(loginFormProblem('login', 'a@example.com', 'P@ss1', '別的')).toBeNull()
  })

  it('註冊：確認密碼空著不能送出', () => {
    expect(loginFormProblem('register', 'a@example.com', 'P@ss1', '')).toBe('請再輸入一次密碼')
  })

  it('註冊：兩次密碼不一致不能送出', () => {
    expect(loginFormProblem('register', 'a@example.com', 'P@ss1', 'P@ss2'))
      .toBe('兩次輸入的密碼不一致，請重新確認')
  })

  it('註冊：三欄都填、兩次一致就可以送出', () => {
    expect(loginFormProblem('register', 'a@example.com', 'P@ss1', 'P@ss1')).toBeNull()
  })
})
