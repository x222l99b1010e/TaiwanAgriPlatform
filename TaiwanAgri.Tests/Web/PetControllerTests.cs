using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.Pet.Dtos.ApiResponses;
using TaiwanAgri.Modules.Pet.Dtos.Queries;
using TaiwanAgri.Modules.Pet.Services;
using TaiwanAgri.Web.Controllers;

namespace TaiwanAgri.Tests.Web
{
	/// <summary>
	/// PetController 動作內的判斷邏輯。
	/// 分頁界限已移到 PagedQueryDto 的 DataAnnotations（由 PagedQueryValidationTests 覆蓋），
	/// 這裡測的是留在動作裡、模型驗證管不到的那幾條規則
	/// </summary>
	public class PetControllerTests
	{
		private static PetController CreateController(Mock<IPetService> service, string? userId)
		{
			var controller = new PetController(service.Object);
			var identity = userId is null
				? new ClaimsIdentity()
				: new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) });
			controller.ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
			};
			return controller;
		}

		[Fact]
		public async Task OnlyMine未登入時回401而不是空清單()
		{
			// 空清單會讓使用者以為「我沒發過任何貼文」，401 才講得出「你根本沒登入」
			var service = new Mock<IPetService>();
			var controller = CreateController(service, userId: null);

			var result = await controller.GetLostPetPosts(new LostPetPostQueryDto { OnlyMine = true });

			Assert.IsType<UnauthorizedResult>(result);
			service.Verify(s => s.GetLostPetPostsAsync(It.IsAny<LostPetPostQueryDto>(), It.IsAny<string?>()),
				Times.Never());
		}

		[Fact]
		public async Task OnlyMine已登入時把使用者Id往下傳()
		{
			var service = new Mock<IPetService>();
			service.Setup(s => s.GetLostPetPostsAsync(It.IsAny<LostPetPostQueryDto>(), "user-001"))
				.ReturnsAsync(PagedResult<LostPetPostResponseDto>.Create(new(), 0, 1, 20));
			var controller = CreateController(service, "user-001");

			var result = await controller.GetLostPetPosts(new LostPetPostQueryDto { OnlyMine = true });

			Assert.IsType<OkObjectResult>(result);
			service.Verify(s => s.GetLostPetPostsAsync(It.IsAny<LostPetPostQueryDto>(), "user-001"), Times.Once());
		}

		[Fact]
		public async Task 未登入仍可瀏覽公開的協尋清單()
		{
			// OnlyMine 為 false 時是公開查詢，未登入不該被擋
			var service = new Mock<IPetService>();
			service.Setup(s => s.GetLostPetPostsAsync(It.IsAny<LostPetPostQueryDto>(), null))
				.ReturnsAsync(PagedResult<LostPetPostResponseDto>.Create(new(), 0, 1, 20));
			var controller = CreateController(service, userId: null);

			var result = await controller.GetLostPetPosts(new LostPetPostQueryDto { OnlyMine = false });

			Assert.IsType<OkObjectResult>(result);
		}

		[Fact]
		public async Task 查無單筆動物時回404()
		{
			var service = new Mock<IPetService>();
			service.Setup(s => s.GetShelterAnimalByIdAsync(999)).ReturnsAsync((ShelterAnimalResponseDto?)null);
			var controller = CreateController(service, userId: null);

			Assert.IsType<NotFoundResult>(await controller.GetShelterAnimalById(999));
		}

		[Fact]
		public async Task 建立協尋貼文時電話與Email至少要有一項()
		{
			// 兩者都空的話拾獲者聯絡不到人，這則貼文等於沒有用
			var service = new Mock<IPetService>();
			var controller = CreateController(service, "user-001");

			var result = await controller.CreateLostPetPost(
				new Modules.Pet.Dtos.ApiRequests.CreateLostPetPostRequestDto
				{
					Title = "走失的黃金獵犬",
					// 這兩個欄位「未填」的表示法是空字串而不是 null（DTO 刻意用 = string.Empty 當預設），
					// 空白字串也要算未填，否則打幾個空格就能繞過這條規則
					Phone = string.Empty,
					Email = "   "
				});

			Assert.IsType<BadRequestObjectResult>(result);
		}
	}
}
