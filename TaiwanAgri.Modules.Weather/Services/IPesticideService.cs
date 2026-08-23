using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;
using TaiwanAgri.Modules.Weather.Dtos.Queries;

namespace TaiwanAgri.Modules.Weather.Services
{
	public interface IPesticideService
	{
		/// <summary>
		/// 以有效成分名稱（中文俗名與／或英文名）查詢農藥許可證與核准用途。
		/// 即時打農業部 API，不落地 DB。輸入驗證由 Controller 負責，此處假設條件已合法。
		/// </summary>
		Task<PesticideSearchOutcome> SearchAsync(PesticideSearchQueryDto query, CancellationToken cancellationToken = default);
	}
}
