using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Newtonsoft.Json.Linq;
using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Globalization;
using System.IO;
using System.IO;
using System.Linq;
using System.Linq;
using System.Linq;
using System.Net.Http;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography;
using System.Text;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks;
using static ScheduleDisconnectLight.Api;
using static ScheduleDisconnectLight.ParamLasZP;
using static System.Net.Mime.MediaTypeNames;
using System.Globalization;


namespace ScheduleDisconnectLight
{





    public class ParamLasZP
    {
        private bool _sendOnlyTestGroup;
        private int _countSendTg = 0;
        public ParamLasZP(bool sendOnlyTestGroup)
        {
            _sendOnlyTestGroup = sendOnlyTestGroup;
        }



        private string _sheetNameZP = "ЗаправкаСтатистика";
        private string _sheetNameOnOff = "OnOffСтатистика";

        public Param GetParam()
        {
            var service = new SpreadSheet().Get();


            var rowLastZpObj = getRowLastZp(service);
            var rowLastZp = rowLastZpObj.Item3;
            var rowCountZp = rowLastZpObj.Item2;
            var rowILastZp = rowLastZpObj.Item1;

            if (rowLastZp == null)
            {
                Console.WriteLine("Не найдена последння заправка");
                return null;
            }
            var lastZP_UserCode = fromRow<string>(rowLastZp, 4);
            var lastZP_UserName = fromRow<string>(rowLastZp, 5);
            var lastZP_Liters = fromRow<int>(rowLastZp, 2);
            var lastZP_IsSend = fromRow<string>(rowLastZp, 8);
            var maxDateZP = fromRow<DateTime>(rowLastZp, 1);

            if (lastZP_IsSend != "так")
            {

                var message =
                  
                  $"✅ <b>Генератор заправлено</b>\n" +
                  $"\n" +
                  $"🙏 Дякуємо <b>{lastZP_UserName}</b>\n" +
                   (!string.IsNullOrEmpty(lastZP_UserCode) ? $"👤 <b>@{lastZP_UserCode}</b>\n" : "") +
                  (lastZP_Liters !=0 ?  $"⛽️ Дозаправлено <b>{lastZP_Liters} л.</b>\n" : "") +
                  $"💪 Це Ваша <b>{rowCountZp}</b> заправка\n" +
                 "\n" +
                  "<b>Дата заправки</b>:\n" +
                  $"📅 {Api.GetCaptionDate(maxDateZP)}\n" +
                  $"🕒 {Api.TimeToStr(maxDateZP)}\n";
                
                new SenderTelegram()
                {
                    SendOnlyTestGroup = _sendOnlyTestGroup,
                    ReplyMarkupObj = InfoGen.GetReplyMarkup(_sendOnlyTestGroup)
                }.Send(message);

                SpreadSheet.SetValue(service, _sheetNameZP, rowILastZp, 8, "так");
            }


           

            var hoursAfterZP = getTimeAfterZP(service, maxDateZP);



            return new Param()
            {
                LastZP_DateTime = maxDateZP,
                ExecHours = hoursAfterZP,
                LastZP_UserCode = lastZP_UserCode,
                LastZP_UserName = lastZP_UserName,
                LastZP_Liters = lastZP_Liters
            };
        }



        private Tuple<int, int, IList<object>> getRowLastZp(SheetsService service)
        {
            var requestZP = service.Spreadsheets.Values.Get(Api.SpreadsheetId, $"{_sheetNameZP}!A:I");
            var valuesZP = requestZP.Execute().Values;


            //var result = new List<SheetRow>();

            if (valuesZP == null || valuesZP.Count == 0)
            {
                Console.WriteLine($"Закладка {_sheetNameZP} пуста");
                return null;
            }



            IList<object> rowMaxDateZP = null;
            DateTime maxDateZP = DateTime.MinValue;
            var maxIDateZP = 0;

            for (int i = 0; i < valuesZP.Count; i++)
            {
                // Пропускаем заголовок
                if (i == 0)
                {
                    continue;
                }


                var row = valuesZP[i];

                if (skipRowZp(row))
                {
                    continue;
                }


                var dateZPCurrent = fromRow<DateTime>(row, 1);
                if (dateZPCurrent > maxDateZP)
                {
                    maxDateZP = dateZPCurrent;
                    maxIDateZP = i;
                    rowMaxDateZP = row;
                }
            }
            var countZP = 0;

            if (rowMaxDateZP != null)
            {
                var userId = fromRow<string>(rowMaxDateZP, 3);


                for (int i = 0; i < valuesZP.Count; i++)
                {
                    var row = valuesZP[i];

                    if (skipRowZp(row))
                    {
                        continue;
                    }

                    if (fromRow<string>(row, 3) == userId)
                    {
                        countZP = countZP + 1;
                    }
                }
            }
            return Tuple.Create(maxIDateZP, countZP, rowMaxDateZP);

        }

