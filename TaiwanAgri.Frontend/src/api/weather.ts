import axios from 'axios'

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
})

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
}

// ─── 通知 API（獨立，等 JWT 後會加 Authorization header）───

export const notificationApi = {
  /** GET /api/Notification/list?userId=...&page=1 */
  getList(userId: string, page = 1): Promise<UserNotificationDto[]> {
    return apiClient
      .get<UserNotificationDto[]>('/api/Notification/list', { params: { userId, page } })
      .then(res => res.data)
  },

  /** GET /api/Notification/unread-count?userId=... */
  getUnreadCount(userId: string): Promise<UnreadCountDto> {
    return apiClient
      .get<UnreadCountDto>('/api/Notification/unread-count', { params: { userId } })
      .then(res => res.data)
  },

  /** PATCH /api/Notification/{id}/read?userId=... */
  markAsRead(id: number, userId: string): Promise<void> {
    return apiClient
      .patch(`/api/Notification/${id}/read`, null, { params: { userId } })
      .then(() => undefined)
  },
}