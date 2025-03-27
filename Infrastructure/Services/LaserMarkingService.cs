using Core.Interfaces;
using Core.Entities.Public;
using Core.Entities.LaserMarking;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR.Protocol;
using System.Data;
using Core.Utilities;
using Org.BouncyCastle.Asn1.Ocsp;
//test
namespace Infrastructure.Services
{
    public class LaserMarkingService : ILaserMarkingService
    {
        private readonly IRepositoryFactory _repositoryFactory;

        public LaserMarkingService(IRepositoryFactory repositoryFactory)
        {
            _repositoryFactory = repositoryFactory;
        }

        public async Task<ApiReturn<IEnumerable<Config>>> GetConfigDataAsync(LaserMarkingRequest request)
        {
            if (request.Product.Length < 4)
                return ApiReturn<IEnumerable<Config>>.Failure($"{request.Product} 輸入有誤！");

            if (string.IsNullOrEmpty(request.Environment) || string.IsNullOrEmpty(request.Product))
                return ApiReturn<IEnumerable<Config>>.Failure("Invalid input parameters.");

            string configName = $"{request.Size}{request.Product}-{request.Version}-{request.StepCode}".Trim('-');

            var repository = _repositoryFactory.CreateRepository(request.Environment);

            //string sql = $"SELECT * FROM CONFIG WHERE CONFIG_NAME = '{configName}'";
            string sql = @"SELECT 
                    CONFIG_NAME AS ConfigName,
                    CUSTOMER AS Customer,
                    SIDE AS Side,
                    BLOCK_QTY AS BlockQty,
                    PANEL_QTY AS PanelQty,
                    YEAR_CODE AS YearCode,
                    MONTH_CODE AS MonthCode,
                    DAY_CODE AS DayCode,
                    TILEID AS TileId,
                    TOP_TILETEXT01 AS TopTileText01,
                    TOP_TILETEXT02 AS TopTileText02,
                    TOP_TILETEXT03 AS TopTileText03,
                    TOP_TILETEXT04 AS TopTileText04,
                    TOP_TILETEXT05 AS TopTileText05,
                    TOP_CELLTEXT01 AS TopCellText01,
                    TOP_CELLTEXT02 AS TopCellText02,
                    TOP_CELLTEXT03 AS TopCellText03,
                    TOP_CELLTEXT04 AS TopCellText04,
                    TOP_CELLTEXT05 AS TopCellText05,
                    TOP_RULEFILE1 AS TopRuleFile1,
                    TOP_RULEFILE2 AS TopRuleFile2,
                    TOP_RULEFILE3 AS TopRuleFile3,
                    TOP_CELLDIRECTION AS TopCellDirection,
                    BACK_TILETEXT01 AS BackTileText01,
                    BACK_TILETEXT02 AS BackTileText02,
                    BACK_TILETEXT03 AS BackTileText03,
                    BACK_TILETEXT04 AS BackTileText04,
                    BACK_TILETEXT05 AS BackTileText05,
                    BACK_CELLTEXT01 AS BackCellText01,
                    BACK_CELLTEXT02 AS BackCellText02,
                    BACK_CELLTEXT03 AS BackCellText03,
                    BACK_CELLTEXT04 AS BackCellText04,
                    BACK_CELLTEXT05 AS BackCellText05,
                    BACK_RULEFILE1 AS BackRuleFile1,
                    BACK_RULEFILE2 AS BackRuleFile2,
                    BACK_RULEFILE3 AS BackRuleFile3,
                    BACK_CELLDIRECTION AS BackCellDirection,
                    CREATEDATE AS CreateDate,
                    CREATETIME AS CreateTime
                FROM config  WHERE CONFIG_NAME = '" + configName + "'";


            var result = await repository.QueryFirstOrDefaultAsync<Config>(sql);

            //Console.WriteLine(result.ConfigName);


            // 包裝到 ApiReturn 中
            return result != null
                ? ApiReturn<IEnumerable<Config>>.Success("Data retrieved successfully.", new List<Config> { result })
                : ApiReturn<IEnumerable<Config>>.Failure("No data found.");
        }


