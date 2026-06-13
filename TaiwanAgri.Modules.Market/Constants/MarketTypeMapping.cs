using System;
using System.Collections.Generic;
using System.Text;

namespace TaiwanAgri.Modules.Market.Constants
{
	public static class MarketTypeMapping
	{
		// MarketType (前端傳入) → TcType (AgriProductsTrans 欄位)
		private static readonly Dictionary<string, string> _map = new()
		{
			{ "Veg",    "N04" },
			{ "Fruit",  "N05" },
			{ "Flower", "N06" },
		};

		public static string? ToTcType(string marketType)
			=> _map.TryGetValue(marketType, out var tcType) ? tcType : null;

		public static bool IsValidMarketType(string? marketType)
			=> marketType is not null && _map.ContainsKey(marketType);
	}
}
