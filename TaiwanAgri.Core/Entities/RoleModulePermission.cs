namespace TaiwanAgri.Core.Entities
{
	public class RoleModulePermission
	{
		public string RoleId { get; set; } = string.Empty;
		public int ModuleId { get; set; }
		public bool CanView { get; set; }
		public NavModule NavModule { get; set; } = null!;
	}
}