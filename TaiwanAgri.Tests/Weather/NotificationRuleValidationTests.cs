using System.ComponentModel.DataAnnotations;
using TaiwanAgri.Modules.Weather.Dtos.ApiRequests;

namespace TaiwanAgri.Tests.Weather
{
	/// <summary>
	/// 建立與更新規則的請求驗證。
	/// <para>
	/// 這一組守的是同一種失效：規則建得起來、引擎卻認不得它，於是每輪跑到它就記一行 log 跳過。
	/// 使用者看到的是通知一直沒來——沒有錯誤畫面、沒有紅字、規則在列表上看起來一切正常。
	/// 所以每一個合法值清單、每一組型態相依的必填規則，都要有測試釘住它擋得下來。
	/// </para>
	/// <para>
	/// 兩件影響測試怎麼寫的事。其一：Validator.TryValidateObject 會在屬性層級的驗證失敗時
	/// 直接回傳，不再呼叫 IValidatableObject.Validate，所以測跨欄位規則時其餘欄位必須是合法的
	/// ——底下兩個工廠方法就是為此存在。其二：這裡刻意用字面字串而不是引用常數，
	/// 因為這些值會存進資料庫、也是 API 對外契約的一部分，改動常數的值必須讓測試變紅。
	/// </para>
	/// </summary>
	public class NotificationRuleValidationTests
	{
		private static List<ValidationResult> Validate(object dto)
		{
			var results = new List<ValidationResult>();
			Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
			return results;
		}

		private static NotificationRuleRequestDto NumericRequest(
			string ruleName = "高溫警戒",
			string ruleType = "Numeric",
			string sourceTable = "WeatherObservation",
			string? metricName = "Temperature",
			string? comparison = "GreaterThan",
			decimal? threshold = 32.0m,
			int? expiryDays = 7) => new()
			{
				RuleName = ruleName,
				RuleType = ruleType,
				SourceTable = sourceTable,
				MetricName = metricName,
				Comparison = comparison,
				Threshold = threshold,
				ExpiryDays = expiryDays,
				FilterCity = "臺中市"
			};

		private static NotificationRuleRequestDto EventRequest(
			string ruleName = "疫情警報",
			string ruleType = "Event",
			string sourceTable = "PlantEpidemic",
			int? expiryDays = 30) => new()
			{
				RuleName = ruleName,
				RuleType = ruleType,
				SourceTable = sourceTable,
				ExpiryDays = expiryDays,
				FilterCity = "臺中市",
				FilterPlantName = "檸檬",
				FilterDateFrom = new DateOnly(2026, 6, 12)
			};

		// ── 基準：合法的請求要通過 ────────────────────────────────────────
		// 底下每一條「要被擋下」的測試都是在這兩個基準上只改一個欄位，
		// 所以基準本身通不過的話，那些測試就會因為錯誤的理由變綠

		[Fact]
		public void 合法的數值門檻規則要通過()
		{
			Assert.Empty(Validate(NumericRequest()));
		}

		[Fact]
		public void 合法的事件規則要通過()
		{
			Assert.Empty(Validate(EventRequest()));
		}

		// ── 規則型態與資料來源 ────────────────────────────────────────────

		[Theory]
		[InlineData("numeric")]      // 大小寫不同就是另一個值，引擎的 switch 認不得
		[InlineData("Threshold")]
		[InlineData("")]
		public void 規則型態只接受兩個合法值(string ruleType)
		{
			Assert.NotEmpty(Validate(NumericRequest(ruleType: ruleType)));
		}

		/// <summary>
		/// 病蟲害旬報不開放的理由是「要比對的那一格根本沒有值」——旬平均值上游未提供，
		/// 實測我方 136 筆與上游單頁 500 筆全部為 null，任何門檻都回 0 筆。
		/// 與樹木病蟲害那條分開寫，是因為兩者不做的理由完全不同：
		/// 日後若其中一個資料源改善了，只會有一條測試需要改。
		/// </summary>
		[Fact]
		public void 病蟲害旬報不能當資料來源()
		{
			Assert.NotEmpty(Validate(NumericRequest(sourceTable: "PestDecade")));
		}

