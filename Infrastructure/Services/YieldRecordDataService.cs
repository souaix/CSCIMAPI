using System.Text;
using Core.Entities.Public;
using Core.Entities.YieldRecordData;
using Core.Interfaces;
using Infrastructure.Utilities;
using System.Data;
using System.IO;

namespace Infrastructure.Services
{
	public class YieldRecordDataService : IYieldRecordDataService
	{
		private readonly IRepositoryFactory _repositoryFactory;

		public YieldRecordDataService(IRepositoryFactory repositoryFactory)
		{
			_repositoryFactory = repositoryFactory;
		}

		public async Task<ApiReturn<YieldRecordDataResult>> LoadYieldRecordDataAsync(YieldRecordDataRequest request)
		{
			try
			{
				var (_, repoCim, _) = RepositoryHelper.CreateRepositories(request.Environment, _repositoryFactory);

				// 查詢路徑設定
				var sql = @"SELECT FILEPATH, FILENAME, FILEEXT, PATHACCOUNT, PATHPASSWORD 
                            FROM ARGOCIMDEVICEFILESETTING 
                            WHERE DEVICEGROUPNO = 'YieldRecord' AND DEVICENO = 'None'";

				var setting = await repoCim.QueryFirstOrDefaultAsync<dynamic>(sql);

				if (setting == null)
					return ApiReturn<YieldRecordDataResult>.Failure("查無檔案設定");

				string filepath = setting.FILEPATH.ToString().Replace("{PN}", request.ProductNo);
				string filename = setting.FILENAME.ToString().Replace("{LotNo}", request.LotNo);
				string fullpath = Path.Combine(filepath, filename + setting.FILEEXT.ToString());

				// ✅ 使用 NetworkShareManager 確保連線一次，避免重複登入登出
				NetworkShareManager.EnsureConnected(filepath, setting.PATHACCOUNT.ToString(), setting.PATHPASSWORD.ToString());

				if (!File.Exists(fullpath))
					return ApiReturn<YieldRecordDataResult>.Failure($"檔案不存在: {fullpath}");

				var lines = await File.ReadAllLinesAsync(fullpath, Encoding.UTF8);

				// 忽略前兩行（P/N 與標題）
				var dataLines = lines.Skip(2);

				var resultList = new List<YieldRecordDataDto>();

				foreach (var line in dataLines)
				{
					var parts = line.Split(',');

					if (parts.Length < 5)
						continue;

					resultList.Add(new YieldRecordDataDto
					{
						LotNo = parts[0].Trim(),
						TileId = parts[1].Trim(),
						GoodQty = int.TryParse(parts[2], out var gq) ? gq : 0,
						BadQty = int.TryParse(parts[3], out var bq) ? bq : 0,
						TotalQty = int.TryParse(parts[4], out var tq) ? tq : 0
					});
				}

				var summary = new YieldRecordDataResult
				{
					Records = resultList,
					GoodQtyTotal = resultList.Sum(x => x.GoodQty),
					BadQtyTotal = resultList.Sum(x => x.BadQty),
					TotalQtyTotal = resultList.Sum(x => x.TotalQty)
				};

				return ApiReturn<YieldRecordDataResult>.Success("讀取成功", summary);
			}
			catch (Exception ex)
			{
				return ApiReturn<YieldRecordDataResult>.Failure($"處理失敗: {ex.Message}");
			}
		}
	}
}
