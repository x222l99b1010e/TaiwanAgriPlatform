using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaiwanAgri.Modules.Pet.Data;
using TaiwanAgri.Modules.Pet.Entities;
using TaiwanAgri.Worker;

namespace TaiwanAgri.Tests.Worker
{
	/// <summary>
	/// 上游一筆壞資料不能讓整批失敗（2026-11 總檢查 3-B8）：
	/// 原本一筆日期格式不對、或一個欄位超長，那一天（那個縣市）整批都寫不進去，隔天重抓同一批又失敗，每天卡住
	/// </summary>
	public class SyncRobustnessTests
	{
		[Fact]
		public void 一筆日期格式不對只略過那一筆()
		{
			var raw = new[] { "2026/09/01", "2026-13-45", "2026/09/03" };

			var result = SyncMapping.MapSkippingMalformed(
				raw, s => DateOnly.ParseExact(s, "yyyy/MM/dd"), s => s, NullLogger.Instance, "[test]");

			Assert.Equal(new[] { new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3) }, result);
		}

		[Fact]
		public void 欄位整個缺值也只略過那一筆()
		{
			var raw = new string?[] { "2026/09/01", null };

			var result = SyncMapping.MapSkippingMalformed(
				raw, s => DateOnly.ParseExact(s!, "yyyy/MM/dd"), s => s, NullLogger.Instance, "[test]");

			Assert.Single(result);
		}

		[Fact]
		public void 不是資料格式的例外照樣往上拋()
		{
			// 程式本身的錯不能被當成「這筆資料壞了」安靜略過
			Assert.Throws<InvalidOperationException>(() => SyncMapping.MapSkippingMalformed<string, int>(
				["x"], _ => throw new InvalidOperationException("程式錯"), s => s, NullLogger.Instance, "[test]"));
		}

		private static PetDbContext CreateDb(string name) =>
			new(new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase(name)
				.AddInterceptors(new TruncateToColumnLengthInterceptor(NullLogger<TruncateToColumnLengthInterceptor>.Instance))
				.Options);

		[Fact]
		public async Task 超過欄位上限的字串截到上限再寫入()
		{
			await using var db = CreateDb(nameof(超過欄位上限的字串截到上限再寫入));
			db.LostPetPosts.Add(new LostPetPost { UserId = "u1", Title = new string('長', 150), Description = "d" });

			await db.SaveChangesAsync();

			Assert.Equal(100, (await db.LostPetPosts.SingleAsync()).Title.Length); // Title 是 MaxLength(100)
		}

		[Fact]
		public async Task 唯一索引裡的欄位不截斷()
		{
			// KeyNo 有唯一索引：截斷可能讓兩筆不同的啟事撞成同一個鍵，寧可照原樣（交給資料庫擋、留在記錄裡）
			await using var db = CreateDb(nameof(唯一索引裡的欄位不截斷));
			var longKey = new string('9', 25); // KeyNo 是 MaxLength(20)
			db.OfficialLostPetPosts.Add(new OfficialLostPetPost { KeyNo = longKey });

			await db.SaveChangesAsync(); // InMemory 不檢查長度，所以存得進去；重點是值沒有被改

			Assert.Equal(longKey, (await db.OfficialLostPetPosts.SingleAsync()).KeyNo);
		}

		[Fact]
		public async Task 不從代理對中間切開()
		{
			await using var db = CreateDb(nameof(不從代理對中間切開));
			// 前 99 個字＋一個表情符號（兩個 char）＝101 個 char，切 100 會留下半個表情符號
			var title = new string('字', 99) + "🍅";
			db.LostPetPosts.Add(new LostPetPost { UserId = "u1", Title = title, Description = "d" });

			await db.SaveChangesAsync();

			Assert.Equal(new string('字', 99), (await db.LostPetPosts.SingleAsync()).Title);
		}
	}
}
