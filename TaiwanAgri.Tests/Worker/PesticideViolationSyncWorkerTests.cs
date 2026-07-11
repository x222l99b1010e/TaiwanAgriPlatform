using Microsoft.Extensions.Logging.Abstractions;
using TaiwanAgri.Modules.FoodSafety.Dtos.WorkerResponses;
using TaiwanAgri.Worker;
using Xunit;

namespace TaiwanAgri.Tests.Worker
{
	public class PesticideViolationSyncWorkerTests
	{
		// ===== MapToEntity =====

		private static PesticideViolationDto CreateDto(string samplingDate = "1150520") => new()
		{
			Number = "A115-113-1-05",
			SamplingDate = samplingDate,
			ProductName = "青江菜",
			ProductId = "VG-001",
			ProducerName = "產戶A",
			SamplingLocation = "台北農產運銷公司",
			InspectResult = "與規定不符",
			Note = "撲滅寧 2.5ppm"
		};

		[Fact]
		public void MapToEntity_合法民國數字日期_成功轉換全部欄位()
		{
			var dto = CreateDto("1150520");

			var result = PesticideViolationSyncWorker.MapToEntity(dto, NullLogger.Instance);

			Assert.NotNull(result);
			// 民國 115 年 = 西元 2026 年
			Assert.Equal(new DateOnly(2026, 5, 20), result.SamplingDate);
			Assert.Equal(dto.Number, result.Number);
			Assert.Equal(dto.ProductName, result.ProductName);
			Assert.Equal(dto.ProductId, result.ProductId);
			Assert.Equal(dto.ProducerName, result.ProducerName);
			Assert.Equal(dto.SamplingLocation, result.SamplingLocation);
			Assert.Equal(dto.InspectResult, result.InspectResult);
			Assert.Equal(dto.Note, result.Note);
		}

		[Theory]
		[InlineData("")]        // 空字串
		[InlineData("115520")]  // 六位數，長度不足
		[InlineData("115A520")] // 含非數字
		[InlineData("1151332")] // 月份超出範圍
		[InlineData("1150230")] // 2 月沒有 30 日
		public void MapToEntity_日期轉換失敗_回傳null跳過該筆(string badDate)
		{
			var dto = CreateDto(badDate);

			var result = PesticideViolationSyncWorker.MapToEntity(dto, NullLogger.Instance);

			// 回傳 null 代表整筆跳過（SamplingDate 是核心查詢欄位，缺日期的違規記錄無法呈現）
			Assert.Null(result);
		}
	}
}
