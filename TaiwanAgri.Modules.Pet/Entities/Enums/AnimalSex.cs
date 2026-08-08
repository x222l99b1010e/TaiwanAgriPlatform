namespace TaiwanAgri.Modules.Pet.Entities.Enums
{
	public enum AnimalSex
	{
		Male,
		Female,
		Other,
		/// <summary>AnimalRecognition 原始值 "N"，實測非零星個案（單日增量曾出現 8 筆／6 筆），
		/// 語意同 Sterilization／Bacterin 已有的 TriState.Unknown——「不知道」，跟 Other（其他值）不同</summary>
		Unknown
	}
}
