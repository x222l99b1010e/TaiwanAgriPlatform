using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Pet.Data;
using TaiwanAgri.Modules.Pet.Dtos.ApiRequests;
using TaiwanAgri.Modules.Pet.Dtos.Queries;
using TaiwanAgri.Modules.Pet.Entities;
using TaiwanAgri.Modules.Pet.Entities.Enums;
using TaiwanAgri.Modules.Pet.Services;

namespace TaiwanAgri.Tests.Pet
{
	public class PetServiceTests
	{
		/// <summary>固定時刻的 TimeProvider，讓 CreatedAt/UpdatedAt 斷言可重現（比照 FoodSafetyServiceTests 既有寫法）</summary>
		private sealed class FixedTimeProvider : TimeProvider
		{
			private readonly DateTimeOffset _utcNow;
			public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
			public override DateTimeOffset GetUtcNow() => _utcNow;
		}

		[Fact]
		public async Task GetShelterAnimalsAsync_FilterByCountyAndKind_ReturnsOnlyMatching()
		{
			// ── Arrange ──────────────────────────────────────────
			// 目標：驗證縣市篩選能透過 Shelter 導覽屬性正確運作（ShelterAnimal 本身沒有 County 欄位），
			// 且 Kind 篩選是精確 enum 比對，兩個條件同時套用時只留下都符合的那一筆

			var options = new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase("TestDb_ShelterAnimals_FilterByCountyAndKind")
				.Options;
			var dbContext = new PetDbContext(options);

			// 兩間不同縣市的收容所
			var shelterTaipei = new Shelter { ShelterPkId = 1, Name = "台北收容所", County = "臺北市", Address = "地址1" };
			var shelterTainan = new Shelter { ShelterPkId = 2, Name = "台南收容所", County = "臺南市", Address = "地址2" };
			dbContext.Shelters.AddRange(shelterTaipei, shelterTainan);

			// 三隻動物：台北的狗（應該被篩到）、台北的貓（縣市符合但 Kind 不符合，應被排除）、
			// 台南的狗（Kind 符合但縣市不符合，應被排除）
			dbContext.ShelterAnimals.AddRange(
				new ShelterAnimal { AnimalSubId = "A001", ShelterPkId = 1, Kind = AnimalKind.Dog },
				new ShelterAnimal { AnimalSubId = "A002", ShelterPkId = 1, Kind = AnimalKind.Cat },
				new ShelterAnimal { AnimalSubId = "A003", ShelterPkId = 2, Kind = AnimalKind.Dog }
			);
			await dbContext.SaveChangesAsync();

			var service = new PetService(dbContext, TimeProvider.System);
			var queryDto = new ShelterAnimalQueryDto { County = "臺北市", Kind = AnimalKind.Dog };

			// ── Act ──────────────────────────────────────────────
			var result = await service.GetShelterAnimalsAsync(queryDto);

			// ── Assert ───────────────────────────────────────────
			// 只有台北+狗那一筆同時符合兩個條件
			var item = Assert.Single(result);
			Assert.Equal("A001", item.AnimalSubId);
			Assert.Equal("臺北市", item.County);
			Assert.Equal("Dog", item.Kind);
		}

		[Fact]
		public async Task GetLegalSpecificPetsAsync_FilterByAnimalTypeRankGradeStateFlagAndBusinessItem_ReturnsOnlyMatching()
		{
			// ── Arrange ──────────────────────────────────────────
			// 目標：驗證 W23 前端串接時補上的四個篩選條件（動物類型／評鑑等級／營業狀態／業務項目）
			// 各自獨立運作、且可以疊加（複合條件）——BusinessItem 用 Contains 比對代碼組合字串

			var options = new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase("TestDb_LegalSpecificPet_Filters")
				.Options;
			var dbContext = new PetDbContext(options);

			dbContext.LegalSpecificPets.AddRange(
				new LegalSpecificPet
				{
					ExternalId = "L001", Name = "符合全部條件的業者",
					AnimalType = LegalPetAnimalType.Dog, RankGrade = LegalPetRankGrade.Excellent,
					StateFlag = LegalPetStateFlag.Operating, BusinessItems = "ABC"
				},
				new LegalSpecificPet
				{
					ExternalId = "L002", Name = "動物類型不符",
					AnimalType = LegalPetAnimalType.Cat, RankGrade = LegalPetRankGrade.Excellent,
					StateFlag = LegalPetStateFlag.Operating, BusinessItems = "ABC"
				},
				new LegalSpecificPet
				{
					ExternalId = "L003", Name = "業務項目不含寄養",
					AnimalType = LegalPetAnimalType.Dog, RankGrade = LegalPetRankGrade.Excellent,
					StateFlag = LegalPetStateFlag.Operating, BusinessItems = "AB"
				}
			);
			await dbContext.SaveChangesAsync();

			var service = new PetService(dbContext, TimeProvider.System);

			// ── Act ──────────────────────────────────────────────
			var result = await service.GetLegalSpecificPetsAsync(new LegalSpecificPetQueryDto
			{
				AnimalType = LegalPetAnimalType.Dog,
				RankGrade = LegalPetRankGrade.Excellent,
				StateFlag = LegalPetStateFlag.Operating,
				BusinessItem = "C"
			});

			// ── Assert ───────────────────────────────────────────
			var item = Assert.Single(result.Items);
			Assert.Equal("L001", item.ExternalId);
		}

