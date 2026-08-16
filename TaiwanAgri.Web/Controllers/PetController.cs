using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaiwanAgri.Modules.Pet.Dtos.ApiRequests;
using TaiwanAgri.Modules.Pet.Dtos.Queries;
using TaiwanAgri.Modules.Pet.Services;

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/pet")]
	[ApiController]
	public class PetController(IPetService petService) : ControllerBase
	{
		// ── 查詢層：三支 Worker 同步進來的資料，未登入即可查（唯讀） ──────────

		/// <summary>收容動物地圖用：一間收容所一筆聚合摘要（含 Dog/Cat/Other 拆分計數），
		/// 取代原本逐隻動物的不分頁清單。結果集本身只有約 30 筆，不需要分頁，也不需要
		/// 防禦性上限或截斷標頭——資料源頭（傳輸量與地圖標記需求不合）已經解決</summary>
		[HttpGet("shelters/summary")]
		public async Task<IActionResult> GetShelterAnimalSummary([FromQuery] ShelterAnimalQueryDto queryDto)
		{
			var result = await petService.GetShelterAnimalSummaryAsync(queryDto);
			return Ok(result);
		}

		/// <summary>動物詳情頁用：單筆查詢，收容所詳情頁表格列點進去看這裡</summary>
		[HttpGet("shelter-animals/{id:int}")]
		public async Task<IActionResult> GetShelterAnimalById(int id)
		{
			var result = await petService.GetShelterAnimalByIdAsync(id);
			return result is null ? NotFound() : Ok(result);
		}

		/// <summary>收容所詳情頁用：地圖 popup「查看全部」連結下鑽到這裡，分頁列出單一收容所的全部在養動物。
		/// shelterId 不存在或該所目前無在養動物一律回傳空頁（TotalCount=0），不視為錯誤——
		/// 跟其他分頁端點對「查無資料」的處理方式一致</summary>
		[HttpGet("shelters/{shelterId:int}/animals")]
		public async Task<IActionResult> GetShelterAnimalsByShelter(int shelterId, [FromQuery] ShelterAnimalsByShelterQueryDto queryDto)
		{
			if (queryDto.Page <= 0)
				return BadRequest("頁碼必須大於 0");
			if (queryDto.PageSize <= 0 || queryDto.PageSize > 100)
				return BadRequest("每頁筆數必須大於 0 且小於等於 100");

			var result = await petService.GetShelterAnimalsByShelterAsync(shelterId, queryDto);
			return Ok(result);
		}

		[HttpGet("official-lost-posts")]
		public async Task<IActionResult> GetOfficialLostPetPosts([FromQuery] OfficialLostPetPostQueryDto queryDto)
		{
			if (queryDto.Page <= 0)
				return BadRequest("頁碼必須大於 0");
			if (queryDto.PageSize <= 0 || queryDto.PageSize > 100)
				return BadRequest("每頁筆數必須大於 0 且小於等於 100");

			var result = await petService.GetOfficialLostPetPostsAsync(queryDto);
			return Ok(result);
		}

		[HttpGet("legal-specific-pets")]
		public async Task<IActionResult> GetLegalSpecificPets([FromQuery] LegalSpecificPetQueryDto queryDto)
		{
			if (queryDto.Page <= 0)
				return BadRequest("頁碼必須大於 0");
			if (queryDto.PageSize <= 0 || queryDto.PageSize > 100)
				return BadRequest("每頁筆數必須大於 0 且小於等於 100");

			var result = await petService.GetLegalSpecificPetsAsync(queryDto);
			return Ok(result);
		}

		// ── LostPetPost：未登入唯讀查詢，登入後可 CRUD 自己的貼文 ──────────

		[HttpGet("lost-pet-posts")]
		public async Task<IActionResult> GetLostPetPosts([FromQuery] LostPetPostQueryDto queryDto)
		{
			if (queryDto.Page <= 0)
				return BadRequest("頁碼必須大於 0");
			if (queryDto.PageSize <= 0 || queryDto.PageSize > 100)
				return BadRequest("每頁筆數必須大於 0 且小於等於 100");

			var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

			// OnlyMine 不登入就查不到「自己」，明確回 401 比默默回空清單清楚——
			// 空清單會讓使用者以為「我沒發過任何貼文」，401 才講得出「你根本沒登入」這件事
			if (queryDto.OnlyMine && currentUserId is null)
				return Unauthorized();

			var result = await petService.GetLostPetPostsAsync(queryDto, currentUserId);
			return Ok(result);
		}

		[HttpGet("lost-pet-posts/{id:int}")]
		public async Task<IActionResult> GetLostPetPostById(int id)
		{
			var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var result = await petService.GetLostPetPostByIdAsync(id, currentUserId);
			return result is null ? NotFound() : Ok(result);
		}

		[HttpPost("lost-pet-posts")]
		[Authorize]
		public async Task<IActionResult> CreateLostPetPost([FromBody] CreateLostPetPostRequestDto request)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();

			if (string.IsNullOrWhiteSpace(request.Phone) && string.IsNullOrWhiteSpace(request.Email))
				return BadRequest("電話與 Email 至少填一項，才能讓拾獲者聯絡到你");

			var result = await petService.CreateLostPetPostAsync(userId, request);
			return CreatedAtAction(nameof(GetLostPetPostById), new { id = result.Id }, result);
		}

		[HttpPut("lost-pet-posts/{id:int}")]
		[Authorize]
		public async Task<IActionResult> UpdateLostPetPost(int id, [FromBody] UpdateLostPetPostRequestDto request)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();

			if (string.IsNullOrWhiteSpace(request.Phone) && string.IsNullOrWhiteSpace(request.Email))
				return BadRequest("電話與 Email 至少填一項，才能讓拾獲者聯絡到你");

			var success = await petService.UpdateLostPetPostAsync(id, userId, request);
			return success ? NoContent() : NotFound();
		}

		[HttpDelete("lost-pet-posts/{id:int}")]
		[Authorize]
		public async Task<IActionResult> DeleteLostPetPost(int id)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();

			var success = await petService.DeleteLostPetPostAsync(id, userId);
			return success ? NoContent() : NotFound();
		}
	}
}
