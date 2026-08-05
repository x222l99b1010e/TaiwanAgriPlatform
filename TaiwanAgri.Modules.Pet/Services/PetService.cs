using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.Pet.Data;
using TaiwanAgri.Modules.Pet.Dtos.ApiRequests;
using TaiwanAgri.Modules.Pet.Dtos.ApiResponses;
using TaiwanAgri.Modules.Pet.Dtos.Queries;
using TaiwanAgri.Modules.Pet.Entities;
using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Services
{
	public class PetService(PetDbContext context, TimeProvider timeProvider) : IPetService
	{
		/// <summary>地圖用防禦性上限——正常情況下（篩選後）不會踩到，純粹防查詢失控爆量</summary>
		private const int MapMarkerSafetyLimit = 2000;

		public async Task<List<ShelterAnimalResponseDto>> GetShelterAnimalsAsync(ShelterAnimalQueryDto queryDto)
		{
			var query = context.ShelterAnimals.Include(x => x.Shelter).AsQueryable();

			if (!string.IsNullOrWhiteSpace(queryDto.County))
				query = query.Where(x => x.Shelter.County == queryDto.County);

			if (queryDto.Kind.HasValue)
				query = query.Where(x => x.Kind == queryDto.Kind.Value);

			return await query
				.OrderBy(x => x.Id)
				.Take(MapMarkerSafetyLimit)
				.Select(x => new ShelterAnimalResponseDto
				{
					Id = x.Id,
					AnimalSubId = x.AnimalSubId,
					ShelterName = x.Shelter.Name,
					ShelterAddress = x.Shelter.Address,
					County = x.Shelter.County,
					Latitude = x.Shelter.Latitude,
					Longitude = x.Shelter.Longitude,
					Kind = x.Kind.ToString(),
					Sex = x.Sex.ToString(),
					BodyType = x.BodyType.ToString(),
					Age = x.Age.ToString(),
					Sterilization = x.Sterilization.ToString(),
					Bacterin = x.Bacterin.ToString(),
					Variety = x.Variety,
					Colour = x.Colour,
					FoundPlace = x.FoundPlace,
					Remark = x.Remark,
					OpenDate = x.OpenDate,
					CreatedTime = x.CreatedTime,
					AlbumFile = x.AlbumFile
				})
				.ToListAsync();
		}

		public async Task<PagedResult<OfficialLostPetPostResponseDto>> GetOfficialLostPetPostsAsync(OfficialLostPetPostQueryDto queryDto)
		{
			var query = context.OfficialLostPetPosts.AsQueryable();

			if (queryDto.Category.HasValue)
				query = query.Where(x => x.Category == queryDto.Category.Value);

			if (queryDto.Sex.HasValue)
				query = query.Where(x => x.Sex == queryDto.Sex.Value);

			var totalCount = await query.CountAsync();

			// 縣市篩選刻意不做：這張表沒有結構化的 County 欄位，只有自由文字 LostPlace，
			// 字串比對會跟 B3 技術債（nvarchar LIKE 全表掃描）同一類問題，且不保證準確
			// （owner 2026-08-06 裁示：不划算，不做）。
			// ThenByDescending 只能接在 IOrderedQueryable 後面，所以 tie-breaker 要寫在每個分支裡，
			// 不能像篩選條件那樣共用一段接在後面
			IOrderedQueryable<OfficialLostPetPost> orderedQuery = queryDto.SortBy switch
			{
				OfficialLostPetPostSortBy.Category => queryDto.SortDescending
					? query.OrderByDescending(x => x.Category).ThenByDescending(x => x.Id)
					: query.OrderBy(x => x.Category).ThenByDescending(x => x.Id),
				OfficialLostPetPostSortBy.Sex => queryDto.SortDescending
					? query.OrderByDescending(x => x.Sex).ThenByDescending(x => x.Id)
					: query.OrderBy(x => x.Sex).ThenByDescending(x => x.Id),
				_ => queryDto.SortDescending
					? query.OrderByDescending(x => x.LostTime).ThenByDescending(x => x.Id)
					: query.OrderBy(x => x.LostTime).ThenByDescending(x => x.Id),
			};

			var items = await orderedQuery
				.Skip((queryDto.Page - 1) * queryDto.PageSize)
				.Take(queryDto.PageSize)
				.Select(x => new OfficialLostPetPostResponseDto
				{
					Id = x.Id,
					KeyNo = x.KeyNo,
					ChipNum = x.ChipNum,
					PetName = x.PetName,
					Category = x.Category.ToString(),
					Sex = x.Sex.ToString(),
					Variety = x.Variety,
					Coat = x.Coat,
					Exterior = x.Exterior,
					Feature = x.Feature,
					LostTime = x.LostTime,
					LostPlace = x.LostPlace,
					FeederName = x.FeederName,
					PhoneNum = x.PhoneNum,
					EMail = x.EMail,
					PictureUrl = x.PictureUrl
				})
				.ToListAsync();

			return new PagedResult<OfficialLostPetPostResponseDto>
			{
				Items = items,
				TotalCount = totalCount,
				Page = queryDto.Page,
				PageSize = queryDto.PageSize,
				TotalPages = (int)Math.Ceiling((double)totalCount / queryDto.PageSize)
			};
		}

		public async Task<PagedResult<LegalSpecificPetResponseDto>> GetLegalSpecificPetsAsync(LegalSpecificPetQueryDto queryDto)
		{
			var query = context.LegalSpecificPets.AsQueryable();

			if (!string.IsNullOrWhiteSpace(queryDto.County))
				query = query.Where(x => x.County == queryDto.County);

			if (queryDto.AnimalType.HasValue)
				query = query.Where(x => x.AnimalType == queryDto.AnimalType.Value);

			if (queryDto.RankGrade.HasValue)
				query = query.Where(x => x.RankGrade == queryDto.RankGrade.Value);

			if (queryDto.StateFlag.HasValue)
				query = query.Where(x => x.StateFlag == queryDto.StateFlag.Value);

			// BusinessItems 是像 "ABC" 這種代碼組合字串（欄位上限 10 字元，不是長文字），
			// Contains 在這裡不是 B3 那種 nvarchar(max) 全表掃描等級的效能疑慮
			if (!string.IsNullOrWhiteSpace(queryDto.BusinessItem))
				query = query.Where(x => x.BusinessItems.Contains(queryDto.BusinessItem));

			var totalCount = await query.CountAsync();

			// 刻意不做「許可證效期是否過期」的布林篩選，改成可排序：PermitValidDate 是 DateOnly?，
			// null（查無效期資料）該算過期還是未過期沒有一翻兩瞪眼的答案，排序讓過期的自然排到
			// 一端、使用者自己看得出來，不用回答這個 null 語意問題（owner 2026-08-06 裁示，選項 3）
			IOrderedQueryable<LegalSpecificPet> orderedQuery = queryDto.SortBy switch
			{
				LegalSpecificPetSortBy.PermitValidDate => queryDto.SortDescending
					? query.OrderByDescending(x => x.PermitValidDate).ThenBy(x => x.Id)
					: query.OrderBy(x => x.PermitValidDate).ThenBy(x => x.Id),
				LegalSpecificPetSortBy.RankGrade => queryDto.SortDescending
					? query.OrderByDescending(x => x.RankGrade).ThenBy(x => x.Id)
					: query.OrderBy(x => x.RankGrade).ThenBy(x => x.Id),
				_ => queryDto.SortDescending
					? query.OrderByDescending(x => x.Name).ThenBy(x => x.Id)
					: query.OrderBy(x => x.Name).ThenBy(x => x.Id),
			};

			var items = await orderedQuery
				.Skip((queryDto.Page - 1) * queryDto.PageSize)
				.Take(queryDto.PageSize)
				.Select(x => new LegalSpecificPetResponseDto
				{
					Id = x.Id,
					ExternalId = x.ExternalId,
					County = x.County,
					BusinessItems = x.BusinessItems,
					AnimalType = x.AnimalType.ToString(),
					Name = x.Name,
					Address = x.Address,
					PermitNumber = x.PermitNumber,
					PermitValidDate = x.PermitValidDate,
					OwnerName = x.OwnerName,
					ResponsibleStaffName = x.ResponsibleStaffName,
					RankYear = x.RankYear,
					RankGrade = x.RankGrade.ToString(),
					RankText = x.RankText,
					StateFlag = x.StateFlag.ToString()
				})
				.ToListAsync();

			return new PagedResult<LegalSpecificPetResponseDto>
			{
				Items = items,
				TotalCount = totalCount,
				Page = queryDto.Page,
				PageSize = queryDto.PageSize,
				TotalPages = (int)Math.Ceiling((double)totalCount / queryDto.PageSize)
			};
		}

		public async Task<PagedResult<LostPetPostResponseDto>> GetLostPetPostsAsync(LostPetPostQueryDto queryDto, string? currentUserId)
		{
			var query = context.LostPetPosts.AsQueryable();

			if (queryDto.Status.HasValue)
				query = query.Where(x => x.Status == queryDto.Status.Value);

			if (!string.IsNullOrWhiteSpace(queryDto.County))
				query = query.Where(x => x.County == queryDto.County);

			var totalCount = await query.CountAsync();

			// 這張表沒有動物種類這種可分類欄位（自建貼文只有 Title/Description 自由文字，
			// 決策：不新增結構化分類），可篩選的維度就是 Status／County，能加的是排序
			IOrderedQueryable<LostPetPost> orderedQuery = queryDto.SortBy switch
			{
				LostPetPostSortBy.UpdatedAt => queryDto.SortDescending
					? query.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
					: query.OrderBy(x => x.UpdatedAt).ThenByDescending(x => x.Id),
				_ => queryDto.SortDescending
					? query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
					: query.OrderBy(x => x.CreatedAt).ThenByDescending(x => x.Id),
			};

			// 先撈實體再於記憶體內轉 DTO——MapToResponseDto 是一般 C# 方法，EF Core 無法把它翻譯成 SQL，
			// 直接寫在 Select 裡會在執行期丟例外，必須先 ToListAsync() 讓查詢在 DB 端執行完畢
			var entities = await orderedQuery
				.Skip((queryDto.Page - 1) * queryDto.PageSize)
				.Take(queryDto.PageSize)
				.ToListAsync();

			return new PagedResult<LostPetPostResponseDto>
			{
				Items = entities.Select(x => MapToResponseDto(x, currentUserId)).ToList(),
				TotalCount = totalCount,
				Page = queryDto.Page,
				PageSize = queryDto.PageSize,
				TotalPages = (int)Math.Ceiling((double)totalCount / queryDto.PageSize)
			};
		}

		public async Task<LostPetPostResponseDto?> GetLostPetPostByIdAsync(int id, string? currentUserId)
		{
			var entity = await context.LostPetPosts.FirstOrDefaultAsync(x => x.Id == id);
			return entity is null ? null : MapToResponseDto(entity, currentUserId);
		}

		public async Task<LostPetPostResponseDto> CreateLostPetPostAsync(string userId, CreateLostPetPostRequestDto request)
		{
			var now = timeProvider.GetUtcNow().UtcDateTime;

			var entity = new LostPetPost
			{
				UserId = userId,
				Title = request.Title,
				Description = request.Description,
				County = request.County,
				Phone = request.Phone,
				Email = request.Email,
				PhotoUrl = request.PhotoUrl,
				Latitude = request.Latitude,
				Longitude = request.Longitude,
				Status = LostPetPostStatus.Searching,
				CreatedAt = now,
				UpdatedAt = now
			};

			context.LostPetPosts.Add(entity);
			await context.SaveChangesAsync();

			// 剛建立的貼文，建立者對自己一定是 owner
			return MapToResponseDto(entity, userId);
		}

		public async Task<bool> UpdateLostPetPostAsync(int id, string userId, UpdateLostPetPostRequestDto request)
		{
			var entity = await context.LostPetPosts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
			if (entity is null) return false;

			entity.Title = request.Title;
			entity.Description = request.Description;
			entity.County = request.County;
			entity.Phone = request.Phone;
			entity.Email = request.Email;
			entity.PhotoUrl = request.PhotoUrl;
			entity.Latitude = request.Latitude;
			entity.Longitude = request.Longitude;
			entity.Status = request.Status;
			entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

			await context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> DeleteLostPetPostAsync(int id, string userId)
		{
			var entity = await context.LostPetPosts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
			if (entity is null) return false;

			context.LostPetPosts.Remove(entity);
			await context.SaveChangesAsync();
			return true;
		}

		/// <summary>
		/// currentUserId 為 null（訪客未登入）時 IsOwner 一律 false，不需要另外判斷——
		/// 字串比對對 null 是安全的（左邊先短路），不會丟例外。
		/// </summary>
		private static LostPetPostResponseDto MapToResponseDto(LostPetPost entity, string? currentUserId)
		{
			return new LostPetPostResponseDto
			{
				Id = entity.Id,
				Title = entity.Title,
				Description = entity.Description,
				County = entity.County,
				Phone = entity.Phone,
				Email = entity.Email,
				PhotoUrl = entity.PhotoUrl,
				Latitude = entity.Latitude,
				Longitude = entity.Longitude,
				Status = entity.Status.ToString(),
				CreatedAt = entity.CreatedAt,
				UpdatedAt = entity.UpdatedAt,
				IsOwner = currentUserId != null && entity.UserId == currentUserId
			};
		}
	}
}
