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
        private SendType _sendType = SendType.Auto;

        /// Сервіс для роботи з Excel
        private SheetsService _sheetsService;

        /// <summary>
        /// Не відправляти повідомлення
        /// </summary>
        public bool NotSendMessage { get; set; }


        public GeneratorStatus(SendType sendType)
        {
            _sendType = sendType;
            _sheetsService = new SpreadSheet().GetService();
        }

        public ParamRefuel GetParam()
        {
            //-------------------
            // ПОСЛЕДННЯ ЗАПРАВКА
            // ------------------
            var refuel_Last_Object = getRefuelLastRow();
            if (refuel_Last_Object == null)
            {
                Console.WriteLine("Не найден обьект последней заправки");
                return null;
            }

            var refuel_Last_RowID = refuel_Last_Object.Item1;
            var refuel_Last_Row = refuel_Last_Object.Item2;
            var refuel_Count_All = refuel_Last_Object.Item3;
            var refuel_Count_Month = refuel_Last_Object.Item4;
            

            if (refuel_Last_Row == null)
            {
                Console.WriteLine("Не найдена последння заправка");
                return null;
            }

            var refuel_Last_UserCode = fromRow<string>(refuel_Last_Row, 4);
            var refuel_Last_UserName = fromRow<string>(refuel_Last_Row, 5);
            var refuel_Last_Liters = fromRow<int>(refuel_Last_Row, 2);
            var refuel_Last_IsSend = fromRow<string>(refuel_Last_Row, 8);
            var refuel_Last_DateTime = fromRow<DateTime>(refuel_Last_Row, 1);

            /*
            //-------------------
            // ПОСЛЕДНЕЕ ТО
            // ------------------

            var TO_Last_Object = getTOLastRow();
            if (TO_Last_Object == null)
            {
                Console.WriteLine("Не найден обьект последней заправки");
                return null;
            }

            var TO_Last_RowID = TO_Last_Object.Item1;
            var TO_Last_Row = TO_Last_Object.Item2;
            var TO_Count_All = TO_Last_Object.Item3;
            var TO_Count_Month = TO_Last_Object.Item4;


            if (TO_Last_Row == null)
            {
                Console.WriteLine("Не найдена последння заправка");
                return null;
            }

            var TO_Last_UserCode = fromRow<string>(TO_Last_Row, 4);
            var TO_Last_UserName = fromRow<string>(TO_Last_Row, 5);
            var TO_Last_Liters = fromRow<int>(TO_Last_Row, 2);
            var TO_Last_IsSend = fromRow<string>(TO_Last_Row, 8);
            var TO_Last_DateTime = fromRow<DateTime>(TO_Last_Row, 1);
            */

            //-------------------
            // СКОЛЬКО ВРЕМЕНИ ПРОШЛО С МОМЕНТА СОБЫТИЯ
            // ------------------
            var afterEventHours = getAfterEventHours(refuel_Last_DateTime);
            var afterRefuel_Hours = afterEventHours;
            //var afterTO_Hours = afterEventHours.Item2;


            var result = new ParamRefuel()
            {
                Refuel_Last_DateTime = refuel_Last_DateTime,
                Refuel_ExecAfter_Hours = afterRefuel_Hours,
                Refuel_Last_UserCode = refuel_Last_UserCode,
                Refuel_Last_UserName = refuel_Last_UserName,
                Refuel_Last_Liters = refuel_Last_Liters
            };

            if (refuel_Last_IsSend != "так" && !NotSendMessage)
            {
                ParamRefuel oldGenStatus = null;
                var oldDateTimeUaCurrent = Api.DateTimeUaCurrent;
                Api.DateTimeUaCurrent = refuel_Last_DateTime.AddSeconds(-1);
                try
                {
                    oldGenStatus = new GeneratorStatus(_sendType).GetParam();
                }
                finally
                {
                    Api.DateTimeUaCurrent = oldDateTimeUaCurrent;
                }


                var message =

                  $"✅ <b>Генератор заправлено</b>\n" +
                  $"\n" +
                  $"🙏 <b>Дякуємо {refuel_Last_UserName}</b>\n" +
                   (!string.IsNullOrEmpty(refuel_Last_UserCode) ? $"👤 <b>@{refuel_Last_UserCode}</b>\n" : "") +
                   (refuel_Last_Liters != 0                     ? $"⛽️ дозаправлено - <b>{refuel_Last_Liters} л</b>\n" : "") +
                   (oldGenStatus != null                       ? $"⚙️ використано ~ <b>{oldGenStatus.Refuel_ExecAfter_LitersStr} л</b>\n" : "") +
                  "\n" +
                  "<b>Дата заправки</b>:\n" +
                  $"📅 {Api.GetCaptionDate(refuel_Last_DateTime)}\n" +
                  $"🕒 {Api.TimeToStr(refuel_Last_DateTime)}\n" +
                  $"\n" +
                  $"<b>Ваша винагорода:</b>\n" +
                  $"📅 за <b>{Api.GetMonthName(refuel_Last_DateTime.Month)}</b>\n" +
                  $"💪 заправок: <b>{refuel_Count_Month}</b>\n" +
                  $"💰 <b>винагорода:</b> {refuel_Count_Month}*200=<b>{refuel_Count_Month * 200} грн</b>\n" +
                  $"📈 всього Ваших заправок: <b>{refuel_Count_All}</b>";


                new SenderTelegram()
                {
                    SendType = _sendType,
                    ReplyMarkupObj = GeneratorNotification.GetReplyMarkup(_sendType, new[] { ReplyMarkup.SetIndicators, ReplyMarkup.ShowIndicators })
                }.Send(message);

                SpreadSheet.SetValue(_sheetsService, SpreadSheet.SheetNameFuelStatistic, refuel_Last_RowID, 8, "так");
                SpreadSheet.SetValue(_sheetsService, SpreadSheet.SheetNameFuelStatistic, refuel_Last_RowID, 9, Math.Round(oldGenStatus?.Refuel_ExecAfter_Liters ?? (decimal)0, 3));
            }

            return result;
        }



        /// <summary>
        /// Отримати рядок останньої заправки 
        /// </summary>
        private Tuple<int, IList<object>, int, int> getRefuelLastRow()
        {
            var refuel_Values = _sheetsService.Spreadsheets.Values.Get(SpreadSheet.SpreadsheetId, $"{SpreadSheet.SheetNameFuelStatistic}!A:I").Execute().Values;

            if (refuel_Values == null || refuel_Values.Count == 0)
            {
                Console.WriteLine($"Закладка {SpreadSheet.SheetNameFuelStatistic} пуста");
                return null;
            }

            // Пропустити рядок заправки по умовам 
            bool refuel_SkipRow(IList<object> row, int rowID)
            {
                // 1. Пропускаем заголовок
                if (rowID == 0)
                {
                    return true;
                }

                var refuel_Current_Date = fromRow<DateTime>(row, 1);
                var refuel_Current_RegTest = fromRow<string>(row, 7);

                // 2. Пропускаем, если дата больше чем текущая 
                if (refuel_Current_Date > Api.DateTimeUaCurrent)
                {
                    return true;
                }

                // 3. Проверяем строки: для теста берем только 
                if (Api.SendTestGroup(_sendType))
                {
                    if (refuel_Current_RegTest != "так")
                    {
                        return true;
                    }
                }
                else
                {
                    if (refuel_Current_RegTest == "так")
                    {
                        return true;
                    }
                }
                return false;
            }


            IList<object> refuel_Last_Row = null;
            var refuel_Last_RowID = 0;
            var refuel_Last_Date = DateTime.MinValue;

            //1. Знайти строку з максимальною датою заправки 
            for (int i = 0; i < refuel_Values.Count; i++)
            {
                var row = refuel_Values[i];

                if (refuel_SkipRow(row, i))
                {
                    continue;
                }

                var currentRefuel_Date = fromRow<DateTime>(row, 1);
                if (currentRefuel_Date > refuel_Last_Date)
                {
                    refuel_Last_Date = currentRefuel_Date;
                    refuel_Last_RowID = i;
                    refuel_Last_Row = row;
                }
            }

            //2. Для користувача знайти кільеість заправок
            var countRefuel_All = 0;
            var countRefuel_Month = 0;

            if (refuel_Last_Row != null)
            {
                var userId = fromRow<string>(refuel_Last_Row, 3);


                for (int i = 0; i < refuel_Values.Count; i++)
                {
                    var row = refuel_Values[i];

                    if (refuel_SkipRow(row, i))
                    {
                        continue;
                    }

                    if (fromRow<string>(row, 3) == userId)
                    {

                        countRefuel_All = countRefuel_All + 1;

                        var dateRow = fromRow<DateTime>(row, 1);

                        if (refuel_Last_Date.Year == dateRow.Year && refuel_Last_Date.Month == dateRow.Month)
                        {
                            countRefuel_Month = countRefuel_Month + 1;
                        }
                    }
                }
            }
            return Tuple.Create(refuel_Last_RowID, refuel_Last_Row, countRefuel_All, countRefuel_Month);

        }



        /// <summary>
        /// Отримати рядок останного ТО
        /// </summary>
        public Tuple<int, IList<object>, int> getLastTORow()
        {
            var valuesTO = _sheetsService.Spreadsheets.Values.Get(SpreadSheet.SpreadsheetId, $"{SpreadSheet.SheetNameTOStatistic}!A:H").Execute().Values;

            if (valuesTO == null || valuesTO.Count == 0)
            {
                Console.WriteLine($"Закладка {SpreadSheet.SheetNameTOStatistic} пуста");
                return null;
            }

            // Пропустити рядок заправки по умовам 
            bool skipRowTO(IList<object> row, int rowID)
            {
                // 1. Пропускаем заголовок
                if (rowID == 0)
                {
                    return true;
                }

                var currentTO_Date = fromRow<DateTime>(row, 1);
                var currentTO_RegTest = fromRow<string>(row, 7);

                // 2. Пропускаем, если дата больше чем текущая 
                if (currentTO_Date > Api.DateTimeUaCurrent)
                {
                    return true;
                }

                // 3. Проверяем строки: для теста берем только 
                if (Api.SendTestGroup(_sendType))
                {
                    if (currentTO_RegTest != "так")
                    {
                        return true;
                    }
                }
                else
                {
                    if (currentTO_RegTest == "так")
                    {
                        return true;
                    }
                }
                return false;
            }


            IList<object> lastTO_Row = null;
            var lastTO_RowID = 0;
            var lastTO_Date = DateTime.MinValue;

            //1. Знайти строку з максимальною датою заправки 
            var countTO_All = 0;
            for (int i = 0; i < valuesTO.Count; i++)
            {
                var row = valuesTO[i];

                if (skipRowTO(row, i))
                {
                    continue;
                }
                
                countTO_All++;

                var currentTO_Date = fromRow<DateTime>(row, 1);
                if (currentTO_Date > lastTO_Date)
                {
                    lastTO_Date = currentTO_Date;
                    lastTO_RowID = i;
                    lastTO_Row = row;
                }
            }

           
            return Tuple.Create(lastTO_RowID, lastTO_Row, countTO_All);

        }





        private decimal getAfterEventHours(DateTime lastRefuel_DateTime)
        {


            var requestOnOff = _sheetsService.Spreadsheets.Values.Get(SpreadSheet.SpreadsheetId, $"{SpreadSheet.SheetNameOnOffStatistic}!A:F");
            var valuesOnOff = requestOnOff.Execute().Values;
            if (valuesOnOff == null || valuesOnOff.Count == 0)
            {
                Console.WriteLine($"Закладка {SpreadSheet.SheetNameOnOffStatistic} пуста");
                return 0;//Tuple.Create((decimal)0, (decimal)0);
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




            decimal afterRefuel_Hours = 0;
            decimal afterTO_Hours = 0;

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


                    var dateFrom = item.DateFrom;
                    var dateTo = item.DateTo == DateTime.MinValue ? Api.DateTimeUaCurrent : item.DateTo; 


                    // С МОМЕНТА ПОСЛЕДНЕЙ ЗАПРАВКИ
                    if (dateFrom >= lastRefuel_DateTime)
                    {
                       afterRefuel_Hours = afterRefuel_Hours + (decimal)(dateTo - dateFrom).TotalHours;
                    }
                    else if (dateTo >= lastRefuel_DateTime)
                    {
                        afterRefuel_Hours = afterRefuel_Hours + (decimal)(dateTo - lastRefuel_DateTime).TotalHours;
                    }
                   
                    /*
                    // С МОМЕНТА ПОСЛЕДНЕГО ТО
                    if (item.DateFrom >= lastTO_DateTime)
                    {
                        if (item.IsSetTo)
                        {
                            afterTO_Hours = afterTO_Hours + (decimal)(item.DateTo - item.DateFrom).TotalHours;
                        }
                        else
                        {
                            afterTO_Hours = afterTO_Hours + (decimal)(Api.DateTimeUaCurrent - item.DateFrom).TotalHours;
                        }

                    }
                    else if (item.DateTo >= lastTO_DateTime)
                    {
                        afterTO_Hours = afterTO_Hours + (decimal)(item.DateTo - lastTO_DateTime).TotalHours;
                    }
                    */

                }
            }
            return afterRefuel_Hours;
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
                new SenderTelegram() { SendType = SendType.OnlyTest }.Send(message);
            }

        }

        /// <summary>
        /// Класс параметров состояние заправки
        /// </summary>
        public class ParamRefuel
        {
            private static decimal _liter1Horse = (decimal)7.5;

            private static decimal _totalLitersInGenerator = 118;

            /// <summary>
            /// Заправка. Остаток. Сколько литров
            /// </summary>
            public decimal Refuel_Balance_Liters
            {
                get { return Math.Max(0, _totalLitersInGenerator - Refuel_ExecAfter_Liters); }
            }

            /// <summary>
            /// Заправка. Остаток. Сколько литров для протокола
            /// </summary>
            public string Refuel_Balance_LitersStr
            {
                get
                {
                    return Refuel_Balance_Liters > 0 && Refuel_Balance_Liters < 1 ? "1" : ((int)Math.Round(Refuel_Balance_Liters, 0)).ToString();
                }
            }

            /// <summary>
            /// Заправка. Остаток. ПРоцент 
            /// </summary>
            public int Refuel_Balance_Percent
            {
                get { return (int)Math.Round(Refuel_Balance_Liters / _totalLitersInGenerator * (decimal)100.00, 0); }
            }

 
            /// <summary>
            /// Заправка. Остаток. Сколько часов
            /// </summary>
            public decimal Refuel_Balance_Hours
            {
                get { return Math.Round(Refuel_Balance_Liters / _liter1Horse, 2); }

            }


            /// <summary>
            /// Заправка. Остаток. Сколько часов для протокола
            /// </summary>
            public string Refuel_Balance_HoursStr
            {
                get
                {
                    return Api.GetTimeHours(Refuel_Balance_Hours, true);
                }
            }

            /// <summary>
            /// Заправка. Остаток. Осталось ли время
            /// </summary>
            public bool Refuel_Balance_IsEmptyHours
            {
                get { return Refuel_Balance_Hours == 0; }

            }

            

            /// <summary>
            /// Использовано. Сколько литров
            /// </summary>
            public decimal Refuel_ExecAfter_Liters 
            {
                get
                {
                    return _liter1Horse * Refuel_ExecAfter_Hours;
                }
            }

            /// <summary>
            /// Использовано. Сколько литров
            /// </summary>
            public string Refuel_ExecAfter_LitersStr
            {
                get
                {
                    return Refuel_ExecAfter_Liters > 0 && Refuel_ExecAfter_Liters < 1 ? "1" : ((int)Math.Round(Refuel_ExecAfter_Liters, 0)).ToString();
                }
            }

            /// <summary>
            /// Использовано. Сколько часов после поледней заправки
            /// </summary>
            public decimal Refuel_ExecAfter_Hours;

            /// <summary>
            /// Использовано. Сколько часов после поледней заправки ( для протокола)
            /// </summary>
            public string Refuel_ExecAfter_HoursStr
            {
                get
                {
                    return Api.GetTimeHours(Refuel_ExecAfter_Hours, true);
                }
            }

            /// <summary>
            /// Последння заправка. Время 
            /// </summary>
            public DateTime Refuel_Last_DateTime;

            /// <summary>
            /// Последння заправка. Кто заправил
            /// </summary>
            public string Refuel_Last_UserCode;

            /// <summary>
            /// Последння заправка. Кто заправил
            /// </summary>
            public string Refuel_Last_UserName;

            /// <summary>
            /// Последння заправка. Литры
            /// </summary>
            public int Refuel_Last_Liters;


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









