using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Core.Utilities;

namespace UnitTest.LaserMarking
{
	public class NewSNGeneratorTests
	{
		[Fact]
		public void NormalNewSN_ShouldIncrementProperly_Case1()
		{
			// 測試條件 1
			string lastSN = "0000";
			string availableChars = "ABCXYZ0123456789";
			var generator = new NewSNGenerator(availableChars);

			string newSN = generator.GenerateSN(lastSN, "", true); // 呼叫 NormalNewSN

			Assert.Equal("0002", newSN); // 驗證結果
		}

		[Fact]
		public void NormalNewSN_ShouldIncrementProperly_Case2()
		{
			// 測試條件 2
			string lastSN = "A2C9";
			string availableChars = "ABCXYZ0123456789";
			var generator = new NewSNGenerator(availableChars);

			string newSN = generator.GenerateSN(lastSN, "", true);

			Assert.Equal("A2XA", newSN); // 驗證結果
		}

		[Fact]
		public void A00NewSN_ShouldIncrementProperly_Case1()
		{
			// 測試條件 1
			string lastSN = "A00";
			string charSet = "ABCXYZ0123456789";
			var generator = new NewSNGenerator(charSet);

			string newSN = generator.GenerateSN(lastSN, "A00", true); // 呼叫 A00NewSN

			Assert.Equal("A01", newSN); // 預期結果
		}

		[Fact]
		public void A00NewSN_ShouldIncrementProperly_Case2()
		{
			// 測試條件 2
			string lastSN = "AB9";
			string charSet = "ABCXYZ0123456789";
			var generator = new NewSNGenerator(charSet);

			string newSN = generator.GenerateSN(lastSN, "A00", true);

			Assert.Equal("AC0", newSN); // 預期結果
		}

		[Fact]
		public void AA00NewSN_ShouldIncrementProperly_Case1()
		{
			// 測試條件 1（修正後）
			string lastSN = "AA-00";  // 如果 0 不是合法的 AA00 格式，應該轉為 "AA-01"
			string charSet = "ABCXYZ0123456789";
			var generator = new NewSNGenerator(charSet);

			string newSN = generator.GenerateSN(lastSN, "AA00", true); // 呼叫 AA00NewSN

			Assert.Equal("AA-01", newSN); // 預期結果
		}

		[Fact]
		public void AA00NewSN_ShouldIncrementProperly_Case2()
		{
			// 測試條件 2
			string lastSN = "AA-98";
			string charSet = "ABCXYZ0123456789";
			var generator = new NewSNGenerator(charSet);

			string newSN = generator.GenerateSN(lastSN, "AA00", true);

			Assert.Equal("AB-01", newSN); // 預期結果
		}
	}


}


