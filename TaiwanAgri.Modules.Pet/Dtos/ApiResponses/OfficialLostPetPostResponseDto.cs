namespace TaiwanAgri.Modules.Pet.Dtos.ApiResponses
{
	public class OfficialLostPetPostResponseDto
	{
		public int Id { get; set; }
		public string KeyNo { get; set; } = string.Empty;
		public string ChipNum { get; set; } = string.Empty;
		public string PetName { get; set; } = string.Empty;
		public string Category { get; set; } = string.Empty;
		public string Sex { get; set; } = string.Empty;
		public string Variety { get; set; } = string.Empty;
		public string Coat { get; set; } = string.Empty;
		public string Exterior { get; set; } = string.Empty;
		public string Feature { get; set; } = string.Empty;
		public DateOnly LostTime { get; set; }
		public string LostPlace { get; set; } = string.Empty;
		public string FeederName { get; set; } = string.Empty;
		public string PhoneNum { get; set; } = string.Empty;
		public string EMail { get; set; } = string.Empty;
		public string PictureUrl { get; set; } = string.Empty;
	}
}
