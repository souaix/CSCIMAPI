using System;
using System.Collections.Generic;
using DynamicExpresso;

namespace Infrastructure.Utilities
{
	public static class EvalHelper
	{
		private static readonly Interpreter _interpreter = new Interpreter();

		public static bool Evaluate(string formula, Dictionary<string, object> context)
		{
			try
			{
				// 使用新的 Interpreter 避免變數污染
				var interpreter = new Interpreter();

				// 註冊變數（全部轉成 string 做比對）
				foreach (var kvp in context)
				{
					interpreter.SetVariable(kvp.Key, kvp.Value?.ToString() ?? "");
				}

				// 預處理：去掉包裹的雙引號
				if (formula.StartsWith("\"") && formula.EndsWith("\""))
					formula = formula.Substring(1, formula.Length - 2);

				// 執行表達式
				var result = interpreter.Eval(formula);
				return result is bool b && b;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[Eval Error] formula: {formula}, error: {ex.Message}");
				return false;
			}
		}
	}
}