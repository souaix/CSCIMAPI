// UnitTest/LotTileCheckTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Entities.DboEmap;
using Core.Entities.LotTileCheck;
using Core.Interfaces;
using Infrastructure.Services;
using Infrastructure.Utilities;
//using Mock;
using Xunit;

namespace UnitTest.LotTileCheck
{
	public class LotTileCheckTests
	{
		/* ----------------- 共用：建立 Service 與 RepositoryFactory Mock ----------------- */
		//private static LotTileCheckService BuildService(
		//	Mock<IRepository> repoDboMock,
		//	Mock<IRepository> repoCimMock)
		//{
		//	var repoFactoryMock = new Mock<IRepositoryFactory>();

		//	// 依照傳入的 environment 字串決定回傳哪支 Repository
		//	repoFactoryMock
		//		.Setup(f => f.CreateRepository(It.Is<string>(s =>
		//				s.Contains("dbo", StringComparison.OrdinalIgnoreCase) ||
		//				s.Equals("Prod", StringComparison.OrdinalIgnoreCase) ||
		//				s.Equals("Test", StringComparison.OrdinalIgnoreCase))))
		//		.Returns(repoDboMock.Object);

		//	repoFactoryMock
		//		.Setup(f => f.CreateRepository(It.Is<string>(s =>
		//				s.Contains("cim", StringComparison.OrdinalIgnoreCase))))
		//		.Returns(repoCimMock.Object);

		//	return new LotTileCheckService(repoFactoryMock.Object);
		//}

		///* ----------  Test 1 : 解析查詢範圍 (S4 範例)  ---------- */
		//[Fact]
		//public async Task DataQueryModeAsync_ShouldResolve_S4_StepsAndDeviceIds()
		//{
		//	var repoMock = new Mock<IRepository>();

		//	// 1. 先 Mock 查 STEP → 取得 (QueryMode, StepGroup)
		//	repoMock.Setup(r =>
		//			r.QueryFirstOrDefaultAsync<(string QueryMode, string StepGroup)>(
		//				It.IsAny<string>(), It.IsAny<object>()))
		//		.ReturnsAsync((QueryMode: "S4", StepGroup: "EdgeTrim"));

		//	// 2. Mock 查該群組所有 STEP
		//	repoMock.Setup(r =>
		//			r.QueryAsync<string>(
		//				It.Is<string>(sql => sql.Contains("FROM ARGOAPILOTTILESTEPMODE")),
		//				It.IsAny<object>()))
		//		.ReturnsAsync(new List<string> { "BTS00071", "BTS00072" });

		//	// 3. Mock 查 RULE → DEVICEIDS
		//	repoMock.Setup(r =>
		//			r.QueryAsync<string>(
		//				It.Is<string>(sql => sql.Contains("FROM ARGOAPILOTTILERULECHECK")),
		//				It.IsAny<object>()))
		//		.ReturnsAsync(new List<string> { "AT-001,MT-007", "LU-T01" });

		//	// 4. 呼叫 Service.DataQueryModeAsync
		//	var service = new LotTileCheckService(new Mock<IRepositoryFactory>().Object); // 方法為 static-like，不用 factory
		//	var (steps, devices) = await service.DataQueryModeAsync(repoMock.Object, "BTS00071", "AT-001");

		//	Assert.Equal(2, steps.Count);
		//	Assert.Contains("BTS00072", steps);
		//	Assert.Equal(3, devices.Count);
		//	Assert.Contains("LU-T01", devices);
		//}

		///* ----------  Test 2 : 最新 TILE SQL 被正確產生 ---------- */
		//[Fact]
		//public async Task CheckLotTileAsync_ShouldComposeLatestTileSql()
		//{
		//	var repoDboMock = new Mock<IRepository>();
		//	var repoCimMock = new Mock<IRepository>();

		//	// Mock STEP → S1
		//	repoCimMock.Setup(r =>
		//			r.QueryFirstOrDefaultAsync<(string QueryMode, string StepGroup)>(
		//				It.IsAny<string>(), It.IsAny<object>()))
		//		.ReturnsAsync((QueryMode: "S1", StepGroup: "Debond"));

		//	// Mock RULE 查詢為空 (避免後面流程)
		//	repoCimMock.Setup(q =>
		//		q.QueryAsync<RuleCheckDefinition>(It.IsAny<string>(), It.IsAny<object>()))
		//		.ReturnsAsync(new List<RuleCheckDefinition>
		//		{
		//			new RuleCheckDefinition
		//			{
		//				Step = "BTS00001",
		//				DeviceIds = "LU-01",
		//				EvalFormula = "V001 == \"OK\"",
		//				Reason = "NG",
		//				EnableNg = "Y"
		//			}	
		//		});

