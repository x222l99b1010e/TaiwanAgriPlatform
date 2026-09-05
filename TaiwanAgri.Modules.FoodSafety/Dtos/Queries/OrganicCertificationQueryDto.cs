using TaiwanAgri.Core.Dtos;

namespace TaiwanAgri.Modules.FoodSafety.Dtos.Queries
{
	public class OrganicCertificationQueryDto : PagedQueryDto
	{
		public string? OperatorName { get; set; }
		public string? VerificationBodyName { get; set; }
		public string? ProductKeyword { get; set; }
	}
}
