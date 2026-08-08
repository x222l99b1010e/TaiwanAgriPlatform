using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Entities
{
	/// <summary>
	/// 合法特定寵物業名單。跟 ShelterAnimal／OfficialLostPetPost 不同，這張表用 upsert
	/// 而非單純 insert-only——業者的 StateFlag／RankGrade 等狀態欄位會隨時間變動
	/// （例如今年評優等、明年評甲等；正常營業後來歇業），只新增不更新會讓資料悄悄過期失真。
	/// </summary>
	public class LegalSpecificPet
	{
		public int Id { get; set; }

		/// <summary>官方資料自帶的序號（原始欄位 ID），唯一鍵</summary>
		[MaxLength(20)]
		public string ExternalId { get; set; } = string.Empty;

		/// <summary>
		/// 縣市名稱（如「新北市」），已用真實資料反查 legaltype 代碼對照表後直接轉存中文，
		/// 不存原始代碼（比照 Shelter.County 存人看得懂字串的既有慣例）
		/// </summary>
		[MaxLength(20)]
		public string County { get; set; } = string.Empty;

		/// <summary>營業項目組合字串（如 "ABC"），A=繁殖 B=買賣 C=寄養，維持原始字串不拆欄位</summary>
		[MaxLength(10)]
		public string BusinessItems { get; set; } = string.Empty;

		public LegalPetAnimalType AnimalType { get; set; }
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;
		[MaxLength(300)]
		public string Address { get; set; } = string.Empty;
		[MaxLength(100)]
		public string PermitNumber { get; set; } = string.Empty;
		public DateOnly? PermitValidDate { get; set; }
		[MaxLength(50)]
		public string OwnerName { get; set; } = string.Empty;
		[MaxLength(50)]
		public string ResponsibleStaffName { get; set; } = string.Empty;
		[MaxLength(10)]
		public string RankYear { get; set; } = string.Empty;
		public LegalPetRankGrade RankGrade { get; set; }
		public TriState RankDataConfirmed { get; set; }
		public TriState RankDescriptionConfirmed { get; set; }
		[MaxLength(500)]
		public string RankText { get; set; } = string.Empty;
		public LegalPetStateFlag StateFlag { get; set; }

		[Column(TypeName = "datetime2")]
		public DateTime SyncedAt { get; set; }
	}
}
