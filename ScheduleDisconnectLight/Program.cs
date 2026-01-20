using Newtonsoft.Json;
using Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;



// КРОН //https://console.cron-job.org/jobs/6880027
//      //https://powergen.onrender.com/hello

namespace ScheduleDisconnectLight
{
    internal class Program
    {

        public static bool IsSourceYasno = false;


        static void Main(string[] args)
        {
            // new SenderTelegram().Send(DateTime.Now.ToString(),"+");


            TimeZoneInfo kyiv = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            Api.DateTimeUaCurrent = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, kyiv);
            
            //Api.DateTimeUaCurrent = new DateTime(2025, 12, 24, 01, 00, 0);


            Schedule schedule = null;


            //--------------------------------
            //   СФОРМИРОВАТЬ ГРАФИК
            //--------------------------------

            try
            {
                Console.WriteLine("✅ Запуск scheduleFormer");
                schedule = scheduleFormer();
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Помилка в scheduleFormer:" + ex.Message);
                Console.WriteLine("❌ Стек помилки:" + ex.StackTrace);

                new SenderTelegram() { SendType = SendType.OnlyTest }.Send("Помилка в scheduleFormer");
            }


            //--------------------------------
            //   ЗАПРАВКА ТОПЛИВА НА ГЕНЕРАТОР
            //--------------------------------
            try
            {
                Console.WriteLine("✅ Запуск GeneratorNotification");
                new GeneratorNotification(schedule).Form();
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Помилка в GeneratorNotification:" + ex.Message);
                Console.WriteLine("❌ Стек помилки:" + ex.StackTrace);
                new SenderTelegram() { SendType = SendType.OnlyTest }.Send("Помилка в GeneratorNotification");

            }
            


        }

        private static Schedule scheduleFormer()
        {

            // Определяем путь к корню репозитория
            string repoRoot = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..")
            );

            string stateFile = "";

            if (Api.IsGitHub())
            {
                Console.WriteLine("Это версия ГИТА");
                stateFile = Path.Combine(repoRoot, "appState.json");
            }
            else
            {
                Console.WriteLine("Это локальная версия");
                stateFile = Path.Combine(repoRoot, "appState-local.json");
            }

  
            // Загружаем состояние
            var state = AppState.LoadState(stateFile);

   
            var schedule = IsSourceYasno
            ? new FormerScheduleFromYasno().Get()
            : new FormerScheduleFromDTEK().Get();

            if (schedule == null)
            {
                new SenderTelegram() { SendType = SendType.OnlyTest }.Send("❌ Графік на сайті ДТЕК пустий. Schedule  = null");
                Console.WriteLine("❌ Не найден не один график на ДТЕК. ");
                schedule = Schedule.FormScheduleByState(state);
            }
            if (schedule.DateLastUpdate < state.ScheduleDateLastUpdate)
            {
                Console.WriteLine("❌ Дата в графике меньше, чем дата в статусе. График взят из статуса");
                schedule = Schedule.FormScheduleByState(state);
            }


            schedule.FillServiceProp();






            //--------------------------------
            //   АВАРИЙНЫЕ ОТКЛЮЧЕНИЯ
            //--------------------------------

            // schedule.IsEmergencyShutdowns = false;



            if (state.IsEmergencyShutdowns != schedule.IsEmergencyShutdowns)
            {

                if (schedule.IsEmergencyShutdowns)
                {
                    new SenderTelegram().Send(
                        "🚨 ЯСНО: У Києві екстрені відключення, графіки не діють");
                    Console.WriteLine("Отправлено сообщение: Аварийные отключения!");
                    // Сохраняем статус 
                    state.IsEmergencyShutdowns = true;
                    AppState.SaveState(stateFile, state);
                }
                else
                {
                    new SenderTelegram().Send(
                        "✅ ЯСНО: Екстрені відключення скасовано, повертаємось до графіків");
                    Console.WriteLine("Отправлено сообщение: Аварийные отключения скасовані!");

                    state.IsEmergencyShutdowns = false;
                    AppState.SaveState(stateFile, state);
                }
            }
            else
            {
                if (schedule.IsEmergencyShutdowns)
                {
                    Console.WriteLine("Сейчас действуют аварийные отключения. Уже было отправлено ранее");
                }

            }

            if (IsSourceYasno && schedule.IsEmergencyShutdowns)
            {
                // Если аварийные отключения и формируем по ясно, графики не отправляем, т.к. на ясно графиков нет

                return null;
            }



