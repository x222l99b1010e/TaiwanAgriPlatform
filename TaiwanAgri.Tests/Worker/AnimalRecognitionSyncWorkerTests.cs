using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaiwanAgri.Modules.Pet.Data;
using TaiwanAgri.Modules.Pet.Dtos.WorkerResponses;
using TaiwanAgri.Modules.Pet.Entities;
using TaiwanAgri.Worker.Pet;
using Xunit;

namespace TaiwanAgri.Tests.Worker
{
	public class AnimalRecognitionSyncWorkerTests
	{
		private static PetDbContext CreateInMemoryContext()
		{
			var options = new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase(Guid.NewGuid().ToString())
				.Options;
			return new PetDbContext(options);
		}

		// ===== EnsureSheltersExistAsync =====

		[Fact]
		public async Task EnsureSheltersExistAsync_ShelterPkId已存在_不建立任何新Shelter()
		{
			using var db = CreateInMemoryContext();
			db.Shelters.Add(new Shelter { ShelterPkId = 50, Name = "新北市板橋區公立動物之家", Latitude = 24.995474m, Longitude = 121.448004m });
			await db.SaveChangesAsync();

			var dtos = new List<ShelterAnimalDto>
			{
				new() { AnimalShelterPkId = 50, ShelterName = "新北市板橋區公立動物之家" }
			};

			await AnimalRecognitionSyncWorker.EnsureSheltersExistAsync(db, dtos, NullLogger.Instance, "[Test]", CancellationToken.None);

			Assert.Single(db.Shelters); // 還是只有原本那一筆，沒有多建立
		}

		[Fact]
		public async Task EnsureSheltersExistAsync_ShelterPkId不存在_建立座標留空的Shelter()
		{
			using var db = CreateInMemoryContext();

			var dtos = new List<ShelterAnimalDto>
			{
				new() { AnimalShelterPkId = 999999, ShelterName = "測試新收容所", ShelterAddress = "測試地址", ShelterTel = "02-12345678" }
			};

			await AnimalRecognitionSyncWorker.EnsureSheltersExistAsync(db, dtos, NullLogger.Instance, "[Test]", CancellationToken.None);

			var created = Assert.Single(db.Shelters);
			Assert.Equal(999999, created.ShelterPkId);
			Assert.Equal("測試新收容所", created.Name);
			Assert.Equal("測試地址", created.Address);
			Assert.Equal("02-12345678", created.Tel);
			Assert.Null(created.Latitude);
			Assert.Null(created.Longitude);
			Assert.Equal("資料待補", created.County);
		}

		[Fact]
		public async Task EnsureSheltersExistAsync_ShelterName等欄位為空字串_fallback為提示文字()
		{
			using var db = CreateInMemoryContext();

			var dtos = new List<ShelterAnimalDto>
			{
				new() { AnimalShelterPkId = 888888, ShelterName = "", ShelterAddress = "", ShelterTel = "" }
			};

			await AnimalRecognitionSyncWorker.EnsureSheltersExistAsync(db, dtos, NullLogger.Instance, "[Test]", CancellationToken.None);

			var created = Assert.Single(db.Shelters);
			Assert.Equal("新增收容所，資料待補", created.Name);
			Assert.Equal("資料待補", created.Address);
			Assert.Equal("資料待補", created.Tel);
		}

		[Fact]
		public async Task EnsureSheltersExistAsync_同一ShelterPkId出現在多筆DTO_只建立一筆Shelter()
		{
			using var db = CreateInMemoryContext();

			var dtos = new List<ShelterAnimalDto>
			{
				new() { AnimalShelterPkId = 777777, AnimalSubId = "A1", ShelterName = "測試收容所" },
				new() { AnimalShelterPkId = 777777, AnimalSubId = "A2", ShelterName = "測試收容所" },
				new() { AnimalShelterPkId = 777777, AnimalSubId = "A3", ShelterName = "測試收容所" }
			};

			await AnimalRecognitionSyncWorker.EnsureSheltersExistAsync(db, dtos, NullLogger.Instance, "[Test]", CancellationToken.None);

			Assert.Single(db.Shelters); // 三隻動物同一間收容所，只建立一筆 Shelter
		}

		[Fact]
		public async Task EnsureSheltersExistAsync_部分存在部分不存在_只建立缺的那筆()
		{
			using var db = CreateInMemoryContext();
			db.Shelters.Add(new Shelter { ShelterPkId = 50, Name = "已存在收容所", Latitude = 24.99m, Longitude = 121.44m });
			await db.SaveChangesAsync();

			var dtos = new List<ShelterAnimalDto>
			{
				new() { AnimalShelterPkId = 50, ShelterName = "已存在收容所" },
				new() { AnimalShelterPkId = 666666, ShelterName = "新收容所" }
			};

			await AnimalRecognitionSyncWorker.EnsureSheltersExistAsync(db, dtos, NullLogger.Instance, "[Test]", CancellationToken.None);

			Assert.Equal(2, db.Shelters.Count());
			var newShelter = db.Shelters.Single(s => s.ShelterPkId == 666666);
			Assert.Null(newShelter.Latitude);
		}
	}
}