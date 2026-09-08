using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.User.Data;
using TaiwanAgri.Modules.User.Entities;
using TaiwanAgri.Modules.User.Services;

namespace TaiwanAgri.Tests.User
{
	/// <summary>
	/// UserProfileService 兩支方法。唯一依賴是 UserDbContext，所以直接用 InMemory 資料庫。
	/// 主軸是 Upsert 的「作物清單全量取代」語意——它先刪後寫，所以呼叫端只要少傳一個作物，
	/// 那個作物就被刪掉了。這件事介面註解有寫，但註解攔不住任何人，測試才攔得住。
	/// 另一個重點是新增與更新走不同分支，而 CreatedAt 只能在新增那條路上被設定
	/// </summary>
	public class UserProfileServiceTests
	{
		/// <summary>
		/// InMemory 資料庫依名稱共用；驗證存檔結果時另開一個同名 DbContext，
		/// 確保讀到的是真的寫進去的資料而不是追蹤器裡的快取
		/// </summary>
		private static UserDbContext CreateDbContext(string databaseName) =>
			new(new DbContextOptionsBuilder<UserDbContext>()
				.UseInMemoryDatabase(databaseName)
				.Options);

		private static List<(string CropCode, string CropName)> Crops(params string[] codes) =>
			[.. codes.Select(c => (c, $"{c} 作物"))];

		// ── GetUserFarmProfileAsync ──────────────────────────────────────────

		/// <summary>
		/// 沒建過設定檔時回 null 而不是丟例外——前端用這個 null 決定要顯示「尚未設定」還是既有內容
		/// </summary>
		[Fact]
		public async Task 尚未建立設定檔時回傳null()
		{
			using var db = CreateDbContext(nameof(尚未建立設定檔時回傳null));

			Assert.Null(await new UserProfileService(db).GetUserFarmProfileAsync("u1"));
		}

		/// <summary>
		/// 作物清單是 Include 進來的。漏了 Include 的症狀是設定頁的作物欄位空白，
		/// 而使用者按下儲存之後，那個空白就會照著全量取代的語意寫回資料庫——顯示的 bug 變成資料的 bug
		/// </summary>
		[Fact]
		public async Task 取得設定檔時要一併載入作物清單()
		{
			using var db = CreateDbContext(nameof(取得設定檔時要一併載入作物清單));
			await new UserProfileService(db).UpsertUserFarmProfileAsync("u1", "南投縣", "果樹", Crops("A1", "A2"));

			using var readDb = CreateDbContext(nameof(取得設定檔時要一併載入作物清單));
			var profile = await new UserProfileService(readDb).GetUserFarmProfileAsync("u1");

			Assert.NotNull(profile);
			Assert.Equal("南投縣", profile.FarmCity);
			Assert.Equal("果樹", profile.FarmType);
			Assert.Equal(2, profile.Crops.Count);
		}

		/// <summary>
		/// 只回自己的設定檔。查詢條件掉了的症狀是使用者看到別人的農場設定
		/// </summary>
		[Fact]
		public async Task 只回傳自己的設定檔()
		{
			using var db = CreateDbContext(nameof(只回傳自己的設定檔));
			var service = new UserProfileService(db);
			await service.UpsertUserFarmProfileAsync("u1", "南投縣", "果樹", Crops("A1"));
			await service.UpsertUserFarmProfileAsync("u2", "臺中市", "蔬菜", Crops("B1"));

			using var readDb = CreateDbContext(nameof(只回傳自己的設定檔));
			var profile = await new UserProfileService(readDb).GetUserFarmProfileAsync("u1");

			Assert.Equal("南投縣", profile!.FarmCity);
			Assert.Equal("A1", Assert.Single(profile.Crops).CropCode);
		}

		// ── UpsertUserFarmProfileAsync ───────────────────────────────────────

		/// <summary>
		/// 第一次呼叫走新增分支，主檔與作物一起寫進去
		/// </summary>
		[Fact]
		public async Task 第一次儲存會新增主檔與作物()
		{
			using var db = CreateDbContext(nameof(第一次儲存會新增主檔與作物));
			await new UserProfileService(db).UpsertUserFarmProfileAsync("u1", "南投縣", "果樹", Crops("A1", "A2"));

			using var readDb = CreateDbContext(nameof(第一次儲存會新增主檔與作物));
			Assert.Single(readDb.UserFarmProfiles);
			Assert.Equal(2, await readDb.UserFarmCrops.CountAsync());
		}

		/// <summary>
		/// 這是這支方法最容易造成資料遺失的地方：作物是全量取代，不是累加。
		/// 原本三個作物、只傳一個進來，結果是剩一個而不是四個。
		/// 呼叫端若誤以為是增量更新（只傳新增的那一個），使用者的另外兩個作物會靜靜消失
		/// </summary>
		[Fact]
		public async Task 再次儲存時作物清單是全量取代而不是累加()
		{
			using var db = CreateDbContext(nameof(再次儲存時作物清單是全量取代而不是累加));
			var service = new UserProfileService(db);
			await service.UpsertUserFarmProfileAsync("u1", "南投縣", "果樹", Crops("A1", "A2", "A3"));

			await service.UpsertUserFarmProfileAsync("u1", "南投縣", "果樹", Crops("A9"));

			using var readDb = CreateDbContext(nameof(再次儲存時作物清單是全量取代而不是累加));
			var crops = await readDb.UserFarmCrops.Where(c => c.UserId == "u1").ToListAsync();
			Assert.Equal("A9", Assert.Single(crops).CropCode);
		}

