// src/utils/cropAutocomplete.ts
// 職責：作物自動完成（components/CropAutocomplete.vue）的純邏輯——
// 依輸入過濾建議、方向鍵移動反白。抽出來是為了能直接測，元件只負責接事件與畫面。

export interface CropOption {
  cropCode: string
  cropName: string
}

/** 名稱包含輸入文字、且不在排除名單（已選的）裡的作物，最多 limit 筆；空白輸入不給建議 */
export function cropSuggestions(
  options: readonly CropOption[],
  text: string,
  excludeCodes: readonly string[] = [],
  limit = 10,
): CropOption[] {
  const q = text.trim()
  if (!q) return []
  return options
    .filter(c => c.cropName.includes(q) && !excludeCodes.includes(c.cropCode))
    .slice(0, limit)
}

/**
 * 按 ↓／↑ 之後反白哪一筆。current 是 -1 代表還沒有反白：↓ 到第一筆、↑ 到最後一筆；
 * 到頭再按就繞回另一端（清單最多十幾筆，繞回比卡住好用）
 */
export function nextActiveIndex(current: number, count: number, direction: 1 | -1): number {
  if (count <= 0) return -1
  if (current < 0) return direction === 1 ? 0 : count - 1
  return (current + direction + count) % count
}
