import apiClient from './apiClient'
import authClient from '@/api/authClient'

// ─── Response DTO 型別 ────────────────────────────────

export interface WeatherStationResponseDto {
  stationName: string
  cityName: string
  townName: string
  observedAt: string          // ISO datetime string
  temperature: number | null
  dailyMaxTemp: number | null
  dailyMinTemp: number | null
  humidity: number | null
  windSpeed: number | null
  windDirection: string | null
  maxGust: number | null
  rainfall24h: number | null
  sunshineHours: number | null
  pressure: number | null
}

export interface RainfallResponseDto {
  stationName: string
  cityName: string
  observedAt: string
  hour3: number | null
  hour6: number | null
  hour12: number | null
  hour24: number | null
}

export interface PestAlertResponseDto {
  id: number
  subject: string
  body: string
  prescription: string | null
  pubDate: string
  issue: string | null
  cities: string[]
  crops: string[]
}

export interface PestDecadeResponseDto {
  pestName: string
  year: number
  month: number
  tenDays: number
  city: string
  town: string
  average: number | null
  proportionIsland: number | null
}

// ─── 農藥查詢（W24）────────────────────────────────────
// 三層結構：成分 → 劑型 → 許可證。
// 為什麼分三層而不是一份扁平的許可證清單，兩個獨立理由：
// 1. 核准用途（usages）是掛在「成分＋含量＋劑型」上，不是掛在許可證上——
//    實測亞滅培 53 張許可證背後只有 2 份相異的使用範圍，攤平會讓同一份內容重複數十次。
// 2. 上游用中文名查詢是 contains 模糊比對，會一併撈到其他成分
//    （查「加保扶」會回「丁基加保扶」，那是另一種農藥）。後端刻意不做精確過濾，
//    改用分組讓使用者自己判斷要看哪一種，但不同成分的資料不會混在同一份清單裡。

/** 第三層：一張許可證＝市面上實際存在的一個產品 */
export interface PesticideLicenseResult {
  permit: string                    // 農藥製／農藥進／農藥原製／農藥原進
  permitNumber: string              // 五位數字字串，含前導零
  brandName: string
  vendor: string
  foreignMaker: string
  expireDateRoc: string             // 民國原字串，如 120-02-19
  expireDate: string | null         // 轉西元後的 ISO 日期；無法解析時 null
  isExpired: boolean                // 與 isRevoked 獨立：未廢止但已到期是真實存在的狀態
  isRevoked: boolean
  revocationType: string | null     // 廢止／撤銷／申請廢止／逾期廢止
  revocationDate: string | null
  licenseImageUrl: string
}

/** 核准用途的單筆紀錄 */
export interface PesticideUsage {
  cropName: string
  pestName: string
  dilution: string
  dosagePerHectare: string
  applicationTiming: string
  applicationInterval: string
  applicationMethod: string
  safeHarvestInterval: string       // 安全採收期：這個功能對使用者最關鍵的欄位
  notes: string
  precautions: string
}

/** 第二層：劑型（＝使用範圍真正的分組單位） */
export interface PesticideFormulation {
  formCode: string                  // 原體為空字串
  formName: string                  // 中文劑型；未收錄的代碼 fallback 顯示原碼
  contents: string                  // 含量原始字串，格式不統一，僅供顯示
  isTechnicalGrade: boolean         // 原體＝工業原料，農民買不到、也沒有使用範圍
  licenses: PesticideLicenseResult[]
  usages: PesticideUsage[]
  /** usages 為空時用來區分「真的沒有核准用途」與「這次沒抓到」 */
  usagesAvailable: boolean
}

/** 第一層：有效成分 */
export interface PesticideIngredient {
  pesticideCode: string             // 如 I225；首字母 I 殺蟲／F 殺菌／H 除草／A 殺蟎／X 混合
  chineseName: string
  englishName: string
  category: string                  // 殺蟲劑／殺菌劑／除草劑…
  chemicalType: string              // 醯胺系／有機磷…
  isExactMatch: boolean             // 是否與查詢條件完全相同（供排序與視覺突顯，不是過濾）
  formulations: PesticideFormulation[]
}

