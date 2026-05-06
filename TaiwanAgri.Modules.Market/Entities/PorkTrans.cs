using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.Market.Entities
{
	/// <summary>
	/// 毛豬交易行情實體 (Pork Transaction Market Data)
	/// </summary>
	public class PorkTrans
	{
		[Key]
		public int Id { get; set; }

		/// <summary>
		/// 交易日期 (TransDate)
		/// </summary>
		public DateOnly TransDate { get; set; }

		/// <summary>
		/// 市場名稱 (MarketName)
		/// </summary>
		[Required]
		[MaxLength(50)]
		public string MarketName { get; set; } = string.Empty;

		// --- 成交頭數總數系列 ---
		public int TotalTransCount { get; set; }
		public decimal TotalTransAvgWeight { get; set; }
		public decimal TotalTransAvgPrice { get; set; }

		// --- 規格豬系列 ---
		public int SpecPigCount { get; set; }
		public decimal SpecPigAvgWeight { get; set; }
		public decimal SpecPigAvgPrice { get; set; }

		// --- 95(含)-115(含) 系列 ---
		public int Count95To115kg { get; set; }
		public decimal AvgWeight95To115kg { get; set; }
		public decimal AvgPrice95To115kg { get; set; }

		// --- 75(含)-95(不含) 系列 ---
		public int Count75To95kg { get; set; }
		public decimal AvgWeight75To95kg { get; set; }
		public decimal AvgPrice75To95kg { get; set; }

		// --- 115(含)-135(不含) 系列 (原始 Num_115up) ---
		public int Count115To135kg { get; set; }
		public decimal AvgWeight115To135kg { get; set; }
		public decimal AvgPrice115To135kg { get; set; }

		// --- 75公斤以下 系列 ---
		public int CountUnder75kg { get; set; }
		public decimal AvgWeightUnder75kg { get; set; }
		public decimal AvgPriceUnder75kg { get; set; }

		// --- 淘汰種豬 系列 ---
		public int OutPigsCount { get; set; }
		public decimal OutPigsAvgWeight { get; set; }
		public decimal OutPigsAvgPrice { get; set; }

		// --- 其他豬頭數 系列 ---
		public int OtherPigsCount { get; set; }
		public decimal OtherPigsAvgWeight { get; set; }
		public decimal OtherPigsAvgPrice { get; set; }

		// --- 冷凍廠 系列 ---
		public int FreezerPigsCount { get; set; }
		public decimal FreezerPigsAvgWeight { get; set; }
		public decimal FreezerPigsAvgPrice { get; set; }

		// --- 成交總數(不含冷凍廠) 系列 ---
		public int ExcludeFreezerCount { get; set; }
		public decimal ExcludeFreezerAvgWeight { get; set; }
		public decimal ExcludeFreezerAvgPrice { get; set; }

		// --- 135(含)-155(不含) 系列 (原始 KgPig5) ---
		public int Count135To155kg { get; set; }
		public decimal AvgWeight135To155kg { get; set; }
		public decimal AvgPrice135To155kg { get; set; }

		// --- 155公斤以上 系列 (原始 KgPig6) ---
		public int CountAbove155kg { get; set; }
		public decimal AvgWeightAbove155kg { get; set; }
		public decimal AvgPriceAbove155kg { get; set; }

		/// <summary>
		/// 系統紀錄：資料建立/更新時間
		/// </summary>
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
