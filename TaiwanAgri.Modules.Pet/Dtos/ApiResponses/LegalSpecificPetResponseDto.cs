namespace TaiwanAgri.Modules.Pet.Dtos.ApiResponses
{
	public class LegalSpecificPetResponseDto
	{
		public int Id { get; set; }
		public string ExternalId { get; set; } = string.Empty;
		public string County { get; set; } = string.Empty;
		public string BusinessItems { get; set; } = string.Empty;
		public string AnimalType { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Address { get; set; } = string.Empty;
		public string PermitNumber { get; set; } = string.Empty;
		public DateOnly? PermitValidDate { get; set; }
		public string OwnerName { get; set; } = string.Empty;
		public string ResponsibleStaffName { get; set; } = string.Empty;
		public string RankYear { get; set; } = string.Empty;
		public string RankGrade { get; set; } = string.Empty;
		public string RankText { get; set; } = string.Empty;
		public string StateFlag { get; set; } = string.Empty;
	}
}
