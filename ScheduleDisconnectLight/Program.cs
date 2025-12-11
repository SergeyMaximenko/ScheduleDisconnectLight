using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
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

        public static bool IsGitHub()
        {
            return Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
        }

        public static DateTime DateTimeUaCurrent { get; set; }

        static void Main(string[] args)
        {
            // new SenderTelegram().Send(DateTime.Now.ToString(),"+");


            TimeZoneInfo kyiv = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            DateTimeUaCurrent = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, kyiv);

            //DateTimeUaCurrent = new DateTime(2025, 12, 10, 0, 5, 0);

            // Определяем путь к корню репозитория
            string repoRoot = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..")
            );

            string stateFile = "";

            if (IsGitHub())
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
                    state.EmergencyShutdowns = "+";
                    AppState.SaveState(stateFile, state);
                }
                else
                {
                    new SenderTelegram().Send(
                        "✅ ЯСНО: Екстрені відключення скасовано, повертаємось до графіків");
                    Console.WriteLine("Отправлено сообщение: Аварийные отключения скасовані!");

                    state.EmergencyShutdowns = "";
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

            Console.WriteLine("График старий:" + state.ScheduleHash);

            Console.WriteLine("График новий:" + schedule.ScheduleCurrentDay.GetScheduleHash()+ schedule.ScheduleNextDay.GetScheduleHash());


            // Графики не менялись, если:
            //  1. Новые график входит в старый 
            //    или
            //  2. В старом графике это даты нет и в новом графике эта дату пустая 
            var scheduleCurrentDayIsChange =
                !(
                state.ScheduleHash.Contains(schedule.ScheduleCurrentDay.GetScheduleHash()) ||
                !state.FindCurrentDayInScheduleHash() && schedule.ScheduleCurrentDay.IsEmpty()
                );


            var scheduleNextDayIsChange =
                !(
                state.ScheduleHash.Contains(schedule.ScheduleNextDay.GetScheduleHash()) ||
                !state.FindNextDayInScheduleHash() && schedule.ScheduleNextDay.IsEmpty()
                 );


            Console.WriteLine("scheduleCurrentDayIsChange:" + scheduleCurrentDayIsChange + " scheduleNextDayIsChange:" + scheduleNextDayIsChange);

            //!stateScheduleHash.Contains(schedule.ScheduleCurrentDay.GetScheduleHash()

            if (scheduleCurrentDayIsChange || scheduleNextDayIsChange)
            {
                var message = new StringBuilder();
                // Отправить сообщение об изменении графика 
                message.Append("⚡️<b>Оновлено графік відключення світла</b>\n");
                message.Append("\n");

                var scheduleHashNew = "";

                // По текущему дню отправляем всегда 
                if (1 == 1)
                {
                    scheduleHashNew = scheduleHashNew + schedule.ScheduleCurrentDay.GetScheduleHash();

                    message.Append($"🗓️ <b>{schedule.ScheduleCurrentDay.GetCaptionDate()}</b>\n");
                    if (!schedule.ScheduleCurrentDay.IsEmpty())
                    {
                        message.Append($"📉 <b>{schedule.ScheduleCurrentDay.GetPercentOffPower()}%</b> часу без світла\n");
                    }
                    message.Append(schedule.ScheduleCurrentDay.GetPeriodStrForHtmlSchedule(state.GetTimeStrCurrentDayInScheduleHash()) + "\n");
                    message.Append("\n");
                }
                // По следующему дню отправляем если не пустой или на эту дату уже есть сохраненный график
                if (!schedule.ScheduleNextDay.IsEmpty() || state.FindNextDayInScheduleHash())
                {
                    scheduleHashNew = scheduleHashNew + schedule.ScheduleNextDay.GetScheduleHash();

                    message.Append($"🗓️ <b>{schedule.ScheduleNextDay.GetCaptionDate()}</b>\n");
                    if (!schedule.ScheduleNextDay.IsEmpty())
                    {
                        message.Append($"📉 <b>{schedule.ScheduleNextDay.GetPercentOffPower()}%</b> часу без світла\n");
                    }
                    message.Append(schedule.ScheduleNextDay.GetPeriodStrForHtmlSchedule(state.GetTimeStrNextDayInScheduleHash()) + "\n");
                    message.Append("\n");
                }
                message.Append($"<i>P.S. Оновлено на {(IsSourceYasno ? "Yasno" : "DTEK")} " + schedule.DateLastUpdate.ToString("dd.MM.yyyy HH:mm") + "</i>");

                Console.WriteLine("График збережений:" + scheduleHashNew);


                new SenderTelegram().Send(message.ToString());
                Console.WriteLine("Сообщение об изменении графика отправлено");

                // Сохраняем статус 
                state.ScheduleHashDateSet = DateTimeUaCurrent;
                state.ScheduleHash = scheduleHashNew;

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

                    foreach (var interval in schedule.ScheduleCurrentDay.Periods)
                    {


                        var dateTimePowerOff = schedule.ScheduleCurrentDay.Date + interval.Start;

                        Console.WriteLine($"  - Напоминание о включении света. Период {interval.GetPeriodStrForHash()}. Дата выключения: {dateTimeToStr(dateTimePowerOff)}. Текущая дата {dateTimeToStr(DateTimeUaCurrent)}  ");

                        if (dateTimePowerOff == state.DateTimePowerOffLastMessage)
                        {
                            Console.WriteLine($"      => уже сообщение было отправлено ранее");
                            continue;
                        }

                        // если сейчас ещё не началось отключение
                        if (DateTimeUaCurrent < dateTimePowerOff)
                        {
                            // время до начала отключения
                            TimeSpan diff = dateTimePowerOff - DateTimeUaCurrent;


                            // если осталось <= 30 минут
                            if (diff <= notifyBefore)
                            {

                                if (isPowerOn())
                                {
                                    
                                    isSendMessageOff = true;
                                    new SenderTelegram().Send($"⚠️🔴 О <b>{TimeRange.ConvertTimeToStr(dateTimePowerOff.TimeOfDay)}</b> (через ~<b>" + diff.Minutes.ToString() + "</b> хв) планується відключення світла\n" +
                                        "\n" +
                                        schedule.ScheduleCurrentDay.GetPeriodStrForHtmlNotification(DateTimeUaCurrent.TimeOfDay));

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

                    foreach (var interval in schedule.ScheduleCurrentDay.Periods)
                    {

                        var dateTimePowerOn = interval.EndNextDay != TimeSpan.Zero
                            ? schedule.ScheduleCurrentDay.Date.AddDays(1) + interval.EndNextDay
                            : schedule.ScheduleCurrentDay.Date + interval.End;

                        Console.WriteLine($"  - Напоминание о включении света. Период {interval.GetPeriodStrForHash()}. Дата включения: {dateTimeToStr(dateTimePowerOn)}. Текущая дата {dateTimeToStr(DateTimeUaCurrent)}  ");


                        if (dateTimePowerOn == state.DateTimePowerOnLastMessage)
                        {
                            Console.WriteLine($"      => уже сообщение было отправлено ранее");
                            continue;
                        }

                        // если сейчас ещё не началось отключение
                        if (DateTimeUaCurrent < dateTimePowerOn)
                        {
                            // время до начала отключения
                            TimeSpan diff = dateTimePowerOn - DateTimeUaCurrent;

                            // если осталось <= 30 минут
                            if (diff <= notifyBefore)
                            {
                                if (!isPowerOn())
                                {
                                    
                                 
                                    // Признак, что текущий день закончен. В этом случае не нужно писать, что на сегодня отключения больше не запланированы 
                                    var isDayOff = dateTimePowerOn >= new DateTime(DateTimeUaCurrent.Year, DateTimeUaCurrent.Month, DateTimeUaCurrent.Day, 23, 59, 0);


                                    new SenderTelegram().Send($"⚠️🟢 В <b>{TimeRange.ConvertTimeToStr(dateTimePowerOn.TimeOfDay)}</b> (через ~<b>" + diff.Minutes.ToString() + "</b> хв) очікується відновлення світла\n" +
                                        "\n"+ 
                                        (isDayOff
                                            ? ""
                                            : schedule.ScheduleCurrentDay.GetPeriodStrForHtmlNotification(DateTimeUaCurrent.TimeOfDay)
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





        /// <summary>
        /// Заполнить сервисные свойства 
        /// </summary>
        public void FillServiceProp()
        {
            if (ScheduleCurrentDay == null)
            {
                ScheduleCurrentDay = new ScheduleOneDay() { Date = Program.DateTimeUaCurrent.Date };
            }
            if (ScheduleNextDay == null)
            {
                ScheduleNextDay = new ScheduleOneDay() { Date = Program.DateTimeUaCurrent.Date.AddDays(1) };
            }

            if (ScheduleCurrentDay != null &&
                ScheduleNextDay != null &&
                ScheduleCurrentDay.Periods.Count() > 0 &&
                ScheduleNextDay.Periods.Count() > 0 &&
                ScheduleCurrentDay.Date.AddDays(1) == ScheduleNextDay.Date &&
                ScheduleCurrentDay.Periods.Last().EndIsEndDay() &&
                ScheduleNextDay.Periods.First().StartIsStartDay())
            {
                ScheduleCurrentDay.Periods.Last().SetEndNextDay(ScheduleNextDay.Periods.First().End);
            }
        }
    }






    /// <summary>
    /// График выключения света для одного для 
    /// </summary>
    public class ScheduleOneDay
    {
        /// <summary>
        /// Дата выключения света (без времени) 
        /// </summary>
        public DateTime Date;
        public List<TimeRange> Periods;


        public bool IsEmpty()
        {
            return Periods.Count() == 0;
        }

        public static string DateToStr(DateTime date)
        {
            return date.ToString("dd.MM.yyyy");
        }


        /// <summary>
        /// Получить процент времени, сколько выключен свет
        /// </summary>
        /// <returns></returns>
        public int GetPercentOffPower()
        {
            return (int)Math.Round(Periods.Select(t => (t.End - t.Start).TotalMinutes).Sum() * 100.0 / (60.0 * 24.0), 0);
        }

        /// <summary>
        /// Получить период в виде HTML
        /// </summary>
        public string GetPeriodStrForHtmlNotification(TimeSpan timeStartNext )
        {
            var periods = Periods.Where(t => t.Start > timeStartNext);
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
        public string GetPeriodStrForHtmlSchedule(string oldPeriod)
        {
            if (Periods.Count == 0)
            {
                return "🟢 Відключення не заплановані";
            }

            return string.Join("\n", Periods.Select(t => "🔴 " + t.GetPeriodStrForHtmlSchedule(oldPeriod)));
        }


        /// <summary>
        /// Получить наименование дати, для отправки в ТГ
        /// </summary>
        public string GetCaptionDate()
        {
            // Дата
            var result = DateToStr(Date);
            // День недели
            result = result + " " + Date.ToString("ddd", new CultureInfo("uk-UA"));

            if (Date == Program.DateTimeUaCurrent.Date)
            {
                result = result + " " + "(сьогодні)";
            }
            else if (Date == Program.DateTimeUaCurrent.Date.AddDays(1))
            {
                result = result + " " + "(завтра)";
            }
            else if (Date == Program.DateTimeUaCurrent.Date.AddDays(-1))
            {
                result = result + " " + "(вчора)";
            }
            return result;
        }



        /// <summary>
        /// Получить время для Хеша
        /// </summary>
        public string GetScheduleHash()
        {
            return "[" + DateToStr(Date) + " " + string.Join(" => ", Periods.Select(t => t.GetPeriodStrForHash())) + "] ";
        }

        public ScheduleOneDay()
        {
            Periods = new List<TimeRange>();
        }
    }





    public class CoderScheduleHashParam
    {

        public DateTime Date;
        public string Periods;
    }


  
    /// <summary>
    /// Описание периода времени
    /// </summary>
    public class TimeRange
    {
        public TimeSpan Start { get; private set; }
        public TimeSpan End { get; private set; }

        public bool EndIsEndDay()
        {
            return End.Days == 1 && End.Hours == 0 && End.Minutes == 0;
        }
        public bool StartIsStartDay()
        {
            return Start.Days == 0 && Start.Hours == 0 && Start.Minutes == 0;
        }


        // Период окончания графика на следующий день, если End = 24.00
        public TimeSpan EndNextDay { get; private set; }

        public TimeRange(TimeSpan start, TimeSpan end)
        {


            Start = start;
            End = end;
        }

        public string GetPeriodStrForHash()
        {
            return ConvertTimeToStr(Start) + " - " + ConvertTimeToStr(End);
        }

        public string GetPeriodStrForHtmlSchedule(string oldPeriod)
        {

            var startStr = ConvertTimeToStr(Start);
            var endStr = ConvertTimeToStr(End);

            if (!string.IsNullOrEmpty(oldPeriod) && !oldPeriod.Contains(startStr + " -"))
            {
                //startStr = "<u>" + startStr + "</u>";
            }
            if (!string.IsNullOrEmpty(oldPeriod) && !oldPeriod.Contains("- " + endStr))
            {
                //endStr = "<u>" + endStr + "</u>";
            }
            return startStr + " - " + endStr + "  <i>" + getNameTimeSpan(End - Start) + "</i>";
        }

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
            return ConvertTimeToStr(Start) + " - " + ConvertTimeToStr(endTmp) + "  <i>" + getNameTimeSpan(diff) + "</i>";
        }

        private string getNameTimeSpan(TimeSpan timeSpan)
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
            return !string.IsNullOrEmpty(result) ? "(" + result + ")" : "";

        }


        /// <summary>
        /// Конвертировать время в строку
        /// </summary>
        public static string ConvertTimeToStr(TimeSpan time)
        {
            return time.Days == 1 && time.Hours == 0 && time.Minutes == 0 ? "24:00" : time.Hours.ToString("D2") + ":" + time.Minutes.ToString("D2");
        }

        /// <summary>
        /// Сервисный метод
        /// </summary>
        public void SetEndNextDay(TimeSpan endNextDay)
        {
            EndNextDay = endNextDay;
        }

    }






    /// <summary>
    /// Класс мини база данных 
    /// </summary>
    class AppState
    {
        public string EmergencyShutdowns { get; set; }

        [JsonIgnore]
        public bool IsEmergencyShutdowns { get { return EmergencyShutdowns == "+"; } }

        /// <summary>
        /// Последнее время изменения файла
        /// </summary>
        public DateTime ScheduleHashDateSet { get; set; }

        /// <summary>
        /// Условий Хер код
        /// </summary>
        public string ScheduleHash { get; set; }

        /// <summary>
        /// Время выключения света, по которому уже было отправлено напоминание
        /// </summary>
        public DateTime DateTimePowerOffLastMessage { get; set; }

        /// <summary>
        /// Время включения света, по которому уже было отправлено напоминание
        /// </summary>
        public DateTime DateTimePowerOnLastMessage { get; set; }

        private  Dictionary<string, string> parseScheduleHash()
        {
            var result = new Dictionary<string, string>();

            // Разделяем блоки по "][" и убираем скобки
            var blocks = ScheduleHash.Split(new[] { "] [" }, StringSplitOptions.None);

            foreach (var block in blocks)
            {
                var clean = block.Replace("[", "").Replace("]", "");

                // clean = "09.12.2025 07:00 - 12:00 => 15:30 - 22:00"

                // Разделяем по первому пробелу: слева дата, справа время
                int firstSpace = clean.IndexOf(' ');
                if (firstSpace < 0) continue;

                string date = clean.Substring(0, firstSpace);
                string times = clean.Substring(firstSpace + 1);

                result[date] = times.Trim();
            }

            return result;
        }

        public string GetTimeStrCurrentDayInScheduleHash()
        {
            if (parseScheduleHash().TryGetValue(ScheduleOneDay.DateToStr(Program.DateTimeUaCurrent.Date),out string timeStr))
            {
                return string.IsNullOrEmpty(timeStr) ? "EMPTY" : timeStr;
            }
            return string.Empty;
        }

        public string GetTimeStrNextDayInScheduleHash()
        {
            if (parseScheduleHash().TryGetValue(ScheduleOneDay.DateToStr(Program.DateTimeUaCurrent.Date.AddDays(1)), out string timeStr))
            {
                return string.IsNullOrEmpty(timeStr) ? "EMPTY" : timeStr;
            }
            return string.Empty;
        }


        public bool FindCurrentDayInScheduleHash()
        {
            return ScheduleHash.Contains(ScheduleOneDay.DateToStr(Program.DateTimeUaCurrent.Date));
        }

        public bool FindNextDayInScheduleHash()
        {
            return ScheduleHash.Contains(ScheduleOneDay.DateToStr(Program.DateTimeUaCurrent.Date.AddDays(1)));
        }


        // Считывание state.json
        public static AppState LoadState(string path)
        {
            if (!File.Exists(path))
            {
                return new AppState
                {
                    EmergencyShutdowns = "",
                    ScheduleHashDateSet = DateTime.MinValue,
                    ScheduleHash = string.Empty,
                    DateTimePowerOffLastMessage = DateTime.MinValue,
                    DateTimePowerOnLastMessage = DateTime.MinValue
                };
            }

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<AppState>(json);
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

            if (Program.IsGitHub() && string.IsNullOrEmpty(dd))
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
            if (Program.IsGitHub())
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


            //jsonDtekTmp = jsonTmp();


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


                if (dateSchedule == Program.DateTimeUaCurrent.Date)
                {
                    scheduleDayType = ScheduleDayType.CurrentDay;
                }
                else if (dateSchedule == Program.DateTimeUaCurrent.Date.AddDays(1))
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

                var scheduleOneDay = new ScheduleOneDay();
                scheduleOneDay.Date = dateSchedule;


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
                    scheduleOneDay.Periods.Add(new TimeRange(new TimeSpan(item.Item1.Hours, item.Item1.Minutes, 0), new TimeSpan(item.Item2.Hours, item.Item2.Minutes, 0)));
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
          ""1"": ""yes"",
          ""2"": ""yes"",
          ""3"": ""yes"",
          ""4"": ""yes"",
          ""5"": ""no"",
          ""6"": ""yes"",
          ""7"": ""yes"",
          ""8"": ""yes"",
          ""9"": ""yes"",
          ""10"": ""yes"",
          ""11"": ""yes"",
          ""12"": ""yes"",
          ""13"": ""yes"",
          ""14"": ""yes"",
          ""15"": ""yes"",
          ""16"": ""yes"",
          ""17"": ""yes"",
          ""18"": ""yes"",
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
          ""1"": ""first"",
          ""2"": ""yes"",
          ""3"": ""yes"",
          ""4"": ""yes"",
          ""5"": ""yes"",
          ""6"": ""yes"",
          ""7"": ""yes"",
          ""8"": ""yes"",
          ""9"": ""yes"",
          ""10"": ""yes"",
          ""11"": ""yes"",
          ""12"": ""yes"",
          ""13"": ""yes"",
          ""14"": ""yes"",
          ""15"": ""yes"",
          ""16"": ""yes"",
          ""17"": ""yes"",
          ""18"": ""yes"",
          ""19"": ""yes"",
          ""20"": ""yes"",
          ""21"": ""yes"",
          ""22"": ""yes"",
          ""23"": ""yes"",
          ""24"": ""yes""
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

                    if (dateSchedule == Program.DateTimeUaCurrent.Date)
                    {
                        scheduleDayType = ScheduleDayType.CurrentDay;
                    }
                    else if (dateSchedule == Program.DateTimeUaCurrent.Date.AddDays(1))
                    {
                        scheduleDayType = ScheduleDayType.NextDay;
                    }

                    if (scheduleDayType == null)
                    {
                        continue;
                    }


                    var scheduleOneDay = new ScheduleOneDay();
                    scheduleOneDay.Date = dateSchedule;


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

                        scheduleOneDay.Periods.Add(new TimeRange(timeStart, timeEnd));
                    }

                }
                if (jsonYasno[itemDate]["status"].Value == "EmergencyShutdowns")
                {
                    var scheduleDate = getDateUa(jsonYasno[itemDate]["date"].GetValue<DateTimeOffset>()).Date;
                    if (scheduleDate == Program.DateTimeUaCurrent.Date)
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