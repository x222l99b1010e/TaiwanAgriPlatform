// src/composables/useLatestRequest.ts
// 職責：請求序號機制，防止「舊回應蓋掉新結果」的競態
//
// 使用者連續調整篩選條件時，較早發出的請求可能較晚回來。
// 每次送出請求前呼叫 next() 取得自己的序號，
// 回應抵達時用 isLatest(mySeq) 確認自己仍是最新一次請求，
// 不是最新的回應直接捨棄（比 AbortController 簡單，且涵蓋「回應已到但不該用」的情況）

export function useLatestRequest() {
  let seq = 0

  /** 發出新請求前呼叫，取得本次請求的序號 */
  function next(): number {
    return ++seq
  }

  /** 回應抵達時確認自己是否仍是最新一次請求 */
  function isLatest(mySeq: number): boolean {
    return mySeq === seq
  }

  return { next, isLatest }
}
