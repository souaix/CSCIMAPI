namespace CSCimAPI.Models
{
	public class ApiReturn<T>
	{
		/// <summary>
		/// 是否成功
		/// </summary>
		public string Result { get; set; }

		/// <summary>
		/// 回傳訊息
		/// </summary>
		public string Message { get; set; }

		/// <summary>
		/// 回傳的數據內容
		/// </summary>
		public T Data { get; set; }

		public ApiReturn() { }

		public ApiReturn(string result, string message, T data)
		{
			Result = result;
			Message = message;
			Data = data;
		}

		public static ApiReturn<T> Success(string message, T data)
		{
			return new ApiReturn<T>("Ok", message, data);
		}

		public static ApiReturn<T> Failure(string message, T data = default)
		{
			return new ApiReturn<T>("Fail", message, data);
		}

		public static ApiReturn<T> Warning(string message, T data = default)
		{
			return new ApiReturn<T>("Warning", message, data);
		}
	}
}
