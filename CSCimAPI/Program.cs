
using Microsoft.OpenApi.Models;
using Scrutor;
using Swashbuckle.AspNetCore.Swagger;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.Cookies;
using NLog.Web;
using Infrastructure.Data.Repositories;
using Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Data.Factories;
using System.Net;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
var logger = NLogBuilder.ConfigureNLog("NLog.config").GetCurrentClassLogger();
var builder = WebApplication.CreateBuilder(args);

ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;


// 使用 NLog 替代内置日志记录
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
builder.Host.UseNLog();
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddControllers(options =>
{
	options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMvc();

builder.Services.AddSwaggerGen(c => {
	c.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "TH-CS-API",
		Version = "V1",
		Contact = new OpenApiContact { Name = "Silva", Email = "silva.he@theil.com" },
		Description = "TH WEB-API",
	});

	/// 加入xml檔案到swagger
	var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
	var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
	c.IncludeXmlComments(xmlPath);

});

// Add services to the container.

builder.Services.AddControllers();

// 設定正式區與測試區的資料庫連線字串
var databaseConfigs = new Dictionary<string, Dictionary<string, (string ConnectionString, string DbType)>>
{
    // 🔹 正式區資料庫 (envProduction)
    { "Production", new Dictionary<string, (string, string)>
		{
			{ "cim28", ("Server=10.21.100.28;Database=theil_servernew;User=thiler;Password=thil1234;", "MySQL") },
			{ "CsCimEmap", ("Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=192.168.20.120)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=emap)));User Id=cscim;Password=cscim2025adm!;", "Oracle") },
			{ "DboEmap", ("Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=192.168.20.120)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=emap)));User Id=dbo;Password=Memory1900;", "Oracle") },
			{ "LaserMarkingNormal", ("Server=172.24.5.248;Database=theil_servernew;User=root;Password=;", "MySQL") },
			{ "LaserMarkingFrontend", ("Server=172.24.101.42;Database=theil_base_server;User=root;Password=;", "MySQL") },
			{ "LaserMarkingFrontendProd", ("Server=172.24.101.42;Database=theil_base_server;User=root;Password=;", "MySQL") },
			{ "LaserMarkingBackendSel", ("Server=172.24.5.248;Database=theil_servernew;User=root;Password=;", "MySQL") },
            { "LaserMarkingBackendIns", ("Server=172.24.5.248;Database=theil_servernew;User=root;Password=;", "MySQL") }
        }
	},

	// 🔹 測試區資料庫 (envTest)
	{ "Test", new Dictionary<string, (string, string)>
		{
			{ "cim28", ("Server=10.21.100.28;Database=theil_servernew;User=thiler;Password=thil1234;", "MySQL") },
			{ "CsCimEmap", ("Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=10.30.40.133)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=emap)));User Id=cscim;Password=cscim2025adm!;", "Oracle") },
			{ "DboEmap", ("Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=10.30.40.133)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=emap)));User Id=dbo;Password=Memory1900;", "Oracle") },
			{ "LaserMarkingNormal", ("Server=10.12.1.148;Database=theil_servernew;User=root;Password=;", "MySQL") },
			{ "LaserMarkingFrontend", ("Server=10.12.1.148;Database=theil_base_server;User=root;Password=;", "MySQL") },
			{ "LaserMarkingFrontendProd", ("Server=172.24.101.42;Database=theil_base_server;User=root;Password=;", "MySQL") },
			{ "LaserMarkingBackendSel", ("Server=172.24.5.248;Database=theil_servernew;User=root;Password=;", "MySQL") },
            { "LaserMarkingBackendIns", ("Server=10.21.100.28;Database=theil_servernew;User=thiler;Password=thil1234;", "MySQL") }
        }
	}
};
Environment.SetEnvironmentVariable("NLS_LANG", "AMERICAN_AMERICA.AL32UTF8");

// 註冊 RepositoryFactory，支援多個資料庫
builder.Services.AddSingleton<IRepositoryFactory>(new RepositoryFactory(databaseConfigs));


