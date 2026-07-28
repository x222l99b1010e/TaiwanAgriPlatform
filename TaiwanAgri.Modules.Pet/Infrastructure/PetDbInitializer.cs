using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Pet.Data;
using TaiwanAgri.Modules.Pet.Entities;

namespace TaiwanAgri.Modules.Pet.Infrastructure
{
	public static class PetDbInitializer
	{
		public static async Task SeedAsync(PetDbContext context)
		{
			var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
			if (pendingMigrations.Any())
				throw new InvalidOperationException(
					$"PetDbContext 有 {pendingMigrations.Count()} 筆尚未套用的 Migration，" +
					$"請先執行 Update-Database 再啟動應用程式。\n" +
					$"待套用：{string.Join(", ", pendingMigrations)}");

			await SeedSheltersAsync(context);
		}

		private static async Task SeedSheltersAsync(PetDbContext context)
		{
			if (context.Shelters.Any()) return;

			var shelters = new List<Shelter>
			{
				new() { ShelterPkId = 48, Name = "基隆市寵物銀行",                 Address = "基隆市七堵區大華三路45-12號(欣欣安樂園旁)",     Tel = "02-24560148",         County = "基隆市", Latitude = 25.127371m, Longitude = 121.675325m },
				new() { ShelterPkId = 49, Name = "臺北市動物之家",                 Address = "臺北市內湖區安美街191號",                       Tel = "02-87913254",         County = "臺北市", Latitude = 25.060447m, Longitude = 121.603347m },
				new() { ShelterPkId = 50, Name = "新北市板橋區公立動物之家",       Address = "新北市板橋區板城路28-1號",                      Tel = "02-89662158",         County = "新北市", Latitude = 24.995474m, Longitude = 121.448004m },
				new() { ShelterPkId = 51, Name = "新北市新店區公立動物之家",       Address = "新北市新店區安泰路235號",                       Tel = "02-22159462",         County = "新北市", Latitude = 24.927169m, Longitude = 121.490808m },
				new() { ShelterPkId = 53, Name = "新北市中和區公立動物之家",       Address = "新北市中和區興南路三段100號",                   Tel = "02-86685547",         County = "新北市", Latitude = 24.975631m, Longitude = 121.488749m },
				new() { ShelterPkId = 55, Name = "新北市淡水區公立動物之家",       Address = "新北市淡水區下圭柔山91之3號",                   Tel = "02-26267558",         County = "新北市", Latitude = 25.210003m, Longitude = 121.430393m },
				new() { ShelterPkId = 56, Name = "新北市瑞芳區公立動物之家",       Address = "新北市瑞芳區中坑路3號(瑞芳清潔隊廠區內)",       Tel = "02-24063481",         County = "新北市", Latitude = 25.076075m, Longitude = 121.799348m },
				new() { ShelterPkId = 58, Name = "新北市五股區公立動物之家",       Address = "新北市五股區外寮路9-9號",                       Tel = "02-82925265",         County = "新北市", Latitude = 25.077627m, Longitude = 121.415799m },
				new() { ShelterPkId = 59, Name = "新北市八里區公立動物之家",       Address = "新北市八里區長坑里6鄰長坑道路36號",             Tel = "02-26194428",         County = "新北市", Latitude = 25.087711m, Longitude = 121.398230m },
				new() { ShelterPkId = 60, Name = "新北市三芝區公立動物之家",       Address = "新北市三芝區青山路(龍巖人本旁)",                Tel = "02-26365436",         County = "新北市", Latitude = 25.226576m, Longitude = 121.537706m },
				new() { ShelterPkId = 61, Name = "桃園市動物保護教育園區",         Address = "桃園市新屋區永興里3鄰藻礁路1668號",             Tel = "03-4861760",          County = "桃園市", Latitude = 25.008508m, Longitude = 121.027773m },
				new() { ShelterPkId = 62, Name = "新竹市動物保護教育園區",         Address = "新竹市南寮里海濱路250號",                       Tel = "03-5368329",          County = "新竹市", Latitude = 24.833086m, Longitude = 120.919738m },
				new() { ShelterPkId = 63, Name = "新竹縣動物保護教育園區",         Address = "新竹縣竹北市縣政五街192號",                     Tel = "03-5519548",          County = "新竹縣", Latitude = 24.828469m, Longitude = 121.015064m },
				new() { ShelterPkId = 67, Name = "臺中市動物之家南屯園區",         Address = "臺中市南屯區中台路601號",                       Tel = "04-23850976",         County = "臺中市", Latitude = 24.147148m, Longitude = 120.575614m },
				new() { ShelterPkId = 68, Name = "臺中市動物之家后里園區",         Address = "臺中市后里區堤防路370號",                       Tel = "04-25588024",         County = "臺中市", Latitude = 24.286402m, Longitude = 120.709621m },
				new() { ShelterPkId = 69, Name = "彰化縣流浪狗中途之家臨時收容所", Address = "彰化縣芳苑鄉文津段436-4地號",                  Tel = "0972-821052",         County = "彰化縣", Latitude = 23.932676m, Longitude = 120.367307m },
				new() { ShelterPkId = 70, Name = "南投縣公立動物收容所",           Address = "南投縣南投市嶺興路36-1號",                      Tel = "049-2225440",         County = "南投縣", Latitude = 23.905955m, Longitude = 120.669888m },
				new() { ShelterPkId = 71, Name = "嘉義市動物保護教育園區",         Address = "嘉義市彌陀路31號",                              Tel = "05-2168661",          County = "嘉義市", Latitude = 23.464327m, Longitude = 120.468793m },
				new() { ShelterPkId = 72, Name = "嘉義縣動物保護教育園區",         Address = "嘉義縣民雄鄉松山村後山仔37之2號",               Tel = "05-2721119",          County = "嘉義縣", Latitude = 23.547647m, Longitude = 120.505479m },
				new() { ShelterPkId = 73, Name = "臺南市動物之家灣裡站",           Address = "臺南市南區省躬里14鄰萬年路580巷92號",           Tel = "06-2964439",          County = "臺南市", Latitude = 22.936740m, Longitude = 120.194286m },
				new() { ShelterPkId = 74, Name = "臺南市動物之家善化站",           Address = "臺南市善化區昌隆里東勢寮1-19號",                Tel = "06-5832399",          County = "臺南市", Latitude = 23.148844m, Longitude = 120.331599m },
				new() { ShelterPkId = 75, Name = "高雄市壽山動物保護教育園區",     Address = "高雄市鼓山區萬壽路350號",                       Tel = "07-5519059",          County = "高雄市", Latitude = 22.637045m, Longitude = 120.277995m },
				new() { ShelterPkId = 76, Name = "高雄市燕巢動物保護關愛園區",     Address = "高雄市燕巢區師大路98號",                        Tel = "07-6051002",          County = "高雄市", Latitude = 22.792694m, Longitude = 120.404663m },
				new() { ShelterPkId = 77, Name = "屏東縣公立犬貓中途之家",         Address = "屏東縣內埔鄉學府路1號(屏東科技大學內)",         Tel = "08-7221090 0910-959768", County = "屏東縣", Latitude = 22.650285m, Longitude = 120.604356m },
				new() { ShelterPkId = 78, Name = "宜蘭縣流浪動物中途之家",         Address = "宜蘭縣五結鄉成興村利寶路60號",                  Tel = "03-9602350分機620",   County = "宜蘭縣", Latitude = 24.666685m, Longitude = 121.830823m },
				new() { ShelterPkId = 79, Name = "花蓮縣狗貓躍動園區",             Address = "花蓮縣鳳林鎮林榮里永豐路255號",                 Tel = "038-421452",          County = "花蓮縣", Latitude = 23.805939m, Longitude = 121.498109m },
				new() { ShelterPkId = 80, Name = "臺東縣動物收容中心",             Address = "臺東縣臺東市中華路4段999巷600號",               Tel = "089-362011",          County = "臺東縣", Latitude = 22.719601m, Longitude = 121.100962m },
				new() { ShelterPkId = 81, Name = "連江縣動物之家",                 Address = "連江縣南竿鄉復興村223號",                       Tel = "0836-25003",          County = "連江縣", Latitude = 26.166278m, Longitude = 119.960424m },
				new() { ShelterPkId = 82, Name = "金門縣動物收容中心",             Address = "金門縣金湖鎮裕民農莊20號",                      Tel = "082-336625",          County = "金門縣", Latitude = 24.444153m, Longitude = 118.444816m },
				new() { ShelterPkId = 83, Name = "澎湖縣流浪動物收容中心",         Address = "澎湖縣馬公市烏崁里260、261號",                  Tel = "06-9213559",          County = "澎湖縣", Latitude = 23.552155m, Longitude = 119.627272m },
				new() { ShelterPkId = 89, Name = "雲林縣流浪動物收容所",           Address = "雲林縣斗六市雲林路二段517號",                   Tel = "05-5523300",          County = "雲林縣", Latitude = 23.698296m, Longitude = 120.526052m },
				new() { ShelterPkId = 92, Name = "新北市政府動物保護防疫處",       Address = "新北市板橋區四川路一段157巷2號",                Tel = "02-29596353",         County = "新北市", Latitude = 25.004096m, Longitude = 121.460366m },
				new() { ShelterPkId = 96, Name = "苗栗縣動物保護教育園區",         Address = "苗栗縣銅鑼鄉朝陽村6鄰朝北55-1號",               Tel = "037-558228",          County = "苗栗縣", Latitude = 24.499628m, Longitude = 120.794012m },
			};

			context.Shelters.AddRange(shelters);
			await context.SaveChangesAsync();
		}
	}
}