		[Fact]
		public async Task GetLegalSpecificPetsAsync_SortByPermitValidDateDescending_OrdersNewestExpiryFirst()
		{
			// ── Arrange ──────────────────────────────────────────
			// 目標：驗證排序取代「是否過期」布林篩選的設計（owner 2026-08-06 裁示，選項 3）
			// 確實可用——依效期排序，不需要另外處理 PermitValidDate 為 null 的語意問題

			var options = new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase("TestDb_LegalSpecificPet_SortByPermitValidDate")
				.Options;
			var dbContext = new PetDbContext(options);

			dbContext.LegalSpecificPets.AddRange(
				new LegalSpecificPet { ExternalId = "L001", Name = "早過期", PermitValidDate = new DateOnly(2020, 1, 1) },
				new LegalSpecificPet { ExternalId = "L002", Name = "晚過期", PermitValidDate = new DateOnly(2030, 1, 1) },
				new LegalSpecificPet { ExternalId = "L003", Name = "查無效期", PermitValidDate = null }
			);
			await dbContext.SaveChangesAsync();

			var service = new PetService(dbContext, TimeProvider.System);

			// ── Act ──────────────────────────────────────────────
			var result = await service.GetLegalSpecificPetsAsync(new LegalSpecificPetQueryDto
			{
				SortBy = LegalSpecificPetSortBy.PermitValidDate,
				SortDescending = true
			});

			// ── Assert ───────────────────────────────────────────
			// 降冪：效期最晚的排最前面，null 不會噴例外、落在最後
			Assert.Equal(["L002", "L001", "L003"], result.Items.Select(x => x.ExternalId));
		}

		[Fact]
		public async Task GetOfficialLostPetPostsAsync_FilterByCategoryAndSex_ReturnsOnlyMatching()
		{
			// ── Arrange ──────────────────────────────────────────
			var options = new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase("TestDb_OfficialLostPetPost_Filters")
				.Options;
			var dbContext = new PetDbContext(options);

			dbContext.OfficialLostPetPosts.AddRange(
				new OfficialLostPetPost { KeyNo = "K001", Category = AnimalKind.Dog, Sex = AnimalSex.Male },
				new OfficialLostPetPost { KeyNo = "K002", Category = AnimalKind.Cat, Sex = AnimalSex.Male },
				new OfficialLostPetPost { KeyNo = "K003", Category = AnimalKind.Dog, Sex = AnimalSex.Female }
			);
			await dbContext.SaveChangesAsync();

			var service = new PetService(dbContext, TimeProvider.System);

			// ── Act ──────────────────────────────────────────────
			var result = await service.GetOfficialLostPetPostsAsync(new OfficialLostPetPostQueryDto
			{
				Category = AnimalKind.Dog,
				Sex = AnimalSex.Male
			});

			// ── Assert ───────────────────────────────────────────
			var item = Assert.Single(result.Items);
			Assert.Equal("K001", item.KeyNo);
		}

		[Fact]
		public async Task GetOfficialLostPetPostsAsync_NoSortSpecified_DefaultsToLostTimeDescending()
		{
			// ── Arrange ──────────────────────────────────────────
			// 目標：釘住「沒帶排序參數時行為不變」——這是既有呼叫端（若有）不該被這次改動影響的保證

			var options = new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase("TestDb_OfficialLostPetPost_DefaultSort")
				.Options;
			var dbContext = new PetDbContext(options);

			dbContext.OfficialLostPetPosts.AddRange(
				new OfficialLostPetPost { KeyNo = "K001", LostTime = new DateOnly(2026, 1, 1) },
				new OfficialLostPetPost { KeyNo = "K002", LostTime = new DateOnly(2026, 3, 1) },
				new OfficialLostPetPost { KeyNo = "K003", LostTime = new DateOnly(2026, 2, 1) }
			);
			await dbContext.SaveChangesAsync();

			var service = new PetService(dbContext, TimeProvider.System);

			// ── Act ──────────────────────────────────────────────
			var result = await service.GetOfficialLostPetPostsAsync(new OfficialLostPetPostQueryDto());

			// ── Assert ───────────────────────────────────────────
			Assert.Equal(["K002", "K003", "K001"], result.Items.Select(x => x.KeyNo));
		}

