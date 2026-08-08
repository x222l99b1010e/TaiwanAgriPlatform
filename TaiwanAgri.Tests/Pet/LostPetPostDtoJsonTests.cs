using System.Text.Json;
using TaiwanAgri.Modules.Pet.Dtos.ApiRequests;
using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Tests.Pet
{
	/// <summary>
	/// 測 DTO 與 JSON 之間的反序列化契約，不涉及 Service／DbContext。
	/// W23 前端串接時發現：UpdateLostPetPostRequestDto.Status 是 enum 型別，專案未全域註冊
	/// JsonStringEnumConverter，若無單獨標註，System.Text.Json 反序列化 [FromBody] 請求時
	/// enum 屬性預設只吃數字（0/1/2），前端照專案既有慣例送字串會直接丟例外、回 400。
	/// 這裡釘住「補上 [JsonConverter(typeof(JsonStringEnumConverter))] 之後，字串可以正常反序列化」，
	/// 防止日後有人重構時不小心把這個 attribute 拿掉又沒發現。
	/// </summary>
	public class LostPetPostDtoJsonTests
	{
		[Fact]
		public void UpdateLostPetPostRequestDto_DeserializesStatusFromStringName()
		{
			// ── Arrange ──────────────────────────────────────────
			// 模擬前端實際會送出的 request body：Status 是可讀字串，不是數字
			const string json = """
				{
					"title": "已找到啦",
					"description": "感謝大家幫忙",
					"county": "臺中市",
					"phone": "0912345678",
					"email": "",
					"photoUrl": "",
					"latitude": null,
					"longitude": null,
					"status": "Found"
				}
				""";

			// ── Act ──────────────────────────────────────────────
			// 用 PropertyNameCaseInsensitive 比照 ASP.NET Core MVC 預設的 [FromBody] 反序列化設定
			var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var dto = JsonSerializer.Deserialize<UpdateLostPetPostRequestDto>(json, options);

			// ── Assert ───────────────────────────────────────────
			Assert.NotNull(dto);
			Assert.Equal(LostPetPostStatus.Found, dto!.Status);
			Assert.Equal("已找到啦", dto.Title);
		}

		[Theory]
		[InlineData("Searching", LostPetPostStatus.Searching)]
		[InlineData("Found", LostPetPostStatus.Found)]
		[InlineData("Withdrawn", LostPetPostStatus.Withdrawn)]
		public void UpdateLostPetPostRequestDto_DeserializesEachStatusValue(string statusName, LostPetPostStatus expected)
		{
			var json = $$"""{"title":"t","description":"d","status":"{{statusName}}"}""";
			var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

			var dto = JsonSerializer.Deserialize<UpdateLostPetPostRequestDto>(json, options);

			Assert.Equal(expected, dto!.Status);
		}
	}
}
