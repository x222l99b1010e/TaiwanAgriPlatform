// src/utils/shelterAnimal.ts
// 職責：ShelterAnimal（收容動物）顯示用的純函式，從 ShelterMapView 抽出來給地圖 popup 與
// 收容所詳情頁共用，避免中文對照表兩處各刻一份、日後改一邊忘了改另一邊

export function animalKindLabel(kind: string): string {
  return { Dog: '狗', Cat: '貓', Other: '其他' }[kind] ?? kind
}

export function animalSexLabel(sex: string): string {
  return { Male: '公', Female: '母', Other: '其他', Unknown: '不明' }[sex] ?? sex
}

/** albumFile 是後端原樣帶出的外部相簿連結字串，可能是空字串或非網址內容，渲染前一律重新判定 */
export function isDisplayableAlbumLink(url: string | null | undefined): boolean {
  return !!url && /^https?:\/\//i.test(url)
}

/**
 * Sterilization／Bacterin 後端都是同一個 TriState enum（Yes/No/Unknown），中文措辭卻不同
 * （結紮用「已/未結紮」、疫苗用「已/未施打」），共用同一個底層對照、只換正負面用字，
 * 不要各寫一份 Yes/No/Unknown 的完整對照表。
 */
function triStateLabel(value: string, positive: string, negative: string): string {
  return { Yes: positive, No: negative, Unknown: '不明' }[value] ?? value
}
export function sterilizationLabel(value: string): string {
  return triStateLabel(value, '已結紮', '未結紮')
}
export function bacterinLabel(value: string): string {
  return triStateLabel(value, '已施打疫苗', '未施打疫苗')
}
