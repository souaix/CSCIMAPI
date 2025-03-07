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

namespace CimAPI.Controllers
{
    [ApiController]

	public class CSCimAPIController : ControllerBase
	{

		private readonly ILogger<CSCimAPIController> _logger;
		private readonly IConfiguration _configuration;
		private readonly IRepositoryFactory _RepositoryFactory;
		private readonly ICSCimAPIFacade _facade;
        private readonly ILaserMarkingService _laserMarkingService;

        public CSCimAPIController(ILogger<CSCimAPIController> logger, IRepositoryFactory RepositoryFactory, ICSCimAPIFacade facade)
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
		/// {
		///  "environment": "cim28",
		///  "action" : "GetConfigData",
		///  "size": "",
		///  "product": "2DP000000068",
		///  "version": "AE",
		///  "stepCode": "BTS000P1"
		/// }
		/// action:
		/// 1. GetConfigData
		/// 2. CheckConfigDataExists
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

	}
}
