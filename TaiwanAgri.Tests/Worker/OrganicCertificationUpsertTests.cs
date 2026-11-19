using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.FoodSafety.Data;
using TaiwanAgri.Modules.FoodSafety.Entities;
using TaiwanAgri.Worker.FoodSafety;

namespace TaiwanAgri.Tests.Worker
{
	/// <summary>
	/// 有機驗證改成「有則更新」（2026-11 總檢查 3-B6）：證書狀態在同步之後會變（通過→終止、暫終……），
	/// 原本證號已存在就整筆略過，資料庫裡的狀態永遠停在第一次同步那天
	/// </summary>
	public class OrganicCertificationUpsertTests
	{
		private static OrganicCertification Cert(string sn, string status) => new()
		{
			CertOrganicSn = sn, Name = "某農場", Status = status, SyncedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
		};

		[Fact]
		public async Task 已存在的證書狀態變了要跟著更新新證書照常新增()
		{
			var options = new DbContextOptionsBuilder<FoodSafetyDbContext>()
				.UseInMemoryDatabase(nameof(已存在的證書狀態變了要跟著更新新證書照常新增)).Options;
			await using (var db = new FoodSafetyDbContext(options))
			{
				db.OrganicCertifications.Add(Cert("1-008-000001", "通過"));
				await db.SaveChangesAsync();
			}

			var incoming = new List<OrganicCertification> { Cert("1-008-000001", "終止"), Cert("1-008-000002", "通過") };
			await using (var db = new FoodSafetyDbContext(options))
			{
				var sns = incoming.Select(x => x.CertOrganicSn).ToList();
				await DbSyncHelper.UpsertByKeyAsync(
					db, db.OrganicCertifications.Where(x => sns.Contains(x.CertOrganicSn)), incoming,
					x => x.CertOrganicSn, OrganicCertificationSyncWorker.ApplyUpdate,
					NullLogger.Instance, "[test]", CancellationToken.None);
			}

			await using (var db = new FoodSafetyDbContext(options))
			{
				var all = await db.OrganicCertifications.OrderBy(x => x.CertOrganicSn).ToListAsync();
				Assert.Equal(2, all.Count);
				Assert.Equal("終止", all[0].Status);
				Assert.Equal("通過", all[1].Status);
			}
		}

		[Fact]
		public void 更新時不動證號與主鍵()
		{
			var existing = new OrganicCertification { Id = 7, CertOrganicSn = "A", Status = "通過" };
			var incoming = new OrganicCertification { Id = 0, CertOrganicSn = "B", Status = "暫終", Name = "新名稱" };

			OrganicCertificationSyncWorker.ApplyUpdate(existing, incoming);

			Assert.Equal(7, existing.Id);
			Assert.Equal("A", existing.CertOrganicSn);
			Assert.Equal("暫終", existing.Status);
			Assert.Equal("新名稱", existing.Name);
		}
	}
}
