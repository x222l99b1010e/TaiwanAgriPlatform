using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Entities
{
	/// <summary>
	/// PetLoseList 官方遺失啟事同步資料，唯讀。命名加 Official 字首跟未來的
	/// 自建 LostPetPost（使用者登入後自行張貼的遺失啟事，含 CRUD）做出區隔——
	/// 兩者資料來源、欄位形狀、有沒有 CRUD 權限完全不同，混在一個名字下容易搞混。
	/// </summary>
	public class OfficialLostPetPost
	{
		public int Id { get; set; }

		/// <summary>官方資料自帶的序號，全域唯一（不像 ShelterAnimal 需要搭配收容所才唯一）</summary>
		[MaxLength(20)]
		public string KeyNo { get; set; } = string.Empty;

		[MaxLength(30)]
		public string ChipNum { get; set; } = string.Empty;
		[MaxLength(50)]
		public string PetName { get; set; } = string.Empty;
		public AnimalKind Category { get; set; }
		public AnimalSex Sex { get; set; }
		[MaxLength(100)]
		public string Variety { get; set; } = string.Empty;
		[MaxLength(50)]
		public string Coat { get; set; } = string.Empty;
		[MaxLength(50)]
		public string Exterior { get; set; } = string.Empty;
		[MaxLength(500)]
		public string Feature { get; set; } = string.Empty;
		public DateOnly LostTime { get; set; }
		[MaxLength(300)]
		public string LostPlace { get; set; } = string.Empty;
		[MaxLength(50)]
		public string FeederName { get; set; } = string.Empty;
		/// <summary>原始資料偶有失主一次填多支電話（以「、」分隔），故加大長度</summary>
		[MaxLength(100)]
		public string PhoneNum { get; set; } = string.Empty;
		[MaxLength(100)]
		public string EMail { get; set; } = string.Empty;

		[Column(TypeName = "nvarchar(max)")]
		public string PictureUrl { get; set; } = string.Empty;

		[Column(TypeName = "datetime2")]
		public DateTime SyncedAt { get; set; }
	}
}
