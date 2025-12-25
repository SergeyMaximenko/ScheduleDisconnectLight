using Google.Apis.Sheets.v4;
using Service;
using System;
using System.Collections.Generic;


namespace ScheduleDisconnectLight
{
    /// <summary>
    /// Статус генератора
    /// </summary>
    public class GeneratorStatus
    {
        /// Кількість Разів відправки повідомлення
        private int _countSendTgError = 0;

        /// Відправляти тільки в тестову групу 
        private bool _sendOnlyTestGroupParam;

        /// Сервіс для роботи з Excel
        private SheetsService _sheetsService;

        /// <summary>
        /// Не відправляти повідомлення
        /// </summary>
        public bool NotSendMessage { get; set; }


        public GeneratorStatus(bool sendOnlyTestGroupParam)
        {
            _sendOnlyTestGroupParam = sendOnlyTestGroupParam;
            _sheetsService = new SpreadSheet().GetService();
        }

        public Param GetParam()
        {
            var lastRefuelObject = getLastRefuelRow();
            if (lastRefuelObject == null)
            {
                Console.WriteLine("Не найден обьект последней заправки");
                return null;
            }
    
            var lastRefuel_Row = lastRefuelObject.Item2;
            var countRefuel_All = lastRefuelObject.Item3;
            var countRefuel_Month = lastRefuelObject.Item4;
            var lastRefuel_RowID = lastRefuelObject.Item1;

            if (lastRefuel_Row == null)
            {
                Console.WriteLine("Не найдена последння заправка");
                return null;
            }


            var lastRefuel_UserCode = fromRow<string>(lastRefuel_Row, 4);
            var lastRefuel_UserName = fromRow<string>(lastRefuel_Row, 5);
            var lastRefuel_Liters = fromRow<int>(lastRefuel_Row, 2);
            var lastRefuel_IsSend = fromRow<string>(lastRefuel_Row, 8);
            var lastRefuel_DateTime = fromRow<DateTime>(lastRefuel_Row, 1);

            var hoursAfterZP = getAfterRefuelHours(lastRefuel_DateTime);

            var result = new Param()
            {
                LastRefuel_DateTime = lastRefuel_DateTime,
                AfterRefuel_Hours = hoursAfterZP,
                LastRefuel_UserCode = lastRefuel_UserCode,
                LastRefuel_UserName = lastRefuel_UserName,
                LastRefuel_Liters = lastRefuel_Liters
            };

            if (lastRefuel_IsSend != "так" && !NotSendMessage)
            {
                Param oldGenStatus = null;
                var oldDateTimeUaCurrent = Api.DateTimeUaCurrent;
                Api.DateTimeUaCurrent = lastRefuel_DateTime.AddSeconds(-1);
                try
                {
                    oldGenStatus = new GeneratorStatus(_sendOnlyTestGroupParam).GetParam();
                }
                finally
                {
                    Api.DateTimeUaCurrent = oldDateTimeUaCurrent;
                }


                var message =

                  $"✅ <b>Генератор заправлено</b>\n" +
                  $"\n" +
                  $"🙏 <b>Дякуємо {lastRefuel_UserName}</b>\n" +
                   (!string.IsNullOrEmpty(lastRefuel_UserCode) ? $"👤 <b>@{lastRefuel_UserCode}</b>\n" : "") +
                   (lastRefuel_Liters != 0                     ? $"⛽️ дозаправлено - <b>{lastRefuel_Liters} л</b>\n" : "") +
                   (oldGenStatus != null                       ? $"⚙️ використано ~ <b>{oldGenStatus.AfterRefuel_LitersStr} л</b>\n" : "") +
                  "\n" +
                  "<b>Дата заправки</b>:\n" +
                  $"📅 {Api.GetCaptionDate(lastRefuel_DateTime)}\n" +
                  $"🕒 {Api.TimeToStr(lastRefuel_DateTime)}\n" +
                  $"\n" +
                  $"<b>Ваша винагорода:</b>\n" +
                  $"📅 за <b>{Api.GetMonthName(lastRefuel_DateTime.Month)}</b>\n" +
                  $"💪 заправок: <b>{countRefuel_Month}</b>\n" +
                  $"💰 <b>винагорода:</b> {countRefuel_Month}*200=<b>{countRefuel_Month * 200} грн</b>\n" +
                  $"📈 всього Ваших заправок: <b>{countRefuel_All}</b>";


                new SenderTelegram()
                {
                    SendOnlyTestGroupParam = _sendOnlyTestGroupParam,
                    ReplyMarkupObj = GeneratorNotification.GetReplyMarkup(_sendOnlyTestGroupParam)
                }.Send(message);

                SpreadSheet.SetValue(_sheetsService, SpreadSheet.SheetNameFuelStatistic, lastRefuel_RowID, 8, "так");
                SpreadSheet.SetValue(_sheetsService, SpreadSheet.SheetNameFuelStatistic, lastRefuel_RowID, 9, Math.Round(oldGenStatus?.AfterRefuel_Liters ?? (decimal)0, 3));
            }

            return result;
        }

