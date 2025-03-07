using System.Text.RegularExpressions;

namespace Core.Utilities
{
	public class Utils
	{
		/// <summary>
		/// 驗證表名是否安全，允許字母、數字、下劃線組成，防止 SQL 注入
		/// </summary>
		/// <param name="tableName">表名</param>
		/// <returns>是否有效</returns>
		public static bool IsValidTableName(string tableName)
		{
			return Regex.IsMatch(tableName, @"^[a-zA-Z0-9_]+$");
		}
	}
}
