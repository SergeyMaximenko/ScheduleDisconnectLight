using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using static System.Net.Mime.MediaTypeNames;


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

           // Api.DateTimeUaCurrent = new DateTime(2025, 12, 11, 23, 35, 0);

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

            var schedule = IsSourceYasno ? new FormerScheduleFromYasno().Get() : new FormerScheduleFromDTEK().Get();
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

                return;
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
                        if (stateCurrentDay.IsDefine && totalTimeOffPowerNew != totalTimeOffPowerOld)
                        {
                            if (totalTimeOffPowerNew > totalTimeOffPowerOld)
                            {
                                //
                                message.Append("😡 Плюс <b>" + Api.GetNameTimeSpan(totalTimeOffPowerNew - totalTimeOffPowerOld,true) + "</b> відключень\n");
                            }
                            else
                            {
                                //⬇︎😊
                                message.Append("💚 Мінус <b>" + Api.GetNameTimeSpan(totalTimeOffPowerOld - totalTimeOffPowerNew, true) + "</b> відключень\n");
                            }
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
                        if (stateNextDay.IsDefine && totalTimeOffPowerNew != totalTimeOffPowerOld)
                        {
                            if (totalTimeOffPowerNew > totalTimeOffPowerOld)
                            {
                                //
                                message.Append("😡 Плюс <b>" + Api.GetNameTimeSpan(totalTimeOffPowerNew - totalTimeOffPowerOld, true) + "</b> відключень\n");
                            }
                            else
                            {
                                //⬇︎😊
                                message.Append("💚 Мінус <b>" + Api.GetNameTimeSpan(totalTimeOffPowerOld - totalTimeOffPowerNew, true) + "</b> відключень\n");
                            }
                        }
                        message.Append($"📉 <b>{schedule.ScheduleNextDay.GetPercentOffPower()}%</b> часу без світла\n");
                    }
                    message.Append(schedule.ScheduleNextDay.GetPeriodStrForHtmlSchedule(stateNextDay.IsDefine ? stateNextDay.ScheduleOneDay.Times : null) + "\n");
                    message.Append("\n");
                }
                message.Append($"<i>P.S. Оновлено на {(IsSourceYasno ? "Yasno" : "DTEK")} " + schedule.DateLastUpdate.ToString("dd.MM.yyyy HH:mm") + "</i>");

                Console.WriteLine("График збережений:" + string.Join(" ", scheduleHashNew.Select(t => t.GetHashStr())));


                new SenderTelegram().Send(message.ToString());
                Console.WriteLine("Сообщение об изменении графика отправлено");

                // Сохраняем статус 
                state.ScheduleHashDateSet = Api.DateTimeUaCurrent;
                state.SchedulesHash = scheduleHashNew;

                AppState.SaveState(stateFile, state);

            }
            else
            {
   

                Console.WriteLine("График по свету не изменился - 2");
            }


            // Графики не действуют - оповещение не отправляем
            if (schedule.IsEmergencyShutdowns)
            {
                return;
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

                        Console.WriteLine($"  - Напоминание о включении света. Период {interval.GetHashStr()}. Дата выключения: {dateTimeToStr(dateTimePowerOff)}. Текущая дата {dateTimeToStr(Api.DateTimeUaCurrent)}  ");

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
                                    new SenderTelegram().Send($"⚠️🔴 О <b>{Api.TimeToStr(dateTimePowerOff.TimeOfDay)}</b> (через ~<b>" + diff.Minutes.ToString() + "</b> хв) планується відключення світла\n" +
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

                        Console.WriteLine($"  - Напоминание о включении света. Период {interval.GetHashStr()}. Дата включения: {dateTimeToStr(dateTimePowerOn)}. Текущая дата {dateTimeToStr(Api.DateTimeUaCurrent)}  ");


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


                                    new SenderTelegram().Send($"⚠️🟢 В <b>{Api.TimeToStr(dateTimePowerOn.TimeOfDay)}</b> (через ~<b>" + diff.Minutes.ToString() + "</b> хв) очікується відновлення світла\n" +
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


        }


        private static string dateTimeToStr(DateTime dateTime)
        {
            return dateTime.ToString("dd.MM.yyyy HH:mm");
        }








        private static bool isPowerOn()
        {
          

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
                return "На сьогодні відключення більше не заплановані 😊";
            }

            return "Планові відключення до кінця дня:\n" +
                string.Join("\n", periods.Select(t => "🔴 " + t.GetPeriodStrForHtmlNotification()));
        }

        /// <summary>
        /// Получить период в виде HTML
        /// </summary>
        public string GetPeriodStrForHtmlSchedule(List<TimeRange> oldTimes)
        {
            if (Times.Count == 0)
            {
                return "🟢 Відключення не заплановані";
            }

            return string.Join("\n", Times.Select(t => "🔴 " + t.GetPeriodStrForHtmlSchedule(oldTimes)));
        }


        /// <summary>
        /// Получить наименование дати, для отправки в ТГ
        /// </summary>
        public string GetCaptionDate()
        {
            // Дата
            var result = Api.DateToStr(Date);
            // День недели
            result = result + " " + Date.ToString("ddd", new CultureInfo("uk-UA"));

            if (Date == Api.DateUaCurrent)
            {
                result = result + " " + "(сьогодні)";
            }
            else if (Date == Api.DateUaNext.Date)
            {
                result = result + " " + "(завтра)";
            }
            return result;
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
            return startStr + " - " + endStr + "  <i>" + Api.GetNameTimeSpan(End - Start) + "</i>";
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
            return Api.TimeToStr(Start) + " - " + Api.TimeToStr(endTmp) + "  <i>" + Api.GetNameTimeSpan(diff) + "</i>";
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


    public static class Api
    {
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

        public static string DateToStr(DateTime date)
        {
            return date.ToString("dd.MM.yyyy");
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
        public static string GetNameTimeSpan(TimeSpan timeSpan, bool notAddBrackets = false)
        {
            var result = "";
            if (timeSpan.Hours > 0)
            {
                result = result + (!string.IsNullOrEmpty(result) ? " " : "") + $"{timeSpan.Hours} год.";
            }
            if (timeSpan.Minutes > 0)
            {
                result = result + (!string.IsNullOrEmpty(result) ? " " : "") + $"{(timeSpan.Minutes == 29 || timeSpan.Minutes == 31 ? 30 : timeSpan.Minutes)} хв.";
            }
            if (notAddBrackets)
            {
                return !string.IsNullOrEmpty(result) ? result : "";
            }

            return !string.IsNullOrEmpty(result) ? "(" + result + ")" : "";

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
    class AppState
    {

        /// <summary>
        /// Признак, что действуют аварийные отключения
        /// </summary>
        [JsonProperty("IsEmergencyShutdowns")]
        public bool IsEmergencyShutdowns { get; set; }

        /// <summary>
        /// Последнее время изменения файла
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

    public class SenderTelegram
    {

        public void Send(string message, string dd = "")
        {

            string botToken = getBotToken();
            // Тестова група
            //string chatId = "-1002275491172";

            // Основная группа, которая была раньше
            //string chatId = "-1002336792682";

            // Текущая группа
            string chatId = "";
            string chatIdThread = "";

            if (Api.IsGitHub() && string.IsNullOrEmpty(dd))
            {
                chatId = "-1001043114362";
                chatIdThread = "54031";
            }
            else
            {
                chatId = "-1002275491172";
                chatIdThread = "";
            }



            if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
            {
                Console.WriteLine("BOT_TOKEN или CHAT_ID не заданы.");
                return;
            }

            //string text = "11144__-555ёё221122Ping из C# (.NET Framework 4.7.2)";

            using (var httpClient = new HttpClient())
            {
                string url = $"https://api.telegram.org/bot{botToken}/sendMessage";

                var data = new Dictionary<string, string>
                    {
                        { "chat_id", chatId },
                        { "message_thread_id", chatIdThread },
                        { "text", message },
                        { "parse_mode", "HTML"}
                    };

                Console.WriteLine("START SEND TELEGRAM:");
                Console.WriteLine(message);
                Console.WriteLine("END SEND TELEGRAM:");



                using (var content = new FormUrlEncodedContent(data))
                {
                    // Синхронный POST
                    HttpResponseMessage response = httpClient.PostAsync(url, content).Result;

                    // Бросит исключение, если статус не 2xx
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        private string getBotToken()
        {
            // 1. Если работаем в GitHub Actions
            if (Api.IsGitHub())
            {
                string tokenFromGitHub = Environment.GetEnvironmentVariable("BOT_TOKEN");

                if (string.IsNullOrWhiteSpace(tokenFromGitHub))
                {
                    throw new Exception("BOT_TOKEN не найден в GitHub Actions переменных!");
                }

                return tokenFromGitHub;
            }

            // 2. Локальный режим → читаем appsettings.Local.json
            string repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));

            string localPath = Path.Combine(repoRoot, "appsettings.Local.json");

            if (!File.Exists(localPath))
            {
                throw new Exception($"Файл {localPath} не найден!");
            }

            string token = new Json(File.ReadAllText(localPath))["BotToken"].Value;

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new Exception("BotToken не найден в appsettings.Local.json");
            }

            return token;
        }
    }



    /// <summary>
    /// Формирователь графика по DTEK
    /// </summary>
    public class FormerScheduleFromDTEK
    {
        public Schedule Get()
        {
            //string url = "https://github.com/Baskerville42/outage-data-ua/blob/main/data/kyiv.json";
            string url = "https://raw.githubusercontent.com/Baskerville42/outage-data-ua/main/data/kyiv.json";


            string jsonDtekTmp = "";
            using (var httpClient = new HttpClient())
            {
                try
                {
                    // Синхронный GET
                    HttpResponseMessage response = httpClient.GetAsync(url).Result;

                    // Если ошибка, бросим исключение
                    response.EnsureSuccessStatusCode();

                    // Читаем тело ответа тоже синхронно
                    jsonDtekTmp = response.Content.ReadAsStringAsync().Result;

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка: " + ex.Message);
                    jsonDtekTmp = string.Empty;
                }
            }


           // jsonDtekTmp = jsonTmp();


            var jsonDtek = new Json(jsonDtekTmp)["fact"];
            var schedule = new Schedule();
            schedule.DateLastUpdate = jsonDtek["update"].ValueDate;

            var scheduleFromYasno = new FormerScheduleFromYasno().Get();
            schedule.IsEmergencyShutdowns = scheduleFromYasno.IsEmergencyShutdowns;



            // Идем по датам 
            foreach (var itemDates in jsonDtek["data"].GetDictionary())
            {

                var dateSchedule = convertUnixUtcToDateUa(Convert.ToInt32(itemDates.Key)).Date;

                ScheduleDayType? scheduleDayType = null;


                if (dateSchedule == Api.DateUaCurrent)
                {
                    scheduleDayType = ScheduleDayType.CurrentDay;
                }
                else if (dateSchedule == Api.DateUaNext)
                {
                    scheduleDayType = ScheduleDayType.NextDay;
                }

                if (scheduleDayType == null)
                {
                    continue;
                }




                var listTimeRange = new List<Tuple<TimeSpanUser, TimeSpanUser>>();

                // Идем по часам
                foreach (var itemStatus in itemDates.Value["GPV1.1"].GetDictionary())
                {

                    var numberTime = Convert.ToInt32(itemStatus.Key);
                    var status = itemStatus.Value.Value;

                    Tuple<TimeSpanUser, TimeSpanUser> timeRange = null;
                    if (status == "no")
                    {
                        timeRange = Tuple.Create(new TimeSpanUser(numberTime - 1, 0), new TimeSpanUser(numberTime, 0));
                    }
                    if (status == "first")
                    {
                        timeRange = Tuple.Create(new TimeSpanUser(numberTime - 1, 0), new TimeSpanUser(numberTime - 1, 30));
                    }
                    if (status == "second")
                    {
                        timeRange = Tuple.Create(new TimeSpanUser(numberTime - 1, 30), new TimeSpanUser(numberTime, 0));

                    }
                    if (timeRange != null)
                    {
                        listTimeRange.Add(timeRange);
                    }
                }




                if (listTimeRange.Count() == 0)
                {
                    continue;
                }

                var scheduleOneDay = new ScheduleOneDay(dateSchedule);
         

                schedule.SetSchedule(scheduleOneDay, (ScheduleDayType)scheduleDayType);



                Tuple<TimeSpanUser, TimeSpanUser> prevValue = null;
                var listTimeRangeDelete = new List<Tuple<TimeSpanUser, TimeSpanUser>>();

                foreach (var item in listTimeRange)
                {
                    if (prevValue != null && prevValue.Item2.Hours == item.Item1.Hours && prevValue.Item2.Minutes == item.Item1.Minutes)
                    {
                        prevValue.Item2.Hours = item.Item2.Hours;
                        prevValue.Item2.Minutes = item.Item2.Minutes;
                        listTimeRangeDelete.Add(item);

                    }
                    else
                    {
                        prevValue = item;

                    }
                }
                listTimeRange.RemoveAll(t => listTimeRangeDelete.Contains(t));

                foreach (var item in listTimeRange)
                {
                    scheduleOneDay.Times.Add(new TimeRange(new TimeSpan(item.Item1.Hours, item.Item1.Minutes, 0), new TimeSpan(item.Item2.Hours, item.Item2.Minutes, 0)));
                }

            }


            return schedule;
        }


        private DateTime convertUnixUtcToDateUa(int unixUtc)
        {
            DateTime utcDate = DateTimeOffset.FromUnixTimeSeconds(unixUtc).UtcDateTime;
            // 2. Найдём часовой пояс Киева
            TimeZoneInfo kyiv = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");

            // 3. Переведём UTC → Киев
            return TimeZoneInfo.ConvertTimeFromUtc(utcDate, kyiv);
        }

        private class TimeSpanUser
        {
            public int Hours;
            public int Minutes;
            public TimeSpanUser(int hours, int minutes)
            {
                Hours = hours;
                Minutes = minutes;
            }
        }


        private string jsonTmp()
        {
            return @"
{
  ""regionId"": ""kyiv"",
  ""lastUpdated"": ""2025-12-09T12:32:56.288Z"",
  ""fact"": {
    ""data"": {
      ""1765324800"": {
        ""GPV1.1"": {
          ""1"": ""no"",
          ""2"": ""no"",
          ""3"": ""yes"",
          ""4"": ""yes"",
          ""5"": ""no"",
          ""6"": ""yes"",
          ""7"": ""yes"",
          ""8"": ""no"",
          ""9"": ""yes"",
          ""10"": ""no"",
          ""11"": ""yes"",
          ""12"": ""yes"",
          ""13"": ""no"",
          ""14"": ""yes"",
          ""15"": ""yes"",
          ""16"": ""yes"",
          ""17"": ""no"",
          ""18"": ""no"",
          ""19"": ""yes"",
          ""20"": ""yes"",
          ""21"": ""yes"",
          ""22"": ""yes"",
          ""23"": ""yes"",
          ""24"": ""yes""
        }
      },
      ""1765411200"": {
        ""GPV1.1"": {
          ""1"": ""no"",
          ""2"": ""no"",
          ""3"": ""first"",
          ""4"": ""yes"",
          ""5"": ""yes"",
          ""6"": ""yes"",
          ""7"": ""yes"",
          ""8"": ""yes"",
          ""9"": ""yes"",
          ""10"": ""second"",
          ""11"": ""no"",
          ""12"": ""no"",
          ""13"": ""no"",
          ""14"": ""no"",
          ""15"": ""no"",
          ""16"": ""no"",
          ""17"": ""first"",
          ""18"": ""yes"",
          ""19"": ""yes"",
          ""20"": ""yes"",
          ""21"": ""no"",
          ""22"": ""no"",
          ""23"": ""no"",
          ""24"": ""no""
        }
      }
    },
    ""update"": ""09.12.2025 12:09"",
    ""today"": 1765231200
  }
}
";
        }

    }



    /// <summary>
    /// Формирователь графика по Ясно
    /// </summary>
    public class FormerScheduleFromYasno
    {
        public Schedule Get()
        {
            string url = "https://app.yasno.ua/api/blackout-service/public/shutdowns/regions/25/dsos/902/planned-outages";

            string jsonYasnoTmp = "";
            using (var httpClient = new HttpClient())
            {
                try
                {
                    // Синхронный GET
                    HttpResponseMessage response = httpClient.GetAsync(url).Result;

                    // Если ошибка, бросим исключение
                    response.EnsureSuccessStatusCode();

                    // Читаем тело ответа тоже синхронно
                    jsonYasnoTmp = response.Content.ReadAsStringAsync().Result;

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка: " + ex.Message);
                    jsonYasnoTmp = string.Empty;
                }
            }


            // jsonYasnoTmp = jsonTmp();


            var jsonYasno = new Json(jsonYasnoTmp)["1.1"];
            var schedule = new Schedule();
            schedule.DateLastUpdate = getDateUa(jsonYasno["updatedOn"].GetValue<DateTimeOffset>());

            var listDate = new[] { "today", "tomorrow" };


            foreach (var itemDate in listDate)
            {
                if (jsonYasno[itemDate]["status"].Value == "ScheduleApplies")
                {
                    var dateSchedule = getDateUa(jsonYasno[itemDate]["date"].GetValue<DateTimeOffset>()).Date;


                    ScheduleDayType? scheduleDayType = null;

                    if (dateSchedule == Api.DateUaCurrent)
                    {
                        scheduleDayType = ScheduleDayType.CurrentDay;
                    }
                    else if (dateSchedule == Api.DateUaNext)
                    {
                        scheduleDayType = ScheduleDayType.NextDay;
                    }

                    if (scheduleDayType == null)
                    {
                        continue;
                    }


                    var scheduleOneDay = new ScheduleOneDay(dateSchedule);

                    schedule.SetSchedule(scheduleOneDay, (ScheduleDayType)scheduleDayType);





                    foreach (var item in jsonYasno[itemDate]["slots"].GetArray())
                    {
                        if (item["type"].Value != "Definite")
                        {
                            continue;
                        }


                        double valueStart = item["start"].ValueInt / 60.0;
                        int hoursStart = (int)valueStart;                    // 8
                        int minutesStart = (int)((valueStart - hoursStart) * 60); // 0.5 * 60 = 30
                        var timeStart = new TimeSpan(hoursStart, minutesStart, 0);


                        double valueEnd = item["end"].ValueInt / 60.0;
                        int hoursEnd = (int)valueEnd;                    // 8
                        int minutesEnd = (int)((valueEnd - hoursEnd) * 60); // 0.5 * 60 = 30

                        var timeEnd = new TimeSpan(hoursEnd, minutesEnd, 0);

                        scheduleOneDay.Times.Add(new TimeRange(timeStart, timeEnd));
                    }

                }
                if (jsonYasno[itemDate]["status"].Value == "EmergencyShutdowns")
                {
                    var scheduleDate = getDateUa(jsonYasno[itemDate]["date"].GetValue<DateTimeOffset>()).Date;
                    if (scheduleDate == Api.DateUaCurrent)
                    {
                        schedule.IsEmergencyShutdowns = true;
                    }
                }
            }
            return schedule;
        }



        /// <summary>
        /// Получить дату по Киевскому времени
        /// </summary>
        private static DateTime getDateUa(DateTimeOffset date)
        {
            TimeZoneInfo kyiv = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            // Конвертируем "как задумано" в киевский часовой пояс
            return TimeZoneInfo.ConvertTime(date, kyiv).DateTime;
        }


        private string jsonTmp()
        {
            return @"
            {
              ""1.1"": {
                ""today"": {
                  ""slots"": [
                    {
                      ""start"": 480,
                      ""end"": 575,
                      ""type"": ""Definite""
                    },
                    {
                      ""start"": 810,
                      ""end"": 1134,
                      ""type"": ""Definite""
                    },
                    {
                      ""start"": 1150,
                      ""end"": 1440,
                      ""type"": ""Definite""
                    }
                  ],
                  ""date"": ""2025-12-09T00:00:00+02:00"",
                  ""status"": ""ScheduleApplies""
                },
                ""tomorrow"": {
                  ""slots"": [
                    {
                      ""start"": 0,
                      ""end"": 30,
                      ""type"": ""Definite""
                    },
                    {
                      ""start"": 520,
                      ""end"": 750,
                      ""type"": ""Definite""
                    }
                  ],
                  ""date"": ""2025-12-10T00:00:00+02:00"",
                  ""status"": ""ScheduleApplies""
                },
                ""updatedOn"": ""2025-11-18T04:31:02+00:00""
              }
            }
            ";
        }

    }





}