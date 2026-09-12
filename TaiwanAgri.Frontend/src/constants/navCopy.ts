// src/constants/navCopy.ts
// 職責：導覽用的展示文案——四個模組的英文定譯與一句話說明，以及每個子頁的一句話說明。
// 一律以路由字串為 key：路由是 NavModule 的天然主鍵，改中文名稱時對照表不會跟著失效。
//
// 為什麼不加進 NavModules 資料表：那張表的語意是「哪個角色看得到哪些選單項目」，
// 欄位只有名稱、路徑、圖示、排序與啟用旗標。展示文案塞進去會讓一張權限表同時背兩種責任，
// 而且改一句話就得跑一次 migration。
//
// 為什麼不各自寫在用到的元件裡：首頁的模組卡與四個模組入口頁要用同一批文字，
// 分開寫必然出現「改了一邊、另一邊還是舊的」。
//
// 子頁說明取自各子頁自己的 subtitle，但砍掉了操作指示（「請先選擇市場再查詢」這類）：
// 卡片上的說明要回答「這一頁有什麼」，使用者還沒進去，操作步驟對他是雜訊。

/** 模組的英文定譯，全站唯一一份 */
export const MODULE_NAME_EN: Record<string, string> = {
  '/market': 'MARKET PRICES',
  '/weather': 'SITUATION ROOM',
  '/food-safety': 'FOOD SAFETY',
  '/pet': 'COMPANION ANIMALS',
}

/**
 * 特寫列（`ShowcaseRow`）的 hover 特效種類。
 * 同一款特效放在四個模組上，看久了等於沒有特效——它只證明這裡有動畫，
 * 沒有說出這一塊在做什麼。所以四個模組各綁一種跟自己內容有關的：
 * 行情是走勢線、氣象是雨絲、食安是掃描、寵物是腳印。
 * 'sparks' 是預設的通用款，給對照表裡沒有列到的路由用。
 */
export type ShowcaseEffect = 'sparks' | 'ticks' | 'rain' | 'scan' | 'paws'

export const MODULE_EFFECT: Record<string, ShowcaseEffect> = {
  '/market': 'ticks',
  '/weather': 'rain',
  '/food-safety': 'scan',
  '/pet': 'paws',
}

/** 模組的一句話說明 */
export const MODULE_LEAD: Record<string, string> = {
  '/market': '作物、毛豬、家禽的產地與批發行情，一次比對',
  '/weather': '氣象站觀測、雨量趨勢與病蟲害警報地圖',
  '/food-safety': '農產追溯、農藥違規與有機驗證查詢',
  '/pet': '收容動物地圖與遺失協尋',
}

/**
 * 子頁的英文定譯。全部取自各子頁自己 `title-en` 上已經在用的那一組，
 * 不是另外譯一份——同一頁在兩個地方叫不同名字是最沒必要的不一致。
 */
export const CHILD_NAME_EN: Record<string, string> = {
  '/market/prices': 'CROP PRICES',
  '/market/disasters': 'DISASTER ALERTS',
  '/market/rest-days': 'MARKET CLOSURES',
  '/market/pork': 'HOG PRICES',
  '/market/poultry': 'POULTRY PRICES',

  '/weather/station': 'FIELD WEATHER',
  '/weather/rainfall': 'RAINFALL',
  '/weather/pest-alerts': 'PEST ALERTS',
  '/weather/pest-decade': 'PEST DECADE REPORT',
  '/weather/pesticides': 'PESTICIDES',

  '/food-safety/today-veg': "TODAY'S VEG",
  '/food-safety/traceability': 'TRACEABILITY',
  '/food-safety/pest-violation': 'VIOLATION WALL',
  '/food-safety/organic-certifications': 'ORGANIC CERTS',

  '/pet/shelter-map': 'SHELTER MAP',
  '/pet/lost-pets': 'LOST PETS',
  '/pet/legal-business': 'LICENSED BUSINESSES',
}

/**
 * 子頁的一句話說明。
 * 這裡只列啟用中的子頁；停用的（種子資料裡 IsActive = false 的「智慧提示」）
 * 後端就不會回傳，取不到值的卡片會省略說明那一行，不是顯示空白。
 */
export const CHILD_LEAD: Record<string, string> = {
  '/market/prices': '蔬菜、水果、花卉的每日批發交易均價',
  '/market/disasters': '農業部發布的土石流與潛勢警戒紀錄',
  '/market/rest-days': '各農產品批發市場的休市日期',
  '/market/pork': '毛豬拍賣的成交均價、交易頭數與平均重量',
  '/market/poultry': '雞、鴨、鵝與雞蛋的產地價與批發價',

  '/weather/station': '各地農業氣象站的即時溫度、濕度、風速與 24 小時累積雨量',
  '/weather/rainfall': '指定縣市與區間內，各測站的 24 小時累積雨量走勢',
  '/weather/pest-alerts': '農業部發布的病蟲害警報，以縣市燈號地圖呈現',
  '/weather/pest-decade': '依害蟲名稱查詢各縣市鄉鎮的旬別通報紀錄',
  '/weather/pesticides': '農藥許可證狀態、適用作物與安全採收期',

  '/food-safety/today-veg': '台北一果菜市場的民生蔬菜當日均價',
  '/food-safety/traceability': '以追溯碼查詢蔬果、雞蛋、禽肉的產地與生產者',
  '/food-safety/pest-violation': '農產品農藥殘留抽檢的違規紀錄',
  '/food-safety/organic-certifications': '有機農產品驗證證書的有效狀態、驗證機構與品項範圍',

  '/pet/shelter-map': '全台收容所在養動物的地圖，點標記看該所現況',
  '/pet/lost-pets': '使用者自行張貼的走失／拾獲協尋',
  '/pet/legal-business': '合法寵物業者評鑑資料與官方遺失啟事',
}