//var dboEmapProdConnectionString = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=192.168.20.120)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=emap)));User Id=dbo;Password=Memory1900;";
//var dboEmapTestConnectionString = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=10.30.40.133)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=emap)));User Id=dbo;Password=Memory1900;";

//var csCimEmapProdConnectionString = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=192.168.20.120)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=emap)));User Id=cscim;Password=cscim2025adm!;";
//var csCimEmapTestConnectionString = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=10.30.40.133)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=emap)));User Id=cscim;Password=cscim2025adm!;";

//var laserMarkingNormalProdConnectionString = "Server=172.24.5.248;Database=theil_servernew;User=root;Password=;";
//var laserMarkingNormalTestConnectionString = "Server=10.12.1.148;Database=theil_servernew;User=root;Password=;";

//var laserMarkingCO2ProdConnectionString = "Server=172.24.101.42;Database=theil_base_server;User=root;Password=;";

////龍潭廠100.28 CIM DB
//var cim28ConnectionString = "Server=10.21.100.28;Database=theil_servernew;User=thiler;Password=thil1234;";

//// 註冊 Factory
//builder.Services.AddSingleton<IRepositoryFactory>(
//	new RepositoryFactory(dboEmapProdConnectionString, dboEmapTestConnectionString,csCimEmapProdConnectionString,csCimEmapTestConnectionString, laserMarkingNormalProdConnectionString, laserMarkingNormalTestConnectionString, laserMarkingCO2ProdConnectionString, cim28ConnectionString));


//// 註冊命名服務支持
//builder.Services.AddSingleton<IEnumerable<KeyValuePair<string, object>>>(sp =>
//{
//	return new List<KeyValuePair<string, object>>
//	{
//		new KeyValuePair<string, object>("Prod", new OracleRepository(dboEmapProdConnectionString)),//舊版 暫時保留
//		new KeyValuePair<string, object>("Test", new OracleRepository(dboEmapTestConnectionString)),//舊版 暫時保留
//		new KeyValuePair<string, object>("dboEmapProd", new OracleRepository(dboEmapProdConnectionString)),
//		new KeyValuePair<string, object>("dboEmapTest", new OracleRepository(dboEmapTestConnectionString)),
//		new KeyValuePair<string, object>("CsCimEmapProd", new OracleRepository(csCimEmapProdConnectionString)),
//		new KeyValuePair<string, object>("CsCimEmapTest", new OracleRepository(csCimEmapTestConnectionString)),
//		new KeyValuePair<string, object>("laserMarkingNormalProd", new MySqlRepository(laserMarkingNormalProdConnectionString)),
//		new KeyValuePair<string, object>("laserMarkingNormalTest", new MySqlRepository(laserMarkingNormalTestConnectionString)),
//		new KeyValuePair<string, object>("laserMarkingCO2Prod", new MySqlRepository(laserMarkingCO2ProdConnectionString)),
//		new KeyValuePair<string, object>("cim28", new MySqlRepository(cim28ConnectionString))
//	};
//});

//Scrutor自動掃描註冊
builder.Services.Scan(scan => scan
	.FromAssemblies(
		Assembly.Load("Infrastructure")
	)
		.AddClasses(classes =>
			classes.InNamespaces("Infrastructure.Services") // 限定命名空間
			.Where(type => type != typeof(RepositoryFactory))) // 排除 RepositoryFactory															   
	.AsImplementedInterfaces()
	.WithScopedLifetime()
);

builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();


app.MapControllers();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
	app.UseSwaggerUI(options => // UseSwaggerUI is called only in Development.
	{
		options.SwaggerEndpoint("/swagger/v1/swagger.json", "ApiHelp V1");
		//options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
		options.RoutePrefix = string.Empty;
	});


}
app.UseSwagger();
app.UseSwaggerUI();
app.UseSwaggerUI(options => // UseSwaggerUI is called only in Development.
{
	options.SwaggerEndpoint("/swagger/v1/swagger.json", "ApiHelp V1");
	//options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
	options.RoutePrefix = string.Empty;
});

//app.MapControllerRoute(
//	   name: "default",
//		  pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
