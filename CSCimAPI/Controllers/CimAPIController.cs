using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using CSCimAPI.Models;
using Core.Interfaces;
using Core.Entities.DboEmap;
using Core.Utilities;
using Infrastructure.Data.Repositories;
using Infrastructure.Services;
using Core.Entities.LaserMarking;
using MySqlX.XDevAPI.Common;
using Core.Entities.LeakageCheck;
using Core.Entities.MailSender;
using Core.Entities.TeamsAlarm;
using Core.Entities.LotTileCheck;
using Core.Entities.Recipe2DCodeGenerator;
using System;
using Core.Entities.YieldRecordData;
using Core.Entities.DefectCount;
using Core.Entities.Scada;
using Infrastructure.Data.Factories;
using Infrastructure.Utilities;
using Core.Entities.LaserMarkingFrontend;
using Core.Entities.RecycleLotCopy;
using Core.Entities.CheckLimit;

namespace CimAPI.Controllers
{
    [ApiController]
	//[Route("[controller]")]
	public class CimAPIController : ControllerBase
	{

		private readonly ILogger<CimAPIController> _logger;
		private readonly IConfiguration _configuration;
		private readonly IRepositoryFactory _RepositoryFactory;
		private readonly ICimApiFacade _facade;

        public CimAPIController(ILogger<CimAPIController> logger, IRepositoryFactory RepositoryFactory, ICimApiFacade facade)
		{
			_logger = logger;
			_RepositoryFactory = RepositoryFactory;

			_facade = facade;
            //_laserMarkingService = LaserMarkingService ?? throw new ArgumentNullException(nameof(LaserMarkingService)); // 確保不為 null

        }

		/// <summary>
		/// 插入資料到 TBLMESWIPDATA_xxx
		/// 
		/// </summary>
		/// <remarks>
		/// RECORDATE、DEVICEID、DATAINDEX、STATUS、ALARMCODE不能空值
		/// </remarks>
		[ApiExplorerSettings(IgnoreApi = true)]
		[Route("[controller]/InsertWipData")]
		[HttpPost]
		public async Task<IActionResult> InsertWipData(
			[FromQuery] string environment,
			[FromQuery] string tableName,
			[FromBody] TblMesWipData_Record request)
		{
			var result = await _facade.InsertWipDataAsync(environment, tableName, request);
			return result.Result == "Ok" ? Ok(result) : BadRequest(result);
		}

        /// <summary>
        /// LaserMarking功能
        /// 
        /// </summary>
        /// <remarks>
        /// <para>
        /// {
        ///  "environment": "Test",
        ///  "action" : "GenerateTileIds",
		///  "lotno":"WB2025400086-A00064",
        ///  "size": "",
        ///  "product": "2DP000005408",
        ///  "version": "A",
        ///  "stepCode": "BTS00051"
		///  "subbigqty":"96",
		///  "customer":"OSRAM"
        /// }
        /// action:
        /// 1. GetConfigData
        /// 2. CheckConfigDataExists
        /// 3. GenerateTileIds
        /// </para>
        /// </remarks>

        [Route("[controller]/LaserMarking")]
		[HttpPost]
		public async Task<IActionResult> LaserMarking(
			[FromBody] LaserMarkingRequest request)
		{
			if (request == null || string.IsNullOrEmpty(request.Action))
			{
				return BadRequest("請求或動作(Action)不能為空。");
			}

			try
			{
				switch (request.Action)
				{
					case "GetConfigData":
						var result = await _facade.GetConfigDataAsync(request);
						return result.Result == "Ok" ? Ok(result) : BadRequest(result);

					case "CheckConfigDataExists":
						var checkResult = await _facade.GetConfigDataAsync(request);
						
						if (checkResult.Result == "Fail" || checkResult.Data == null)
						{
							return BadRequest(checkResult);
						}
						
						return Ok(checkResult);

                    case "GenerateTileIds":
                        //var tileIdResult = await _laserMarkingService.GenerateTileIdsAsync(request);
                        var tileIdResult = await _facade.GenerateTileIdsAsync(request);
                        return tileIdResult.Result == "Ok" ? Ok(tileIdResult) : BadRequest(tileIdResult);


                    default:
						return BadRequest($"動作 '{request.Action}' 不被支援。");
				}
			}
			catch (Exception ex)
			{
				// 記錄錯誤日誌
				_logger.LogError(ex, "在處理 LaserMarking 請求時發生錯誤。");

				// 返回 500 內部伺服器錯誤
				return StatusCode(500, "內部伺服器錯誤，請稍後再試。"+ex.Message);
			}
		}

