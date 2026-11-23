import { describe, it, expect } from 'vitest'
import { redirectAfterLogin, shouldRedirectToLogin } from '../authRedirect'

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

describe('redirectAfterLogin（登入成功後回到哪一頁）', () => {
  it('有 redirect 就回那一頁', () => {
    expect(redirectAfterLogin('/profile')).toBe('/profile')
  })

  it('沒有 redirect 回首頁', () => {
    expect(redirectAfterLogin(undefined)).toBe('/')
    expect(redirectAfterLogin(null)).toBe('/')
    expect(redirectAfterLogin('')).toBe('/')
  })

  it('redirect 帶了兩次（Vue Router 給陣列）取第一個', () => {
    expect(redirectAfterLogin(['/watchlist', '/profile'])).toBe('/watchlist')
  })

  it('陣列的第一個是 null（?redirect&redirect=/a）回首頁，不把 null 當網址', () => {
    expect(redirectAfterLogin([null, '/profile'])).toBe('/')
  })
})