		[Fact]
		public async Task GetLostPetPostsAsync_SortByUpdatedAtAscending_OrdersOldestUpdateFirst()
		{
			// ── Arrange ──────────────────────────────────────────
			// 目標：驗證 LostPetPost 新增的排序選項（依更新時間，這張表沒有動物種類可篩選，
			// 能加的就是排序）——同時釘住 SortDescending=false 時方向確實反過來

			var options = new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase("TestDb_LostPetPost_SortByUpdatedAt")
				.Options;
			var dbContext = new PetDbContext(options);

			dbContext.LostPetPosts.AddRange(
				new LostPetPost { UserId = "u1", Title = "A", Description = "A", UpdatedAt = new DateTime(2026, 1, 1), CreatedAt = new DateTime(2026, 1, 1) },
				new LostPetPost { UserId = "u1", Title = "B", Description = "B", UpdatedAt = new DateTime(2026, 3, 1), CreatedAt = new DateTime(2026, 1, 1) },
				new LostPetPost { UserId = "u1", Title = "C", Description = "C", UpdatedAt = new DateTime(2026, 2, 1), CreatedAt = new DateTime(2026, 1, 1) }
			);
			await dbContext.SaveChangesAsync();

			var service = new PetService(dbContext, TimeProvider.System);

			// ── Act ──────────────────────────────────────────────
			var result = await service.GetLostPetPostsAsync(
				new LostPetPostQueryDto { SortBy = LostPetPostSortBy.UpdatedAt, SortDescending = false },
				currentUserId: null
			);

			// ── Assert ───────────────────────────────────────────
			Assert.Equal(["A", "C", "B"], result.Items.Select(x => x.Title));
		}

		[Fact]
		public async Task UpdateLostPetPostAsync_WrongUser_ReturnsFalseAndDoesNotModify()
		{
			// ── Arrange ──────────────────────────────────────────
			// 目標：驗證「不是自己的貼文」無法被 Update——這是防越權的核心邏輯，
			// 查詢條件必須同時比對 Id 與 UserId，不能只比對 Id

			var options = new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase("TestDb_LostPetPost_UpdateWrongUser")
				.Options;
			var dbContext = new PetDbContext(options);

			var original = new LostPetPost
			{
				UserId = "owner-001",
				Title = "原標題",
				Description = "原描述",
				Status = LostPetPostStatus.Searching,
				CreatedAt = new DateTime(2026, 8, 1),
				UpdatedAt = new DateTime(2026, 8, 1)
			};
			dbContext.LostPetPosts.Add(original);
			await dbContext.SaveChangesAsync();

			var service = new PetService(dbContext, TimeProvider.System);
			var request = new UpdateLostPetPostRequestDto { Title = "被冒用的標題", Description = "被冒用的描述", Status = LostPetPostStatus.Found };

			// ── Act ──────────────────────────────────────────────
			// 用不是原作者的 userId 嘗試更新
			var success = await service.UpdateLostPetPostAsync(original.Id, "attacker-002", request);

			// ── Assert ───────────────────────────────────────────
			Assert.False(success);

			// DB 裡的內容應該完全沒被改動
			var stillOriginal = await dbContext.LostPetPosts.FindAsync(original.Id);
			Assert.Equal("原標題", stillOriginal!.Title);
			Assert.Equal(LostPetPostStatus.Searching, stillOriginal.Status);
		}

		[Fact]
		public async Task DeleteLostPetPostAsync_WrongUser_ReturnsFalseAndDoesNotDelete()
		{
			// ── Arrange ──────────────────────────────────────────
			var options = new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase("TestDb_LostPetPost_DeleteWrongUser")
				.Options;
			var dbContext = new PetDbContext(options);

			var original = new LostPetPost { UserId = "owner-001", Title = "標題", Description = "描述" };
			dbContext.LostPetPosts.Add(original);
			await dbContext.SaveChangesAsync();

			var service = new PetService(dbContext, TimeProvider.System);

			// ── Act ──────────────────────────────────────────────
			var success = await service.DeleteLostPetPostAsync(original.Id, "attacker-002");

			// ── Assert ───────────────────────────────────────────
			Assert.False(success);
			Assert.Equal(1, await dbContext.LostPetPosts.CountAsync());
		}

