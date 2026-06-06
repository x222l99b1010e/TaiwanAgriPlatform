namespace TaiwanAgri.Web.Dtos
{
	public class RegisterRequestDto
	{
		public string Email { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
		public string? DisplayName { get; set; }
		public string? UserType { get; set; } // "Farmer" / "Consumer" / "Researcher"
	}
}
