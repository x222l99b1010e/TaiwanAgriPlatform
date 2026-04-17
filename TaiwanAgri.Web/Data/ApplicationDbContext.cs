using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Entities;

namespace TaiwanAgri.Web.Data
{
	public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)// 加上泛型參數，指向 ApplicationUser
	{
	}
}
