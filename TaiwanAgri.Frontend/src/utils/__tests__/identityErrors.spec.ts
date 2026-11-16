import { describe, it, expect } from 'vitest'
import { translateIdentityErrors } from '@/utils/identityErrors'

describe('translateIdentityErrors', () => {
  it('密碼同時缺好幾種字元時，每一條都翻出來（原本只翻第一條）', () => {
    const raw = "Passwords must have at least one non alphanumeric character., "
      + "Passwords must have at least one digit ('0'-'9')., "
      + "Passwords must have at least one uppercase ('A'-'Z')."
    expect(translateIdentityErrors(raw)).toEqual([
      '密碼需包含至少一個特殊符號（如 !@#$）',
      '密碼需包含至少一個數字',
      '密碼需包含至少一個大寫字母',
    ])
  })

  it('使用者名稱與 Email 重複是同一件事，只顯示一次', () => {
    const raw = "Username 'a@b.c' is already taken., Email 'a@b.c' is already taken."
    expect(translateIdentityErrors(raw)).toEqual(['此 Email 已被註冊，請直接登入或使用其他信箱'])
  })

  it('訊息本身裡的逗號不當成分隔', () => {
    const raw = "Username 'x y' is invalid, can only contain letters or digits."
    expect(translateIdentityErrors(raw)).toEqual(['Email 格式不正確'])
  })

  it('中文訊息原樣顯示，不補英文句點', () => {
    expect(translateIdentityErrors('帳號或密碼錯誤')).toEqual(['帳號或密碼錯誤'])
  })
})