		/// <summary>
		/// LaserMarkingFrontend 功能
		/// </summary>
		/// <remarks>
		/// <para>
		/// {
		///  "environment": "Test",
		///  "action":"CREATE",
		///  "lotno": "WB2025400255-A00002",
		///  "eqno": "LS-030",
		///  "qty": 46,
		///  "productno": "1DP000005433",
		///  "checkouttime": "2025-05-12T08:00:00"
		/// }
		/// </para>
		/// </remarks>

		[Route("[controller]/LaserMarkingFrontend")]
		[HttpPost]
		public async Task<IActionResult> LaserMarkingFrontend([FromBody] LaserMarkingFrontendRequest request)
		{
			try
			{
				if (request == null ||
					string.IsNullOrWhiteSpace(request.Environment) ||
					string.IsNullOrWhiteSpace(request.LotNo) ||
					string.IsNullOrWhiteSpace(request.EqNo) ||
					string.IsNullOrWhiteSpace(request.ProductNo) ||
					request.CheckoutTime == default ||
					request.Qty <= 0 || double.IsNaN(request.Qty))
				{
					return BadRequest(ApiReturn<string>.Failure("參數不完整"));
				}

				object result;

				if (request.Action == "CREATE")
				{
					result = await _facade.GenerateFrontendTileIdsAsync(request);
				}
				else
				{
					return BadRequest(ApiReturn<string>.Failure("Action參數不完整"));
				}

				return Ok(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "生成雷射二維條碼時發生錯誤");
				return StatusCode(500, "系統錯誤：" + ex.Message);
			}
		}

		/// <summary>
		/// TestDefectCount功能
		/// 
		/// </summary>
		/// <remarks>
		/// <para>
		///{
		///  "Environment": "Production",
		///  "Action": "DefectCount",
		///  "Programename": "1AM000005111",
		///  "Lotno": "WB2025500042-D00004",
		///  "OpNo": "BTS00091"
		///	}
		/// action:
		/// 1. CountDefects
		/// </para>
		/// </remarks>
		/// 


		[Route("[controller]/TestDefectCount")]
        [HttpPost]
        public async Task<IActionResult> DefectCount([FromBody] DefectCountRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Environment) || string.IsNullOrWhiteSpace(request.Lotno))
                return BadRequest(ApiReturn<string>.Failure("參數不完整"));

