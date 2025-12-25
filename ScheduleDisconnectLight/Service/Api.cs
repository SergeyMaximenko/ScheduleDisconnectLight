using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service
{

    public enum SendType
    {
        Auto = 0,
        OnlyProd = 1,
        OnlyTest = 2
    }

    public static class Api
    {
        public static string GetMonthName(int month)
        {
            if (month < 1 || month > 12)
                throw new ArgumentOutOfRangeException(nameof(month), "Місяць має бути від 1 до 12");

            var cultureInfo = new CultureInfo("uk-UA");
            return cultureInfo.DateTimeFormat.GetMonthName(month);
        }

        //DecimalToString(5m);        // "5"
        //DecimalToString(5.0m);      // "5"
        //DecimalToString(5.25m);     // "5.25"
        //DecimalToString(5.200m);    // "5.2"
        //DecimalToString(0.75m);     // "0.75"
        //DecimalToString(10.123400m);// "10.1234"




        public static string DecimalToString(decimal value)
        {
            // Проверяем, есть ли дробная часть
            if (value == decimal.Truncate(value))
            {
                // Целое число → без .0
                return value.ToString("0", CultureInfo.InvariantCulture);
            }

            // Дробное число → с точкой
            return value.ToString("0.################", CultureInfo.InvariantCulture);
        }

        public static string DateToStr(DateTime date)
        {
            return date.Date.ToString("dd.MM.yyyy");
        }

        public static string DateTimeToStr(DateTime dateTime)
        {
            return DateToStr(dateTime) + " " + TimeToStr(dateTime);
        }


        public static string GetCaptionDate(DateTime datePar)
        {
            var date = datePar.Date;
            // Дата
            var result = DateToStr(date);
            // День недели
            result = result + " " + date.ToString("ddd", new CultureInfo("uk-UA"));

            if (date == Api.DateUaCurrent)
            {
                result = result + " " + "(сьогодні)";
            }
            else if (date == Api.DateUaNext.Date)
            {
                result = result + " " + "(завтра)";
            }
            else if (date == Api.DateUaCurrent.AddDays(-1).Date)
            {
                result = result + " " + "(вчора)";
            }
            else if (date == Api.DateUaCurrent.AddDays(-2).Date)
            {
                result = result + " " + "(позавчора)";
            }
            return result;
        }

        public static string GetCaptionDateTime(DateTime dateTimePar)
        {
            return GetCaptionDate(dateTimePar) + " " + TimeToStr(dateTimePar);

        }

        public static bool SendTestGroup(SendType sendType = SendType.Auto)
        {
            if (sendType == SendType.Auto)
            {
                return !Api.IsGitHub();
            }
            if (sendType == SendType.OnlyTest)
            {
                return true;
            }
            if (sendType == SendType.OnlyProd)
            {
                return false;
            }
            return true;
            
        }

       
        


        /// <summary>
        /// Конвертировать время в строку
        /// </summary>
        public static string TimeToStr(DateTime dateTime)
        {
            return TimeToStr(dateTime.TimeOfDay);
        }

        /// <summary>
        /// Конвертировать время в строку
        /// </summary>
        public static string TimeToStr(TimeSpan time)
        {
            return time.Days == 1 && time.Hours == 0 && time.Minutes == 0 ? "24:00" : time.Hours.ToString("D2") + ":" + time.Minutes.ToString("D2");
        }

        /// <summary>
        /// Конвертировать Строку в время
        /// </summary>
        public static TimeSpan StrToTime(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return new TimeSpan(0, 0, 0);
            }
            var strSplit = str.Split(':');
            return new TimeSpan(Convert.ToInt32(strSplit[0]), Convert.ToInt32(strSplit[1]), 0);
        }



        public static bool IsGitHub()
        {
            return Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
        }



        public static DateTime DateTimeUaCurrent { get; set; }

        public static DateTime DateUaCurrent => DateTimeUaCurrent.Date;

        public static DateTime DateUaNext => DateTimeUaCurrent.Date.AddDays(1);


        /// <summary>
        /// Получить наименования количества часов
        /// </summary>
        public static string GetTimeHours(decimal hours, bool notAddBrackets = false)
        {
            return GetTimeHours(TimeSpan.FromHours((double)hours), notAddBrackets);
        }

        /// <summary>
        /// Получить наименования количества часов
        /// </summary>
        public static string GetTimeHours(TimeSpan timeSpan, bool notAddBrackets = false)
        {
            var result = "";
            if (timeSpan.Hours > 0)
            {
                result = result + (!string.IsNullOrEmpty(result) ? " " : "") + $"{timeSpan.Hours} год";
            }
            if (timeSpan.Minutes > 0)
            {
                result = result + (!string.IsNullOrEmpty(result) ? " " : "") + $"{(timeSpan.Minutes == 29 || timeSpan.Minutes == 31 ? 30 : timeSpan.Minutes)} хв";
            }
            if (string.IsNullOrEmpty(result))
            {
                result = "0 год 0 хв";
            }


            if (notAddBrackets)
            {
                return !string.IsNullOrEmpty(result) ? result : "";
            }

            return !string.IsNullOrEmpty(result) ? "(" + result + ")" : "";

        }
        /*
        public static T ConverValue<T>(object value)
        {
            Type t = typeof(T);
            // null / DBNull
            if (value == null || value == DBNull.Value)
                return default(T);

            // "" / whitespace
            if (value is string s0 && string.IsNullOrWhiteSpace(s0))
            {
                // ВАЖЛИВОЕ ПРАВИЛО
                if (t == typeof(decimal) || t == typeof(decimal?))
                    return (T)(object)0m;

                if (t == typeof(int) || t == typeof(int?))
                    return (T)(object)0;

                if (t == typeof(string))
                    return (T)(object)string.Empty;

                return default(T);
            }

            Type targetType = Nullable.GetUnderlyingType(t) ?? t;

            // уже нужный тип
            if (value is T ready)
                return ready;

            // decimal
            if (targetType == typeof(decimal))
            {
                if (value is decimal dm)
                    return (T)(object)dm;

                // Google Sheets / Excel API часто возвращает double
                if (value is double dd)
                    return (T)(object)Convert.ToDecimal(dd, CultureInfo.InvariantCulture);

                if (value is float ff)
                    return (T)(object)Convert.ToDecimal(ff, CultureInfo.InvariantCulture);

                if (value is string s)
                {
                    s = s.Trim();

                    // 1) пробуем украинскую/русскую культуру (запятая как дробь)
                    if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.GetCultureInfo("uk-UA"), out var v1) ||
                        decimal.TryParse(s, NumberStyles.Number, CultureInfo.GetCultureInfo("ru-RU"), out v1))
                        return (T)(object)v1;

                    // 2) пробуем инвариантную (точка как дробь)
                    if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v2))
                        return (T)(object)v2;

                    // 3) последний шанс: нормализуем запятую в точку
                    s = s.Replace(" ", "").Replace(",", ".");
                    return (T)(object)decimal.Parse(s, NumberStyles.Number, CultureInfo.InvariantCulture);
                }

                return (T)(object)Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(int))
            {
                if (value is int m)
                    return (T)(object)m;

                if (value is string s)
                    return (T)(object)int.Parse(s.Trim());

                return (T)(object)Convert.ToDecimal(value);
            }

            // остальные типы
            return (T)Convert.ChangeType(value, targetType);
        }


        */

    }

    public class ConnectParam
    {
        public string BotToken { get; private set; }

        public string ChatId { get; private set; }
        public string ChatIdThread { get; private set; }
        
        public string ChatIdThreadAdditional { get; private set; }

        public readonly string BotUsername = "Chavdar13_2bot";

        public bool SendInTestGroup { get; private set; }


        public ConnectParam(SendType sendType = SendType.Auto)
        {

            if (Api.SendTestGroup(sendType))
            {
                SendInTestGroup = true;
                ChatId = "-1002275491172";
                ChatIdThread = "";
                ChatIdThreadAdditional = "";

                //ChatId = "-1003462831682";
                //ChatIdThread = "2";

            }
            else
            {

                SendInTestGroup = false;
                ChatId = "-1001043114362";
                ChatIdThread = "54031";
                // Это дополнительная група, где собраны показатели
                ChatIdThreadAdditional = "55539";

            }



            // 1. Если работаем в GitHub Actions
            if (Api.IsGitHub())
            {
                string tokenFromGitHub = Environment.GetEnvironmentVariable("BOT_TOKEN");

                if (string.IsNullOrWhiteSpace(tokenFromGitHub))
                {
                    throw new Exception("BOT_TOKEN не найден в GitHub Actions переменных!");
                }

                BotToken = tokenFromGitHub;
            }
            else
            {
                // 2. Локальный режим → читаем appsettings.Local.json
                string repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));

                string localPath = Path.Combine(repoRoot, "appsettings.Local.json");

                if (!File.Exists(localPath))
                {
                    throw new Exception($"Файл {localPath} не найден!");
                }

                BotToken = new Json(File.ReadAllText(localPath))["BotToken"].Value;

                if (string.IsNullOrWhiteSpace(BotToken))
                {
                    throw new Exception("BotToken не найден в appsettings.Local.json");
                }
            }

        }
    }
}