export interface PesticideSearchResult {
  keyword: string
  englishName: string
  ingredients: PesticideIngredient[]
}

/** GET /api/Notification/list 回傳的單筆通知 */
export interface UserNotificationDto {
  id: number
  message: string
  ruleName: string
  triggeredAt: string
  isRead: boolean
}

/** GET /api/Notification/unread-count 回傳 */
export interface UnreadCountDto {
  count: number
}
// ─── API 呼叫函式 ─────────────────────────────────────

export const weatherApi = {
  /** GET /api/Weather/stations?cityName=臺北市 */
  getStations(cityName: string): Promise<WeatherStationResponseDto[]> {
    return apiClient
      .get<WeatherStationResponseDto[]>('/api/Weather/stations', { params: { cityName } })
      .then(res => res.data)
  },

  /** GET /api/Weather/rainfall?cityName=臺北市&startDate=...&endDate=... */
  getRainfall(cityName: string, startDate?: string, endDate?: string): Promise<RainfallResponseDto[]> {
    return apiClient
      .get<RainfallResponseDto[]>('/api/Weather/rainfall', { params: { cityName, startDate, endDate } })
      .then(res => res.data)
  },

  /** GET /api/Pest/alerts?cityName=臺北市&page=1
   *  cityName 可省略（省略 = 全台）
   *  每頁固定 20 筆，page 從 1 開始
   */
  getPestAlerts(cityName?: string, page = 1): Promise<PestAlertResponseDto[]> {
    return apiClient
      .get<PestAlertResponseDto[]>('/api/Pest/alerts', { params: { cityName, page } })
      .then(res => res.data)
  },

  /** GET /api/Pest/pest-names
   *  回傳所有害蟲名稱清單（distinct），供下拉選單使用
   */
  getPestNames(): Promise<string[]> {
    return apiClient
      .get<string[]>('/api/Pest/pest-names')
      .then(res => res.data)
  },

  /** GET /api/Pest/decade-density?pestName=東方果實蠅
   *  依害蟲名稱查詢旬密度歷史資料
   *  回傳依年月旬降序排列，含城市、鄉鎮、平均密度、全島比例
   */
  getPestDecade(pestName: string): Promise<PestDecadeResponseDto[]> {
    return apiClient
      .get<PestDecadeResponseDto[]>('/api/Pest/decade-density', { params: { pestName } })
      .then(res => res.data)
  },

  /** GET /api/Weather/pesticides?keyword=亞滅培&englishName=&includeRevoked=false
   *  中英文名至少要填一個；兩個都填時上游會取交集。
   *  後端回 400 的三種情況（呼叫端要把訊息顯示給使用者，不要一律當成「查詢失敗」）：
   *  兩個都沒填／英文名含非法字元／關鍵字過廣導致上游結果被截斷。
   */
  searchPesticides(
    keyword: string,
    englishName: string,
    includeRevoked = false,
  ): Promise<PesticideSearchResult> {
    return apiClient
      .get<PesticideSearchResult>('/api/Weather/pesticides', {
        // 空字串不送出，避免後端把「有填但是空的」跟「沒填」當成不同情況處理
        params: {
          keyword: keyword || undefined,
          englishName: englishName || undefined,
          includeRevoked,
        },
      })
      .then(res => res.data)
  },
}

// ─── 通知 API（獨立，等 JWT 後會加 Authorization header）───

export const notificationApi = {
  /** GET /api/Notification/list?userId=...&page=1 */
  getList(page = 1): Promise<UserNotificationDto[]> {
    return authClient
      .get<UserNotificationDto[]>('/api/Notification/list', { params: { page } })
      .then(res => res.data)
  },

  /** GET /api/Notification/unread-count?userId=... */
  getUnreadCount(): Promise<UnreadCountDto> {
      return authClient
        .get<UnreadCountDto>('/api/Notification/unread-count')
        .then(res => res.data)
    },

  /** PATCH /api/Notification/{id}/read?userId=... */
  markAsRead(id: number): Promise<void> {
    return authClient
      .patch(`/api/Notification/${id}/read`)
      .then(() => undefined)
  },
}