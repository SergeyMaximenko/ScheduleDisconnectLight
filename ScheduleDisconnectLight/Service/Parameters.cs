using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;

namespace Service
{
    /// <summary>
    /// Параметри з закладки "Parameters" (Google Sheet). Колонка A - код, колонка C - значення "Прод".
    /// Читаємо ОДИН раз на початку запуску (Load). Якщо прочитати не вдалося, програма завершується
    /// і нічого не робить (див. Program.Main): наступний запуск спробує ще раз.
    ///
    /// Значення вмикача: 1 (або "так"/"true") - відправляти, все інше (0, порожньо) - не відправляти.
    /// Коди НЕ МІНЯТИ (їх використовує код); назви в колонці B можна змінювати як завгодно.
    /// </summary>
    public static class Parameters
    {
        /// <summary>Код групи відключення світла (значення, не вмикач)</summary>
        public const string GroupCode = "GROUP_CODE";

        /// <summary>Інфо про включення\виключення світла (світло з'явилось / зникло)</summary>
        public const string TgLightEvent = "TG_LIGHT_EVENT";
        /// <summary>Нагадування про заплановане відключення\включення світла (за графіком)</summary>
        public const string TgOutageReminder = "TG_OUTAGE_REMINDER";
        /// <summary>Нагадування про низький рівень палива (потрібна заправка)</summary>
        public const string TgFuelLow = "TG_FUEL_LOW";
        /// <summary>Нагадування про необхідність проведення ТО</summary>
        public const string TgToNeeded = "TG_TO_NEEDED";
        /// <summary>Повідомлення про факт заправки генератора</summary>
        public const string TgRefuelDone = "TG_REFUEL_DONE";
        /// <summary>Повідомлення про факт проведення ТО</summary>
        public const string TgToDone = "TG_TO_DONE";
        /// <summary>Повідомлення про екстрені відключення</summary>
        public const string TgEmergency = "TG_EMERGENCY";
        /// <summary>Повідомлення про зміну графіка відключень</summary>
        public const string TgScheduleChange = "TG_SCHEDULE_CHANGE";

        private static Dictionary<string, string> _values;

        /// <summary>Читає закладку одним запитом. Кидає виняток, якщо не вдалося.</summary>
        public static void Load()
        {
            ValueRange response = null;
            var range = SpreadSheet.SheetParam + "!A1:C200";
            GoogleExecuteRetry.Exec("Parameters: " + range, () =>
            {
                response = SpreadSheet.Service.Spreadsheets.Values.Get(SpreadSheet.SpreadsheetId, range).Execute();
            });

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (response != null && response.Values != null)
            {
                foreach (var row in response.Values)
                {
                    if (row == null || row.Count == 0) continue;
                    var code = Convert.ToString(row[0]).Trim();
                    if (code.Length == 0) continue;
                    if (values.ContainsKey(code))
                    {
                        Console.WriteLine("Parameters: код " + code + " зустрічається більше одного разу, береться перший");
                        continue;
                    }
                    values[code] = row.Count > 2 && row[2] != null ? Convert.ToString(row[2]).Trim() : "";
                }
            }
            if (values.Count == 0) throw new Exception("Закладка " + SpreadSheet.SheetParam + " порожня або не містить кодів у колонці A");

            _values = values;
            Console.WriteLine("Parameters: прочитано " + values.Count + " параметрів");
        }

        /// <summary>Значення параметра або null, якщо коду немає в таблиці.</summary>
        public static string GetString(string code)
        {
            if (_values == null) Load();
            string value;
            return _values.TryGetValue(code, out value) ? value : null;
        }

        /// <summary>
        /// Вмикач. 1 (або "так"/"true") - увімкнено. Усе інше - вимкнено: 0, порожньо, а також коли коду немає в таблиці
        /// (тоді в лог пишеться попередження). Значень за замовчуванням немає: усе регулюється таблицею.
        /// </summary>
        public static bool IsOn(string code)
        {
            var value = GetString(code);
            if (value == null)
            {
                Console.WriteLine("Parameters: параметра " + code + " немає в таблиці, вважається вимкненим (0)");
                return false;
            }
            return value == "1"
                || string.Equals(value, "так", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
