using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Core.Entities
{
	public class NavModule
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Route { get; set; } = string.Empty;
		public string Icon { get; set; } = string.Empty;
		public bool IsActive { get; set; }
		public int SortOrder { get; set; }
		public int? ParentId { get; set; }
		public NavModule? Parent { get; set; }          // 指向父層
		public ICollection<NavModule> Children { get; set; } = new List<NavModule>();  // 子功能清單

		public ICollection<RoleModulePermission> RoleModulePermissions { get; set; } = new List<RoleModulePermission>(); // 角色權限關聯
	}
}