		/// <summary>
		/// 樹木病蟲害不開放的理由是「資料的形狀不對」——沒有時間戳（無法判斷新出現）、
		/// 沒有唯一識別欄位（無法去重），且語意是歷史診斷案例而非即時警報。
		/// </summary>
		[Fact]
		public void 樹木病蟲害不能當資料來源()
		{
			Assert.NotEmpty(Validate(EventRequest(sourceTable: "TreePest")));
		}

		/// <summary>
		/// 兩個值本身都合法、只是配錯型態。不擋的話規則建得起來，
		/// 引擎每輪跑到它就跳過——使用者永遠收不到通知，也看不到任何錯誤
		/// </summary>
		[Theory]
		[InlineData("Numeric", "PlantEpidemic")]
		[InlineData("Event", "WeatherObservation")]
		public void 資料來源必須與規則型態相符(string ruleType, string sourceTable)
		{
			var dto = NumericRequest(ruleType: ruleType, sourceTable: sourceTable);
			Assert.Contains(Validate(dto), e => e.MemberNames.Contains(nameof(dto.SourceTable)));
		}

		// ── 數值型專用欄位 ────────────────────────────────────────────────

		[Theory]
		[InlineData("Humidity")]     // 該表確實有這個欄位，但規則只開兩項
		[InlineData("temperature")]
		[InlineData(null)]
		public void 觀測項目只接受氣溫與二十四小時雨量(string? metricName)
		{
			Assert.NotEmpty(Validate(NumericRequest(metricName: metricName)));
		}

		[Theory]
		[InlineData("Temperature")]
		[InlineData("Rainfall24h")]
		public void 兩個合法的觀測項目本身要通過(string metricName)
		{
			Assert.Empty(Validate(NumericRequest(metricName: metricName)));
		}

		[Theory]
		[InlineData("Equals")]
		[InlineData(">")]
		[InlineData(null)]
		public void 比較方向只接受大於與小於(string? comparison)
		{
			Assert.NotEmpty(Validate(NumericRequest(comparison: comparison)));
		}

		[Theory]
		[InlineData("GreaterThan")]
		[InlineData("LessThan")]
		public void 兩個合法的比較方向本身要通過(string comparison)
		{
			Assert.Empty(Validate(NumericRequest(comparison: comparison)));
		}

		/// <summary>
		/// 門檻沒填的話引擎會直接跳過整條規則——這正是「建得起來但永遠不觸發」那一族
		/// </summary>
		[Fact]
		public void 數值門檻規則必須填門檻值()
		{
			var dto = NumericRequest(threshold: null);
			Assert.Contains(Validate(dto), e => e.MemberNames.Contains(nameof(dto.Threshold)));
		}

		/// <summary>
		/// 資料庫欄位是 decimal(4,1)，多的小數位 SQL Server 會自己捨入而且不報錯——
		/// 使用者填 32.55、系統實際拿 32.6 去比對，而他無從發現
		/// </summary>
		[Theory]
		[InlineData(32.55)]
		[InlineData(0.01)]
		[InlineData(-12.345)]
		public void 門檻值超過小數點後一位要被擋下(double threshold)
		{
			Assert.NotEmpty(Validate(NumericRequest(threshold: (decimal)threshold)));
		}

		[Theory]
		[InlineData(32)]
		[InlineData(32.5)]
		[InlineData(-999.9)]
		[InlineData(999.9)]
		public void 一位小數與邊界值本身要通過(double threshold)
		{
			Assert.Empty(Validate(NumericRequest(threshold: (decimal)threshold)));
		}

		[Theory]
		[InlineData(1000.0)]
		[InlineData(-1000.0)]
		public void 門檻值超出欄位精度容得下的範圍要被擋下(double threshold)
		{
			Assert.NotEmpty(Validate(NumericRequest(threshold: (decimal)threshold)));
		}

		/// <summary>
		/// 事件型不需要這三個欄位，所以它們是 null 也要照樣通過——
		/// 少了這條，把「數值型必填」誤寫成「一律必填」不會有任何測試變紅
		/// </summary>
		[Fact]
		public void 事件規則不必填數值型的三個欄位()
		{
			Assert.Empty(Validate(EventRequest()));
		}

