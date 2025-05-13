using Core.Entities.Scada;
using System.Threading.Tasks;

namespace Core.Interfaces
{
	public interface IScadaService
	{
		/// <summary>
		/// 將資料寫入指定的 OPC UA Tag
		/// </summary>
		/// <param name="request">包含 endpoint、nodeId 與要寫入的值</param>
		/// <returns>回傳是否寫入成功</returns>
		Task<bool> WriteTagAsync(ScadaWriteRequest request);
	}
}
