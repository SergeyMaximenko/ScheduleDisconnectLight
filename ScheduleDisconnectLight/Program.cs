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
using System.Runtime.CompilerServices;
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

            //DateTimeUaCurrent = new DateTime(2025, 12, 04, 1, 30, 0);

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

            var schedule = IsSourceYasno
                ? new FormerScheduleFromYasno().Get()
                : new FormerScheduleFromDTEK().Get();

            schedule.FillServiceProp();



            //--------------------------------
            //   АВАРИЙНЫЕ ОТКЛЮЧЕНИЯ
            //--------------------------------

            //schedule.IsEmergencyShutdowns = false;




            if (state.IsEmergencyShutdowns == schedule.IsEmergencyShutdowns)
            {
                if (schedule.IsEmergencyShutdowns)
                {
                    Console.WriteLine("Сейчас действуют аварийные отключения. Уже было отправлено ранее. Выйти с програми");
                    return;
                }
                else
                {
                    // Аварийные отключения не действуют 
                    // Проверить, когда было последнее сообщение. Если уже другой день и прошло больше 8 часов, удалить
                    if (state.EmergencyShutdownsLastMessageId != 0 && state.EmergencyShutdownsDateSendMessage.Date < DateTimeUaCurrent && (DateTimeUaCurrent - state.EmergencyShutdownsDateSendMessage).TotalHours >= 8)
                    {
                        Console.WriteLine("Удалено последнее сообщение с аварийными отключениям");
                        new TelegramApi().Delete(state.EmergencyShutdownsLastMessageId);
                        state.EmergencyShutdownsLastMessageId = 0;
                        AppState.SaveState(stateFile, state);
                    }
                }
            }
            else
            {

                if (schedule.IsEmergencyShutdowns)
                {
                    if (state.EmergencyShutdownsLastMessageId != 0)
                    {
                        new TelegramApi().Delete(state.EmergencyShutdownsLastMessageId);
                    }
                    var messageId = new TelegramApi().Send("🚨 ДТЕК: У Києві екстрені відключення. Графіки не діють");
                    Console.WriteLine("Отправлено сообщение: Аварийные отключения!");
                    state.EmergencyShutdownsLastMessageId = messageId;
                    state.EmergencyShutdownsDateSendMessage = DateTimeUaCurrent;
                    state.EmergencyShutdowns = "+"; // Сохраняем статус 
                    AppState.SaveState(stateFile, state);
                }
                else
                {
                    if (state.EmergencyShutdownsLastMessageId != 0)
                    {
                        new TelegramApi().Delete(state.EmergencyShutdownsLastMessageId);
                    }
                    var messageId = new TelegramApi().Send("✅ ДТЕК: Екстрені відключення скасовано");
                    Console.WriteLine("Отправлено сообщение: Аварийные отключения скасовані!");
                    state.EmergencyShutdownsLastMessageId = messageId;
                    state.EmergencyShutdownsDateSendMessage = DateTimeUaCurrent;
                    state.EmergencyShutdowns = "";
                    AppState.SaveState(stateFile, state);
                }
            }




            //--------------------------------
            //   С М Е Н А    Г Р А Ф И К А 
            //--------------------------------

            Console.WriteLine("График старий:" + state.ScheduleHash);

            Console.WriteLine("График новий:" + schedule.GetScheduleHash());

            var isSendNewSchedule = false;

            if (state.ScheduleHash.Trim() != schedule.GetScheduleHash().Trim())
            {

                var message = new StringBuilder();
                // Отправить сообщение об изменении графика 

                if (state.ScheduleHash.Contains(schedule.GetScheduleHash()))
                {
                    message.Append("⚡️<b>Графік відключення світла</b>\n");
                }
                else
                {
                    message.Append("⚡️<b>Оновлено графік відключення світла</b>\n");
                }

                message.Append("\n");

                if (schedule.ScheduleDate1 != null || schedule.ScheduleDate2 != null)
                {
                    if (schedule.ScheduleDate1 != null)
                    {
                        message.Append($"🗓️ <b>{schedule.ScheduleDate1.GetCaptionDate()}</b>\n");
                        message.Append($"📉 <b>{schedule.ScheduleDate1.GetPercentOffPower()}%</b> часу без світла\n");
                        message.Append(schedule.ScheduleDate1.GetHtmlPeriod() + "\n");
                        message.Append("\n");
                    }
                    if (schedule.ScheduleDate2 != null)
                    {
                        message.Append($"🗓️ <b>{schedule.ScheduleDate2.GetCaptionDate()}</b>\n");
                        message.Append($"📉 <b>{schedule.ScheduleDate2.GetPercentOffPower()}%</b> часу без світла\n");
                        message.Append(schedule.ScheduleDate2.GetHtmlPeriod() + "\n");
                        message.Append("\n");
                    }
                }
                else
                {
                    message.Append($"🟢 Відключення не заплановані\n");
                    message.Append("\n");
                }
                message.Append($"<i>P.S. Оновлено на {(IsSourceYasno ? "Yasno" : "DTEK")} " + schedule.DateLastUpdate.ToString("dd.MM.yyyy HH:mm") + "</i>");

                if (state.ScheduleLastMessageId != 0)
                {
                    new TelegramApi().Delete(state.ScheduleLastMessageId);
                }

                isSendNewSchedule = true;

                var messageId = new TelegramApi().Send(message.ToString());
                state.ScheduleLastMessageId = messageId;
                Console.WriteLine("Сообщение об изменении графика отправлено");

                // Сохраняем статус 
                state.ScheduleDateSendMessage = DateTimeUaCurrent;
                state.ScheduleHash = schedule.GetScheduleHash();

                AppState.SaveState(stateFile, state);

            }
            else
            {
                Console.WriteLine("График по свету не изменился - 1");
            }






            /*
            // Уведомления отправляем только по текущей дате. Определить, какой из графиков относится к текущей дате
           ;
            ScheduleOneDay scheduleOneDay = null;
            if (schedule.ScheduleDate1 != null && DateTimeUaCurrent.Date == schedule.ScheduleDate1.Date)
            {
                scheduleOneDay = schedule.ScheduleDate1;
            }
            if (schedule.ScheduleDate2 != null && DateTimeUaCurrent.Date == schedule.ScheduleDate2.Date)
            {
                scheduleOneDay = schedule.ScheduleDate2;
            }


            if (scheduleOneDay != null)
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

                    foreach (var interval in scheduleOneDay.Periods)
                    {


                        var dateTimePowerOff = scheduleOneDay.Date + interval.Start;

                        Console.WriteLine($"  - Напоминание о включении света. Период {interval.GetPeriodToStringOnlyDay()}. Дата выключения: {dateTimeToStr(dateTimePowerOff)}. Текущая дата {dateTimeToStr(DateTimeUaCurrent)}  ");

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
                                    // messageTimeOff пустой быть не может 
                                    var messageTimeOff = scheduleOneDay.GetHtmlPeriod(DateTimeUaCurrent.TimeOfDay);

                                    state.DateTimePowerOffLastMessage = dateTimePowerOff;
                                    isSendMessageOff = true;
                                    new SenderTelegram().Send("⚠️🔴 Світло може зникнути орієнтовно через <b>" + diff.Minutes.ToString() + $" хв.</b> в <b>{TimeRange.ConvertTimeToStr(dateTimePowerOff.TimeOfDay)}</b> \n" +
                                            (!string.IsNullOrEmpty(messageTimeOff) ? "\nПланові відключення до кінця дня: \n" + messageTimeOff : "")
                                            );

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

                    foreach (var interval in scheduleOneDay.Periods)
                    {

                        var dateTimePowerOn = interval.EndNextDay != TimeSpan.Zero
                            ? scheduleOneDay.Date.AddDays(1) + interval.EndNextDay
                            : scheduleOneDay.Date + interval.End;

                        Console.WriteLine($"  - Напоминание о включении света. Период {interval.GetPeriodToStringOnlyDay()}. Дата включения: {dateTimeToStr(dateTimePowerOn)}. Текущая дата {dateTimeToStr(DateTimeUaCurrent)}  ");


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
                                    state.DateTimePowerOnLastMessage = dateTimePowerOn;
                                    var messageTimeOff = scheduleOneDay.GetHtmlPeriod(DateTimeUaCurrent.TimeOfDay);

                                    // Признак, что текущий день закончен. В этом случае не нужно писать, что на сегодня отключения больше не запланированы 
                                    var isDayOff = dateTimePowerOn >= new DateTime(DateTimeUaCurrent.Year, DateTimeUaCurrent.Month, DateTimeUaCurrent.Day, 23, 59, 0);

                                    new SenderTelegram().Send("⚠️🟢 Світло за графіком має з'явити орієнтовно через <b>" + diff.Minutes.ToString() + $" хв.</b> в <b>{TimeRange.ConvertTimeToStr(dateTimePowerOn.TimeOfDay)}</b> \n" +
                                        (!string.IsNullOrEmpty(messageTimeOff)
                                            ? "\nПланові відключення до кінця дня: \n" + messageTimeOff
                                            : !isDayOff
                                                ? "\nНа сьогодні відключення більше не заплановані 😊"
                                                : "")
                                        );

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
            */

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
        public ScheduleOneDay ScheduleDate1;

        /// <summary>
        /// График для 2-й дати
        /// </summary>
        public ScheduleOneDay ScheduleDate2;


        /// <summary>
        /// Получить ХЕШ 
        /// </summary>
        /// <returns></returns>
        public string GetScheduleHash()
        {
            var result = new StringBuilder();
            if (ScheduleDate1 != null)
            {
                result.Append("[" + ScheduleDate1.Date.ToString("dd.MM.yyyy") + " " + ScheduleDate1.GetScheduleHash() + "] ");

            }
            if (ScheduleDate2 != null)
            {
                result.Append("[" + ScheduleDate2.Date.ToString("dd.MM.yyyy") + " " + ScheduleDate2.GetScheduleHash() + "] ");
            }
            return result.ToString();
        }



        /// <summary>
        /// Заполнить сервисные свойства 
        /// </summary>
        public void FillServiceProp()
        {
            if (ScheduleDate1 != null &&
                ScheduleDate2 != null &&
                ScheduleDate1.Periods.Count() > 0 &&
                ScheduleDate2.Periods.Count() > 0 &&
                ScheduleDate1.Date.AddDays(1) == ScheduleDate2.Date &&
                ScheduleDate1.Periods.Last().EndIsEndDay() &&
                ScheduleDate2.Periods.First().StartIsStartDay())
            {
                ScheduleDate1.Periods.Last().SetEndNextDay(ScheduleDate2.Periods.First().End);
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
        /// <param name="timeStartNext">Время начала отключения, после которого получить список</param>
        /// <returns></returns>
        public string GetHtmlPeriod(TimeSpan? timeStartNext = null)
        {
            if (Periods.Count == 0)
            {
                return "🟢 Відключення не плануються";
            }

            return string.Join("\n", Periods.Where(t => timeStartNext == null ? true : t.Start > timeStartNext)
                .Select(t => "🔴 " + (timeStartNext != null ? t.GetPeriodToStringAndNextDay(true) : t.GetPeriodToStringOnlyDay(true))));
        }

        /// <summary>
        /// Получить наименование дати, для отправки в ТГ
        /// </summary>
        public string GetCaptionDate()
        {
            // Дата
            var result = Date.ToString("dd.MM.yyyy");
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
            return string.Join(" => ", Periods.Select(t => t.GetPeriodToStringOnlyDay()));
        }

        public ScheduleOneDay()
        {
            Periods = new List<TimeRange>();
        }
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

        /// <summary>
        /// Конвертировать в строку только текущий день
        /// </summary>
        public string GetPeriodToStringOnlyDay(bool addDiff = false)
        {
            var addDiffText = "";
            if (addDiff)
            {
                var diff = (End - Start);
                addDiffText = $"  <i>{getNameTimeSpan(diff)}</i>";
            }

            return ConvertTimeToStr(Start) + " - " + ConvertTimeToStr(End) + addDiffText;
        }

        /// <summary>
        /// Конвертировать в строку только следующий день, если его окончание попадает на следующий день
        /// </summary>
        public string GetPeriodToStringAndNextDay(bool addDiff = false)
        {
            var addDiffText = "";
            if (addDiff)
            {
                var diff = (End - Start);
                if (EndNextDay != TimeSpan.Zero)
                {
                    diff = diff + EndNextDay;
                }
                addDiffText = $"  <i>{getNameTimeSpan(diff)}</i>";
            }

            return ConvertTimeToStr(Start) + " - " + ConvertTimeToStr(EndNextDay != TimeSpan.Zero ? EndNextDay : End) + addDiffText;
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
        /// <summary>
        /// Код последнего сообщения
        /// </summary>
        public int EmergencyShutdownsLastMessageId { get; set; }

        /// <summary>
        /// Дата последнего сообщения
        /// </summary>
        public DateTime EmergencyShutdownsDateSendMessage { get; set; }

        public string EmergencyShutdowns { get; set; }

        [JsonIgnore]
        public bool IsEmergencyShutdowns { get { return EmergencyShutdowns == "+"; } }

        /// <summary>
        /// Код последнего сообщения
        /// </summary>
        public int ScheduleLastMessageId { get; set; }

        /// <summary>
        /// Дата последнего сообщения
        /// </summary>
        public DateTime ScheduleDateSendMessage { get; set; }


        /// <summary>
        /// Условий Хер код
        /// </summary>
        public string ScheduleHash { get; set; }

        /*
        /// <summary>
        /// Время выключения света, по которому уже было отправлено напоминание
        /// </summary>
        public DateTime DateTimePowerOffLastMessage { get; set; }

        /// <summary>
        /// Время включения света, по которому уже было отправлено напоминание
        /// </summary>
        public DateTime DateTimePowerOnLastMessage { get; set; }
        */

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
                result = new AppState();
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

    public class TelegramApi
    {

        public void Delete(int messageId)
        {

            var param = Param.Get();


            if (string.IsNullOrWhiteSpace(param.BotToken))
            {
                Console.WriteLine("BOT_TOKEN не заданы.");
                return;
            }

            //string text = "11144__-555ёё221122Ping из C# (.NET Framework 4.7.2)";


            using (var httpClient = new HttpClient())
            {
                string url = $"https://api.telegram.org/bot{param.BotToken}/deleteMessage";

                var data = new Dictionary<string, string>
                    {
                        { "chat_id", param.ChatId },
                        { "message_id", messageId.ToString() }
                    };

                Console.WriteLine("START DELETE TELEGRAM:");

                using (var content = new FormUrlEncodedContent(data))
                {
                    // Синхронный POST
                    HttpResponseMessage response = httpClient.PostAsync(url, content).Result;

                    // Бросит исключение, если статус не 2xx
                    response.EnsureSuccessStatusCode();

                }
                Console.WriteLine("END DELETE TELEGRAM:");

            }
            return;
        }



        public int Send(string message)
        {

            var param = Param.Get();


            if (string.IsNullOrWhiteSpace(param.BotToken))
            {
                Console.WriteLine("BOT_TOKEN не заданы.");
                return 0;
            }

            //string text = "11144__-555ёё221122Ping из C# (.NET Framework 4.7.2)";

            var messageId = 0;
            using (var httpClient = new HttpClient())
            {
                string url = $"https://api.telegram.org/bot{param.BotToken}/sendMessage";

                var data = new Dictionary<string, string>
                    {
                        { "chat_id", param.ChatId },
                        { "message_thread_id", param.ChatIdThread },
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

                    var responseString = response.Content.ReadAsStringAsync().Result;

                    if (response.IsSuccessStatusCode)
                    {
                        messageId = new Json(responseString)["result"]["message_id"].ValueInt;
                    }

                }
            }
            return messageId;
        }


        private class Param
        {
            public string ChatId { get; private set; }
            public string ChatIdThread { get; private set; }
            public string BotToken { get; private set; }

            public static Param Get()
            {
                var param = new Param();

                param.BotToken = getBotToken();
                // Тестова група
                //string chatId = "-1002275491172";

                // Основная группа, которая была раньше
                //string chatId = "-1002336792682";

                // Текущая группа


                if (Program.IsGitHub())
                {
                    param.ChatId = "-1001043114362";
                    param.ChatIdThread = "54031";
                }
                else
                {
                    param.ChatId = "-1002275491172";
                    param.ChatIdThread = "";
                }

                return param;
            }
            private static string getBotToken()
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


            //jsonYasnoTmp = jsonTmp();


            var jsonDtek = new Json(jsonDtekTmp)["fact"];
            var schedule = new Schedule();
            schedule.DateLastUpdate = jsonDtek["update"].ValueDate;

            var scheduleFromYasno = new FormerScheduleFromYasno().Get();
            if (scheduleFromYasno.IsEmergencyShutdowns)
            {
                schedule.IsEmergencyShutdowns = true;
                return schedule;
            }



            var count = 0;

            // Идем по датам 
            foreach (var itemDates in jsonDtek["data"].GetDictionary())
            {

                var dateSchedule = convertUnixUtcToDateUa(Convert.ToInt32(itemDates.Key)).Date;
                if (dateSchedule < Program.DateTimeUaCurrent.Date)
                {
                    continue;
                }

                if (count >= 3)
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

                count++;
                var scheduleOneDay = new ScheduleOneDay();
                if (count == 1)
                {
                    schedule.ScheduleDate1 = scheduleOneDay;
                }
                else
                {
                    schedule.ScheduleDate2 = scheduleOneDay;
                }
                scheduleOneDay.Date = dateSchedule;



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

            var count = 0;
            foreach (var itemDate in listDate)
            {
                if (jsonYasno[itemDate]["status"].Value == "ScheduleApplies")
                {
                    var scheduleDate = getDateUa(jsonYasno[itemDate]["date"].GetValue<DateTimeOffset>()).Date;
                    if (scheduleDate >= Program.DateTimeUaCurrent.Date)
                    {
                        count++;
                        var scheduleOneDay = new ScheduleOneDay();
                        if (count == 1)
                        {
                            schedule.ScheduleDate1 = scheduleOneDay;
                        }
                        else
                        {
                            schedule.ScheduleDate2 = scheduleOneDay;
                        }

                        scheduleOneDay.Date = scheduleDate;

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
                      ""start"": 1200,
                      ""end"": 1440,
                      ""type"": ""Definite""
                    }
                  ],
                  ""date"": ""2025-12-02T00:00:00+02:00"",
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
                  ""date"": ""2025-12-03T00:00:00+02:00"",
                  ""status"": ""ScheduleApplies""
                },
                ""updatedOn"": ""2025-11-18T04:31:02+00:00""
              }
            }
            ";
        }

    }





}