		/// <summary>
		/// 另一個型態的欄位被填了值時不回 400——那些欄位對該型態沒有作用，
		/// 由服務層清成 null。這條釘的是「不擋」這個刻意的決定，
		/// 否則日後有人順手加上檢查，前端切換型態時忘了清舊值就會變成 400
		/// </summary>
		[Fact]
		public void 事件規則帶了數值型欄位不算驗證錯誤()
		{
			var dto = EventRequest();
			dto.MetricName = "Temperature";
			dto.Threshold = 32.0m;

			Assert.Empty(Validate(dto));
		}

		// ── 通知保留天數 ──────────────────────────────────────────────────

		/// <summary>
		/// 這條上限最硬的理由不是行動價值，是 ExpireAt = now.AddDays(ExpiryDays) 會溢位拋例外，
		/// 而那個例外會離開整支評估方法——一個使用者填的數字讓當天全系統所有人的規則都不被評估。
		/// 先釘住危險本身、再釘住擋它的驗證：只驗後者的話，日後有人放寬上限時看不出自己解除了什麼
		/// </summary>
		[Theory]
		[InlineData(2_000_000_000)]
		[InlineData(int.MaxValue)]
		public void 保留天數大到會讓到期日溢位的值要被擋下(int expiryDays)
		{
			Assert.Throws<ArgumentOutOfRangeException>(
				() => new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc).AddDays(expiryDays));

			Assert.NotEmpty(Validate(NumericRequest(expiryDays: expiryDays)));
			Assert.NotEmpty(Validate(EventRequest(expiryDays: expiryDays)));
		}

		/// <summary>兩種型態的上限不同，所以分開驗；共用一個數字的話其中一邊會鬆掉</summary>
		[Theory]
		[InlineData(0)]
		[InlineData(-1)]
		[InlineData(31)]
		public void 數值型的保留天數必須介於一到三十天(int expiryDays)
		{
			Assert.NotEmpty(Validate(NumericRequest(expiryDays: expiryDays)));
		}

		[Theory]
		[InlineData(1)]
		[InlineData(7)]
		[InlineData(30)]
		public void 數值型的保留天數邊界值本身要通過(int expiryDays)
		{
			Assert.Empty(Validate(NumericRequest(expiryDays: expiryDays)));
		}

		[Theory]
		[InlineData(0)]
		[InlineData(-1)]
		[InlineData(181)]
		public void 事件型的保留天數必須介於一到一百八十天(int expiryDays)
		{
			Assert.NotEmpty(Validate(EventRequest(expiryDays: expiryDays)));
		}

		[Theory]
		[InlineData(1)]
		[InlineData(30)]
		[InlineData(180)]
		public void 事件型的保留天數邊界值本身要通過(int expiryDays)
		{
			Assert.Empty(Validate(EventRequest(expiryDays: expiryDays)));
		}

		/// <summary>
		/// 事件型可以填到 180，同一個值放到數值型要被擋——兩個上限真的是分開判斷的，
		/// 不是共用一個較寬的數字
		/// </summary>
		[Fact]
		public void 事件型的上限值放到數值型要被擋下()
		{
			Assert.Empty(Validate(EventRequest(expiryDays: 180)));
			Assert.NotEmpty(Validate(NumericRequest(expiryDays: 180)));
		}

		/// <summary>沒帶值是合法的，預設值由服務層依型態補上</summary>
		[Fact]
		public void 保留天數可以不帶值()
		{
			Assert.Empty(Validate(NumericRequest(expiryDays: null)));
			Assert.Empty(Validate(EventRequest(expiryDays: null)));
		}

		// ── 規則名稱 ──────────────────────────────────────────────────────

		/// <summary>
		/// Required 擋得住 null 與空字串，擋不住空白——
		/// 那會建出一條在列表與通知上都看不見名字的規則
		/// </summary>
		[Theory]
		[InlineData("   ")]
		[InlineData("\t")]
		public void 規則名稱不能只有空白(string ruleName)
		{
			var dto = NumericRequest(ruleName: ruleName);
			Assert.Contains(Validate(dto), e => e.MemberNames.Contains(nameof(dto.RuleName)));
		}

		[Fact]
		public void 規則名稱不得超過一百個字()
		{
			Assert.NotEmpty(Validate(NumericRequest(ruleName: new string('高', 101))));
			Assert.Empty(Validate(NumericRequest(ruleName: new string('高', 100))));
		}
	}
}