        /// <summary>
        /// Отримати рядок останньої заправки 
        /// </summary>
        private Tuple<int, IList<object>, int, int> getLastRefuelRow()
        {
            var valuesRefuel = _sheetsService.Spreadsheets.Values.Get(SpreadSheet.SpreadsheetId, $"{SpreadSheet.SheetNameFuelStatistic}!A:I").Execute().Values;

            if (valuesRefuel == null || valuesRefuel.Count == 0)
            {
                Console.WriteLine($"Закладка {SpreadSheet.SheetNameFuelStatistic} пуста");
                return null;
            }

            // Пропустити рядок заправки по умовам 
            bool skipRowRefuel(IList<object> row, int rowID)
            {
                // 1. Пропускаем заголовок
                if (rowID == 0)
                {
                    return true;
                }

                var currentRefuel_Date = fromRow<DateTime>(row, 1);
                var currentRefuel_RegTest = fromRow<string>(row, 7);

                // 2. Пропускаем, если дата больше чем текущая 
                if (currentRefuel_Date > Api.DateTimeUaCurrent)
                {
                    return true;
                }

                // 3. Проверяем строки: для теста берем только 
                if (Api.SendOnlyTestGroup(_sendOnlyTestGroupParam))
                {
                    if (currentRefuel_RegTest != "так")
                    {
                        return true;
                    }
                }
                else
                {
                    if (currentRefuel_RegTest == "так")
                    {
                        return true;
                    }
                }
                return false;
            }


            IList<object> lastRefuel_Row = null;
            var lastRefuel_RowID = 0;
            var lastRefuel_Date = DateTime.MinValue;

            //1. Знайти строку з максимальною датою заправки 
            for (int i = 0; i < valuesRefuel.Count; i++)
            {
                var row = valuesRefuel[i];

                if (skipRowRefuel(row, i))
                {
                    continue;
                }

                var currentRefuel_Date = fromRow<DateTime>(row, 1);
                if (currentRefuel_Date > lastRefuel_Date)
                {
                    lastRefuel_Date = currentRefuel_Date;
                    lastRefuel_RowID = i;
                    lastRefuel_Row = row;
                }
            }

            //2. Для користувача знайти кільеість заправок
            var countRefuel_All = 0;
            var countRefuel_Month = 0;

            if (lastRefuel_Row != null)
            {
                var userId = fromRow<string>(lastRefuel_Row, 3);


                for (int i = 0; i < valuesRefuel.Count; i++)
                {
                    var row = valuesRefuel[i];

                    if (skipRowRefuel(row, i))
                    {
                        continue;
                    }

                    if (fromRow<string>(row, 3) == userId)
                    {

                        countRefuel_All = countRefuel_All + 1;

                        var dateRow = fromRow<DateTime>(row, 1);

                        if (lastRefuel_Date.Year == dateRow.Year && lastRefuel_Date.Month == dateRow.Month)
                        {
                            countRefuel_Month = countRefuel_Month + 1;
                        }
                    }
                }
            }
            return Tuple.Create(lastRefuel_RowID, lastRefuel_Row, countRefuel_All, countRefuel_Month);

        }






