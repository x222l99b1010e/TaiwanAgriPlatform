namespace TaiwanAgri.Modules.FoodSafety.Dtos.Queries
{
	public class OrganicCertificationQueryDto
	{
		public string? OperatorName { get; set; }
		public string? VerificationBodyName { get; set; }
		public string? ProductKeyword { get; set; }
		public int Page { get; set; } = 1;
		public int PageSize { get; set; } = 20;
	}
}
