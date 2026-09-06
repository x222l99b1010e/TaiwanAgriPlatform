using System.ComponentModel.DataAnnotations;
using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.Pet.Dtos.Queries;

namespace TaiwanAgri.Tests.Web
{
	/// <summary>
	/// 分頁參數的界限驗證。
	/// 這段邏輯原本是六個 Controller 動作裡逐字重複的 if，沒有任何測試；
	/// 收斂到 PagedQueryDto 的 DataAnnotations 之後，界限本身值得釘住——
	/// 它現在是「用了這個 DTO 就自動生效」，壞掉的話所有分頁端點一起失去保護
	/// </summary>
	public class PagedQueryValidationTests
	{
		private static List<ValidationResult> Validate(object dto)
		{
			var results = new List<ValidationResult>();
			Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
			return results;
		}

		[Fact]
		public void 預設值是第一頁每頁二十筆且通過驗證()
		{
			var dto = new LostPetPostQueryDto();
			Assert.Equal(1, dto.Page);
			Assert.Equal(20, dto.PageSize);
			Assert.Empty(Validate(dto));
		}

		[Theory]
		[InlineData(0)]
		[InlineData(-1)]
		public void 頁碼不得小於一(int page)
		{
			var errors = Validate(new LostPetPostQueryDto { Page = page });
			Assert.Contains(errors, e => e.ErrorMessage == "頁碼必須大於 0");
		}

		[Theory]
		[InlineData(0)]
		[InlineData(-5)]
		[InlineData(101)]
		[InlineData(int.MaxValue)]
		public void 每頁筆數必須落在一到一百之間(int pageSize)
		{
			var errors = Validate(new LostPetPostQueryDto { PageSize = pageSize });
			Assert.Contains(errors, e => e.ErrorMessage == "每頁筆數必須大於 0 且小於等於 100");
		}

		[Theory]
		[InlineData(1)]
		[InlineData(20)]
		[InlineData(100)]
		public void 邊界值本身要通過(int pageSize)
		{
			Assert.Empty(Validate(new LostPetPostQueryDto { PageSize = pageSize }));
		}

		[Fact]
		public void 所有分頁查詢DTO都吃同一套界限()
		{
			// 這條防的是「新加的分頁端點忘記繼承共用基底」——
			// 忘記的話它就沒有任何界限保護，而且不會有任何錯誤訊號
			var dtos = new PagedQueryDto[]
			{
				new LostPetPostQueryDto(),
				new OfficialLostPetPostQueryDto(),
				new LegalSpecificPetQueryDto(),
				new ShelterAnimalsByShelterQueryDto(),
				new TaiwanAgri.Modules.FoodSafety.Dtos.Queries.ViolationQueryDto(),
				new TaiwanAgri.Modules.FoodSafety.Dtos.Queries.OrganicCertificationQueryDto(),
			};

			foreach (var dto in dtos)
			{
				dto.PageSize = 101;
				Assert.NotEmpty(Validate(dto));
			}
		}
	}
}