        public async Task<ApiReturn<string>> GenerateTileIdsAsync(LaserMarkingRequest request)
        {
            var repository = _repositoryFactory.CreateRepository(request.Environment);

            var config = await repository.QueryFirstOrDefaultAsync<Config>(
                "SELECT * FROM Config WHERE Config_Name = @ConfigName",
                new { ConfigName = request.Product }
            );
            if (config == null)
                return ApiReturn<string>.Failure("Config 設定檔未找到!");

            var customerConfig = await repository.QueryFirstOrDefaultAsync<CustomerConfig>(
                "SELECT * FROM CustomerConfig WHERE Customer = @Customer",
                new { Customer = request.Customer }
            );
            if (customerConfig == null)
                return ApiReturn<string>.Failure("CustomerConfig 設定未找到!");

            // 檢查 StepCode 是否存在於 opno_prefix
            bool stepCodeExistsInOpnoPrefix = await repository.QueryFirstOrDefaultAsync<bool>(
                "SELECT COUNT(*) > 0 FROM opno_prefix WHERE opno = @StepCode",
                new { StepCode = request.StepCode }
            );


            // **處理正面編碼**
            var (topTileIds, topLotCreatorList, topLastSN) = await GenerateTileIds(request, config, customerConfig, isBackSide: false, repository);
            await SaveLotCreatorData(repository, request.LotNo, topLotCreatorList, request.StepCode);

            

            string finalProductTileId = topLotCreatorList.LastOrDefault(l => !string.IsNullOrEmpty(l.TileText02))?.TileText02
                ?? topLotCreatorList.LastOrDefault(l => !string.IsNullOrEmpty(l.TileText01))?.TileText01;

            Console.WriteLine($"Final TileID for Product Table: {finalProductTileId}");

            // **存入 Product Table**
            if (stepCodeExistsInOpnoPrefix)
            {
                // StepCode 存在於 opno_prefix，只插入 C+LotNo 和 D+LotNo
                await SaveProductTable(repository, "C" + request.LotNo, request.Customer, $"{request.Product}-TOP-C", topTileIds, topLastSN, finalProductTileId);
                await SaveProductTable(repository, "D" + request.LotNo, request.Customer, $"{request.Product}-TOP-D", topTileIds, topLastSN, finalProductTileId);
            }
            else
            {
                // StepCode 不存在於 opno_prefix，插入 LotNo
                await SaveProductTable(repository, request.LotNo, request.Customer, $"{request.Product}-TOP", topTileIds, topLastSN, finalProductTileId);
            }

            // **存入 Product Table (正面)**
            //await repository.ExecuteAsync(@"
            //INSERT INTO Product (LotNO, Customer, ProductName, TileID, TileIDEnd, LastSN, Quantity, CreateDate, CreateTime)
            //VALUES (@LotNO, @Customer, @ProductName, @TileID, @TileIDEnd, @LastSN, @Quantity, @CreateDate, @CreateTime)",
            //    new
            //    {
            //        LotNO = request.LotNo,
            //        Customer = request.Customer,
            //        ProductName = config.Side == 2 ? $"{request.Product}-TOP" : request.Product,
            //        TileID = topTileIds.First(),
            //        //TileIDEnd = topTileIds.Last(),
            //        TileIDEnd = finalProductTileId,
            //        LastSN = topLastSN,
            //        Quantity = topTileIds.Count,
            //        CreateDate = DateTime.Now.ToString("yyMMddWW"),
            //        CreateTime = DateTime.Now.ToString("HH:mm:ss")
            //    });


            // **處理背面編碼 (如果 SIDE = 2)**
            List<string> backTileIds = new();
            string backLastSN = ""; // 預設一個空字串
            //Console.WriteLine(topTileIds.Last());

            // **存入 Product Table (背面)**
            if (config.Side == 2)
            {

                var (generatedBackTileIds, backLotCreatorList, tempBackLastSN) = await GenerateTileIds(request, config, customerConfig, isBackSide: true, repository);
                backTileIds = generatedBackTileIds;
                backLastSN = tempBackLastSN; // 這裡使用 tempBackLastSN 來避免衝突
                await SaveLotCreatorData(repository, "B" + request.LotNo, backLotCreatorList, request.StepCode);


                // **取得背面 TileIDEnd，應該存入 TileText02 的最後一筆**
                string finalBackTileId = backLotCreatorList.LastOrDefault(l => !string.IsNullOrEmpty(l.TileText02))?.TileText02
                    ?? backLotCreatorList.LastOrDefault(l => !string.IsNullOrEmpty(l.TileText01))?.TileText01;

                // **存入背面 Product Table**

                if (stepCodeExistsInOpnoPrefix)
                {
                    // StepCode 存在於 opno_prefix，插入 BC+LotNo 和 BD+LotNo
                    await SaveProductTable(repository, "BC" + request.LotNo, request.Customer, $"{request.Product}-BACK-C", backTileIds, backLastSN, finalBackTileId);
                    await SaveProductTable(repository, "BD" + request.LotNo, request.Customer, $"{request.Product}-BACK-D", backTileIds, backLastSN, finalBackTileId);
                }
                else
                {
                    // StepCode 不存在於 opno_prefix，插入 B+LotNo
                    await SaveProductTable(repository, "B" + request.LotNo, request.Customer, $"{request.Product}-BACK", backTileIds, backLastSN, finalBackTileId);
                }


                //await repository.ExecuteAsync(@"
                //INSERT INTO Product (LotNO, Customer, ProductName, TileID, TileIDEnd, LastSN, Quantity, CreateDate, CreateTime)
                //VALUES (@LotNO, @Customer, @ProductName, @TileID, @TileIDEnd, @LastSN, @Quantity, @CreateDate, @CreateTime)",
                //        new
                //        {
                //            LotNO = "B" + request.LotNo,
                //            Customer = request.Customer,
                //            ProductName = $"{request.Product}-BACK",
                //            TileID = backTileIds.First(),
                //            //TileIDEnd = backTileIds.Last(),
                //            TileIDEnd = finalBackTileId,
                //            LastSN = backLastSN,
                //            Quantity = backTileIds.Count,
                //            CreateDate = DateTime.Now.ToString("yyMMddWW"),
                //            CreateTime = DateTime.Now.ToString("HH:mm:ss")
                //        });
            }


            return ApiReturn<string>.Success("Tile ID 產生成功", topTileIds.Last());

            
        }

