using Microsoft.Extensions.Logging.Abstractions;
using TaiwanAgri.Modules.FoodSafety.Dtos.WorkerResponses;
using TaiwanAgri.Worker.FoodSafety;
using Xunit;

namespace TaiwanAgri.Tests.Worker
{
	public class OrganicCertificationSyncWorkerTests
	{
		// ===== SplitCertOrganicSn =====

		[Fact]
		public void SplitCertOrganicSn_單一值_回傳單一元素陣列()
		{
			var result = OrganicCertificationSyncWorker.SplitCertOrganicSn("1-008-246501");

			Assert.Single(result);
			Assert.Equal("1-008-246501", result[0]);
		}

		[Fact]
		public void SplitCertOrganicSn_同值重複_去重後回傳單一元素陣列()
		{
			// 對應樣本：AZIENDA AGRICOLA ICARDI SSA
			var result = OrganicCertificationSyncWorker.SplitCertOrganicSn("1-008-205501、1-008-205501");

			Assert.Single(result);
			Assert.Equal("1-008-205501", result[0]);
		}

		[Fact]
		public void SplitCertOrganicSn_異值並存_回傳兩個元素陣列()
		{
			// 對應樣本：山外山有機生態茶園農場
			var result = OrganicCertificationSyncWorker.SplitCertOrganicSn("1-009-110011、1-009-120840");

			Assert.Equal(2, result.Length);
			Assert.Contains("1-009-110011", result);
			Assert.Contains("1-009-120840", result);
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData(null)]
		public void SplitCertOrganicSn_空字串或空白_回傳空陣列(string? raw)
		{
			var result = OrganicCertificationSyncWorker.SplitCertOrganicSn(raw!);

			Assert.Empty(result);
		}

		[Fact]
		public void SplitCertOrganicSn_舊制格式字號_視為單一值正常處理()
		{
			// 對應樣本：仁宗農場，CertOrganicSn 本身是舊格式 "TOC-C0417"，不含頓號
			var result = OrganicCertificationSyncWorker.SplitCertOrganicSn("TOC-C0417");

			Assert.Single(result);
			Assert.Equal("TOC-C0417", result[0]);
		}

		// ===== ParseEffectiveDate =====

		[Fact]
		public void ParseEffectiveDate_合法格式_成功解析()
		{
			var result = OrganicCertificationSyncWorker.ParseEffectiveDate("2028/10/14", "1-009-300022", NullLogger.Instance);

			Assert.NotNull(result);
			Assert.Equal(new DateOnly(2028, 10, 14), result);
		}

		[Theory]
		[InlineData("")]
		[InlineData("不是日期")]
		[InlineData("2028-10-14")] // 格式用 - 而非 /，非預期格式
		public void ParseEffectiveDate_無法解析_回傳null不拋例外(string raw)
		{
			var result = OrganicCertificationSyncWorker.ParseEffectiveDate(raw, "1-009-300022", NullLogger.Instance);

			Assert.Null(result);
		}

		// ===== MapToEntities =====

		[Fact]
		public void MapToEntities_單一值案例_回傳一筆Entity且IsMultiCertSource為false()
		{
			var dto = new OrganicCertificationDto
			{
				Name = "Nord Road LLC",
				CertOrganicSn = "1-009-300022",
				EffectiveDate = "2028/10/14",
				Status = "通過",
				Products = "其他",
				ContainCrops = "Hushkhan輕烘焙有機蒙古雪松子"
			};

			var result = OrganicCertificationSyncWorker.MapToEntities(dto, NullLogger.Instance);

			Assert.Single(result);
			Assert.Equal("1-009-300022", result[0].CertOrganicSn);
			Assert.False(result[0].IsMultiCertSource);
			Assert.Equal(new DateOnly(2028, 10, 14), result[0].EffectiveDate);
		}

		[Fact]
		public void MapToEntities_同值重複案例_清洗後仍回傳一筆Entity()
		{
			// 對應樣本：AZIENDA AGRICOLA ICARDI SSA
			var dto = new OrganicCertificationDto
			{
				Name = "AZIENDA AGRICOLA ICARDI SSA (唯一進口商:Chen Hsu)",
				CertOrganicSn = "1-008-205501、1-008-205501",
				EffectiveDate = "2026/11/30",
				Status = "通過",
				Products = "小漿果",
				ContainCrops = "葡萄"
			};

			var result = OrganicCertificationSyncWorker.MapToEntities(dto, NullLogger.Instance);

			Assert.Single(result);
			Assert.Equal("1-008-205501", result[0].CertOrganicSn);
			Assert.False(result[0].IsMultiCertSource); // 同值重複清洗後，不視為多值來源
		}

		[Fact]
		public void MapToEntities_異值並存案例_拆分為兩筆Entity且皆標記IsMultiCertSource()
		{
			// 對應樣本：山外山有機生態茶園農場
			var dto = new OrganicCertificationDto
			{
				Name = "山外山有機生態茶園農場",
				CertOrganicSn = "1-009-110011、1-009-120840",
				EffectiveDate = "2027/08/25",
				Status = "通過",
				Products = "茶、自產農產加工品、柑桔、非供食用之作物",
				ContainCrops = "茶菁、茶葉、葡萄柚、柚子、澳洲茶樹"
			};

			var result = OrganicCertificationSyncWorker.MapToEntities(dto, NullLogger.Instance);

			Assert.Equal(2, result.Count);
			Assert.All(result, entity => Assert.True(entity.IsMultiCertSource));
			Assert.Contains(result, x => x.CertOrganicSn == "1-009-110011");
			Assert.Contains(result, x => x.CertOrganicSn == "1-009-120840");

			// 拆分後的每一筆，Products／ContainCrops 應沿用同一份完整原始字串
			Assert.All(result, entity => Assert.Equal(dto.Products, entity.Products));
			Assert.All(result, entity => Assert.Equal(dto.ContainCrops, entity.ContainCrops));
		}

		[Fact]
		public void MapToEntities_CertOrganicSn為空_回傳空清單()
		{
			var dto = new OrganicCertificationDto
			{
				Name = "仁宗農場",
				CertOrganicSn = "",
				Status = "結束"
			};

			var result = OrganicCertificationSyncWorker.MapToEntities(dto, NullLogger.Instance);

			Assert.Empty(result);
		}

		[Fact]
		public void MapToEntities_EffectiveDate解析失敗_該筆EffectiveDate為null但其他欄位正常寫入()
		{
			var dto = new OrganicCertificationDto
			{
				Name = "測試經營者",
				CertOrganicSn = "1-099-999999",
				EffectiveDate = "格式錯誤的日期",
				Status = "通過",
				CompanyName = "測試驗證機構"
			};

			var result = OrganicCertificationSyncWorker.MapToEntities(dto, NullLogger.Instance);

			Assert.Single(result);
			Assert.Null(result[0].EffectiveDate);
			// 其他欄位不受 EffectiveDate 解析失敗影響，仍正常寫入
			Assert.Equal("1-099-999999", result[0].CertOrganicSn);
			Assert.Equal("測試經營者", result[0].Name);
			Assert.Equal("通過", result[0].Status);
			Assert.Equal("測試驗證機構", result[0].CompanyName);
		}
	}
}
