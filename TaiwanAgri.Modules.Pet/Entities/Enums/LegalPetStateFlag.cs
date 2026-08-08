namespace TaiwanAgri.Modules.Pet.Entities.Enums
{
	/// <summary>
	/// 商家狀態代碼（state_flag）。官方 API 介接說明書這張表的 PDF 轉文字順序錯位，
	/// 改用真實資料交叉比對 validdate（許可證到期日）驗證語意（1000 筆樣本，2026-07-29）：
	/// N（435 筆，93%未過期）→ 營業中，高信心；B（65 筆，98%已過期）→ 廢止，高信心；
	/// P（487 筆，90%已過期）→ 歇業，中信心；S（13 筆，69%已過期）→ 停業，中信心
	/// （停業依法規屬暫時性狀態、不影響證照效期，過期比例較分散符合此特性）。
	/// 之後找到官方確切對照表可回頭修正 P/S 的判斷。
	/// </summary>
	public enum LegalPetStateFlag
	{
		Operating,
		Closed,
		Suspended,
		Revoked,
		Unknown
	}
}