            //--------------------------------
            //   С М Е Н А    Г Р А Ф И К А -
            //--------------------------------

   
            Console.WriteLine("График старий:" + state.GetHashStr());

            Console.WriteLine("График новий:" + schedule.GetHashStr());


            var stateCurrentDay = state.GetParamCurrentDay();
            var stateNextDay = state.GetParamNextDay();


            // Графики не менялись, если:
            //  1. Новые график входит в старый 
            //    или
            //  2. В старом графике это даты нет и в новом графике эта дату пустая 
            

            var scheduleCurrentDayIsChange = stateCurrentDay.ScheduleOneDay.GetHashStr() != schedule.ScheduleCurrentDay.GetHashStr();
            var scheduleNextDayIsChange = stateNextDay.ScheduleOneDay.GetHashStr() != schedule.ScheduleNextDay.GetHashStr();
            

            Console.WriteLine("scheduleCurrentDayIsChange:" + scheduleCurrentDayIsChange + " scheduleNextDayIsChange:" + scheduleNextDayIsChange);

            //!stateScheduleHash.Contains(schedule.ScheduleCurrentDay.GetScheduleHash()

            if (scheduleCurrentDayIsChange || scheduleNextDayIsChange)
            {
                var message = new StringBuilder();
                // Отправить сообщение об изменении графика 
                message.Append("⚡️<b>Оновлено графік відключення світла</b>\n");
                message.Append("\n");

                var scheduleHashNew = new List<ScheduleOneDay>();

                // По текущему дню отправляем всегда 
                if (1 == 1)
                {
                    scheduleHashNew.Add(schedule.ScheduleCurrentDay);

                    message.Append($"🗓️ <b>{schedule.ScheduleCurrentDay.GetCaptionDate()}</b>\n");
                    if (!schedule.ScheduleCurrentDay.IsEmpty())
                    {
                        var totalTimeOffPowerNew = schedule.ScheduleCurrentDay.GetTotalTimeOffPower();
                        var totalTimeOffPowerOld = stateCurrentDay.ScheduleOneDay.GetTotalTimeOffPower();
                        if (stateCurrentDay.IsDefine)
                        {
                            if (totalTimeOffPowerNew != totalTimeOffPowerOld)
                            {
                                if (totalTimeOffPowerNew > totalTimeOffPowerOld)
                                {
                                    //
                                    message.Append("😡 Плюс <b>" + Api.GetTimeHours(totalTimeOffPowerNew - totalTimeOffPowerOld, true) + "</b> відключень\n");
                                }
                                else
                                {
                                    //⬇︎😊
                                    message.Append("💚 Мінус <b>" + Api.GetTimeHours(totalTimeOffPowerOld - totalTimeOffPowerNew, true) + "</b> відключень\n");
                                }
                            }
                            else
                            {
                                if (scheduleCurrentDayIsChange)
                                {
                                    message.Append("💛 Графік <b>змінився</b>, але загальний час без змін\n");
                                }
                                else
                                {
                                    message.Append("💛 Графік <b>без змін</b>\n");
                                }
                                
                            }
                        }
                        else
                        {
                            message.Append("🔔 Графік щойно <b>з'явився</b>\n");
                        }
          

                        message.Append($"📉 <b>{schedule.ScheduleCurrentDay.GetPercentOffPower()}%</b> часу без світла\n");
                        



                    }
                    message.Append(schedule.ScheduleCurrentDay.GetPeriodStrForHtmlSchedule(stateCurrentDay.IsDefine ? stateCurrentDay.ScheduleOneDay.Times : null) + "\n");
                    message.Append("\n");
                }
                // По следующему дню отправляем если не пустой или на эту дату уже есть сохраненный график
                if (!schedule.ScheduleNextDay.IsEmpty() || stateNextDay.IsDefine)
                {
                    scheduleHashNew.Add(schedule.ScheduleNextDay);

                    message.Append($"🗓️ <b>{schedule.ScheduleNextDay.GetCaptionDate()}</b>\n");
                    if (!schedule.ScheduleNextDay.IsEmpty())
                    {
                        var totalTimeOffPowerNew = schedule.ScheduleNextDay.GetTotalTimeOffPower();
                        var totalTimeOffPowerOld = stateNextDay.ScheduleOneDay.GetTotalTimeOffPower();
                        if (stateNextDay.IsDefine)
                        {
                            if (totalTimeOffPowerNew != totalTimeOffPowerOld)
                            {
                                if (totalTimeOffPowerNew > totalTimeOffPowerOld)
                                {
                                    //
                                    message.Append("😡 Плюс <b>" + Api.GetTimeHours(totalTimeOffPowerNew - totalTimeOffPowerOld, true) + "</b> відключень\n");
                                }
                                else
                                {
                                    //⬇︎😊
                                    message.Append("💚 Мінус <b>" + Api.GetTimeHours(totalTimeOffPowerOld - totalTimeOffPowerNew, true) + "</b> відключень\n");
                                }
                            }
                            else
                            {
                                if (scheduleNextDayIsChange)
                                {
                                    message.Append("💛 Графік <b>змінився</b>, але загальний час без змін\n");
                                }
                                else
                                {
                                    message.Append("💛 Графік <b>без змін</b>\n");
                                }
                            }
                        }
                        else
                        {
                            message.Append("🔔 Графік щойно <b>з'явився</b>\n");
                        }
                        message.Append($"📉 <b>{schedule.ScheduleNextDay.GetPercentOffPower()}%</b> часу без світла\n");
                    }
                    message.Append(schedule.ScheduleNextDay.GetPeriodStrForHtmlSchedule(stateNextDay.IsDefine ? stateNextDay.ScheduleOneDay.Times : null) + "\n");
                    message.Append("\n");
                }
                message.Append($"<i>P.S. Оновлено на {(IsSourceYasno ? "Yasno" : "DTEK")} " + Api.DateTimeToStr(schedule.DateLastUpdate) + "</i>");

                Console.WriteLine("График збережений:" + string.Join(" ", scheduleHashNew.Select(t => t.GetHashStr())));


                new SenderTelegram().Send(message.ToString());
                Console.WriteLine("Сообщение об изменении графика отправлено");

                // Сохраняем статус 
                state.ScheduleHashDateSet = Api.DateTimeUaCurrent;
                state.ScheduleDateLastUpdate = schedule.DateLastUpdate;
                state.SchedulesHash = scheduleHashNew;

                AppState.SaveState(stateFile, state);

            }
            else
            {
   

                Console.WriteLine("График по свету не изменился");
            }


