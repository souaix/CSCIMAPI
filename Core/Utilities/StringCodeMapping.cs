using System;
using System.Linq;

namespace Core.Utilities
{
    public static class StringCodeMapping
    {
        /// <summary>
        /// 根據字串轉換規則進行處理
        /// </summary>
        /// <param name="input">要轉換的字串</param>
        /// <param name="requestLotNo">來自 Request 的 LotNo</param>
        /// <returns>轉換後的字串</returns>
        public static string Convert(string input, string requestLotNo)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // 6.1. 若字串以 "TXT=" 開頭，則去掉 "TXT="，保留後面的字串
            if (input.StartsWith("TXT="))
                return input.Substring(4);

            // 6.2. 若字串以 "DWG=" 開頭，則去掉 "DWG="，保留後面的字串
            if (input.StartsWith("DWG="))
                return input.Substring(4);

            // 6.3. 若字串為 "LN"，則轉換為 request.LotNo
            if (input == "LN")
                return requestLotNo;

            // 6.4. 若字串為 "LN2"，則拆解 request.LotNo，去除第 0 位，其他部分重組成字串
            if (input == "LN2")
            {
                var lotNoParts = requestLotNo.Split('-');
                if (lotNoParts.Length > 1)
                {
                    return string.Join("-", lotNoParts.Skip(1)); // 移除第一個部分，重新組合
                }
                return requestLotNo; // 若 LotNo 無 "-"，則不變
            }

            // 預設回傳原始輸入
            return input;
        }
    }
}
