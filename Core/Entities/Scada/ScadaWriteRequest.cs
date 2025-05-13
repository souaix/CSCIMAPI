namespace Core.Entities.Scada
{
	public class ScadaWriteRequest
	{
		/// <summary>
		/// OPC UA Server 的端點，例如：opc.tcp://localhost:49320
		/// </summary>
		public string EndpointUrl { get; set; }

		/// <summary>
		/// 要寫入的 Tag NodeId，例如：ns=2;s=Channel1.Device1.Tag1
		/// </summary>
		public string NodeId { get; set; }

		/// <summary>
		/// 要寫入的值（可為 int, double, bool, string 等）
		/// </summary>
		public object Value { get; set; }

	}
}
