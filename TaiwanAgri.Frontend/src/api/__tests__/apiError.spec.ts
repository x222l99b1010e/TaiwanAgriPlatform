import { describe, it, expect } from 'vitest'
import { AxiosError, AxiosHeaders, type AxiosResponse } from 'axios'
import { badRequestMessage, responseMessage } from '@/api/apiError'

function axiosError(status: number, data: unknown): AxiosError {
  const config = { headers: new AxiosHeaders() }
  const response = { status, data, statusText: '', headers: {}, config } as AxiosResponse
  return new AxiosError('failed', String(status), config, null, response)
}

describe('badRequestMessage', () => {
  it('400 的本體是字串時原樣取出（日期區間的檢查訊息）', () => {
    expect(badRequestMessage(axiosError(400, '查詢區間不得超過 1096 天，目前為 2478 天')))
      .toBe('查詢區間不得超過 1096 天，目前為 2478 天')
  })

  it('400 的請求驗證（errors 物件）把每一條都列出來', () => {
    const data = { title: 'One or more validation errors occurred.', errors: { Page: ['頁碼至少為 1'], PageSize: ['每頁筆數需介於 1 到 100'] } }
    expect(badRequestMessage(axiosError(400, data))).toBe('頁碼至少為 1；每頁筆數需介於 1 到 100')
  })

  it('500 的本體不是寫給使用者看的，回 null 讓頁面用通用句', () => {
    expect(badRequestMessage(axiosError(500, '伺服器發生錯誤'))).toBeNull()
  })

  it('不是請求錯誤（例如程式自己丟的例外）回 null', () => {
    expect(badRequestMessage(new Error('x'))).toBeNull()
  })
})

describe('responseMessage', () => {
  it('不限狀態碼，有訊息就取出（例如 409 的已存在）', () => {
    expect(responseMessage(axiosError(409, '這筆監看項目已存在'))).toBe('這筆監看項目已存在')
  })

  it('空字串本體視為沒有訊息', () => {
    expect(responseMessage(axiosError(400, '   '))).toBeNull()
  })
})