            var result = await _facade.CountDefectsAsync(request);
            return result.Result == "Ok" ? Ok(result) : BadRequest(result);
        }


        /// <summary>
        /// 填洞測漏檢查 API
        /// </summary>
        /// <remarks>
        /// <para>
        /// {
        ///  "environment": "Production",
        ///  "action":"SELECT",
        ///  "lotno": "WB2025200242-A00072",
        ///  "opno": "BTS00001",
        ///  "deviceid": "LK-007"
        /// }
        /// </para>
        /// </remarks>

        [Route("[controller]/LeakageCheck")]
		[HttpPost]
		public async Task<IActionResult> LeakageCheck([FromBody] LeakageCheckRequest request)
		{
			if (request == null ||
				string.IsNullOrWhiteSpace(request.Environment) ||
				string.IsNullOrWhiteSpace(request.Lotno) ||
				string.IsNullOrWhiteSpace(request.Opno) 
				)
			{
				return BadRequest(ApiReturn<string>.Failure("參數不完整"));
			}

			object result;

			if (request.Action == "CHECK")
			{
				result = await _facade.LeakageCheckAsync(request);
			}
			else if(request.Action == "SELECT")
			{
				result = await _facade.LeakageSelectAsync(request);
			}
			else
			{
				return BadRequest(ApiReturn<string>.Failure("Action參數不完整"));
			}

			return Ok(result);


		}


		/// <summary>
		/// 呼叫 Teams Alarm
		/// </summary>
		/// <remarks>
		/// <para>
		/// { 
		///  "uri":"uri",
		///  "message":"test"
		/// }
		/// </para>
		/// <para>
		/// 診療室 : 
		/// https://prod-36.southeastasia.logic.azure.com:443/workflows/45e10a07a3d6442ab5b609dcdd0dcca2/triggers/manual/paths/invoke?api-version=2016-06-01&amp;sp=%2Ftriggers%2Fmanual%2Frun&amp;sv=1.0&amp;sig=7DfCU2gMui_RyEXFnpeaiGImC6EBN6-_SocDsj14pGU		
		/// </para>
		/// <para>
		/// 
		/// </para>
		/// </remarks>	
		[Route("[controller]/TeamsAlarm")]
		[HttpPost]
		public async Task<IActionResult> TeamsAlarm([FromBody] TeamsAlarmRequest request)
		{
			//_logger.LogInformation("收到 Teams Alarm 請求: {Message}", request.message);

			var result = await _facade.SendTeamsAlarmAsync(request);

			return result.Result == "Ok" ? Ok(result) : BadRequest(result);
		}

		/// <summary>
		/// 呼叫 Teams Alarm by Group
		/// </summary>
		/// <remarks>
		/// <para>
		/// { 
		/// "environment": "Imesprod",
		///  "notifygroup": "發信群組",
		///  "message":"test"
		/// }
		/// </para>
		/// 
		/// </remarks>			
		[Route("[controller]/TeamsAlarmByGroup")]
		[HttpPost]
		public async Task<IActionResult> TeamsAlarmByGroup([FromBody] TeamsAlarmByGroupRequest request)
		{
			//_logger.LogInformation("收到 Teams Alarm 請求: {Message}", request.message);

			var result = await _facade.SendTeamsAlarmByGroupAsync(request);

			return result.Result == "Ok" ? Ok(result) : BadRequest(result);
		}

		/// <summary>
		/// 使用LT Mail Server發送信件
		/// </summary>
		/// <remarks>
		/// <para>
		/// Sample request:
		///{
		///  "environment": "Production",
		///  "notifygroup": "發信群組",
		///  "title": "標題",
		/// "context": "內容", --語法與HTML相同；若要顯示圖片則用 &lt;img src='cid:image1'&gt;
		///  "attachments": [
		///	"檔案路徑"
		///  ],
		///  "inlineimages": {
		///    "additionalProp1": "圖片的64編碼",
		///    "additionalProp2": "圖片的64編碼",
		///    "additionalProp3": "圖片的64編碼"
		///  }
		///}
		///</para>
		/// </remarks>
		[Route("[controller]/MailSender")]
		[HttpPost]
		public async Task<IActionResult> MailSender([FromBody] MailSenderRequest request)
		{
			try
			{
				var result = await _facade.SendEmailAsync(request);
				return Ok(result);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Mail sending failed: {ex.Message}");
				return BadRequest(new { result = "Fail", message = ex.Message });
			}
		}

		/// <summary>
		/// 黑白名單
		/// </summary>
		/// <remarks>
		/// <para>
		/// 填洞測漏:
		///{
		///  "environment": "Production",
		///  "action": "CHECK",
		///  "lotno": "WB2025200170-A00039",
		///  "opno" : "BTS00001",
		///  "deviceid" : "LK-004"
		///}
		///</para>
		/// <para>
		/// 測試站:
		///{
		///  "environment": "Production",
		///  "action": "CHECK",
		///  "lotno": "WB2025200170-A00039",
		///  "opno" : "BTS00091",
		///  "deviceid" : "FT-044"
		///}
		///</para>
		/// <para>
		/// 剝片/循邊站:
		///{
		///  "environment": "Production",
		///  "action": "CHECK",
		///  "lotno": "WB2025200170-A00039",
		///  "opno" : "BTS000D1",
		///  "deviceid" : "AS-004"
		///}
		///</para>
		///<para>
		/// 尺寸量測站:
		///{
		///  "environment": "Production",
		///  "action": "CHECK",
		///  "lotno": "WB2025200170-A00039",
		///  "opno" : "BTS00071",
		///  "deviceid" : "MT-007"
		///}
		///</para>
		///<para>
		/// 剝片站:
		///{
		///  "environment": "Production",
		///  "action": "CHECK",
		///  "lotno": "WB2025200170-A00039",
		///  "opno" : "BTS000D1",
		///  "deviceid" : ""
		///}
		///</para>
		///<para>
		/// AOI 檢驗站（反面/正面）:
		///{
		///  "environment": "Production",
		///  "action": "CHECK",
		///  "lotno": "WB2025200170-A00039",
		///  "opno" : "BTS00071",
		///  "deviceid" : "AT-001"
		///}
		///</para>				
		///<para>
		/// 雷射標示站:
		///{
		///  "environment": "Production",
		///  "action": "CHECK",
		///  "lotno": "WB2025200170-A00039",
		///  "opno" : "BTS00061",
		///  "deviceid" : "LS-025"
		///}
		///</para>
		///
		/// </remarks>
		[Route("[controller]/LotTileCheck")]
		[HttpPost]
		public async Task<IActionResult> LotTileCheck([FromBody] LotTileCheckRequest request)
		{
			if (request == null || string.IsNullOrWhiteSpace(request.Environment) || string.IsNullOrWhiteSpace(request.LotNo))
			{
				return BadRequest("請求或參數不能為空。");
			}
			if (string.IsNullOrWhiteSpace(request.Action))
			{
				return BadRequest("請求或動作(Action)不能為空。");
			}
			if (request.Action == "CHECK")
			{
				var result = await _facade.LotTileCheckAsync(request);
				return Ok(result);
			}
			else
			{
				return BadRequest("請求或動作(Action)不能為空。");
			}
		}

		/// <summary>
		/// Recipe 2DCode 產生器
		/// </summary>
		/// <remarks>
		/// <para>
		/// {
		///  "environment": "Production",
		///  "action":"GENERATE",
		///  "length":300,
		///  "step": "BTS00001",
		///  "pn" : "2DP000000068",
		///  "lotno": "WB2025200242-A00072",
		///  "gbom": "",
		///  "sequence":"8",		
		///  "recipe": ""
		/// }
		/// </para>
		/// </remarks>
		/// 
		[Route("[controller]/Recipe2DCodeGenerator")]
		[HttpPost]
		public async Task<IActionResult> Generate2DCode([FromBody] Recipe2DCodeRequest request)
		{
			try
			{
				if (request.Action == "GENERATE") 
				{
					var result = await _facade.Save2DCodeAsync(request);
					return result.Result == "Ok" ? Ok(result) : BadRequest(result);
				}
				else if(request.Action == "DOWNLOAD")
				{
					//var repo = _RepositoryFactory.CreateRepository(request.Environment);
					var repositories = RepositoryHelper.CreateRepositories(request.Environment, _RepositoryFactory);
					var repo = repositories["CsCimEmap"];
				
					var sql = "SELECT RECIPE2DCODE FROM ARGOMESRECIPE2DCODE WHERE LOTNO = :Lotno";
					var result = await repo.QueryFirstOrDefaultAsync<byte[]>(sql, new { Lotno = request.Lotno });

					if (result == null || result.Length == 0)
						return NotFound("查無圖檔");

					return File(result, "image/png", $"{request.Lotno}_2dcode.png");
				}
				else
				{
					return BadRequest("不支援的 Action，請使用 GENERATE 或 DOWNLOAD。");
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "生成 2D 條碼時發生錯誤");
				return StatusCode(500, "系統錯誤：" + ex.Message);
			}
		}

		/// <summary>
		/// 讀取良率記錄檔案 API
		/// </summary>
		/// <remarks>
		/// <para>
		/// {
		///   "environment": "Production",
		///   "action": "SELECT",
		///   "productno": "1DP000000026",
		///   "lotno": "WB2024300494-A00003"
		/// }
		/// </para>
		/// </remarks>
		[Route("[controller]/YieldRecordData")]
		[HttpPost]
		public async Task<IActionResult> YieldRecordData([FromBody] YieldRecordDataRequest request)
		{
			if (request == null ||
				string.IsNullOrWhiteSpace(request.Environment) ||
				string.IsNullOrWhiteSpace(request.Action) ||
				string.IsNullOrWhiteSpace(request.ProductNo) ||
				string.IsNullOrWhiteSpace(request.LotNo))
			{
				return BadRequest(ApiReturn<string>.Failure("參數不完整"));
			}

			if (request.Action == "SELECT")
			{
				var result = await _facade.LoadYieldRecordDataAsync(request);
				return result.Result == "Ok" ? Ok(result) : BadRequest(result);
			}

			return BadRequest(ApiReturn<string>.Failure("不支援的 Action"));
		}


		/// <summary>
		/// 寫入 SCADA Tag (透過 OPC UA)
		/// </summary>
		/// <remarks>
		/// <para>
		/// {
		///     "endpointUrl": "opc.tcp://10.14.5.134:49320",
		///     "nodeId": "ns=2;s=ns=2;s=C001.T.T",
		///     "value": 123
		/// }
		/// </para>
		/// </remarks>
		[HttpPost]
		[Route("[controller]/Scada/WriteTag")]
		public async Task<IActionResult> WriteScadaTag([FromBody] ScadaWriteRequest request)
		{
			if (request == null || string.IsNullOrWhiteSpace(request.EndpointUrl) || string.IsNullOrWhiteSpace(request.NodeId))
			{
				return BadRequest(ApiReturn<string>.Failure("請求參數不完整"));
			}

			var result = await _facade.WriteScadaTagAsync(request);
			return result.Result == "Ok" ? Ok(result) : BadRequest(result);
        }


        /// <summary>
        /// RecycleLotCopy功能
        /// 
        /// </summary>
        /// <remarks>
        /// <para>
        ///	{
        /// "environment": "Test",
        /// "action": "RecycleLotCopy",
        /// "Emapping": "Y",
        /// "productno": "2DP000005344",
        /// "lotno": "WB2025500147-D00045",
        /// "m_lotno": "WB2025300115-A00004",
        /// "tileID": ["JKH0X5F", "JKH0X5L", "JKH0X5M", "JKH0X5Y", "JKH0X5Z", "JKH0X83", "JKH0X84", "JKH0X85", "JKH0X88", "JKH0X8N"]
        /// }
        /// action:
        /// 1. RecycleLotCopy
        /// </para>
        /// </remarks>
        /// 
        [Route("[controller]/RecycleLotCopy")]
        [HttpPost]
        public async Task<IActionResult> RecycleLotCopy([FromBody] RecycleLotCopyRequest request)
        {
            var result = await _facade.RecycleLotCopyAsync(request);
            return result.Result == "Ok" ? Ok(result) : BadRequest(result);
        }


        /// <summary>
        /// CheckLimit功能
		///
        /// </summary>
        /// <remarks>
        /// <para>
        ///	{
        /// "environment": "Production",
        /// "action": "CheckLimit",
        /// "deviceid": "PL-014",
        /// "opno": "BDP000G1",
        /// "lotno": "WB2025400110-A00005"
        /// }
        /// {
	    ///"environment": "Production",
	    ///"action": "CheckLimit",
	    ///"deviceid": "CR-060",  
	    ///"opno": "BLS001G1",
	    ///"lotno": "WB2025500189-A00032"
	    ///}
        /// action:
        /// 1. CheckLimit
        /// MESPD003 設備參數異常   ->MESPD-HOLD
        /// MESPD001 無生產批       ->MESPD-HOLD
        /// MESPD005 WIP設備錯誤    ->MESPD-HOLD
        /// MESPD002 設備參數正常   ->可正常CHECK-OUT
        /// MESPD004 MES無設備主檔  ->無法CHECK-OUT請通知IT
        /// </para>
        /// </remarks>
        /// 
    [Route("[controller]/CheckLimit")]
        [HttpPost]
        public async Task<IActionResult> CheckLimit([FromBody] CheckLimitRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Environment) ||
                string.IsNullOrWhiteSpace(request.Action) ||
                string.IsNullOrWhiteSpace(request.DeviceId) ||
                string.IsNullOrWhiteSpace(request.Opno) ||
                string.IsNullOrWhiteSpace(request.Lotno))
            {
                return BadRequest(ApiReturn<string>.Failure("參數不完整"));
            }

            if (request.Action != "CheckLimit")
            {
                return BadRequest(ApiReturn<string>.Failure("不支援的 Action"));
            }

            var result = await _facade.CheckLimitAsync(request);
            return result.Result == "Ok" ? Ok(result) : BadRequest(result);
        }


    }
}
