using System;
using System.Collections.Generic;
using System.Globalization;

namespace Core.Utilities
{
    public class DateCodeMapping
    {
        /// <summary>
        /// 依據轉換規則，將日期格式轉換為對應的代碼
        /// </summary>
        /// <param name="input">要轉換的日期代碼</param>
        /// <param name="configMapping">對應的轉換規則 (Config.YearCode, MonthCode, DayCode)</param>
        /// <returns>轉換後的字串</returns>
        public static string Convert(string input, Dictionary<string, string> configMapping)
        {
            DateTime now = DateTime.Now; // 7.1 取得現在時間

            // 7.2 ~ 7.5 標準日期格式
            if (input == "YYYY") return now.ToString("yyyy");
            if (input == "YY") return now.ToString("yy");
            if (input == "MM") return now.ToString("MM");
            if (input == "DD") return now.ToString("dd");

            // 7.6 取得當前週數 (依 zh-TW 文化特性)
            if (input == "WW")
            {
                var taiwanCulture = new CultureInfo("zh-TW");
                var calendar = taiwanCulture.Calendar;
                int weekOfYear = calendar.GetWeekOfYear(now, CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
                return weekOfYear.ToString("D2");
            }

            // 7.7 ~ 7.9 自訂日期轉換規則
            if (input == "YC") return MapCustomCode(now.ToString("yy"), configMapping);
            if (input == "MC") return MapCustomCode(now.ToString("MM"), configMapping);
            if (input == "DC") return MapCustomCode(now.ToString("dd"), configMapping);

            return input; // 預設回傳原字串
        }

        /// <summary>
        /// 依據 `Config` 的對應表進行轉換 (範例：24=AA, 25=BB)
        /// </summary>
        private static string MapCustomCode(string key, Dictionary<string, string> configMapping)
        {
            //if (configMapping != null && configMapping.TryGetValue(key, out string mappedValue))
            //{
            //    return mappedValue; // 找到對應轉換碼
            //}

            //throw new Exception("日期轉換失敗，請通知工程師檢查設定檔");

            return configMapping.TryGetValue(key, out var value) ? value : key; // fallback to key
        }
    }
}