        private async Task<(List<string>, List<LotCreator>, string lastSN)> GenerateTileIds(LaserMarkingRequest request, Config config, CustomerConfig customerConfig, bool isBackSide,
    IRepository repository)
        {
            var tileIds = new List<string>();
            var lotCreators = new List<LotCreator>();
            var existingTileIds = new HashSet<string>();
            //int encodeCount = request.SubBigQty * config.Block_Qty + 4;
            // 轉換 SubBigQty 為 int，並確保 Block_Qty 不是 null
            // 查詢最後一筆 SN
            string query = "SELECT TileID, Quantity, TileIDEnd, LastSN FROM Product WHERE LotNO = @LotNO ORDER BY CreateDate DESC LIMIT 1";
            var lastProduct = await repository.QueryFirstOrDefaultAsync<Product>(query, new { LotNO = request.LotNo });


            int encodeCount = int.Parse(request.SubBigQty) * (config.Block_Qty ?? 1) + 4;
            var tileTextFields = isBackSide
                ? new List<string?> { config.Back_TileText01, config.Back_TileText02, config.Back_TileText03, config.Back_TileText04, config.Back_TileText05 }
                : new List<string?> { config.Top_TileText01, config.Top_TileText02, config.Top_TileText03, config.Top_TileText04, config.Top_TileText05 };
            var cellTextFields = isBackSide
                ? new List<string?> { config.Back_CellText01, config.Back_CellText02, config.Back_CellText03, config.Back_CellText04, config.Back_CellText05 }
                : new List<string?> { config.Top_CellText01, config.Top_CellText02, config.Top_CellText03, config.Top_CellText04, config.Top_CellText05 };

            // 將 Config 轉換為 Dictionary
            var configMapping = new Dictionary<string, string>
                {
                    { "YEAR_CODE", config.Year_Code },
                    { "MONTH_CODE", config.Month_Code },
                    { "DAY_CODE", config.Day_Code }
                };

            var snGenerator = new NewSNGenerator();
            //string lastSN = customerConfig.SelectLastSn;
            int snLength = customerConfig.SnLength ?? 5; // 預設值 5
            string lastSN = lastProduct?.LastSN ?? new string('0', snLength);

            //string lastSN = lastProduct;

            for (int i = 0; i < encodeCount; i++)
            {
                var tileTexts = new List<string>();
                //var cellTexts = new List<string>();

                foreach (var tileText in tileTextFields)
                {
                    if (string.IsNullOrEmpty(tileText)) continue;
                    var elements = tileText.Split(',');
                    var convertedElements = new List<string>();
                    
                    foreach (var element in elements)
                    {
                        string convertedValue = null;


                        // 只對 SN1 / GSC1 呼叫 NewSNGenerator
                        if (element == "SN1" || element == "GSC1")
                        {
                            convertedValue = snGenerator.GenerateSN(lastSN, customerConfig.NewSnPattern, true);
                            lastSN = convertedValue; // 更新 lastSN
                        }
                        // 只對 SN / GSC 呼叫 NewSNGenerator（但不遞增）
                        else if (element == "SN" || element == "GSC")
                        {
                            convertedValue = snGenerator.GenerateSN(lastSN, customerConfig.NewSnPattern, false);
                        }
                        // 其他元素才進行 StringCodeMapping 或 DateCodeMapping
                        else
                        {
                            convertedValue = StringCodeMapping.Convert(element, request.LotNo) ??
                                             DateCodeMapping.Convert(element, configMapping) ??
                                             element; // 保留原始值
                        }

                        convertedElements.Add(convertedValue);

                    }
                    //tileIds.Add(string.Join("", convertedElements));
                    tileTexts.Add(string.Join("", convertedElements));
                }

                string finalTileId = tileTexts.ElementAtOrDefault(0);
                Console.WriteLine($"Generated TileID: {finalTileId}");

                if (existingTileIds.Contains(finalTileId))
                    throw new Exception($"TileID: {finalTileId} 重複，請通知 IT 人員");

                existingTileIds.Add(finalTileId);
                tileIds.Add(finalTileId);

                lotCreators.Add(new LotCreator
                {
                    TileID = finalTileId,
                    MachineID = "Machine001", // 這裡可以根據你的需求填充
                    MarkDate = DateTime.Now.ToString("yyMMdd"),
                    MarkTime = DateTime.Now.ToString("HHmmss"),
                    ReworkDate = "",
                    ReworkTime = "",
                    TileText01 = tileTexts.ElementAtOrDefault(0),
                    TileText02 = tileTexts.ElementAtOrDefault(1),
                    TileText03 = tileTexts.ElementAtOrDefault(2),
                    TileText04 = tileTexts.ElementAtOrDefault(3),
                    TileText05 = tileTexts.ElementAtOrDefault(4),
                    //CellText01 = cellTexts.ElementAtOrDefault(0),
                    //CellText02 = cellTexts.ElementAtOrDefault(1),
                    //CellText03 = cellTexts.ElementAtOrDefault(2),
                    //CellText04 = cellTexts.ElementAtOrDefault(3),
                    //CellText05 = cellTexts.ElementAtOrDefault(4),
                    CellText01 = "",
                    CellText02 = "",
                    CellText03 = "",
                    CellText04 = "",
                    CellText05 = "",
                    DotData = "",
                    Tile01Count = "0",
                    Tile02Count = "0",
                    Tile03Count = "0",
                    Tile04Count = "0",
                    Tile05Count = "0"
                });
            }
            //return tileIds;
            return (tileIds, lotCreators, lastSN);
        }


