using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.User.Data;
using TaiwanAgri.Modules.User.Dtos.ApiRequests;
using TaiwanAgri.Modules.User.Entities;
using TaiwanAgri.Modules.User.Services;

namespace TaiwanAgri.Tests.User
{
	public class UserWatchlistServiceTests
	{
		[Fact]
		public async Task 重複加入同一項目時回傳失敗()
		{
			// ── Arrange ──────────────────────────────────────────
			// 目標：模擬「DB 裡已有一筆監看項目，再新增同一筆」的情境
			// 防重複邏輯靠的是 UserId + CropCode + MarketCode 三個欄位同時符合

			// 1. 建立 InMemory DB
			//    UserWatchlistService 只依賴 UserDbContext，不需要 Mock Redis 或 IConfiguration
			//    這是跟 MarketService 測試最大的不同：隔離策略更簡單，直接用 InMemory DB 就夠
			var options = new DbContextOptionsBuilder<UserDbContext>()
				.UseInMemoryDatabase("TestDb_DuplicateWatchlist")
				.Options;
			var dbContext = new UserDbContext(options);

			// 2. 塞一筆已存在的監看項目進 DB
			//    這三個欄位是防重複的 key：UserId、CropCode、MarketCode
			//    之後呼叫 AddWatchlistItemAsync 時傳入完全相同的值，應該被擋下來
			dbContext.UserWatchlists.Add(new UserWatchlist
			{
				UserId = "user-001",
				CropCode = "A001",
				CropName = "高麗菜",
				MarketCode = "M001",
				MarketName = "台北果菜",
				MarketType = "Veg"
			});
			await dbContext.SaveChangesAsync();

			// 3. 建立被測對象
			//    直接 new，不需要 Mock，因為只有一個依賴
			var service = new UserWatchlistService(dbContext);

			// 4. 準備重複的新增請求
			//    CropCode 和 MarketCode 跟第 2 步完全一樣
			//    搭配相同的 userId → AnyAsync 條件三個都符合 → 應該被擋下來
			var request = new AddWatchlistRequestDto
			{
				CropCode = "A001",
				CropName = "高麗菜",
				MarketCode = "M001",
				MarketName = "台北果菜",
				MarketType = "Veg"
			};

			// ── Act ──────────────────────────────────────────────
			// 5. 呼叫被測方法，傳入跟已存在資料相同的 userId 和 request
			var result = await service.AddWatchlistItemAsync("user-001", request);

			// ── Assert ───────────────────────────────────────────

			// 6. 驗證回傳 false
			//    方法偵測到重複，直接回傳 false，不繼續新增
			Assert.False(result);

			// 7. 驗證 DB 還是只有一筆
			//    防重複邏輯有效 → 第二筆沒有被新增進去
			//    如果邏輯寫錯讓第二筆進去，count 會是 2，Assert 就會失敗
			var count = await dbContext.UserWatchlists.CountAsync();
			Assert.Equal(1, count);
		}

		[Fact]
		public async Task 加入新項目時回傳成功()
		{
			// ── Arrange ──────────────────────────────────────────
			// 目標：模擬「DB 是空的，新增一筆全新的監看項目」的情境
			// 沒有重複，應該成功新增並回傳 true

			// 1. 建立 InMemory DB（空的，不預先塞任何資料）
			//    注意：DB 名稱必須跟其他測試不同，避免測試之間共用資料庫互相干擾
			var options = new DbContextOptionsBuilder<UserDbContext>()
				.UseInMemoryDatabase("TestDb_NewWatchlist")
				.Options;
			var dbContext = new UserDbContext(options);

			// 2. 建立被測對象
			//    DB 是空的，直接 new 即可
			var service = new UserWatchlistService(dbContext);

			// 3. 準備全新的新增請求
			//    DB 裡沒有任何資料，AnyAsync 條件不符合 → 應該成功新增
			var request = new AddWatchlistRequestDto
			{
				CropCode = "A001",
				CropName = "高麗菜",
				MarketCode = "M001",
				MarketName = "台北果菜",
				MarketType = "Veg"
			};

			// ── Act ──────────────────────────────────────────────
			// 4. 呼叫被測方法
			var result = await service.AddWatchlistItemAsync("user-001", request);

			// ── Assert ───────────────────────────────────────────

			// 5. 驗證回傳 true
			//    DB 裡沒有重複，方法應該成功新增並回傳 true
			Assert.True(result);

			// 6. 驗證 DB 現在有一筆
			//    新增成功 → DB 從 0 筆變成 1 筆
			//    如果邏輯寫錯沒有真的存進去，count 會是 0，Assert 就會失敗
			var count = await dbContext.UserWatchlists.CountAsync();
			Assert.Equal(1, count);
		}

		/// <summary>
		/// 監看清單不依賴農場檔案。
		/// <para>
		/// 這張表原本有外鍵指向 <c>UserFarmProfiles.UserId</c>，於是沒填過「農場設定」的帳號
		/// 一新增監看就違反外鍵、拿到 500，而畫面顯示的是「請稍後再試」——再試幾次都不會成功。
		/// 農場檔案是選填的偏好設定（系統只有 Guest／Admin 兩個角色，沒有「農民」這個身分），
		/// 監看清單問的是「我關心哪些作物」，兩者沒有依賴關係。
		/// </para>
		/// <para>
		/// ⚠ 這條測試不能用「新增一筆看會不會成功」來寫：EF InMemory 根本不強制外鍵，
		/// 那種寫法在移除外鍵之前就已經是綠的，等於什麼都沒檢查。
		/// 所以直接斷言模型設定——把外鍵加回去，這條就會紅。
		/// </para>
		/// </summary>
		[Fact]
		public void 監看清單的UserId沒有外鍵指向農場檔案()
		{
			var options = new DbContextOptionsBuilder<UserDbContext>()
				.UseInMemoryDatabase(nameof(監看清單的UserId沒有外鍵指向農場檔案))
				.Options;
			using var dbContext = new UserDbContext(options);

			var foreignKeys = dbContext.Model.FindEntityType(typeof(UserWatchlist))!.GetForeignKeys();
			Assert.Empty(foreignKeys);

			// 對照組：UserFarmCrop 指向農場檔案的外鍵要留著。
			// 少了這一半，一個「把整個 UserDbContext 的關聯都拿掉」的改動也會讓上面那句通過
			var cropForeignKey = dbContext.Model.FindEntityType(typeof(UserFarmCrop))!
				.GetForeignKeys()
				.Single(fk => fk.PrincipalEntityType.ClrType == typeof(UserFarmProfile));
			Assert.Equal(DeleteBehavior.Cascade, cropForeignKey.DeleteBehavior);
		}
	}
}
