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

		/// <summary>回應標頭：本次結果是否因為觸及地圖標記上限而被截斷（"true"／"false"）</summary>
		private const string ResultTruncatedHeader = "X-Result-Truncated";

		[HttpGet("shelter-animals")]
		public async Task<IActionResult> GetShelterAnimals([FromQuery] ShelterAnimalQueryDto queryDto)
		{
			var result = await petService.GetShelterAnimalsAsync(queryDto);

			// 這支端點刻意不分頁（地圖要完整清單），但有防禦上限，回傳筆數觸頂就代表資料被切掉了。
			// 由後端直接回答「有沒有被截斷」而不是讓前端拿筆數去比對一份自己維護的上限常數——
			// 上限值只存在於後端一處，日後調整不需要同步修改前端，也不會有兩邊走鐘的風險。
			Response.Headers[ResultTruncatedHeader] =
				(result.Count >= PetService.MapMarkerSafetyLimit).ToString().ToLowerInvariant();

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