        private bool skipRowZp(IList<object> row)
        {
            var regTest = fromRow<string>(row, 7);
            if (Api.SendOnlyTestGroup(_sendOnlyTestGroup))
            {
                if (regTest != "так")
                {
                    return true;
                }
            }
            else
            {
                if (regTest == "так")
                {
                    return true;
                }
            }
            return false;
        }




        private decimal getTimeAfterZP(SheetsService service, DateTime maxDateZP)
        {


            var requestOnOff = service.Spreadsheets.Values.Get(Api.SpreadsheetId, $"{_sheetNameOnOff}!A:F");
            var valuesOnOff = requestOnOff.Execute().Values;


            var rangesPower = new List<Range>();
            var rangesGen = new List<Range>();

            Range rangePower = null;
            Range rangeGen = null;

            for (int i = 0; i < valuesOnOff.Count; i++)
            {
                // Пропускаем заголовок
                if (i == 0)
                {
                    continue;
                }
                var row = valuesOnOff[i];

                var tip = fromRow<string>(row, 0);

                if (tip == "Світло")
                {
                    fillRanges(Source.Power, row, ref rangePower, rangesPower);
                }
                else if (tip == "Генератор")
                {
                    fillRanges(Source.Gen, row, ref rangeGen, rangesGen);
                }
                else
                {
                    continue;
                }
            }
            if (rangePower != null)
            {
                rangesPower.Add(rangePower);
            }
            if (rangeGen != null)
            {
                rangesGen.Add(rangeGen);
            }




            decimal timeGenZP = 0;

            var listRangeRources = new[] { new RangeSource(Source.Power, rangesPower), new RangeSource(Source.Gen, rangesGen) };

            foreach (var rangeRource in listRangeRources)
            {

                for (int i = 0; i < rangeRource.Ranges.Count; i++)
                {
                    var item = rangeRource.Ranges[i];

                    var isLastRow = i == rangeRource.Ranges.Count - 1;

                    // дата с должна быть заполнена 
                    if (!item.IsSetFrom)
                    {
                        sendTestTelegram($"Для {rangeRource.GetNameSource()} для {item.DateTo} не заполнена дата с ");
                        continue;
                    }
                    if (!isLastRow && !item.IsSetTo)
                    {
                        sendTestTelegram($"Для {rangeRource.GetNameSource()} для {item.DateFrom} не заполнена дата по ");
                        continue;
                    }

                    if (item.IsErrorTime)
                    {
                        sendTestTelegram($"Для {rangeRource.GetNameSource()} для {item.DateTo} некорректно вказано час\r\n" +
                            $"Вказано: {item.Time} Має бути: {item.TimeCalc} ");

                        // Йдемо далі, т.я. час використовуємо розрахунковий
                    }

                    if (rangeRource.Source == Source.Power)
                    {
                        continue;
                    }

                    if (item.DateFrom >= maxDateZP)
                    {
                        if (item.IsSetTo)
                        {
                            timeGenZP = timeGenZP + (decimal)(item.DateTo - item.DateFrom).TotalHours;
                        }
                        else
                        {
                            timeGenZP = timeGenZP + (decimal)(Api.DateTimeUaCurrent - item.DateFrom).TotalHours;
                        }

                    }
                    else if (item.DateTo >= maxDateZP)
                    {
                        timeGenZP = timeGenZP + (decimal)(item.DateTo - maxDateZP).TotalHours;
                    }

                }
            }
            return timeGenZP;
        }