            // Графики не действуют - оповещение не отправляем
            if (schedule.IsEmergencyShutdowns)
            {
                return schedule;
            }

            // Уведомления отправляем только по текущей дате. Определить, какой из графиков относится к текущей дате


            if (!schedule.ScheduleCurrentDay.IsEmpty())
            {
                Console.WriteLine("Напоминание об отключении света: старт");
                // за сколько минут до события отправлять оповещение в телеграм 
                TimeSpan notifyBefore = TimeSpan.FromMinutes(30);

                var isSendMessageOff = false;


                //----------------------------------------------------------------------
                //   Н А П О М И Н А Н И Е    П Р О    О Т К Л Ю Ч Е Н И Е    С В Е Т А
                //----------------------------------------------------------------------

                // відправити повідомлення з оповіщенням про відключення світла

                if (1 == 1)
                {

                    foreach (var interval in schedule.ScheduleCurrentDay.Times)
                    {


                        var dateTimePowerOff = schedule.ScheduleCurrentDay.Date + interval.Start;

                        Console.WriteLine($"  - Напоминание о включении света. Период {interval.GetHashStr()}. Дата выключения: {Api.DateTimeToStr(dateTimePowerOff)}. Текущая дата {Api.DateTimeToStr(Api.DateTimeUaCurrent)}  ");

                        if (dateTimePowerOff == state.DateTimePowerOffLastMessage)
                        {
                            Console.WriteLine($"      => уже сообщение было отправлено ранее");
                            continue;
                        }

                        // если сейчас ещё не началось отключение
                        if (Api.DateTimeUaCurrent < dateTimePowerOff)
                        {
                            // время до начала отключения
                            TimeSpan diff = dateTimePowerOff - Api.DateTimeUaCurrent;


                            // если осталось <= 30 минут
                            if (diff <= notifyBefore)
                            {

                                if (isPowerOn())
                                {
                                    
                                    isSendMessageOff = true;
                                    new SenderTelegram().Send($"⚠️🔴 О <b>{Api.TimeToStr(dateTimePowerOff.TimeOfDay)}</b> (через ~<b>" + (diff.Minutes+1).ToString() + "</b> хв) планується відключення світла\n" +
                                        "\n" +
                                        schedule.ScheduleCurrentDay.GetPeriodStrForHtmlNotification(Api.DateTimeUaCurrent.TimeOfDay));

                                    state.DateTimePowerOffLastMessage = dateTimePowerOff;
                                    AppState.SaveState(stateFile, state);
                                    Console.WriteLine($"      => сообщение отправлено");
                                }
                                else
                                {
                                    Console.WriteLine($"      => в EXCEL указано, что СВЕТА и так НЕТ");
                                }

                            }
                            else
                            {
                                Console.WriteLine($"      => еще не достигнуто 30 мин.");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"      => текущая дата больше даты выключения");
                        }
                    }
                }
                //----------------------------------------------------------------------
                //   Н А П О М И Н А Н И Е    П Р О    В К Л Ю Ч Е Н И Е    С В Е Т А
                //----------------------------------------------------------------------
                // відправити повідомлення з оповіщенням про включення світла
                if (!isSendMessageOff)
                {
                    Console.WriteLine("Напоминание о включении света: старт");

                    foreach (var interval in schedule.ScheduleCurrentDay.Times)
                    {

                        var dateTimePowerOn = interval.EndNextDay != TimeSpan.Zero
                            ? schedule.ScheduleCurrentDay.Date.AddDays(1) + interval.EndNextDay
                            : schedule.ScheduleCurrentDay.Date + interval.End;

                        Console.WriteLine($"  - Напоминание о включении света. Период {interval.GetHashStr()}. Дата включения: {Api.DateTimeToStr(dateTimePowerOn)}. Текущая дата {Api.DateTimeToStr(Api.DateTimeUaCurrent)}  ");


                        if (dateTimePowerOn == state.DateTimePowerOnLastMessage)
                        {
                            Console.WriteLine($"      => уже сообщение было отправлено ранее");
                            continue;
                        }

                        // если сейчас ещё не началось отключение
                        if (Api.DateTimeUaCurrent < dateTimePowerOn)
                        {
                            // время до начала отключения
                            TimeSpan diff = dateTimePowerOn - Api.DateTimeUaCurrent;

                            // если осталось <= 30 минут
                            if (diff <= notifyBefore)
                            {
                                if (!isPowerOn())
                                {
                                    
                                 
                                    // Признак, что текущий день закончен. В этом случае не нужно писать, что на сегодня отключения больше не запланированы 
                                    var isDayOff = dateTimePowerOn >= new DateTime(Api.DateTimeUaCurrent.Year, Api.DateTimeUaCurrent.Month, Api.DateTimeUaCurrent.Day, 23, 59, 0);


                                    new SenderTelegram().Send($"⚠️🟢 В <b>{Api.TimeToStr(dateTimePowerOn.TimeOfDay)}</b> (через ~<b>" + (diff.Minutes + 1).ToString() + "</b> хв) очікується відновлення світла\n" +
                                        "\n"+ 
                                        (isDayOff
                                            ? ""
                                            : schedule.ScheduleCurrentDay.GetPeriodStrForHtmlNotification(Api.DateTimeUaCurrent.TimeOfDay)
                                         )
                                        );

                                    state.DateTimePowerOnLastMessage = dateTimePowerOn;
                                    AppState.SaveState(stateFile, state);
                                    Console.WriteLine($"      => сообщение отправлено");
                                }
                                else
                                {
                                    Console.WriteLine($"      => в EXCEL указано, что СВЕТ есть");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"      => еще не достигнуто 30 мин.");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"      => текущая дата больше даты включения");
                        }
                    }
                }
            }
            return schedule;


        }










        private static bool isPowerOn()
        {

            var service = new SpreadSheet().GetService();
            return SpreadSheet.GetValue<int>(service, SpreadSheet.SheetNameOnOffStatus, 1, 2) == 1;

            /*
            string url = "https://script.google.com/macros/s/AKfycbzQMlzERj-TDWq6SYEG69Th0KW1u07CuHOx-SJNgVoyWn6J_OSV1YI8dMBm4FkCNfiIfQ/exec";

            string result = "";
            using (var httpClient = new HttpClient())
            {
                try
                {
                    // Синхронный GET
                    HttpResponseMessage response = httpClient.GetAsync(url).Result;

                    // Если ошибка, бросим исключение
                    response.EnsureSuccessStatusCode();

                    // Читаем тело ответа тоже синхронно
                    result = response.Content.ReadAsStringAsync().Result;

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка: " + ex.Message);
                    result = "+";
                }
            }

            return result == "+";
            */
        }

    }



    public enum ScheduleDayType
    {
        CurrentDay,
        NextDay
    }


    /// <summary>
    /// График выключения света
    /// </summary>
    public class Schedule
    {
        public bool IsEmergencyShutdowns;

        /// <summary>
        /// Дата последнего обновления
        /// </summary>
        public DateTime DateLastUpdate;

        /// <summary>
        /// График для 1-й дати
        /// </summary>
        public ScheduleOneDay ScheduleCurrentDay;

        /// <summary>
        /// График для 2-й дати
        /// </summary>
        public ScheduleOneDay ScheduleNextDay;


      


        public void SetSchedule(ScheduleOneDay scheduleOneDay, ScheduleDayType scheduleDayType)
        {
            if (scheduleDayType == ScheduleDayType.CurrentDay)
            {
                ScheduleCurrentDay = scheduleOneDay;
            }
            else
            {
                ScheduleNextDay = scheduleOneDay;
            }
        }

        public string GetHashStr()
        {
            return ScheduleCurrentDay.GetHashStr() + " " + ScheduleNextDay.GetHashStr();
        }

        
        public static Schedule FormScheduleByState(AppState state)
        {
            var shedule = new Schedule();
            shedule.ScheduleCurrentDay = state.GetParamCurrentDay().ScheduleOneDay;
            shedule.ScheduleNextDay = state.GetParamNextDay().ScheduleOneDay;
            shedule.IsEmergencyShutdowns = state.IsEmergencyShutdowns;
            shedule.DateLastUpdate = state.ScheduleDateLastUpdate;
            return shedule;
        }



        /// <summary>
        /// Заполнить сервисные свойства 
        /// </summary>
        public void FillServiceProp()
        {
            if (ScheduleCurrentDay == null)
            {
                ScheduleCurrentDay = new ScheduleOneDay(Api.DateUaCurrent);
            }
            if (ScheduleNextDay == null)
            {
                ScheduleNextDay = new ScheduleOneDay(Api.DateUaNext);
            }

            if (ScheduleCurrentDay != null &&
                ScheduleNextDay != null &&
                ScheduleCurrentDay.Times.Count() > 0 &&
                ScheduleNextDay.Times.Count() > 0 &&
                ScheduleCurrentDay.Date.AddDays(1) == ScheduleNextDay.Date &&
                ScheduleCurrentDay.Times.Last().EndIsEndDay() &&
                ScheduleNextDay.Times.First().StartIsStartDay())
            {
                ScheduleCurrentDay.Times.Last().SetEndNextDay(ScheduleNextDay.Times.First().End);
            }
        }
    }






    /// <summary>
    /// График выключения света для одного для 
    /// </summary>
    public class ScheduleOneDay
    {
        //************ СВОЙСТВА, КОТОРЫЕ ХРАНЯТЬСЯ В JSON **********************
        /// <summary>
        /// Дата выключения света (без времени) 
        /// </summary>
        [JsonProperty("Date")]
        public DateTime Date { get; set; }

        /// <summary>
        /// Периоды времени
        /// </summary>
        [JsonProperty("Times")]
        public List<TimeRange> Times { get; set; }

        //************************************************************************

        /// <summary>
        /// График пустой
        /// </summary>
        public bool IsEmpty()
        {
            return Times.Count() == 0;
        }

        /// <summary>
        /// Получить процент времени, сколько выключен свет
        /// </summary>
        public int GetPercentOffPower()
        {
            return (int)Math.Round(Times.Select(t => (t.End - t.Start).TotalMinutes).Sum() * 100.0 / (60.0 * 24.0), 0);
        }

        /// <summary>
        /// Получить процент времени, сколько выключен свет
        /// </summary>
        public TimeSpan GetTotalTimeOffPower()
        {
            var totalTimeList = Times.Select(t => t.End - t.Start);
            TimeSpan timeSpanAll = new TimeSpan();
            foreach (var time in totalTimeList)
            {
                timeSpanAll = timeSpanAll + time;
            }
            return timeSpanAll;
        }

        /// <summary>
        /// Получить период в виде HTML
        /// </summary>
        public string GetPeriodStrForHtmlNotification(TimeSpan timeStartNext )
        {
            var periods = Times.Where(t => t.Start > timeStartNext);
            if (periods.Count() == 0)
            {
                return "🟡 На сьогодні інформація про відключення відсутня";
            }

            return "Планові відключення до кінця дня:\n" +
                string.Join("\n", periods.Select(t => "🔴 " + t.GetPeriodStrForHtmlNotification()));
        }

        /// <summary>
        /// Получить период в виде HTML для статуса генератора
        /// </summary>
        public string GetPeriodStrForHtmlStatusGen()
        {
            
            if (Date == Api.DateUaCurrent)
            {
                var periods = Times.Where(t => t.End >= Api.DateTimeUaCurrent.TimeOfDay);
                if (periods.Count() == 0)
                {
                    return "🟡 На сьогодні інформація про відключення відсутня";
                }
                return string.Join("\n", periods.Select(t => "🔴 " + t.GetPeriodStrForHtmlSchedule(null)));
            }
            
            return string.Join("\n", Times.Select(t => "🔴 " + t.GetPeriodStrForHtmlSchedule(null)));
            
        }

        /// <summary>
        /// Получить период в виде HTML
        /// </summary>
        public string GetPeriodStrForHtmlSchedule(List<TimeRange> oldTimes)
        {
            if (Times.Count == 0)
            {
                return "🟡 Інформація про відключення відсутня";
            }

            return string.Join("\n", Times.Select(t => "🔴 " + t.GetPeriodStrForHtmlSchedule(oldTimes)));
        }




        /// <summary>
        /// Получить наименование дати, для отправки в ТГ
        /// </summary>
        public string GetCaptionDate()
        {
            return Api.GetCaptionDate(Date);
        }

        /// <summary>
        /// Получить ХЕШ
        /// </summary>
        public string GetHashStr()
        {
            return "[" + Api.DateToStr(Date) + " " + string.Join(" => ", Times.Select(t => t.GetHashStr())) + "]";
        }


        public ScheduleOneDay()
        {
            Times = new List<TimeRange>();
        }
        public ScheduleOneDay(DateTime date) :this()
        {
            Date = date;
        }
    }






    /// <summary>
    /// Описание периода времени
    /// </summary>
    public class TimeRange 
    {
        //************ СВОЙСТВА, КОТОРЫЕ ХРАНЯТЬСЯ В JSON **********************

        /// <summary>
        /// Время старта
        /// </summary>
        [JsonProperty("From")]
        private string _startStr;

        /// <summary>
        /// Время окончания
        /// </summary>
        [JsonProperty("To")]
        private string _endStr;

        //************************************************************************


        /// <summary>
        /// Время старта
        /// </summary>
        [JsonIgnore]
        public TimeSpan Start
        {
            get { return Api.StrToTime(_startStr); }
            private set
            {
                _startStr = Api.TimeToStr(value);
            }
        }

        /// <summary>
        /// Время окончания
        /// </summary>
        [JsonIgnore]
        public TimeSpan End
        {
            get { return Api.StrToTime(_endStr); }
            private set
            {
                _endStr = Api.TimeToStr(value);
            }
        }

        /// <summary>
        /// Период окончания графика на следующий день, если End = 24.00
        /// </summary>
        [JsonIgnore]
        public TimeSpan EndNextDay { get; private set; }


        /// <summary>
        /// Признак, что это конец дня
        /// </summary>
        public bool EndIsEndDay()
        {
            return End.Days == 1 && End.Hours == 0 && End.Minutes == 0;
        }

        /// <summary>
        /// Признак, что это начало дня
        /// </summary>
        public bool StartIsStartDay()
        {
            return Start.Days == 0 && Start.Hours == 0 && Start.Minutes == 0;
        }

        /// <summary>
        /// Получить строковый ХЕШ
        /// </summary>
        public string GetHashStr()
        {
            return _startStr + " - " + _endStr;
        }

        /// <summary>
        /// Получить период для формирования HTML графика
        /// </summary>
        public string GetPeriodStrForHtmlSchedule(List<TimeRange> oldTimes)
        {

            var startStr = Api.TimeToStr(Start);
            var endStr = Api.TimeToStr(End);

            if (oldTimes != null && !oldTimes.Any(t=>t._startStr ==_startStr))
            {
                startStr = "<u>" + startStr + "</u>";
            }
            if (oldTimes != null && !oldTimes.Any(t => t._endStr == _endStr))
            {
                endStr = "<u>" + endStr + "</u>";
            }
            return startStr + " - " + endStr + "  <i>" + Api.GetTimeHours(End - Start) + "</i>";
        }

        /// <summary>
        /// Получить период для оповещение
        /// </summary>
        public string GetPeriodStrForHtmlNotification()
        {
            TimeSpan endTmp;
            var diff = (End - Start);
            if (EndNextDay != TimeSpan.Zero)
            {
                endTmp = EndNextDay;
                diff = diff + EndNextDay;
            }
            else
            {
                endTmp = End;
            }
            return Api.TimeToStr(Start) + " - " + Api.TimeToStr(endTmp) + "  <i>" + Api.GetTimeHours(diff) + "</i>";
        }



        /// <summary>
        /// Сервисный метод
        /// </summary>
        public void SetEndNextDay(TimeSpan endNextDay)
        {
            EndNextDay = endNextDay;
        }


        public TimeRange(TimeSpan start, TimeSpan end)
        {
            Start = start;
            End = end;
        }
    }

    
 

    public class StateScheduleOneDay
    {
        public ScheduleOneDay ScheduleOneDay { get; private set; }

        public bool IsDefine { get; set; }

        public StateScheduleOneDay(DateTime date)
        {
            ScheduleOneDay = new ScheduleOneDay(date);
            IsDefine = false;
        }
        public StateScheduleOneDay(ScheduleOneDay scheduleOneDay)
        {
            ScheduleOneDay = scheduleOneDay;
            IsDefine = true;
        }
    }


    /// <summary>
    /// Класс мини база данных 
    /// </summary>
    public class AppState
    {

        /// <summary>
        /// Признак, что действуют аварийные отключения
        /// </summary>
        [JsonProperty("IsEmergencyShutdowns")]
        public bool IsEmergencyShutdowns { get; set; }


        /// <summary>
        /// Последнее время изменения графика на ДТЕК
        /// </summary>
        [JsonProperty("ScheduleDateLastUpdate")]
        public DateTime ScheduleDateLastUpdate { get; set; }

        /// <summary>
        /// Последнее время установки графика 
        /// </summary>
        [JsonProperty("ScheduleHashDateSet")]
        public DateTime ScheduleHashDateSet { get; set; }

        [JsonProperty("SchedulesHash")]
        public List<ScheduleOneDay> SchedulesHash;


        /// <summary>
        /// Получить время для Хеша
        /// </summary>
        public string GetHashStr()
        {
            return string.Join(" ", SchedulesHash.Select(t => t.GetHashStr()));
        }


        //public string ScheduleHash { get; set; }


        /// <summary>
        /// Время выключения света, по которому уже было отправлено напоминание
        /// </summary>
        [JsonProperty]
        public DateTime DateTimePowerOffLastMessage { get; set; }

        /// <summary>
        /// Время включения света, по которому уже было отправлено напоминание
        /// </summary>
        [JsonProperty]
        public DateTime DateTimePowerOnLastMessage { get; set; }

       

        public StateScheduleOneDay GetParamCurrentDay()
        {
            var scheduleOneDay = SchedulesHash.FirstOrDefault(t => t.Date == Api.DateUaCurrent);
            if (scheduleOneDay == null)
            {
                return new StateScheduleOneDay(Api.DateUaCurrent);
            }
            return new StateScheduleOneDay(scheduleOneDay);
        }

        public StateScheduleOneDay GetParamNextDay()
        {
            var scheduleOneDay = SchedulesHash.FirstOrDefault(t => t.Date == Api.DateUaNext);
            if (scheduleOneDay == null)
            {
                return new StateScheduleOneDay(Api.DateUaNext);
            }
            return new StateScheduleOneDay(scheduleOneDay);
        }

        public AppState()
        {
            SchedulesHash = new List<ScheduleOneDay>();
        }


        // Считывание state.json
        public static AppState LoadState(string path)
        {
            if (!File.Exists(path))
            {
                return new AppState();
            }

            string json = File.ReadAllText(path);
            var result = JsonConvert.DeserializeObject<AppState>(json);
            if (result == null)
            {
                return new AppState();
            }
            return result;
        }

        // Сохранение state.json
        public static void SaveState(string path, AppState state)
        {
            string json = JsonConvert.SerializeObject(state, Formatting.Indented);
            File.WriteAllText(path, json);
        }


    }

   
    





}