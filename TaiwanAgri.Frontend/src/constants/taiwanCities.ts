// src/constants/taiwanCities.ts
// 職責：22 個縣市的正式名稱與順序（一律用「臺」）。全站的縣市下拉與病蟲害地圖都從這一份取
//
// 原本有四份（CitySelector、天災頁、農場設定頁、病蟲害地圖的座標表），其中一份還用「台」
// （2026-11 總檢查 1-C1）。農業部資料裡的「台」由後端換掉（TaiwanCityName），前端只用這一套

export const TAIWAN_CITIES = [
  '臺北市', '新北市', '桃園市', '臺中市', '臺南市', '高雄市',
  '基隆市', '新竹市', '嘉義市', '新竹縣', '苗栗縣', '彰化縣',
  '南投縣', '雲林縣', '嘉義縣', '屏東縣', '宜蘭縣', '花蓮縣',
  '臺東縣', '澎湖縣', '金門縣', '連江縣',
] as const

export type TaiwanCity = (typeof TAIWAN_CITIES)[number]
