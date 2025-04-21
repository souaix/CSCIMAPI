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
				// 註冊 context 中的每個變數
				foreach (var kvp in context)
				{
					// 將字串轉成 C# 可比對的格式（null, "NG", 1, ...）
					var value = kvp.Value;
					_interpreter.SetVariable(kvp.Key, value ?? "");
				}

				var result = _interpreter.Eval(formula);
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