        private decimal getAfterRefuelHours(DateTime lastRefuel_DateTime)
        {


            var requestOnOff = _sheetsService.Spreadsheets.Values.Get(SpreadSheet.SpreadsheetId, $"{SpreadSheet.SheetNameOnOffStatistic}!A:F");
            var valuesOnOff = requestOnOff.Execute().Values;
            if (valuesOnOff == null || valuesOnOff.Count == 0)
            {
                Console.WriteLine($"Закладка {SpreadSheet.SheetNameOnOffStatistic} пуста");
                return 0;
            }

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

                    if (item.DateFrom >= lastRefuel_DateTime)
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
                    else if (item.DateTo >= lastRefuel_DateTime)
                    {
                        timeGenZP = timeGenZP + (decimal)(item.DateTo - lastRefuel_DateTime).TotalHours;
                    }

                }
            }
            return timeGenZP;
        }



        private void fillRanges(Source source, IList<object> row, ref Range range, List<Range> ranges)
        {

            var date = fromRow<DateTime>(row, 1);

            if (date > Api.DateTimeUaCurrent)
            {
                return;
            }

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
            if (NotSendMessage)
            {
                return;
            }

            _countSendTgError++;
            // Иначе через спам может быть ошибка
            if (_countSendTgError <= 5)
            {
                new SenderTelegram() { SendOnlyTestGroupParam = true }.Send(message);
            }

        }

        /// <summary>
        /// Класс параметров состояние генератора
        /// </summary>
        public class Param
        {
            private static decimal _liter1Horse = 8;

            private static decimal _totalLitersInGenerator = 117;

            /// <summary>
            /// Остаток. Сколько литров
            /// </summary>
            public decimal Balance_Liters
            {
                get { return Math.Max(0, _totalLitersInGenerator - AfterRefuel_Liters); }
            }

            /// <summary>
            /// Остаток. Сколько литров для протокола
            /// </summary>
            public string Balance_LitersStr
            {
                get
                {
                    return Balance_Liters > 0 && Balance_Liters < 1 ? "1" : ((int)Math.Round(Balance_Liters, 0)).ToString();
                }
            }

            /// <summary>
            /// Остаток. % 
            /// </summary>
            public int Balance_Percent
            {
                get { return (int)Math.Round(Balance_Liters / _totalLitersInGenerator * (decimal)100.00, 0); }
            }

 
            /// <summary>
            /// Остаток. Сколько часов
            /// </summary>
            public decimal Balance_Hours
            {
                get { return Math.Round(Balance_Liters / _liter1Horse, 2); }

            }


            /// <summary>
            /// Остаток. Сколько часов для протокола
            /// </summary>
            public string Balance_HoursStr
            {
                get
                {
                    return Api.GetTimeHours(Balance_Hours, true);
                }
            }

            /// <summary>
            /// Время не осталось
            /// </summary>
            public bool IsBalanceEmpty
            {
                get { return Balance_Hours == 0; }

            }

            

            /// <summary>
            /// Использовано. Сколько литров
            /// </summary>
            public decimal AfterRefuel_Liters 
            {
                get
                {
                    return _liter1Horse * AfterRefuel_Hours;
                }
            }

            /// <summary>
            /// Использовано. Сколько литров
            /// </summary>
            public string AfterRefuel_LitersStr
            {
                get
                {
                    return AfterRefuel_Liters > 0 && AfterRefuel_Liters < 1 ? "1" : ((int)Math.Round(AfterRefuel_Liters, 0)).ToString();
                }
            }

            /// <summary>
            /// Использовано. Сколько часов
            /// </summary>
            public decimal AfterRefuel_Hours;

            /// <summary>
            /// Использовано. Сколько часов для протокола
            /// </summary>
            public string AfterRefuel_HoursStr
            {
                get
                {
                    return Api.GetTimeHours(AfterRefuel_Hours, true);
                }
            }

            /// <summary>
            /// Последння заправка. Время 
            /// </summary>
            public DateTime LastRefuel_DateTime;

            /// <summary>
            /// Последння заправка. Кто заправил
            /// </summary>
            public string LastRefuel_UserCode;

            /// <summary>
            /// Последння заправка. Кто заправил
            /// </summary>
            public string LastRefuel_UserName;

            /// <summary>
            /// Последння заправка. Литры
            /// </summary>
            public int LastRefuel_Liters;


        }


        private T fromRow<T>(IList<object> row, int columnID)
        {
            if (columnID + 1 > row.Count)
            {
                return default(T);
            }


            return TypeTools.Convert<T>(row[columnID]);

        }


        

        private class Range
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









