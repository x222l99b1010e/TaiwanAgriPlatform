// src/utils/lostPetPost.ts
// 職責：LostPetPost（自建遺失啟事）卡片渲染共用的純函式，抽出來給列表頁與詳情頁共用，
// 避免同一段判斷邏輯在兩個地方各寫一次、日後改一邊忘了改另一邊（避免重蹈「只寫不讀」的教訓）

import type { LostPetPostStatusValue } from '@/api/pet'

export const lostPetPostStatusOptions: { value: LostPetPostStatusValue | ''; label: string }[] = [
  { value: '',          label: '全部' },
  { value: 'Searching', label: '協尋中' },
  { value: 'Found',     label: '已找到' },
  { value: 'Withdrawn', label: '已撤回' },
]

export function lostPetPostStatusLabel(status: LostPetPostStatusValue): string {
  return lostPetPostStatusOptions.find(o => o.value === status)?.label ?? status
}

export function lostPetPostStatusClass(status: LostPetPostStatusValue): string {
  return { Searching: 'searching', Found: 'found', Withdrawn: 'withdrawn' }[status]
}

/** "2026-08-05T12:00:00" -> "2026-08-05" */
export function formatLostPetPostDate(iso: string): string {
  return iso.slice(0, 10)
}

/**
 * PhotoUrl 存的是使用者自貼的外部圖床連結，DB 欄位是 nvarchar(max)、早期也沒有驗證，
 * 因此裡面可能是任何字串（實測有純文字的舊資料）。渲染前一律重新判定，不能假設它是網址。
 * 只放行 http/https：其餘協定（data:／javascript: 之類）不該出現在「外部圖床連結」這個語意的欄位。
 */
export function isDisplayableImageUrl(url: string | null | undefined): boolean {
  if (!url) return false
  try {
    const protocol = new URL(url).protocol
    return protocol === 'http:' || protocol === 'https:'
  } catch {
    return false // URL 建構失敗 = 不是合法網址（例如舊資料存的純文字）
  }
}

/** Google 地圖連結：協尋情境真正需要的是「怎麼過去」，外部地圖能直接導航，站內小圖做不到 */
export function googleMapsLink(latitude: number, longitude: number): string {
  return `https://www.google.com/maps?q=${latitude},${longitude}`
}
