using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.Pet.Dtos.ApiRequests;
using TaiwanAgri.Modules.Pet.Dtos.ApiResponses;
using TaiwanAgri.Modules.Pet.Dtos.Queries;

namespace TaiwanAgri.Modules.Pet.Services
{
	public interface IPetService
	{
		/// <summary>地圖用，一間收容所一筆聚合摘要（含 Dog/Cat/Other 拆分計數），取代原本逐隻動物的
		/// 不分頁清單——結果集本身只有約 30 筆，不需要分頁也不需要防禦性上限</summary>
		Task<List<ShelterAnimalSummaryDto>> GetShelterAnimalSummaryAsync(ShelterAnimalQueryDto queryDto, CancellationToken cancellationToken = default);

		/// <summary>動物詳情頁用，單筆查詢。找不到回傳 null（id 打錯或資料已被同步流程移除）</summary>
		Task<ShelterAnimalResponseDto?> GetShelterAnimalByIdAsync(int id, CancellationToken cancellationToken = default);

		/// <summary>收容所詳情頁用，分頁列出單一收容所的全部在養動物。shelterId 不存在或該所目前無在養動物時，
		/// 回傳 TotalCount=0 的空頁（與其他分頁端點對「查無資料」的一貫處理方式相同，不視為錯誤）</summary>
		Task<PagedResult<ShelterAnimalResponseDto>> GetShelterAnimalsByShelterAsync(int shelterId, ShelterAnimalsByShelterQueryDto queryDto, CancellationToken cancellationToken = default);

		Task<PagedResult<OfficialLostPetPostResponseDto>> GetOfficialLostPetPostsAsync(OfficialLostPetPostQueryDto queryDto, CancellationToken cancellationToken = default);

		Task<PagedResult<LegalSpecificPetResponseDto>> GetLegalSpecificPetsAsync(LegalSpecificPetQueryDto queryDto, CancellationToken cancellationToken = default);

		/// <summary>currentUserId 為 null 代表訪客未登入，回傳項目的 IsOwner 一律 false</summary>
		Task<PagedResult<LostPetPostResponseDto>> GetLostPetPostsAsync(LostPetPostQueryDto queryDto, string? currentUserId, CancellationToken cancellationToken = default);

		/// <summary>currentUserId 為 null 代表訪客未登入，回傳項目的 IsOwner 一律 false</summary>
		Task<LostPetPostResponseDto?> GetLostPetPostByIdAsync(int id, string? currentUserId, CancellationToken cancellationToken = default);

		Task<LostPetPostResponseDto> CreateLostPetPostAsync(string userId, CreateLostPetPostRequestDto request, CancellationToken cancellationToken = default);

		/// <summary>false 代表該筆不存在或不屬於此 userId，兩者不區分（避免透過回應差異洩漏他人資料是否存在）</summary>
		Task<bool> UpdateLostPetPostAsync(int id, string userId, UpdateLostPetPostRequestDto request, CancellationToken cancellationToken = default);

		/// <summary>false 代表該筆不存在或不屬於此 userId，兩者不區分</summary>
		Task<bool> DeleteLostPetPostAsync(int id, string userId, CancellationToken cancellationToken = default);
	}
}
