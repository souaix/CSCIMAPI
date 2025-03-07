using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
namespace Core.Entities.LaserMarking
{
	public class LaserMarkingRequest
	{
		public string Environment { get; set; }

		public string Action { get; set; }

		public string LotNo { get; set; }

		public string Size { get; set; } 

		public string Product { get; set; }
		
		public string Version { get; set; }
				
		public string StepCode { get; set; }

		public string SubBigQty { get; set; }

		public string Customer { get; set; }

	}
}