		[Fact]
		public async Task CreateLostPetPostAsync_SetsDefaultStatusSearchingAndTimestampsFromTimeProvider()
		{
			// ── Arrange ──────────────────────────────────────────
			// 目標：新建貼文一律從 Searching 開始（使用者不能一開始就送出 Found/Withdrawn），
			// 且 CreatedAt/UpdatedAt 要來自注入的 TimeProvider，不是 DateTime.UtcNow

			var options = new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase("TestDb_LostPetPost_CreateDefaults")
				.Options;
			var dbContext = new PetDbContext(options);

			var fixedNow = new DateTimeOffset(2026, 8, 4, 10, 0, 0, TimeSpan.Zero);
			var clock = new FixedTimeProvider(fixedNow);
			var service = new PetService(dbContext, clock);

			var request = new CreateLostPetPostRequestDto
			{
				Title = "小白走失了",
				Description = "在公園附近走丟",
				Phone = "0912345678"
			};

			// ── Act ──────────────────────────────────────────────
			var result = await service.CreateLostPetPostAsync("owner-001", request);

			// ── Assert ───────────────────────────────────────────
			Assert.Equal("Searching", result.Status);
			Assert.Equal(fixedNow.UtcDateTime, result.CreatedAt);
			Assert.Equal(fixedNow.UtcDateTime, result.UpdatedAt);
		}

		[Fact]
		public async Task GetLostPetPostsAsync_IsOwnerFlag_TrueForOwnerFalseForOthersAndGuests()
		{
			// ── Arrange ──────────────────────────────────────────
			// 目標：釘住 W23 前端串接時修正的設計缺口——同一筆資料，帶自己的 userId 查
			// IsOwner 要是 true，帶別人的 userId 或完全不帶（訪客）都要是 false。
			// DTO 不外露 UserId，前端只能靠這個算好的布林值決定要不要顯示編輯／刪除按鈕

			var options = new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase("TestDb_LostPetPost_IsOwnerFlag")
				.Options;
			var dbContext = new PetDbContext(options);

			var original = new LostPetPost
			{
				UserId = "owner-001",
				Title = "小白走失了",
				Description = "在公園附近走丟",
				Status = LostPetPostStatus.Searching,
				CreatedAt = new DateTime(2026, 8, 1),
				UpdatedAt = new DateTime(2026, 8, 1)
			};
			dbContext.LostPetPosts.Add(original);
			await dbContext.SaveChangesAsync();

			var service = new PetService(dbContext, TimeProvider.System);
			var queryDto = new LostPetPostQueryDto();

			// ── Act ──────────────────────────────────────────────
			var ownerView = await service.GetLostPetPostsAsync(queryDto, "owner-001");
			var otherUserView = await service.GetLostPetPostsAsync(queryDto, "attacker-002");
			var guestView = await service.GetLostPetPostsAsync(queryDto, null);

			// ── Assert ───────────────────────────────────────────
			Assert.True(Assert.Single(ownerView.Items).IsOwner);
			Assert.False(Assert.Single(otherUserView.Items).IsOwner);
			Assert.False(Assert.Single(guestView.Items).IsOwner);
		}

		[Fact]
		public async Task UpdateLostPetPostAsync_OwnPost_UpdatesFieldsAndBumpsUpdatedAt()
		{
			// ── Arrange ──────────────────────────────────────────
			var options = new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase("TestDb_LostPetPost_UpdateOwnPost")
				.Options;
			var dbContext = new PetDbContext(options);

			var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
			var original = new LostPetPost
			{
				UserId = "owner-001",
				Title = "原標題",
				Description = "原描述",
				Status = LostPetPostStatus.Searching,
				CreatedAt = createdAt,
				UpdatedAt = createdAt
			};
			dbContext.LostPetPosts.Add(original);
			await dbContext.SaveChangesAsync();

			var updatedAt = new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);
			var service = new PetService(dbContext, new FixedTimeProvider(updatedAt));

			var request = new UpdateLostPetPostRequestDto
			{
				Title = "已找到啦",
				Description = "感謝大家幫忙",
				Status = LostPetPostStatus.Found
			};

			// ── Act ──────────────────────────────────────────────
			var success = await service.UpdateLostPetPostAsync(original.Id, "owner-001", request);

			// ── Assert ───────────────────────────────────────────
			Assert.True(success);

			var updated = await dbContext.LostPetPosts.FindAsync(original.Id);
			Assert.Equal("已找到啦", updated!.Title);
			Assert.Equal(LostPetPostStatus.Found, updated.Status);
			Assert.Equal(updatedAt.UtcDateTime, updated.UpdatedAt);
			// CreatedAt 不該被 Update 動到
			Assert.Equal(createdAt, updated.CreatedAt);
		}
	}
}