        private async Task SaveLotCreatorData(IRepository repository, string lotNo, List<LotCreator> lotCreators, string stepCode)
        {
            // 查詢 opno_prefix，確認 stepCode 是否存在
            bool stepCodeExists = await repository.QueryFirstOrDefaultAsync<string>(
                "SELECT opno FROM opno_prefix WHERE opno = @StepCode",
                new { StepCode = stepCode }
            ) != null;

            // 需要創建的 Table 清單
            List<string> tableNames = new();

            //if (stepCodeExists)
            //{
            //    // 若 LotNo 以 "B" 開頭，則創建 BC+LotNo 和 BD+LotNo
            //    if (lotNo.StartsWith("B"))
            //    {
            //        //tableNames.Add($"BC{lotNo}");
            //        //tableNames.Add($"BD{lotNo}");
            //        // 修正插入 C 和 D 的位置
            //        //tableNames.Add($"B{lotNo[1]}C{lotNo.Substring(2)}");
            //        //tableNames.Add($"B{lotNo[1]}D{lotNo.Substring(2)}");

            //        // 修正插入 C 和 D 的位置
            //        //tableNames.Add($"B{lotNo.Insert(1, "C")}");
            //        //tableNames.Add($"B{lotNo.Insert(1, "D")}");

            //        // 在 "B" 之後的 "W" 之前插入 "C" 或 "D"
            //        tableNames.Add($"B{lotNo.Substring(1, 1)}C{lotNo.Substring(2)}");
            //        tableNames.Add($"B{lotNo.Substring(1, 1)}D{lotNo.Substring(2)}");

            //    }
            //    else
            //    {
            //        // 一般情況，創建 C+LotNo 和 D+LotNo
            //        tableNames.Add($"C{lotNo}");
            //        tableNames.Add($"D{lotNo}");
            //    }
            //}
            //else
            //{
            //    // 若 stepCode 不存在於 opno_prefix，則使用原始 LotNo 作為 Table 名稱
            //    tableNames.Add(lotNo);
            //}


            if (stepCodeExists)
            {
                if (lotNo.StartsWith("B"))
                {
                    // 找到 "W" 的位置
                    int wIndex = lotNo.IndexOf("W");
                    if (wIndex > 0)
                    {
                        // 在 "W" 之前插入 "C" 或 "D"
                        tableNames.Add($"{lotNo.Insert(wIndex, "C")}");
                        tableNames.Add($"{lotNo.Insert(wIndex, "D")}");
                    }
                    else
                    {
                        // 如果找不到 W，回退到一般邏輯
                        tableNames.Add($"C{lotNo}");
                        tableNames.Add($"D{lotNo}");
                    }
                }
                else
                {
                    tableNames.Add($"C{lotNo}");
                    tableNames.Add($"D{lotNo}");
                }
            }
            else
            {
                tableNames.Add(lotNo);
            }


            // 迭代所有需要創建的 Table，執行建立 Table 與插入數據
            foreach (var tableName in tableNames)
            {
                await repository.ExecuteAsync($@"
                CREATE TABLE IF NOT EXISTS `{tableName}` (
                    TileID VARCHAR(50) PRIMARY KEY,
                    MachineID VARCHAR(50),
                    MarkDate VARCHAR(10),
                    MarkTime VARCHAR(10),
                    ReworkDate VARCHAR(10),
                    ReworkTime VARCHAR(10),
                    TileText01 VARCHAR(50),
                    TileText02 VARCHAR(50),
                    TileText03 VARCHAR(50),
                    TileText04 VARCHAR(50),
                    TileText05 VARCHAR(50),
                    CellText01 VARCHAR(50),
                    CellText02 VARCHAR(50),
                    CellText03 VARCHAR(50),
                    CellText04 VARCHAR(50),
                    CellText05 VARCHAR(50),
                    DotData VARCHAR(50),
                    Tile01Count INT,
                    Tile02Count INT,
                    Tile03Count INT,
                    Tile04Count INT,
                    Tile05Count INT
                )");

                foreach (var lotCreator in lotCreators)
                {
                    await repository.ExecuteAsync($@"
                INSERT INTO `{tableName}` (TileID, MachineID, MarkDate, MarkTime, ReworkDate, ReworkTime,
                                             TileText01, TileText02, TileText03, TileText04, TileText05,
                                             CellText01, CellText02, CellText03, CellText04, CellText05,
                                             DotData, Tile01Count, Tile02Count, Tile03Count, Tile04Count, Tile05Count)
                VALUES (@TileID, @MachineID, @MarkDate, @MarkTime, @ReworkDate, @ReworkTime,
                        @TileText01, @TileText02, @TileText03, @TileText04, @TileText05,
                        @CellText01, @CellText02, @CellText03, @CellText04, @CellText05,
                        @DotData, @Tile01Count, @Tile02Count, @Tile03Count, @Tile04Count, @Tile05Count)", lotCreator);
                }
            }


            //string lotTableName = $"LotCreator_{lotNo}";
            //string lotTableName = lotNo; // 直接使用 lotNo 作為表名

            //await repository.ExecuteAsync($@"
            //CREATE TABLE IF NOT EXISTS `{lotTableName}` (
            //    TileID VARCHAR(50) PRIMARY KEY,
            //    MachineID VARCHAR(50),
            //    MarkDate VARCHAR(10),
            //    MarkTime VARCHAR(10),
            //    ReworkDate VARCHAR(10),
            //    ReworkTime VARCHAR(10),
            //    TileText01 VARCHAR(50),
            //    TileText02 VARCHAR(50),
            //    TileText03 VARCHAR(50),
            //    TileText04 VARCHAR(50),
            //    TileText05 VARCHAR(50),
            //    CellText01 VARCHAR(50),
            //    CellText02 VARCHAR(50),
            //    CellText03 VARCHAR(50),
            //    CellText04 VARCHAR(50),
            //    CellText05 VARCHAR(50),
            //    DotData VARCHAR(50),
            //    Tile01Count INT,
            //    Tile02Count INT,
            //    Tile03Count INT,
            //    Tile04Count INT,
            //    Tile05Count INT
            //)");

            //    foreach (var lotCreator in lotCreators)
            //{
            //    await repository.ExecuteAsync($@"
            //    INSERT INTO `{lotTableName}` (TileID, MachineID, MarkDate, MarkTime, ReworkDate, ReworkTime,
            //                                 TileText01, TileText02, TileText03, TileText04, TileText05,
            //                                 CellText01, CellText02, CellText03, CellText04, CellText05,
            //                                 DotData, Tile01Count, Tile02Count, Tile03Count, Tile04Count, Tile05Count)
            //    VALUES (@TileID, @MachineID, @MarkDate, @MarkTime, @ReworkDate, @ReworkTime,
            //            @TileText01, @TileText02, @TileText03, @TileText04, @TileText05,
            //            @CellText01, @CellText02, @CellText03, @CellText04, @CellText05,
            //            @DotData, @Tile01Count, @Tile02Count, @Tile03Count, @Tile04Count, @Tile05Count)", lotCreator);
            //}
        }


        private async Task SaveProductTable(IRepository repository, string lotNo,string customer, string productName, List<string> tileIds, string lastSN,string finalTileid)
        {
            string tileIDEnd = tileIds.LastOrDefault() ?? tileIds.First();

            await repository.ExecuteAsync(@"
            INSERT INTO Product (LotNO, Customer, ProductName, TileID, TileIDEnd, LastSN, Quantity, CreateDate, CreateTime)
            VALUES (@LotNO, @Customer, @ProductName, @TileID, @TileIDEnd, @LastSN, @Quantity, @CreateDate, @CreateTime)",
                new
                {
                    LotNO = lotNo,
                    Customer = customer,
                    ProductName = productName,
                    TileID = tileIds.First(),
                    TileIDEnd = finalTileid,
                    LastSN = lastSN,
                    Quantity = tileIds.Count,
                    CreateDate = DateTime.Now.ToString("yyMMddWW"),
                    CreateTime = DateTime.Now.ToString("HH:mm:ss")
                });
        }
    }
}