        private void fillRanges(Source source, IList<object> row, ref Range range, List<Range> ranges)
        {




            var date = fromRow<DateTime>(row, 1);
            var onOff = fromRow<int>(row, 2);
            var time = source == Source.Gen
                ? fromRow<decimal>(row, 3)
                : fromRow<decimal>(row, 4);


            var isStart = source == Source.Power ? onOff == 0 : onOff == 1;
            if (isStart)
            {
                // Виключили свет, новое событие 
                if (range != null)
                {
                    ranges.Add(range);
                    range = null;
                }

                range = new Range()
                {
                    DateFrom = date,
                };
            }
            else
            {
                if (range == null)
                {
                    range = new Range()
                    {
                        DateTo = date,
                        Time = time
                    };
                }
                else
                {

                    if (!range.IsSetTo)
                    {
                        range.DateTo = date;
                        range.Time = time;
                        ranges.Add(range);
                        range = null;
                    }
                    else
                    {
                        ranges.Add(range);

                        range = new Range()
                        {
                            DateTo = date,
                            Time = time
                        };
                        ranges.Add(range);
                        range = null;
                    }

                }

            }


        }

        private enum Source
        {
            Gen,
            Power
        }

        private class RangeSource
        {
            public Source Source { get; private set; }

            public string GetNameSource()
            {
                return Source == Source.Power ? "Світло" : "Генератор";
            }
            public List<Range> Ranges { get; private set; }

            public RangeSource(Source source, List<Range> rangeSource)
            {
                Source = source;
                Ranges = rangeSource;
            }
        }

        private void sendTestTelegram(string message)
        {
            _countSendTg++;
            // Иначе через спам может быть ошибка
            if (_countSendTg <= 5)
            {
                new SenderTelegram() { SendOnlyTestGroup = true }.Send(message);
            }
            
        }

        public class Param
        {
            private decimal _liter1Horse = (decimal)8;

            public decimal TotalLiters = 117;

            /// <summary>
            /// Остаток. Сколько литров
            /// </summary>
            public decimal BalanceLiters
            {
                get { return Math.Max(0, Math.Round(TotalLiters - ExecLiters, 1)); }
            }

            public int BalancePercent
            {
                get { return (int)Math.Round(BalanceLiters/TotalLiters*(decimal)100.00,0); }
            }

            /// <summary>
            /// Остаток. Сколько часов
            /// </summary>
            public decimal BalanceHours
            {
                get { return Math.Round(BalanceLiters / _liter1Horse, 2); }

            }

            public bool IsBalanceEmpty
            {
                get { return BalanceHours == 0; }

            }

            public string BalanceHours_Str
            {
                get
                {
                    return Api.GetTimeHours(BalanceHours, true);
                }
            }

            /// <summary>
            /// Использовано. Сколько литров
            /// </summary>
            public decimal ExecLiters
            {
                get
                {
                    return Math.Round(_liter1Horse * ExecHours, 1);
                }
            }


            /// <summary>
            /// Использовано. Сколько часов
            /// </summary>
            public decimal ExecHours;

            public string ExecHours_Str
            {
                get
                {
                    return Api.GetTimeHours(ExecHours, true);
                }
            }


            /// <summary>
            /// Последння заправка. Время 
            /// </summary>
            public DateTime LastZP_DateTime;

            /// <summary>
            /// Последння заправка. Кто заправил
            /// </summary>
            public string LastZP_UserCode;

            /// <summary>
            /// Последння заправка. Кто заправил
            /// </summary>
            public string LastZP_UserName;

            /// <summary>
            /// Последння заправка. Литры
            /// </summary>
            public int LastZP_Liters;


        }


        private T fromRow<T>(IList<object> row, int i)
        {
            if (i + 1 > row.Count)
            {
                return default(T);
            }


            return ConverValue<T>(row[i]);

            




        }


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


        class Range
        {
            public bool IsSetTo { get { return DateTo != DateTime.MinValue; } }
            public bool IsSetFrom { get { return DateFrom != DateTime.MinValue; } }
            public DateTime DateFrom;
            public DateTime DateTo;

            public decimal TimeCalc
            {
                get { return (decimal)(DateTo - DateFrom).TotalHours; }
            }
            public bool IsErrorTime
            {
                get { return IsSetFrom && IsSetTo ? Math.Abs(TimeCalc - Time) > (decimal)0.1 : false; }
            }

            public decimal Time;

        }

    }


}









