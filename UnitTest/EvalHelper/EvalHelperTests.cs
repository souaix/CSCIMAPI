using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using Infrastructure.Utilities;
namespace UnitTest.EvalHelperTests
{
	public class EvalHelperTests
	{
		[Fact]
		public void Test_V007_NG_Should_Return_True()
		{
			var formula = "V007 == \"NG\"";
			var context = new Dictionary<string, object>
			{
				["V007"] = "NG"
			};

			var result = EvalHelper.Evaluate(formula, context);
			Assert.True(result);
		}

		[Fact]
		public void Test_V001_Or_V002_Should_Return_True()
		{
			var formula = "V001 == \"NG\" || V002 == \"NG\"";
			var context = new Dictionary<string, object>
			{
				["V001"] = "OK",
				["V002"] = "NG"
			};

			var result = EvalHelper.Evaluate(formula, context);
			Assert.True(result);
		}

		[Fact]
		public void Test_Combined_And_Should_Return_False()
		{
			var formula = "V005 == \"1\" && V036 == \"1\"";
			var context = new Dictionary<string, object>
			{
				["V005"] = "1",
				["V036"] = "0"
			};

			var result = EvalHelper.Evaluate(formula, context);
			Assert.False(result);
		}
	}
}
