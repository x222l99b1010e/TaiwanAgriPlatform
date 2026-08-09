using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.Pet.Dtos.ApiRequests;
using TaiwanAgri.Modules.Pet.Dtos.ApiResponses;
using TaiwanAgri.Modules.Pet.Dtos.Queries;

namespace TaiwanAgri.Modules.Pet.Services
{
	public interface IPetService
	{
		/// <summary>地圖用，不分頁——回傳篩選後的完整清單，供前端 MarkerCluster 聚合</summary>
		Task<List<ShelterAnimalResponseDto>> GetShelterAnimalsAsync(ShelterAnimalQueryDto queryDto);

		/// <summary>動物詳情頁用，單筆查詢。找不到回傳 null（id 打錯或資料已被同步流程移除）</summary>
		Task<ShelterAnimalResponseDto?> GetShelterAnimalByIdAsync(int id);

		/// <summary>收容所詳情頁用，分頁列出單一收容所的全部在養動物。shelterId 不存在或該所目前無在養動物時，
		/// 回傳 TotalCount=0 的空頁（與其他分頁端點對「查無資料」的一貫處理方式相同，不視為錯誤）</summary>
		Task<PagedResult<ShelterAnimalResponseDto>> GetShelterAnimalsByShelterAsync(int shelterId, ShelterAnimalsByShelterQueryDto queryDto);

		Task<PagedResult<OfficialLostPetPostResponseDto>> GetOfficialLostPetPostsAsync(OfficialLostPetPostQueryDto queryDto);

		Task<PagedResult<LegalSpecificPetResponseDto>> GetLegalSpecificPetsAsync(LegalSpecificPetQueryDto queryDto);

		/// <summary>currentUserId 為 null 代表訪客未登入，回傳項目的 IsOwner 一律 false</summary>
		Task<PagedResult<LostPetPostResponseDto>> GetLostPetPostsAsync(LostPetPostQueryDto queryDto, string? currentUserId);

		/// <summary>currentUserId 為 null 代表訪客未登入，回傳項目的 IsOwner 一律 false</summary>
		Task<LostPetPostResponseDto?> GetLostPetPostByIdAsync(int id, string? currentUserId);

		Task<LostPetPostResponseDto> CreateLostPetPostAsync(string userId, CreateLostPetPostRequestDto request);

		/// <summary>false 代表該筆不存在或不屬於此 userId，兩者不區分（避免透過回應差異洩漏他人資料是否存在）</summary>
		Task<bool> UpdateLostPetPostAsync(int id, string userId, UpdateLostPetPostRequestDto request);

		/// <summary>false 代表該筆不存在或不屬於此 userId，兩者不區分</summary>
		Task<bool> DeleteLostPetPostAsync(int id, string userId);
	}
}