		//	var service = BuildService(repoDboMock, repoCimMock);

		//	await service.CheckLotTileAsync(new LotTileCheckRequest
		//	{
		//		Environment = "Test",
		//		LotNo = "WB2024C00011",
		//		Step =  "BTS00001" ,
		//		DeviceId =  "LU-01" 
		//	});

		//	// 驗證 DBO Repository 有被呼叫到 SQL 且包含 ROW_NUMBER() 片段
		//	repoDboMock.Verify(r =>
		//			r.QueryAsync<TblMesWipData_Record>(
		//				It.Is<string>(sql => sql.Contains("ROW_NUMBER() OVER") && sql.Contains("RN = 1")),
		//				It.IsAny<object>()),
		//		Times.Once);
		//}

		/* ----------  Test 3 : 每筆 WIP 僅套用自己的規則 ---------- */
		//[Fact]
		//public async Task CheckLotTileAsync_ShouldApply_CorrectRulePerDevice()
		//{
		//	var repoDboMock = new Mock<IRepository>();
		//	var repoCimMock = new Mock<IRepository>();

		//	// STEP → S1
		//	repoCimMock.Setup(r =>
		//			r.QueryFirstOrDefaultAsync<(string, string)>(It.IsAny<string>(), It.IsAny<object>()))
		//		.ReturnsAsync((QueryMode: "S1", StepGroup: "Debond"));

		//	// RULE：LU-T01 → V036 == "NG"
		//	repoCimMock.Setup(r =>
		//			r.QueryAsync<RuleCheckDefinition>(It.IsAny<string>(), It.IsAny<object>()))
		//		.ReturnsAsync(new List<RuleCheckDefinition>
		//		{
		//			new RuleCheckDefinition
		//			{
		//				Step = "BTS00071",
		//				DeviceIds = "LU-T01",
		//				EvalFormula = "V036 == \"NG\"",
		//				Reason = "NG",
		//				DaysRange = 7,
		//				EnableNg = "Y"
		//			}
		//		});

		//	// Mock WIP 最新資料：V036 = "NG"
		//	repoDboMock.Setup(r =>
		//			r.QueryAsync<TblMesWipData_Record>(It.IsAny<string>(), It.IsAny<object>()))
		//		.ReturnsAsync(new List<TblMesWipData_Record>
		//		{
		//			new TblMesWipData_Record
		//			{
		//				TileId = "TL001",
		//				LotNo = "WB2024C00011",
		//				Step = "BTS00071",
		//				DeviceId = "LU-T01",
		//				RecordDate = DateTime.Now,
		//				V036 = "NG"
		//			}
		//		});

		//	var service = BuildService(repoDboMock, repoCimMock);

		//	var ret = await service.CheckLotTileAsync(new LotTileCheckRequest
		//	{
		//		Environment = "Test",
		//		LotNo = "WB2024C00011",
		//		Step =  "BTS00071" ,
		//		DeviceId =  "LU-T01" 
		//	});

		//	//Assert.Equal("Ok", ret.Result);
		//	//var dto = Assert.Single(ret.Data);
		//	//Assert.Equal("Black", dto.ResultList);
		//	//Assert.Equal("NG", dto.Reason);

		//	Assert.Equal("Warning", ret.Result);
		//	Assert.Equal("無對應規則", ret.Message);

		//}

		/* ----------  Test 4 : 無規則 → Fail ---------- */
		//[Fact]
		//public async Task CheckLotTileAsync_ShouldFail_WhenNoRuleMatched()
		//{
		//	var repoDboMock = new Mock<IRepository>();
		//	var repoCimMock = new Mock<IRepository>();

		//	// STEP → S1
		//	repoCimMock.Setup(r =>
		//			r.QueryFirstOrDefaultAsync<(string, string)>(It.IsAny<string>(), It.IsAny<object>()))
		//		.ReturnsAsync((QueryMode: "S1", StepGroup: "Debond"));

		//	// 規則清單為空
		//	repoCimMock.Setup(r =>
		//			r.QueryAsync<RuleCheckDefinition>(It.IsAny<string>(), It.IsAny<object>()))
		//		.ReturnsAsync(new List<RuleCheckDefinition>());

		//	var service = BuildService(repoDboMock, repoCimMock);

		//	var ret = await service.CheckLotTileAsync(new LotTileCheckRequest
		//	{
		//		Environment = "Test",
		//		LotNo = "WB2024C00011",
		//		Step =  "BTS00001" ,
		//		DeviceId = "LU-01" 
		//	});

		//	Assert.Equal("Warning", ret.Result);
		//	Assert.Equal("無對應規則", ret.Message);
		//}
	}
}
