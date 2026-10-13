using TaiwanAgri.Modules.Market.Dtos.WorkerResponses;
using TaiwanAgri.Worker.Market;
using Xunit;

namespace TaiwanAgri.Tests.Worker
{
	public class MarketRestDaySyncWorkerTests
	{
		private static MarketRestDayDto Market(string code, string type, int year, params (int Month, string Days)[] months) => new()
		{
			MarketCode = code,
			MarketName = $"市場{code}",
			MarketTypeList =
			[
				new MarketRestDayTypeDto
				{
					Type = type,
					YearList =
					[
						new MarketRestDayYearDto
						{
							Year = year,
							MonthList = [.. months.Select(m => new MarketRestDayMonthDto { Month = m.Month, RestDay = m.Days })]
						}
					]
				}
			]
		};

		[Fact]
		public void FlattenToEntities_同一月份兩個版本_取聯集且每天只留一筆()
		{
			// 對應上游實例：423 市場花卉類 115 年 10 月同時出現兩個版本
			var markets = new[] { Market("423", "F", 115, (10, "03、04、11、18、25、31"), (10, "04、10、11、18、25、31")) };

			var (entities, duplicates) = MarketRestDaySyncWorker.FlattenToEntities(markets);

			Assert.Equal([3, 4, 10, 11, 18, 25, 31], entities.Select(e => e.RestDay).Order());
			Assert.Equal(5, duplicates.Count);
			Assert.Contains("423/F 115-10-04", duplicates);
		}

		[Fact]
		public void FlattenToEntities_不同市場或不同類別的同一天_各自保留且不算重複()
		{
			var markets = new[]
			{
				Market("109", "V", 115, (10, "05")),
				Market("400", "V", 115, (10, "05")),
				Market("109", "F", 115, (10, "05")),
			};

			var (entities, duplicates) = MarketRestDaySyncWorker.FlattenToEntities(markets);

			Assert.Equal(3, entities.Count);
			Assert.Empty(duplicates);
		}

		[Fact]
		public void FlattenToEntities_不是數字的片段_略過不寫入()
		{
			var markets = new[] { Market("109", "V", 115, (10, "05、、無")) };

			var (entities, duplicates) = MarketRestDaySyncWorker.FlattenToEntities(markets);

			Assert.Equal(5, Assert.Single(entities).RestDay);
			Assert.Empty(duplicates);
		}
	}
}
