using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Market.Dtos.WorkerResponses
{
	public class PorkTransTypeDto
	{
		[JsonPropertyName("TransDate")]
		public string TransDate { get; set; } = string.Empty;

		[JsonPropertyName("MarketName")]
		public string MarketName { get; set; } = string.Empty;

		[JsonPropertyName("TransNum_Total")]
		public int TotalTransCount { get; set; }
		[JsonPropertyName("TransNum_AvgWgt")]
		public decimal TotalTransAvgWeight { get; set; }
		[JsonPropertyName("TransNum_AvgPrice")]
		public decimal TotalTransAvgPrice { get; set; }

		[JsonPropertyName("SpecPig_Num")]
		public int SpecPigCount { get; set; }
		[JsonPropertyName("SpecPig_AvgWgt")]
		public decimal SpecPigAvgWeight { get; set; }
		[JsonPropertyName("SpecPig_AvgPrice")]
		public decimal SpecPigAvgPrice { get; set; }

		[JsonPropertyName("Num_95in_115in")]
		public int Count95To115kg { get; set; }
		[JsonPropertyName("AvgWgt_95in_115in")]
		public decimal AvgWeight95To115kg { get; set; }
		[JsonPropertyName("AvgPrice_95in_115in")]
		public decimal AvgPrice95To115kg { get; set; }

		[JsonPropertyName("Num_75in_95")]
		public int Count75To95kg { get; set; }
		[JsonPropertyName("AvgWgt_75in_95")]
		public decimal AvgWeight75To95kg { get; set; }
		[JsonPropertyName("AvgPrice_75in_95")]
		public decimal AvgPrice75To95kg { get; set; }

		[JsonPropertyName("Num_115up")]
		public int Count115To135kg { get; set; }
		[JsonPropertyName("AvgWgt_115up")]
		public decimal AvgWeight115To135kg { get; set; }
		[JsonPropertyName("AvgPrice_115up")]
		public decimal AvgPrice115To135kg { get; set; }

		[JsonPropertyName("Num_75low")]
		public int CountUnder75kg { get; set; }
		[JsonPropertyName("AvgWgt_75low")]
		public decimal AvgWeightUnder75kg { get; set; }
		[JsonPropertyName("AvgPrice_75low")]
		public decimal AvgPriceUnder75kg { get; set; }

		[JsonPropertyName("OutPigs_Num")]
		public int OutPigsCount { get; set; }
		[JsonPropertyName("OutPigs_AvgWgt")]
		public decimal OutPigsAvgWeight { get; set; }
		[JsonPropertyName("OutPigs_AvgPrice")]
		public decimal OutPigsAvgPrice { get; set; }

		[JsonPropertyName("OtherPigs_Num")]
		public int OtherPigsCount { get; set; }
		[JsonPropertyName("OtherPigs_AvgWgt")]
		public decimal OtherPigsAvgWeight { get; set; }
		[JsonPropertyName("OtherPigs_AvgPrice")]
		public decimal OtherPigsAvgPrice { get; set; }

		[JsonPropertyName("FreezerPigs_Num")]
		public int FreezerPigsCount { get; set; }
		[JsonPropertyName("FreezerPigs_AvgWgt")]
		public decimal FreezerPigsAvgWeight { get; set; }
		[JsonPropertyName("FreezerPigs_AvgPrice")]
		public decimal FreezerPigsAvgPrice { get; set; }

		[JsonPropertyName("TotalTrans_ExcludeFreezer_Num")]
		public int ExcludeFreezerCount { get; set; }
		[JsonPropertyName("TotalTrans_ExcludeFreezer_AvgWeight")]
		public decimal ExcludeFreezerAvgWeight { get; set; }
		[JsonPropertyName("TotalTrans_ExcludeFreezer_AvgPrice")]
		public decimal ExcludeFreezerAvgPrice { get; set; }

		[JsonPropertyName("KgPig5_Q")]
		public int Count135To155kg { get; set; }
		[JsonPropertyName("KgPig5_W")]
		public decimal AvgWeight135To155kg { get; set; }
		[JsonPropertyName("KgPig5_P")]
		public decimal AvgPrice135To155kg { get; set; }

		[JsonPropertyName("KgPig6_Q")]
		public int CountAbove155kg { get; set; }
		[JsonPropertyName("KgPig6_W")]
		public decimal AvgWeightAbove155kg { get; set; }
		[JsonPropertyName("KgPig6_P")]
		public decimal AvgPriceAbove155kg { get; set; }
	}
}
