namespace TaiwanAgri.Modules.Pet.Dtos.Queries
{
	public class LegalSpecificPetQueryDto
	{
		public string? County { get; set; }
		public int Page { get; set; } = 1;
		public int PageSize { get; set; } = 20;
	}
}