		/// <summary>
		/// 全量取代的邊界：傳空清單代表「一個作物都不留」，而不是「沒有要動作物」。
		/// 這兩種語意在呼叫端看起來一樣，但結果差很多
		/// </summary>
		[Fact]
		public async Task 傳入空作物清單會把原有作物全部清掉()
		{
			using var db = CreateDbContext(nameof(傳入空作物清單會把原有作物全部清掉));
			var service = new UserProfileService(db);
			await service.UpsertUserFarmProfileAsync("u1", "南投縣", "果樹", Crops("A1", "A2"));

			await service.UpsertUserFarmProfileAsync("u1", "南投縣", "果樹", []);

			using var readDb = CreateDbContext(nameof(傳入空作物清單會把原有作物全部清掉));
			Assert.Empty(await readDb.UserFarmCrops.Where(c => c.UserId == "u1").ToListAsync());
			Assert.Single(readDb.UserFarmProfiles);  // 主檔還在，只是沒有作物
		}

		/// <summary>
		/// 更新分支只改主檔欄位、不重建列，所以 CreatedAt 必須保持第一次建立時的值。
		/// 若更新分支誤設了 CreatedAt，「這個帳號什麼時候開始用」這個資訊會被每次儲存抹掉一次
		/// </summary>
		[Fact]
		public async Task 更新時保留原本的建立時間並推進更新時間()
		{
			using var db = CreateDbContext(nameof(更新時保留原本的建立時間並推進更新時間));
			var service = new UserProfileService(db);
			await service.UpsertUserFarmProfileAsync("u1", "南投縣", "果樹", Crops("A1"));

			using var midDb = CreateDbContext(nameof(更新時保留原本的建立時間並推進更新時間));
			var 建立時間 = (await midDb.UserFarmProfiles.AsNoTracking().SingleAsync()).CreatedAt;
			var 首次更新時間 = (await midDb.UserFarmProfiles.AsNoTracking().SingleAsync()).UpdatedAt;

			await new UserProfileService(midDb).UpsertUserFarmProfileAsync("u1", "臺中市", "蔬菜", Crops("A1"));

			using var readDb = CreateDbContext(nameof(更新時保留原本的建立時間並推進更新時間));
			var profile = await readDb.UserFarmProfiles.SingleAsync();
			Assert.Equal(建立時間, profile.CreatedAt);
			Assert.True(profile.UpdatedAt >= 首次更新時間);
			Assert.Equal("臺中市", profile.FarmCity);
			Assert.Equal("蔬菜", profile.FarmType);
		}

		/// <summary>
		/// 縣市與類型都是可為 null 的欄位，清空是合法操作（使用者可以只設作物不設縣市）。
		/// 把 null 當成「沿用舊值」會讓使用者永遠清不掉已經填錯的縣市
		/// </summary>
		[Fact]
		public async Task 縣市與類型可以被更新成null()
		{
			using var db = CreateDbContext(nameof(縣市與類型可以被更新成null));
			var service = new UserProfileService(db);
			await service.UpsertUserFarmProfileAsync("u1", "南投縣", "果樹", Crops("A1"));

			await service.UpsertUserFarmProfileAsync("u1", null, null, Crops("A1"));

			using var readDb = CreateDbContext(nameof(縣市與類型可以被更新成null));
			var profile = await readDb.UserFarmProfiles.SingleAsync();
			Assert.Null(profile.FarmCity);
			Assert.Null(profile.FarmType);
		}

		/// <summary>
		/// 全量取代刪的必須只是自己的作物。RemoveRange 的來源若寫成整張表而不是 existing.Crops，
		/// 症狀是別人的作物跟著被刪掉，而且刪掉的人完全不會知道
		/// </summary>
		[Fact]
		public async Task 全量取代不得刪到其他使用者的作物()
		{
			using var db = CreateDbContext(nameof(全量取代不得刪到其他使用者的作物));
			var service = new UserProfileService(db);
			await service.UpsertUserFarmProfileAsync("u1", "南投縣", "果樹", Crops("A1", "A2"));
			await service.UpsertUserFarmProfileAsync("u2", "臺中市", "蔬菜", Crops("B1", "B2"));

			await service.UpsertUserFarmProfileAsync("u1", "南投縣", "果樹", Crops("A9"));

			using var readDb = CreateDbContext(nameof(全量取代不得刪到其他使用者的作物));
			Assert.Equal("A9", Assert.Single(await readDb.UserFarmCrops.Where(c => c.UserId == "u1").ToListAsync()).CropCode);
			Assert.Equal(2, await readDb.UserFarmCrops.CountAsync(c => c.UserId == "u2"));
		}
	}
}
