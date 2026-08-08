namespace TaiwanAgri.Modules.Pet.Entities.Enums
{
	/// <summary>
	/// 評鑑字號（rank_code）。官方 API 介接說明書「評鑑字號對照表」順序清晰無爭議：
	/// A=優等 B=甲等 C=乙等 D=丙等
	/// </summary>
	public enum LegalPetRankGrade
	{
		Excellent,
		GradeA,
		GradeB,
		GradeC,
		Unknown
	}
}
