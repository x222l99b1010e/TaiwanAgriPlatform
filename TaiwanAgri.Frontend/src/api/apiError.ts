// src/api/apiError.ts
// 職責：從失敗的請求取出後端寫給使用者看的訊息。
//
// 後端的錯誤回應有兩種形狀，都帶著中文訊息：
//   ① [ApiController] 的請求驗證：{ errors: { 欄位: ["訊息"] } }
//   ② Controller／服務層自己回的：回應本體直接是一個字串
//      （「開始日期不得晚於結束日期」「查詢區間不得超過 1096 天，目前為 2478 天」）
// 一律換成「查詢失敗，請稍後再試」會把講得清清楚楚的原因蓋掉——使用者以為要等一下，
// 實際上是條件要改，重試幾次都一樣。
import axios from 'axios'

/** 回應本體裡的訊息；取不到（網路錯誤、沒有本體、本體不是這兩種形狀）時回 null */
export function responseMessage(error: unknown): string | null {
  if (!axios.isAxiosError(error)) return null

  const data = error.response?.data
  if (typeof data === 'string' && data.trim() !== '') return data

  if (data !== null && typeof data === 'object' && 'errors' in data) {
    const messages = Object.values((data as { errors: Record<string, string[]> }).errors).flat()
    if (messages.length > 0) return messages.join('；')
  }

  return null
}

/**
 * 只取 400（請求本身有問題）的訊息。查詢頁用這個：其他狀態碼（500、逾時）的本體
 * 不是寫給使用者看的，維持各頁原本的通用句
 */
export function badRequestMessage(error: unknown): string | null {
  if (!axios.isAxiosError(error) || error.response?.status !== 400) return null
  return responseMessage(error)
}
