using Core.Interfaces;
using Core.Entities.Public;
using Core.Entities.LaserMarking;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR.Protocol;
using System.Data;
using Core.Utilities;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Globalization;
using Infrastructure.Utilities;
using System.Text.RegularExpressions;

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

            //var repository = _repositoryFactory.CreateRepository(request.Environment);
            var (oracleRepo, repository) = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);

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
            
            //20250422 改接兩個DB設定
            var (oracleRepo, repository) = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);

            // 2. 取得 Config（依據 request.Product）
            var config = await repository.QueryFirstOrDefaultAsync<Config>(
                "SELECT * FROM Config WHERE Config_Name = @ConfigName",
                new { ConfigName = request.Product }
            );
            if (config == null)
                return ApiReturn<string>.Failure("Config 設定檔未找到!");

			// 1.1 取得 CustomerConfig（依據 request.Customer）
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
            // 4.1 呼叫 GenerateTileIds，處理正面（isBackSide: false）
            //var (topTileIds, topLotCreatorList, topLastSN) = await GenerateTileIds(request, config, customerConfig, isBackSide: false, repository);
            var (topTileIds, topLotCreatorList, originalLastSN, topLastSN) = await GenerateTileIds(request, config, customerConfig, isBackSide: false, repository);
            // 4.2.6 呼叫 SaveLotCreatorData，儲存正面資料（LotNo 不加 B）
            await SaveLotCreatorData(repository, request.LotNo, topLotCreatorList, request.StepCode);
            
            

            string lotTable = request.LotNo;
            var panelRows = await repository.QueryAsync<dynamic>($"SELECT * FROM `{lotTable}`");

            var panelTileList = new List<List<string>>();

            foreach (var row in panelRows)
            {
                var tiles = new List<string>();

                if (row.TileText01 != null) tiles.Add((string)row.TileText01);
                if (row.TileText02 != null) tiles.Add((string)row.TileText02);
                if (row.TileText03 != null) tiles.Add((string)row.TileText03);
                if (row.TileText04 != null) tiles.Add((string)row.TileText04);
                if (row.TileText05 != null) tiles.Add((string)row.TileText05);

                panelTileList.Add(tiles);
            }

            //增加寫入 MAP 的 TBLWIPLOTMARKINGDATA
            await InsertWipLotMarkingDataToOracle(request.LotNo, panelTileList, oracleRepo);

            // 4.2.7.5 取得最後一筆正面 TileText 作為 TileIDEnd
            string finalProductTileId = topLotCreatorList.LastOrDefault(l => !string.IsNullOrEmpty(l.TileText02))?.TileText02
                ?? topLotCreatorList.LastOrDefault(l => !string.IsNullOrEmpty(l.TileText01))?.TileText01;

            Console.WriteLine($"Final TileID for Product Table: {finalProductTileId}");


			// **存入 Product Table**
			// 4.2.8 將正面編碼結果存入 Product 表（根據 stepCode 判斷 Table 命名）
			if (stepCodeExistsInOpnoPrefix)
            {
                // StepCode 存在於 opno_prefix，只插入 C+LotNo 和 D+LotNo
                await SaveProductTable(repository, "C" + request.LotNo, request.Customer, $"{request.Product}-TOP-C", topTileIds, topLastSN, finalProductTileId, config,true);
                await SaveProductTable(repository, "D" + request.LotNo, request.Customer, $"{request.Product}-TOP-D", topTileIds, topLastSN, finalProductTileId, config,true);
            }
            else
            {
                // StepCode 不存在於 opno_prefix，插入 LotNo
                if (config.Side == 1)
                { await SaveProductTable(repository, request.LotNo, request.Customer, $"{request.Product}", topTileIds, topLastSN, finalProductTileId, config,true); }
                else 
                { await SaveProductTable(repository, request.LotNo, request.Customer, $"{request.Product}-TOP", topTileIds, topLastSN, finalProductTileId, config,true); }
                
            }

            

            // **處理背面編碼 (如果 SIDE = 2)**
            List<string> backTileIds = new();
            string backLastSN = ""; // 預設一個空字串
									//Console.WriteLine(topTileIds.Last());

			// **存入 Product Table (背面)**
			// 5.1 若 Config.Side == 2 則需要處理背面
			if (config.Side == 2)
            {
				// 5.2 呼叫 GenerateTileIds，處理背面（isBackSide: true）
				var (generatedBackTileIds, backLotCreatorList, topOriginalSN, tempBackLastSN) = await GenerateTileIds(request, config, customerConfig, isBackSide: true, repository, originalLastSN);
                backTileIds = generatedBackTileIds;
                backLastSN = tempBackLastSN; // 這裡使用 tempBackLastSN 來避免衝突

				// 5.3.1 呼叫 SaveLotCreatorData，儲存背面資料（LotNo 前面加上 B）
				await SaveLotCreatorData(repository, "B" + request.LotNo, backLotCreatorList, request.StepCode);
              

                // **取得背面 TileIDEnd，應該存入 TileText02 的最後一筆**
                // 5.3.2 取得背面最後一筆 TileText 作為 TileIDEnd
                string finalBackTileId = backLotCreatorList.LastOrDefault(l => !string.IsNullOrEmpty(l.TileText02))?.TileText02
                    ?? backLotCreatorList.LastOrDefault(l => !string.IsNullOrEmpty(l.TileText01))?.TileText01;

				// **存入背面 Product Table**
				// 5.3.2 將背面編碼結果存入 Product 表
				if (stepCodeExistsInOpnoPrefix)
                {
                    // StepCode 存在於 opno_prefix，插入 BC+LotNo 和 BD+LotNo
                    await SaveProductTable(repository, "BC" + request.LotNo, request.Customer, $"{request.Product}-BACK-C", backTileIds, backLastSN, finalBackTileId,config,false);
                    await SaveProductTable(repository, "BD" + request.LotNo, request.Customer, $"{request.Product}-BACK-D", backTileIds, backLastSN, finalBackTileId, config,false);
                }
                else
                {
                    // StepCode 不存在於 opno_prefix，插入 B+LotNo
                    await SaveProductTable(repository, "B" + request.LotNo, request.Customer, $"{request.Product}-BACK", backTileIds, backLastSN, finalBackTileId, config,false);
                }


                
            }

			// 回傳成功，並帶出最後一筆正面 TileID
			return ApiReturn<string>.Success("Tile ID 產生成功", topTileIds.Last());

            
        }

        private Dictionary<string, string> BuildCodeMapping(string rawCodeString)
        {
            return (rawCodeString ?? "")
                .Split(',')
                .Select(s => s.Split('='))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0], p => p[1]);
        }

        private Dictionary<string, string> BuildFullDateCodeMapping(Config config)
        {
            var mapping = new Dictionary<string, string>();

            // 使用年、月、日代碼表進行轉換
            // 安全轉換 Year
            var yearMap = BuildCodeMapping(config.Year_Code);
            if (yearMap.Any())
                mapping["YC"] = DateCodeMapping.Convert("YC", yearMap);

            // 安全轉換 Month
            var monthMap = BuildCodeMapping(config.Month_Code);
            if (monthMap.Any())
                mapping["MC"] = DateCodeMapping.Convert("MC", monthMap);

            // 安全轉換 Day
            var dayMap = BuildCodeMapping(config.Day_Code);
            if (dayMap.Any())
                mapping["DC"] = DateCodeMapping.Convert("DC", dayMap);

            // 標準日期格式（不需要對映表）
            mapping["YY"] = DateTime.Now.ToString("yy");
            mapping["MM"] = DateTime.Now.ToString("MM");
            mapping["DD"] = DateTime.Now.ToString("dd");
            mapping["WW"] = CultureInfo.InvariantCulture.Calendar
                .GetWeekOfYear(DateTime.Now, CalendarWeekRule.FirstDay, DayOfWeek.Sunday)
                .ToString("D2");

            return mapping;
        }

        public static string GetAvailableCharacters(string encode, string exclude)
        {
            var excludeSet = new HashSet<char>(
                (exclude ?? "").Split(',').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).Select(c => c[0])
            );

            return new string((encode ?? "").Where(c => !excludeSet.Contains(c)).ToArray());
        }


        private async Task<(List<string>, List<LotCreator>, string originalLastSN, string finalLastSN)> GenerateTileIds(LaserMarkingRequest request, Config config, CustomerConfig customerConfig, bool isBackSide,
    IRepository repository,
    string? initialLastSN = null)
        {
            var tileIds = new List<string>();

            var lotCreators = new List<LotCreator>();
            var existingTileIds = new HashSet<string>();

        


            //--------自動替換 selectLastsn
            // 1. 建立完整對映字典（支援 YC, MC, DC, YY, WW 等）
            //var configMappingL = BuildFullDateCodeMapping(config);

            // 2. 將 TILEID 拆解並解析成每段轉換值（例：TXT=JK → JK, YC → H）
            var tileIdParts = config.TileId?.Split(',') ?? Array.Empty<string>();

            // 3. 抓取 TILEID 第一段（對應 {@CONFIG.TILEID[0].SUBSTRING(x)} 替換用）
            //string tilePrefix = resolvedParts.FirstOrDefault() ?? "";
            //string tilePrefix = StringCodeMapping.Convert(tileIdParts.FirstOrDefault(), request.LotNo) ?? "";
            string tilePrefix = tileIdParts.FirstOrDefault()?.Trim() ?? "";
            var configMappingL = BuildFullDateCodeMapping(config);
            // 4. 替換 SQL 條件中 {@...} 的所有變數
            string whereClause = BuildWhereClause(customerConfig.SelectLastSn, configMappingL, tilePrefix);

            // （可選）除錯 log
            //Console.WriteLine($"[tilePrefix] {tilePrefix}");
            //Console.WriteLine($"[whereClause] {whereClause}");
            //--------自動替換 selectLastsn




            // 查詢 Product 表中符合條件的最後一筆 SN
            string query = $@"
                SELECT TileID, Quantity, TileIDEnd, LastSN
                FROM Product
                {whereClause}
                LIMIT 1";

            var lastProduct = await repository.QueryFirstOrDefaultAsync<Product>(query);


            // 3.3 若查到資料，且 TileIDEnd 有值但 LastSN 為 null，代表異常情況 → 拋出錯誤
            if (lastProduct != null && !string.IsNullOrEmpty(lastProduct.TileIDEnd) && string.IsNullOrEmpty(lastProduct.LastSN))
			{
				throw new Exception($"{query}，查詢LastSN錯誤，請通知IT人員處理。");
			}


			// 2.2 計算編碼數量：SubBigQty * BLOCK_QTY + 4
			int encodeCount = int.Parse(request.SubBigQty) * (config.Block_Qty ?? 1) + 4;

			// 2.5/2.6 依 isBackSide 決定使用 TOP 或 BACK 編碼欄位
			var tileTextFields = isBackSide
                ? new List<string?> { config.Back_TileText01, config.Back_TileText02, config.Back_TileText03, config.Back_TileText04, config.Back_TileText05 }
                : new List<string?> { config.Top_TileText01, config.Top_TileText02, config.Top_TileText03, config.Top_TileText04, config.Top_TileText05 };
            var cellTextFields = isBackSide
                ? new List<string?> { config.Back_CellText01, config.Back_CellText02, config.Back_CellText03, config.Back_CellText04, config.Back_CellText05 }
                : new List<string?> { config.Top_CellText01, config.Top_CellText02, config.Top_CellText03, config.Top_CellText04, config.Top_CellText05 };

			// 2.3 日期轉換對應碼（用於 DateCodeMapping）
			//var configMapping = new Dictionary<string, string>
   //             {
   //                 { "YEAR_CODE", config.Year_Code },
   //                 { "MONTH_CODE", config.Month_Code },
   //                 { "DAY_CODE", config.Day_Code }
   //             };
            var configMapping = BuildFullDateCodeMapping(config);

            //var snGenerator = new NewSNGenerator();
            string availableChars = GetAvailableCharacters(customerConfig.CharacterEncode, customerConfig.CharacterExclude);

            //var snGenerator = new NewSNGenerator(customerConfig.CharacterEncode);
            var snGenerator = new NewSNGenerator(availableChars);


            //string lastSN = customerConfig.SelectLastSn;

            // 1.2 取得 SN 長度；若查無 LastSN 則以 '0' * 長度填補
            int snLength = customerConfig.SnLength ?? 5; // 預設值 5
            //string lastSN = lastProduct?.LastSN ?? new string('0', snLength);
            string lastSN = initialLastSN ?? lastProduct?.LastSN ?? "";
           // string originalLastSN = lastSN;
            string originalLastSN = initialLastSN ?? lastProduct?.LastSN ?? "";
            //string lastSN = lastProduct;

            // 4.2.1 三層迴圈：第一層為總筆數編碼迴圈
            for (int i = 0; i < encodeCount; i++)
            {
				
				// 4.2.2 每次迴圈初始化 tileTexts（對應 TileText01~05）
				var tileTexts = new List<string>();
				var cellTexts = new List<string>();

				// 4.2.3 第二層迴圈：遍歷每一個欄位（最多五個欄位）
				foreach (var tileText in tileTextFields)
                {
                    if (string.IsNullOrEmpty(tileText)) continue;

					// 4.2.3 拆解欄位內容：以逗號分隔成轉換元素陣列
					var elements = tileText.Split(',');
                    var convertedElements = new List<string>();

					// 4.2.4 第三層迴圈：處理每個轉換元素
					foreach (var element in elements)
                    {
                        string convertedValue = null;


						// 4.1.5.1 處理流水號遞增（SN1/GSC1）呼叫 SN 產生器
						if (element == "SN1" || element == "GSC1")
                        {
                            convertedValue = snGenerator.GenerateSN(lastSN, customerConfig.NewSnPattern, true);
                            lastSN = convertedValue; // 更新 lastSN
                        }
                        // 4.1.5 處理不遞增的流水號（SN/GSC）
                        else if (element == "SN" || element == "GSC")
                        {
                            convertedValue = snGenerator.GenerateSN(lastSN, customerConfig.NewSnPattern, false);
                        }
						// 4.1.3 與 4.1.4 處理字串與日期轉換邏輯
						else
						{
                            // 4.1.3 字串轉換
                            //convertedValue = StringCodeMapping.Convert(element, request.LotNo) ??// 6.1 ~ 6.4
                            //				 DateCodeMapping.Convert(element, configMapping) ??// 7.1 ~ 7.10
                            //				 element; // 保留原始值
                            //convertedValue = StringCodeMapping.Convert(element, request.LotNo)
                            //            ?? (configMapping.TryGetValue(element, out var val) ? val : element);
                            //var test = StringCodeMapping.Convert("YC", request.LotNo);  // 看會不會回傳 "YC"

                            string cleaned = element?.Trim();
                            //convertedValue = StringCodeMapping.Convert(cleaned, request.LotNo)
                            //                  ?? (configMapping.TryGetValue(cleaned, out var val) ? val : cleaned);
                            if (cleaned == "YC" || cleaned == "MC" || cleaned == "DC")
                            {
                                // 日期代碼直接使用 configMapping 對映
                                convertedValue = configMapping.TryGetValue(cleaned, out var val) ? val : cleaned;
                            }
                            else
                            {
                                // 先走 StringCodeMapping，若無結果才查對映表
                                convertedValue = StringCodeMapping.Convert(cleaned, request.LotNo)
                                                  ?? (configMapping.TryGetValue(cleaned, out var val) ? val : cleaned);
                            }

                        }
                        // 4.2.4 結合轉換結果
                        convertedElements.Add(convertedValue);

                    }
					//tileIds.Add(string.Join("", convertedElements));
					// 4.1.6 合併為最終字串（欄位值）
					tileTexts.Add(string.Join("", convertedElements));
				}


                //檢查處理celltext
                foreach (var cellText in cellTextFields)
                {
                    if (string.IsNullOrEmpty(cellText)) continue;

                    var elements = cellText.Split(',');
                    var convertedElements = new List<string>();

                    foreach (var element in elements)
                    {
                        string cleaned = element?.Trim();
                        string convertedValue = null;

                        if (cleaned == "SN1" || cleaned == "GSC1")
                        {
                            convertedValue = snGenerator.GenerateSN(lastSN, customerConfig.NewSnPattern, true);
                            lastSN = convertedValue;
                        }
                        else if (cleaned == "SN" || cleaned == "GSC")
                        {
                            convertedValue = snGenerator.GenerateSN(lastSN, customerConfig.NewSnPattern, false);
                        }
                        else if (cleaned == "YC" || cleaned == "MC" || cleaned == "DC")
                        {
                            convertedValue = configMapping.TryGetValue(cleaned, out var val) ? val : cleaned;
                        }
                        else
                        {
                            convertedValue = StringCodeMapping.Convert(cleaned, request.LotNo)
                                              ?? (configMapping.TryGetValue(cleaned, out var val) ? val : cleaned);
                        }

                        convertedElements.Add(convertedValue);
                    }

                    cellTexts.Add(string.Join("", convertedElements));
                }

                // 組合完成後取得主編碼 TileID（通常為第一欄）
                string finalTileId = tileTexts.ElementAtOrDefault(0);
                Console.WriteLine($"Generated TileID: {finalTileId}");


				// 4.2.5.1 檢查是否重複（同一 Lot 不能重複）
				if (existingTileIds.Contains(finalTileId))
                    throw new Exception($"TileID: {finalTileId} 重複，請通知 IT 人員");

                existingTileIds.Add(finalTileId);
                tileIds.Add(finalTileId);

				// 4.2.5.2 將轉換結果存入 LotCreator
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
                    CellText01 = cellTexts.ElementAtOrDefault(0),
                    CellText02 = cellTexts.ElementAtOrDefault(1),
                    CellText03 = cellTexts.ElementAtOrDefault(2),
                    CellText04 = cellTexts.ElementAtOrDefault(3),
                    CellText05 = cellTexts.ElementAtOrDefault(4),
                    // 4.2.5.2 CELL 預設空值（❗實作上未用 cellTextFields）
                    //CellText01 = "",
                    //               CellText02 = "",
                    //               CellText03 = "",
                    //               CellText04 = "",
                    //               CellText05 = "",
                    // 4.2.5.3 DotData 固定為空字串
                    DotData = null,
                    Tile01Count = null,
                    Tile02Count = null,
                    Tile03Count = null,
                    Tile04Count = null,
                    Tile05Count = null
                });
			}// 第一層迴圈結束
             //return tileIds;
             //return (tileIds, lotCreators, lastSN);


            // 20250424 檢查第一片與最後一片是否重複
            //正面才需做檢查
            if (isBackSide == false)
            {
                string? firstTileId = lotCreators.FirstOrDefault()?.TileID;
                string? lastTileId = lotCreators.LastOrDefault()?.TileID;

                if (!string.IsNullOrEmpty(firstTileId))
                {
                    var dupLot1 = await CheckDuplicatedTileIdAsync(repository, firstTileId, request.Customer);
                    if (dupLot1 != null)
                        throw new Exception($"[重複片號] 第一片 TileID [{firstTileId}] 已存在於批號 [{dupLot1}] 中！");
                }

                if (!string.IsNullOrEmpty(lastTileId))
                {
                    var dupLot2 = await CheckDuplicatedTileIdAsync(repository, lastTileId, request.Customer);
                    if (dupLot2 != null)
                        throw new Exception($"[重複片號] 最後一片 TileID [{lastTileId}] 已存在於批號 [{dupLot2}] 中！");
                }
            }

            

            //return (tileIds, lotCreators, originalLastSN);
            return (tileIds, lotCreators, originalLastSN, lastSN);  // 最後一筆 SN
        }


        private async Task SaveLotCreatorData(IRepository repository, string lotNo, List<LotCreator> lotCreators, string stepCode)
        {
            // 查詢 opno_prefix，確認 stepCode 是否存在（控制 table 命名規則）
            bool stepCodeExists = await repository.QueryFirstOrDefaultAsync<string>(
                "SELECT opno FROM opno_prefix WHERE opno = @StepCode",
                new { StepCode = stepCode }
            ) != null;

			// 初始化需建立的資料表清單
			List<string> tableNames = new();

			

			//判斷 table 命名規則邏輯
			if (stepCodeExists)
            {
                if (lotNo.StartsWith("B"))
                {
					// 5.3.1 若是背面資料 (B開頭)，則需建立 BC+LotNo、BD+LotNo
					// 以下邏輯：在 W 前插入 C 或 D
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
						// 若找不到 W，則 fallback 為 C+LotNo 和 D+LotNo
						// 如果找不到 W，回退到一般邏輯
						tableNames.Add($"C{lotNo}");
                        tableNames.Add($"D{lotNo}");
                    }
                }
                else
                {
					// 一般正面情況：C+LotNo、D+LotNo
					tableNames.Add($"C{lotNo}");
                    tableNames.Add($"D{lotNo}");
                }
            }
            else
            {
				// stepCode 不存在於 opno_prefix，使用原始 LotNo 作為 table 名稱
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
				// 將 lotCreator 資料寫入該表
				foreach (var lotCreator in lotCreators)
                {
                    await repository.ExecuteAsync($@"
                INSERT INTO `{tableName}` (TileID, MachineID, MarkDate, MarkTime, ReworkDate, ReworkTime,
                                             TileText01, TileText02, TileText03, TileText04, TileText05,
                                             CellText01, CellText02, CellText03, CellText04, CellText05,
                                             DotData, Tile01Count, Tile02Count, Tile03Count, Tile04Count, Tile05Count)
                VALUES (@TileID, NULL, NULL, NULL, NULL, NULL,
                        @TileText01, @TileText02, @TileText03, @TileText04, @TileText05,
                        @CellText01, @CellText02, @CellText03, @CellText04, @CellText05,
                        @DotData, @Tile01Count, @Tile02Count, @Tile03Count, @Tile04Count, @Tile05Count)", lotCreator);
                }
            }


           
        }

        private string GetTileIdEndFromPanelData(List<dynamic> panelRows, Config config)
        {
            var configTexts = new[]
            {
                config.Top_TileText01,
                config.Top_TileText02,
                config.Top_TileText03,
                config.Top_TileText04,
                config.Top_TileText05
            };

            // 只挑出包含 SN1 或 GSC1 的欄位 index（表示會遞增），內含有增機序號才成為最後SN
            var incrementIndexes = configTexts
                .Select((value, index) => new { value, index })
                .Where(x => x.value != null && (x.value.Contains("SN1") || x.value.Contains("GSC1")))
                .Select(x => x.index)
                .ToList();

 
        

            // 反向找最後一筆有值的 TileID
            foreach (var row in panelRows.AsEnumerable().Reverse())
            {
                foreach (var idx in incrementIndexes.OrderByDescending(i => i))
                {
                    string val = idx switch
                    {
                        0 => row.TileText01,
                        1 => row.TileText02,
                        2 => row.TileText03,
                        3 => row.TileText04,
                        4 => row.TileText05,
                        _ => null
                    };

                    if (!string.IsNullOrEmpty(val))
                        return val;
                }
            }

            return null;
        }
        private async Task<string?> CheckDuplicatedTileIdAsync(IRepository repository, string tileId, string customer)
        {
            // 查詢最近一筆相同長度、TileID <= 當前的記錄
            string? lotNo = await repository.QueryFirstOrDefaultAsync<string>(
                @"SELECT LotNo FROM Product
                  WHERE LENGTH(TileID) = @Length
                    AND TileID <= @TileID
                    AND Customer = @Customer
                  ORDER BY TileID DESC
                  LIMIT 1",
                new { Length = tileId.Length, TileID = tileId, Customer = customer });

            if (string.IsNullOrEmpty(lotNo))
                return null;

            // ⬇️ 檢查該 table 是否存在
            bool tableExists = await repository.QueryFirstOrDefaultAsync<bool>(
                @"SELECT COUNT(*)
                  FROM information_schema.tables
                  WHERE table_schema = DATABASE() AND table_name = @TableName",
                new { TableName = lotNo });

            if (!tableExists)
            {
                Console.WriteLine($"[警告] Table `{lotNo}` 不存在，跳過檢查 TileID 重複。");
                return null;
            }



            // 查該批號 Table 是否含該 TileID
            int count = await repository.QueryFirstOrDefaultAsync<int>(
                $@"SELECT COUNT(*) FROM `{lotNo}`
               WHERE TILETEXT01 = @TileID
                  OR TILETEXT02 = @TileID
                  OR TILETEXT03 = @TileID
                  OR TILETEXT04 = @TileID
                  OR TILETEXT05 = @TileID",
                new { TileID = tileId });

            return count > 0 ? lotNo : null;
        }



        private async Task SaveProductTable(IRepository repository, string lotNo,string customer, string productName, List<string> tileIds, string lastSN,string finalTileid, Config config, bool isTopSide)
        {
            // 4.2.7.5 確保最後一筆編碼字串存在（TileIDEnd 若為 null 則 fallback 為首筆）
            //string tileIDEnd = tileIds.LastOrDefault() ?? tileIds.First();
            string ruleFile1 = isTopSide ? config.Top_RuleFile1 : config.Back_RuleFile1;
            string ruleFile2 = isTopSide ? config.Top_RuleFile2 : config.Back_RuleFile2;
            string ruleFile3 = isTopSide ? config.Top_RuleFile3 : config.Back_RuleFile3;

            var panelRows = await repository.QueryAsync<dynamic>($"SELECT * FROM `{lotNo}`");
            string tileIdEnd = GetTileIdEndFromPanelData(panelRows.ToList(), config);
            // 4.2.8 寫入 Product 資料表
            await repository.ExecuteAsync(@"
            INSERT INTO Product (LotNO, Customer, ProductName, TileID, TileIDEnd, LastSN, Quantity, RuleFile1, RuleFile2, RuleFile3, CreateDate, CreateTime)
            VALUES (@LotNO, @Customer, @ProductName, @TileID, @TileIDEnd, @LastSN, @Quantity, @RuleFile1, @RuleFile2, @RuleFile3, @CreateDate, @CreateTime)",
                new
                {
                    LotNO = lotNo,
                    Customer = customer,
					// 4.2.7.4 ProductName（已於外層組合完成，包含 TOP/BACK 判斷）
					ProductName = productName,
                    TileID = tileIds.First(),
                    //TileIDEnd = finalTileid,
                    TileIDEnd = tileIdEnd,
                    LastSN = lastSN,
					// 4.2.7.7 總編碼筆數 → Quantity
					Quantity = tileIds.Count,
                    RuleFile1 = ruleFile1,
                    RuleFile2 = ruleFile2,
                    RuleFile3 = ruleFile3,
                    CreateDate = DateTime.Now.ToString("yyMMdd"),
                    CreateTime = DateTime.Now.ToString("HH:mm:ss")
                });
        }

        private string BuildWhereClause(string template, Dictionary<string, string> configMapping, string tilePrefix)
        {
            if (string.IsNullOrEmpty(template)) return "";

            var pattern = @"\{@(.*?)\}";
            var matches = Regex.Matches(template, pattern);

            var replacements = new Dictionary<string, string>();

            foreach (Match match in matches)
            {
                string raw = match.Groups[1].Value;
                string fullKey = match.Value;
                string result = "";

                if (raw.Contains("SUBSTRING"))  // ✅ 修正重點在這
                {
                    var subMatch = Regex.Match(raw, @"SUBSTRING\((\d+)(?:,\s*(\d+))?\)");
                    if (subMatch.Success)
                    {
                        int start = int.Parse(subMatch.Groups[1].Value);
                        int length = subMatch.Groups[2].Success ? int.Parse(subMatch.Groups[2].Value) : -1;

                        if (start >= tilePrefix.Length)
                        {
                            result = "";
                        }
                        else if (length == -1)
                        {
                            result = tilePrefix.Substring(start);
                        }
                        else if (start + length <= tilePrefix.Length)
                        {
                            result = tilePrefix.Substring(start, length);
                        }
                        else
                        {
                            result = tilePrefix.Substring(start);
                        }
                    }
                }
                else if (raw == "YC" || raw == "MC" || raw == "DC" || raw == "YY" || raw == "MM" || raw == "DD" || raw == "WW")
                {
                    configMapping.TryGetValue(raw, out result);
                }
                else if (Regex.IsMatch(raw, @"@")) // 例如 @yy@MM@dd
                {
                    result = raw
                        .Replace("yy", DateTime.Now.ToString("yy"))
                        .Replace("MM", DateTime.Now.ToString("MM"))
                        .Replace("dd", DateTime.Now.ToString("dd"))
                        .Replace("WW", CultureInfo.InvariantCulture.Calendar
                            .GetWeekOfYear(DateTime.Now, CalendarWeekRule.FirstDay, DayOfWeek.Sunday)
                            .ToString("D2"));
                }
                else
                {
                    configMapping.TryGetValue(raw, out result);
                }

                replacements[fullKey] = result ?? "";
            }

            return replacements.Aggregate(template, (current, pair) => current.Replace(pair.Key, pair.Value));
        }




        private async Task InsertWipLotMarkingDataToOracle(string lotNo, List<List<string>> panelTileList, IRepository oracleRepo)
        {
            string timestamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");

            for (int groupIndex = 0; groupIndex < panelTileList.Count; groupIndex++)
            {
                var panel = panelTileList[groupIndex];
                int tileGroup = groupIndex + 1;

                for (int itemIndex = 0; itemIndex < panel.Count; itemIndex++)
                {
                    string tileId = panel[itemIndex];
                    if (!string.IsNullOrEmpty(tileId))
                    {
                        await oracleRepo.ExecuteAsync(@"
                            INSERT INTO TBLWIPLOTMARKINGDATA 
                            (LOTNO, TILEGROUP, TILEGROUP_ITEM, TILEID, RECORDDATE)
                            VALUES (:LotNo, :TileGroup, :TileGroupItem, :TileID, TO_DATE(:RecordDate,'YYYY/MM/DD HH24:MI:SS'))
                        ", new
                        {
                            LotNo = lotNo,
                            TileGroup = tileGroup,
                            TileGroupItem = itemIndex + 1,
                            TileID = tileId,
                            RecordDate = timestamp
                        });
                    }
                }
            }
        }


        
    }
}
