using System;

namespace Core.Utilities
{
    public  class NewSNGenerator
    {
        private readonly string _availableChars; // 可用的編碼字元 (如 ABCDEFGHIJKLMNOPQRSTUVWXYZ)

        public NewSNGenerator(string availableChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
        {
            _availableChars = availableChars;
        }

        /// <summary>
        /// 8. 流水編碼轉換函數
        /// 根據 NewSnPattern 呼叫對應的 SN 產生函數
        /// </summary>
        /// <param name="lastSN">上一個 SN</param>
        /// <param name="pattern">NewSnPattern 定義的編碼模式</param>
        /// <param name="increment">是否遞增</param>
        /// <returns>新產生的 SN</returns>
        public  string GenerateSN(string lastSN, string pattern, bool increment)
        {
            // 8.1 若 NewSnPattern 為 null 或空值，則呼叫 NormalNewSN
            if (string.IsNullOrEmpty(pattern))
                return NormalNewSN(lastSN, increment);

            // 8.2 若 NewSnPattern 為 A00，則呼叫 A00NewSN
            if (pattern == "A00")
                return A00NewSN(lastSN, increment);

            // 8.3 若 NewSnPattern 為 AA-00，則呼叫 AA00NewSN
            if (pattern == "AA-00")
                return AA00NewSN(lastSN, increment);

            // 8.4 若 NewSnPattern 為 A000，則呼叫 A000NewSN
            if (pattern == "A000")
                return A000NewSN(lastSN, increment);

            // 預設使用 NormalNewSN
            return NormalNewSN(lastSN, increment);
        }

        /// <summary>
        /// 這裡將會補充 `NormalNewSN`, `A00NewSN`, `AA00NewSN`, `A000NewSN` 的實作
        /// </summary>
        /// 
        /// <summary>
        /// 9. NormalNewSN函數 - 一般流水號編碼
        /// </summary>
        /// <param name="lastSN">上一個 SN</param>
        /// <param name="increment">是否遞增</param>
        /// <returns>新的 SN</returns>
        private string NormalNewSN(string lastSN, bool increment)
        {
            // 9.6 若傳入值為 SN / GSC，則編碼不遞增，不更新 LastSN
            if (!increment) return lastSN;

            char[] snArray = lastSN.ToCharArray();
            int index = snArray.Length - 1; // 取得 SN 的最後一碼索引

            // 9.7 若傳入值為 SN1 / GSC1，則編碼遞增+1，並更新 LastSN
            while (index >= 0)
            {
                int charIndex = _availableChars.IndexOf(snArray[index]); // 找到當前字元在可用字元集的位置
                if (charIndex == _availableChars.Length - 1) // 9.5.1 若已是可用字元集最後一個，則進位
                {
                    snArray[index] = _availableChars[0]; // 該位歸零
                    index--; // 處理前一碼
                }
                else
                {
                    snArray[index] = _availableChars[charIndex + 1]; // 9.5 遞增當前位數
                    break;
                }
            }

            return new string(snArray);
        }



        //private string NormalNewSN(string lastSN, bool increment)
        //{
        //    if (!increment) return lastSN;

        //    char[] snArray = lastSN.ToCharArray();
        //    int index = snArray.Length - 1; // 取得 SN 的最後一碼索引

        //    while (index >= 0)
        //    {
        //        int charIndex = _availableChars.IndexOf(snArray[index]);

        //        if (charIndex == -1)
        //            throw new Exception($"無效的 SN 字元: {snArray[index]}");

        //        if (charIndex == _availableChars.Length - 1) // 若已是最大值，則進位
        //        {
        //            // 當字元達到最後一個字元時，應該回到 `availableChars[0]`
        //            snArray[index] = _availableChars[0];
        //            index--; // 讓前一個字元繼續進位
        //        }
        //        else
        //        {
        //            // **正確遞增當前字元**
        //            snArray[index] = _availableChars[charIndex + 1];
        //            return new string(snArray); // 立即返回，避免繼續進位
        //        }
        //    }

        //    return new string(snArray);
        //}

        /// <summary>
        /// 這裡將會補充 `A00NewSN`, `AA00NewSN`, `A000NewSN` 的實作
        /// </summary>
        /// <summary>
        /// 10. A00 編碼函數
        /// 以 A00 開始，從第三碼開始遞增
        /// </summary>
        /// <param name="lastSN">上一個 SN</param>
        /// <param name="increment">是否遞增</param>
        /// <returns>新的 SN</returns>
        private string A00NewSN(string lastSN, bool increment)
        {
            char firstChar = lastSN[0];  // 第一碼 (字母)
            char secondChar = lastSN[1]; // 第二碼 (字母或數字)
            int number = int.Parse(lastSN.Substring(2)); // 第三碼 (數字)

            // 10.3 若為 SN / GSC，則不遞增，不更新 LastSN
            if (!increment) return lastSN;

            // 10.4 若為 SN1 / GSC1，則遞增號碼 +1，並更新 LastSN
            number++;

            if (char.IsDigit(secondChar)) // 10.5 若第二碼為數字
            {
                if (number > 9) // 進位
                {
                    number = 0;
                    int index = _availableChars.IndexOf(firstChar);
                    if (index < _availableChars.Length - 1) // 第一碼進位
                    {
                        firstChar = _availableChars[index + 1];
                    }
                    else
                    {
                        throw new Exception("SN 超過最大範圍");
                    }
                }
            }
            else // 10.6 若第二碼為字母
            {
                if (number > 9) // 當第三碼 9 進位時，第二碼遞增
                {
                    number = 0;
                    int index = _availableChars.IndexOf(secondChar);
                    if (index < _availableChars.Length - 1)
                    {
                        secondChar = _availableChars[index + 1];
                    }
                    else
                    {
                        secondChar = _availableChars[0]; // 第二碼歸零
                        int firstIndex = _availableChars.IndexOf(firstChar);
                        if (firstIndex < _availableChars.Length - 1)
                        {
                            firstChar = _availableChars[firstIndex + 1]; // 第一碼進位
                        }
                        else
                        {
                            throw new Exception("SN 超過最大範圍");
                        }
                    }
                }
            }

            return $"{firstChar}{secondChar}{number}";
        }
        /// <summary>
        /// 11. AA00 編碼函數
        /// 以 AA-01 開始，最後兩碼為數字，當數字達 98 進位時，前兩碼遞增
        /// </summary>
        /// <param name="lastSN">上一個 SN</param>
        /// <param name="increment">是否遞增</param>
        /// <returns>新的 SN</returns>
        private string AA00NewSN(string lastSN, bool increment)
        {
            char firstChar = lastSN[0];  // 第一碼 (字母)
            char secondChar = lastSN[1]; // 第二碼 (字母)
            int number = int.Parse(lastSN.Substring(3)); // 最後兩碼數字 (忽略固定的 "-")

            // 11.3 若為 SN / GSC，則不遞增，不更新 LastSN
            if (!increment) return lastSN;

            // 11.4 若為 SN1 / GSC1，則遞增號碼 +1，並更新 LastSN
            number++;

            if (number > 98) // 11.5 最後兩碼從 01 遞增，最大到 98，再遞增時則遞增首兩碼
            {
                number = 1; // 重置為 01
                int secondIndex = _availableChars.IndexOf(secondChar);
                if (secondIndex < _availableChars.Length - 1)
                {
                    secondChar = _availableChars[secondIndex + 1];
                }
                else
                {
                    secondChar = _availableChars[0]; // 第二碼歸零
                    int firstIndex = _availableChars.IndexOf(firstChar);
                    if (firstIndex < _availableChars.Length - 1)
                    {
                        firstChar = _availableChars[firstIndex + 1]; // 第一碼進位
                    }
                    else
                    {
                        throw new Exception("SN 超過最大範圍");
                    }
                }
            }

            return $"{firstChar}{secondChar}-{number:D2}";
        }
        /// <summary>
        /// 12. A000 編碼函數
        /// 以 A000 開始，最後三碼為數字，當數字達 999 進位時，前一碼遞增
        /// </summary>
        /// <param name="lastSN">上一個 SN</param>
        /// <param name="increment">是否遞增</param>
        /// <returns>新的 SN</returns>
        private string A000NewSN(string lastSN, bool increment)
        {
            char firstChar = lastSN[0];  // 第一碼 (字母)
            int number = int.Parse(lastSN.Substring(1)); // 後三碼數字

            // 12.2 若為 SN / GSC，則不遞增，不更新 LastSN
            if (!increment) return lastSN;

            // 12.3 若為 SN1 / GSC1，則遞增號碼 +1，並更新 LastSN
            number++;

            if (number > 999) // 12.4.1 若數字超過 999，則第一碼進位
            {
                number = 1; // 重置為 001
                int firstIndex = _availableChars.IndexOf(firstChar);
                if (firstIndex < _availableChars.Length - 1)
                {
                    firstChar = _availableChars[firstIndex + 1]; // 第一碼進位
                }
                else
                {
                    throw new Exception("SN 超過最大範圍");
                }
            }

            return $"{firstChar}{number:D3}";
        }
    }
}
