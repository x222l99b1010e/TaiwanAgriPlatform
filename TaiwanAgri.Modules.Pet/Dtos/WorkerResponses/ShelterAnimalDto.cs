using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Pet.Dtos.WorkerResponses
{
	/// <summary>
	/// 收容動物資訊 API 回應 DTO（AnimalRecognition，新制）
	/// 忠實承接原始資料形狀，型別轉換（enum／DateOnly）與清洗（Trim）延後至 MapToEntity 執行
	/// </summary>
	public class ShelterAnimalDto
	{
		[JsonPropertyName("animal_subid")]
		public string AnimalSubId { get; set; } = string.Empty;

		[JsonPropertyName("animal_shelter_pkid")]
		public int AnimalShelterPkId { get; set; }

		[JsonPropertyName("animal_kind")]
		public string AnimalKind { get; set; } = string.Empty;

		[JsonPropertyName("animal_Variety")]
		public string AnimalVariety { get; set; } = string.Empty;

		[JsonPropertyName("animal_sex")]
		public string AnimalSex { get; set; } = string.Empty;

		[JsonPropertyName("animal_bodytype")]
		public string AnimalBodyType { get; set; } = string.Empty;

		[JsonPropertyName("animal_colour")]
		public string AnimalColour { get; set; } = string.Empty;

		[JsonPropertyName("animal_age")]
		public string AnimalAge { get; set; } = string.Empty;

		[JsonPropertyName("animal_sterilization")]
		public string AnimalSterilization { get; set; } = string.Empty;

		[JsonPropertyName("animal_bacterin")]
		public string AnimalBacterin { get; set; } = string.Empty;

		[JsonPropertyName("animal_foundplace")]
		public string AnimalFoundPlace { get; set; } = string.Empty;

		[JsonPropertyName("animal_remark")]
		public string AnimalRemark { get; set; } = string.Empty;

		/// <summary>
		/// 原始格式為 "2026-06-22"，保留字串，轉換延後至 Entity 映射層
		/// </summary>
		[JsonPropertyName("animal_opendate")]
		public string AnimalOpenDate { get; set; } = string.Empty;

		/// <summary>
		/// 原始格式為 "2026/06/22"，保留字串，轉換延後至 Entity 映射層
		/// </summary>
		[JsonPropertyName("animal_createtime")]
		public string AnimalCreateTime { get; set; } = string.Empty;

		[JsonPropertyName("album_file")]
		public string AlbumFile { get; set; } = string.Empty;
		/// <summary>
		/// 原始格式為 "2026/07/23"，保留字串，轉換延後至 Entity 映射層
		/// </summary>
		[JsonPropertyName("animal_update")]
		public string AnimalUpdate { get; set; } = string.Empty;

		/// <summary>
		/// 僅供 EnsureSheltersExistAsync 建立 placeholder Shelter 使用，
		/// 不會映射進 ShelterAnimal（避免每筆動物重複存收容所資訊，見 Shelter 導覽屬性）
		/// </summary>
		[JsonPropertyName("shelter_name")]
		public string ShelterName { get; set; } = string.Empty;

		[JsonPropertyName("shelter_address")]
		public string ShelterAddress { get; set; } = string.Empty;

		[JsonPropertyName("shelter_tel")]
		public string ShelterTel { get; set; } = string.Empty;
	}
}