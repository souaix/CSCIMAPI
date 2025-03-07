using Microsoft.AspNetCore.Mvc;

namespace CimAPI.Controllers
{
	public class HomeController : Controller
	{
		public IActionResult Index()
		{
			return View();
		}
	}
}
