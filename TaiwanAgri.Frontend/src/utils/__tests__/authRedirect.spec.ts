import { describe, it, expect } from 'vitest'
import { shouldRedirectToLogin } from '../authRedirect'

describe('shouldRedirectToLogin（登入過期時要不要換頁）', () => {
  it('要登入的頁面帶去登入頁：不換頁的話那一頁什麼都載不出來', () => {
    expect(shouldRedirectToLogin({ name: 'profile', meta: { requiresAuth: true } })).toBe(true)
  })

  it('公開頁留在原地：背景的鈴鐺輪詢不能把正在看行情的人拉走', () => {
    expect(shouldRedirectToLogin({ name: undefined, meta: {} })).toBe(false)
  })

  it('已經在登入頁就不再導一次', () => {
    expect(shouldRedirectToLogin({ name: 'login', meta: {} })).toBe(false)
  })
})
