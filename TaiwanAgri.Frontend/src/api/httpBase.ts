// src/api/httpBase.ts
// 職責：兩支 axios instance 的共用設定與共用常數。
//
// 為什麼要抽出來：apiClient 與 authClient 原本各自寫一份 axios.create，兩邊會分岔；
// 而 token 的 localStorage key 更是散在 authClient 與 authStore 兩個檔共四處，
// 改一邊就會壞掉且不會有任何錯誤訊息。

import axios, { type AxiosInstance } from 'axios'

/** JWT 在 localStorage 的 key。authClient 與 authStore 一律引用這個常數，不要各寫字面值 */
export const TOKEN_STORAGE_KEY = 'token'
/** 使用者資訊在 localStorage 的 key */
export const USER_STORAGE_KEY = 'user'

/**
 * 單一請求的等待上限。沒有這個上限時，後端卡住（不是回錯，是不回應）
 * 會讓畫面永遠停在載入中——使用者看到的是「當掉」，而不是「失敗」，
 * 連重試都不知道要重試。
 */
const REQUEST_TIMEOUT_MS = 15_000

/** 收到 401 時要做的事，由 app 啟動時註冊（放在這裡會與 router／store 互相 import） */
let onUnauthorized: (() => void) | null = null

export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler
}

export function createHttpClient(options: { withAuth: boolean }): AxiosInstance {
  const client = axios.create({
    baseURL: import.meta.env.VITE_API_BASE_URL,
    headers: { 'Content-Type': 'application/json' },
    timeout: REQUEST_TIMEOUT_MS,
  })

  if (options.withAuth) {
    client.interceptors.request.use(config => {
      const token = localStorage.getItem(TOKEN_STORAGE_KEY)
      if (token) {
        config.headers.Authorization = `Bearer ${token}`
      }
      return config
    })
  }

  // 401 統一處理：token 過期時每個 store 各自面對 401 的話，畫面會停在
  // 「載入失敗」而不會把人帶去重新登入，使用者不知道自己其實只是登入過期了。
  // 路由守衛只看 token 存在與否、不看有效性，所以要在這裡把過期的 token 清掉，
  // 守衛下一次才擋得住。
  client.interceptors.response.use(
    response => response,
    (error: unknown) => {
      if (axios.isAxiosError(error) && error.response?.status === 401) {
        localStorage.removeItem(TOKEN_STORAGE_KEY)
        localStorage.removeItem(USER_STORAGE_KEY)
        onUnauthorized?.()
      }
      return Promise.reject(error)
    },
  )

  return client
}
