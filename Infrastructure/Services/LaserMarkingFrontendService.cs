using Core.Entities.LaserMarkingFrontend;
using Core.Entities.Public;
using Core.Entities.YieldRecordData;
using Core.Interfaces;
using Infrastructure.Utilities;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Infrastructure.Services
{
	public class RepositoryContainer
	{
		public IRepository LaserRepo { get; }
		public IRepository CsCimRepo { get; }

		public RepositoryContainer(Dictionary<string, IRepository> repositories)
		{
			LaserRepo = repositories["LaserMarkingFrontend"];
			CsCimRepo = repositories["CsCimEmap"];
		}
	}

	public class LaserMarkingFrontendService : ILaserMarkingFrontendService
	{
		private readonly IRepositoryFactory _repositoryFactory;
		public LaserMarkingFrontendService(IRepositoryFactory repositoryFactory)
		{
			_repositoryFactory = repositoryFactory;
		}

		public async Task<ApiReturn<LaserMarkingFrontendProduct>> GenerateFrontendTileIdsAsync(LaserMarkingFrontendRequest request)
		{
			try
			{
				// 依據環境建立對應 repoContainer
				var repositories = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);
				var repoContainer = new RepositoryContainer(repositories);

				// 動態建立該次操作所需的 service 實例
				var oracleService = new LaserMarkingFrontendOracleConfigService(repoContainer);
				var tileBuilder = new LaserMarkingFrontendTileIdBuilder(oracleService);
				var mysqlService = new LaserMarkingFrontendMySqlService(repoContainer);
				var snService = new LaserMarkingFrontendSnQueryService(new LaserMarkingFrontendCodeGenerator());
				var tableService = new LaserMarkingFrontendTableDataService(mysqlService);
				var productService = new LaserMarkingFrontendProductService(mysqlService);

				// 1.建立 TileID
				string tileId = await tileBuilder.BuildTileId(request);

				// 2.依據 TileID 查詢前次序號
				string lastSn = await mysqlService.GetLastSn(tileId);

				// 3.依據 Qty 產生所需 n 筆編碼
				List<string> snList = snService.GenerateSnList(lastSn, request.Qty);

				// 4.建立 table <LotNo> 所需參數
				var sns = snList.Select(sn => new LaserMarkingFrontendGeneratedSn
				{
					Sn = sn,
					TileId = tileId,
					LotNo = request.LotNo
				}).ToList();

				// 5.寫入 table <LotNo>
				await tableService.SaveSnListToLotTable(request.LotNo, sns);

				// 避免多次 checkout, 若已建立 table <LotNo>, 以 table 資料筆數取代 request.Qty
				int actualQty = await mysqlService.GetLotSnCount(request.LotNo);
				int finalQty = actualQty == 0 ? request.Qty : actualQty;

				// 6.建立 table product 所需參數
				var product = new LaserMarkingFrontendProduct
				{
					LotNO = request.LotNo,
					ProductName = null,
					TileID = tileId,
					LastSN = snList.Last(),
					Quantity = finalQty.ToString(),
					CreateDate = DateTime.Now.ToString("yyMMdd"),
					CreateTime = DateTime.Now.ToString("HH:mm:ss"),
					NoteData = request.CheckoutTime.ToString("yyyyMMdd.HHmmss"),
				};

				// 7.寫入 table product
				await productService.UpsertProductRecord(product, request.ProductNo);

				return ApiReturn<LaserMarkingFrontendProduct>.Success("操作成功!", product);
			}
			catch (Exception ex)
			{
				return ApiReturn<LaserMarkingFrontendProduct>.Failure(ex.Message); // 回傳錯誤訊息
			}
		}
	}

	#region Code Generator
	public class LaserMarkingFrontendCodeGenerator
	{
		private const string Base36Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		private const int CodeLength = 3;
		private const int MaxValue = 36 * 36 * 36 - 1; // 46,655
		private const int MinValue = 1;               // 對應 "001"

		/// <summary>
		/// 依據上一個編碼與片數，產生指定數量的連續編碼
		/// </summary>
		public List<string> GenerateNextCodes(string? lastSN, int count)
		{
			var result = new List<string>();
			if (count <= 0) return result;

			int startValue = string.IsNullOrWhiteSpace(lastSN)
				? MinValue
				: Base36ToInt(lastSN) + 1;

			// 確保不超過最大值
			if (startValue + count - 1 > MaxValue)
				throw new InvalidOperationException("編碼超出可產生的最大範圍 (ZZZ)");

			for (int i = 0; i < count; i++)
			{
				int currentValue = startValue + i;
				string code = IntToBase36(currentValue).PadLeft(CodeLength, '0');
				result.Add(code);
			}

			return result;
		}

		/// <summary>
		/// 將 Base36 字串轉為整數
		/// </summary>
		private int Base36ToInt(string input)
		{
			input = input.ToUpper();
			int result = 0;
			foreach (char c in input)
			{
				int digit = Base36Chars.IndexOf(c);
				if (digit == -1)
					throw new ArgumentException($"不合法的字元: {c}");

				result = result * 36 + digit;
			}
			return result;
		}

		/// <summary>
		/// 將整數轉為 Base36 字串
		/// </summary>
		private string IntToBase36(int value)
		{
			if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));

			string result = string.Empty;
			do
			{
				result = Base36Chars[value % 36] + result;
				value /= 36;
			} while (value > 0);

			return result;
		}
	}
	#endregion


	#region TileIdBuilder
	public class LaserMarkingFrontendTileIdBuilder
	{
		private readonly LaserMarkingFrontendOracleConfigService _oracleService;

		public LaserMarkingFrontendTileIdBuilder(LaserMarkingFrontendOracleConfigService oracleService)
		{
			_oracleService = oracleService;
		}

		// 將料號補足到15碼 (長度15碼)
		public string PadProductNo(string productNo)
		{
			return productNo.PadLeft(15, '0');
		}

		// 將設備&切割時間轉換為機台編碼 (長度4碼)
		public async Task<string> GetEqCode(string eqNo, DateTime checkoutTime)
		{
			return await _oracleService.GetEqCode(eqNo, checkoutTime);
		}

		// 將料號和機台編碼組 TileId (長度19碼)
		public async Task<string> BuildTileId(LaserMarkingFrontendRequest request)
		{
			string paddedProductNo = PadProductNo(request.ProductNo);
			string eqCode = await GetEqCode(request.EqNo, request.CheckoutTime);
			return paddedProductNo + eqCode;
		}
	}
	#endregion


	#region Oracle Config Service
	public class LaserMarkingFrontendOracleConfigService
	{
		private readonly IRepository _repo;
		public LaserMarkingFrontendOracleConfigService(RepositoryContainer repoContainer)
		{
			_repo = repoContainer.CsCimRepo;
		}
		public async Task<string> GetEqCode(string eqNo, DateTime checkoutTime)
		{
			// 1. 從資料庫讀取所有設定
			var configRows = await _repo.QueryAsync<LaserMarkingFrontendConfig>("SELECT * FROM ARGOAPIFRONTEND2DCONFIG");

			// 2. 建立四個類型的字典
			var machineMap = new Dictionary<string, string>();
			var yearMap = new Dictionary<string, string>();
			var monthMap = new Dictionary<string, string>();
			var dayMap = new Dictionary<string, string>();

			foreach (var row in configRows)
			{
				switch (row.Item?.Trim().ToUpper())
				{
					case "MACHINE":
						if (!machineMap.ContainsKey(row.Source))
							machineMap[row.Source] = row.Transfer;
						break;
					case "YEAR":
						if (!yearMap.ContainsKey(row.Source))
							yearMap[row.Source] = row.Transfer;
						break;
					case "MONTH":
						if (!monthMap.ContainsKey(row.Source))
							monthMap[row.Source] = row.Transfer;
						break;
					case "DAY":
						if (!dayMap.ContainsKey(row.Source))
							dayMap[row.Source] = row.Transfer;
						break;
				}
			}

			// 3. 根據 eqNo 比對 MACHINE
			string result = "";
			if (!machineMap.TryGetValue(eqNo, out string machineCode))
				throw new Exception($"設備代碼未定義: {eqNo}");
			result += machineCode;

			// 4. 年份 (格式 yyyy)
			string year = checkoutTime.ToString("yyyy");
			if (!yearMap.TryGetValue(year, out string yearCode))
				throw new Exception($"年份代碼未定義: {year}");
			result += yearCode;

			// 5. 月份 (格式 M 或 MM) 
			string month = checkoutTime.Month.ToString();         // "1" ~ "12"
			string paddedMonth = checkoutTime.Month.ToString("00"); // "01" ~ "12"
			if (!monthMap.TryGetValue(month, out string monthCode))
			{
				if (!monthMap.TryGetValue(paddedMonth, out monthCode))
					throw new Exception($"月份代碼未定義: {month} / {paddedMonth}");
			}
			result += monthCode;

			// 6. 日 (格式 d 或 dd) 
			string day = checkoutTime.Day.ToString();         // "1" ~ "31"
			string paddedDay = checkoutTime.Day.ToString("00"); // "01" ~ "31"
			if (!dayMap.TryGetValue(day, out string dayCode))
			{
				if (!dayMap.TryGetValue(paddedDay, out dayCode))
					throw new Exception($"日期代碼未定義: {day} / {paddedDay}");
			}
			result += dayCode;

			return result; // 長度4碼
		}
	}
	#endregion


	#region INI Parser
	public class LaserMarkingFrontendIniParser
	{
		private const string BasePath = @"\\10.10.22.80\TENG1\TENG1LSD\2dserver_LOT\INI\";
		public Dictionary<string, string> LoadParameters(string productNo)
		{
			var result = new Dictionary<string, string>();

			string iniPath = Path.Combine(BasePath, $"{productNo}.INI");
			if (!File.Exists(iniPath))
				throw new FileNotFoundException($"找不到 INI 檔案: {iniPath}");

			var lines = File.ReadAllLines(iniPath);
			foreach (var line in lines)
			{
				if (line.Trim().StartsWith("Auto_Recipe", StringComparison.OrdinalIgnoreCase))
				{
					var parts = line.Split('=', 2);
					if (parts.Length == 2)
					{
						result["Auto_Recipe"] = parts[1].Trim();
						break;
					}
				}
			}

			if (!result.ContainsKey("Auto_Recipe"))
				throw new Exception($"在檔案中找不到 Auto_Recipe 設定: {iniPath}");

			return result;
		}
	}
	#endregion


	#region MySQL Service
	public class LaserMarkingFrontendMySqlService
	{
		private readonly IRepository _repo;
		public LaserMarkingFrontendMySqlService(RepositoryContainer repoContainer)
		{
			_repo = repoContainer.LaserRepo;
		}

		// 查詢前次 SN
		public async Task<string> GetLastSn(string tileId)
		{
			string lastSN = await _repo.QueryFirstOrDefaultAsync<string>(
				"Select LastSN From product Where TileID = @TileId Order By LastSN Desc Limit 1",
				new { TileId = tileId });

			if (string.IsNullOrEmpty(lastSN))
			{
				// 找不到記錄回傳 null, 將從 001 開始編碼
				return null;
			}

			return lastSN;
		}

		// 建立 table <LotNo>
		public async Task CreateLotTableIfNotExist(string lotNo)
		{
			try
			{
				// 確認 table 是否存在
				string checkTableSql = @"
						SELECT COUNT(*) 
						FROM information_schema.tables 
						WHERE table_schema = DATABASE() AND table_name = @TableName";

				int tableExists = await _repo.QueryFirstOrDefaultAsync<int>(checkTableSql, new { TableName = lotNo });

				if (tableExists > 0)
				{
					Console.WriteLine($"Table '{lotNo}' 已存在，略過建立資料表。");
					return;
				}

				// 若不存在，執行建表
				string createTableSql = $@"
					CREATE TABLE `{lotNo}` (
						`TileID` varchar(25) NOT NULL,
						`MachineID` varchar(25) DEFAULT NULL,
						`MarkDate` varchar(6) DEFAULT NULL,
						`MarkTime` varchar(8) DEFAULT NULL,
						`ReworkDate` varchar(6) DEFAULT NULL,
						`ReworkTime` varchar(8) DEFAULT NULL,
						`TileText01` varchar(25) DEFAULT NULL,
						`ChangeMachineID` varchar(25) DEFAULT NULL,
						`ChangeMarkDate` varchar(6) DEFAULT NULL,
						`ChangeMarkTime` varchar(8) DEFAULT NULL,
						`ChangeReworkDate` varchar(6) DEFAULT NULL,
						`ChangeReworkTime` varchar(8) DEFAULT NULL,
						`ChangeTileText01` varchar(25) DEFAULT NULL,
						PRIMARY KEY (`TileID`)
					) ENGINE=InnoDB DEFAULT CHARSET=utf8;";

				await _repo.ExecuteAsync(createTableSql);
				Console.WriteLine($"已建立 Table '{lotNo}'。");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"建立 Table '{lotNo}' 發生錯誤: {ex.Message}");
				throw new Exception($"建立 Table '{lotNo}' 發生錯誤: {ex.Message}");
			}
		}

		// INSERT data to table <LotNo>
		public async Task InsertToLotTable(string lotNo, List<LaserMarkingFrontendGeneratedSn> sns)
		{
			try
			{
				if (sns == null || sns.Count == 0)
				{
					Console.WriteLine($"傳入序號清單為空，Lot: {lotNo}");
					throw new Exception($"傳入序號清單為空，Lot: {lotNo}");
				}

				string sql = $@"INSERT INTO `{lotNo}` (TileID, TileText01)
								VALUES (@TileID, @TileText01);";

				// 批次準備資料
				var records = sns.Select(x => new
				{
					TileID = x.TileId + x.Sn,
					TileText01 = x.TileId + x.Sn
				});

				await _repo.ExecuteAsync(sql, records);
				Console.WriteLine($"已成功寫入 {records.Count()} 筆資料到 Table `{lotNo}`");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"INSERT 資料時發生錯誤，Lot: {lotNo}, 訊息: {ex.Message}");
				throw new Exception($"INSERT 資料時發生錯誤，Lot: {lotNo}, 訊息: {ex.Message}");
			}
		}

		// 避免批次 checkout, 以 insert into table <LotNo> 的資料筆數作為 product.Quantity 來源 
		public async Task<int> GetLotSnCount(string lotNo)
		{
			try
			{
				// 先確認資料表是否存在
				string checkTableSql = @"SELECT COUNT(*) 
										FROM information_schema.tables 
										WHERE table_schema = DATABASE() AND table_name = @TableName";

				int tableExists = await _repo.QueryFirstOrDefaultAsync<int>(checkTableSql, new { TableName = lotNo });

				if (tableExists == 0)
				{
					Console.WriteLine($"Table '{lotNo}' 不存在，回傳 0 筆。");
					return 0;
				}

				// Table 存在，查詢筆數
				string countSql = $"SELECT COUNT(*) FROM `{lotNo}`;";
				int count = await _repo.QueryFirstOrDefaultAsync<int>(countSql);
				Console.WriteLine($"Table '{lotNo}' 共 {count} 筆資料。");
				return count;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"查詢 Table '{lotNo}' 筆數時發生錯誤: {ex.Message}");
				throw new Exception($"查詢 Table '{lotNo}' 筆數時發生錯誤: {ex.Message}");
			}
		}

		public async Task InsertOrUpdateProduct(LaserMarkingFrontendProduct product)
		{
			try
			{
				// 1. 查詢是否已有該 LotNo 資料
				string checkSql = "SELECT COUNT(*) FROM product WHERE LotNo = @LotNo";
				int exists = await _repo.QueryFirstOrDefaultAsync<int>(checkSql, new { product.LotNO });

				if (exists > 0)
				{
					// 2. 更新
					string updateSql = @"UPDATE product
											SET ProductName = @ProductName, 
												TileID = @TileID,
												LastSN = @LastSN,
												Quantity = @Quantity,
												CreateDate = @CreateDate,
												CreateTime = @CreateTime,
												NoteData = @NoteData
											WHERE LotNO = @LotNO";

					await _repo.ExecuteAsync(updateSql, product);
					Console.WriteLine($"更新 LotNo: {product.LotNO} 成功");
				}
				else
				{
					// 3. 插入新資料
					string insertSql = @"INSERT INTO product (LotNO, ProductName, TileID, LastSN, Quantity, CreateDate, CreateTime, NoteData)
										VALUES (@LotNO, @ProductName, @TileID, @LastSN, @Quantity, @CreateDate, @CreateTime, @NoteData)";

					await _repo.ExecuteAsync(insertSql, product);
					Console.WriteLine($"Insert LotNo: {product.LotNO} 成功");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"處理 Table Product.LotNO: {product.LotNO} 發生錯誤: {ex.Message}");
				throw new Exception($"處理 Table Product.LotNO: {product.LotNO} 發生錯誤: {ex.Message}");
			}
		}
	}
	#endregion


	#region SnQueryService
	public class LaserMarkingFrontendSnQueryService
	{
		private readonly LaserMarkingFrontendCodeGenerator _generator;

		public LaserMarkingFrontendSnQueryService(LaserMarkingFrontendCodeGenerator generator)
		{
			_generator = generator;
		}

		public List<string> GenerateSnList(string lastSn, int qty)
		{
			return _generator.GenerateNextCodes(lastSn, qty);
		}
	}
	#endregion


	#region TableDataService
	public class LaserMarkingFrontendTableDataService
	{
		private readonly LaserMarkingFrontendMySqlService _mysqlService;

		public LaserMarkingFrontendTableDataService(LaserMarkingFrontendMySqlService mysqlService)
		{
			_mysqlService = mysqlService;
		}

		// 處理 MySQL table <LotNo> 
		public async Task SaveSnListToLotTable(string lotNo, List<LaserMarkingFrontendGeneratedSn> sns)
		{
			await _mysqlService.CreateLotTableIfNotExist(lotNo);
			await _mysqlService.InsertToLotTable(lotNo, sns);
		}
	}
	#endregion


	#region ProductService
	public class LaserMarkingFrontendProductService
	{
		private readonly LaserMarkingFrontendMySqlService _mysqlService;

		public LaserMarkingFrontendProductService(LaserMarkingFrontendMySqlService mysqlService)
		{
			_mysqlService = mysqlService;
		}

		// 處理 MySQL table product
		public async Task UpsertProductRecord(LaserMarkingFrontendProduct product, string productNo)
		{
			var parser = new LaserMarkingFrontendIniParser();
			var config = parser.LoadParameters(productNo); // 對應 <productNo>.INI
			var autoRecipe = config["Auto_Recipe"];

			// 更新 product.ProductName 為 autoRecipe
			product.ProductName = autoRecipe;

			await _mysqlService.InsertOrUpdateProduct(product);
		}
	}
	#endregion


	//#region SnGenerateService
	//public class SnGenerateService
	//{
	//	private readonly TileIdBuilder _tileBuilder;
	//	private readonly MySqlService _mysqlService;
	//	private readonly SnQueryService _snQueryService;
	//	private readonly TableDataService _tableService;
	//	private readonly ProductService _productService;

	//	public SnGenerateService(
	//		TileIdBuilder tileBuilder,
	//		MySqlService mysqlService,
	//		SnQueryService snQueryService,
	//		TableDataService tableService,
	//		ProductService productService)
	//	{
	//		_tileBuilder = tileBuilder;
	//		_mysqlService = mysqlService;
	//		_snQueryService = snQueryService;
	//		_tableService = tableService;
	//		_productService = productService;
	//	}

	//	public void Generate(SnGenerateRequest request)
	//	{
	//		string tileId = _tileBuilder.BuildTileId(request);
	//		string lastSn = _mysqlService.GetLastSn(tileId);
	//		List<string> snList = _snQueryService.GenerateSnList(lastSn, request.Qty);

	//		var sns = snList.Select(sn => new GeneratedSn
	//		{
	//			Sn = sn,
	//			TileId = tileId,
	//			LotNo = request.LotNo
	//		}).ToList();

	//		_tableService.SaveSnListToLotTable(request.LotNo, sns);

	//		int actualQty = _mysqlService.GetLotSnCount(request.LotNo);
	//		_productService.UpsertProductRecord(request.LotNo, actualQty);
	//	}
	//}
	//#endregion
}
