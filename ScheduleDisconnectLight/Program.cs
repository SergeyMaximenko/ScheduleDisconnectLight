using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace ScheduleDisconnectLight
{
    internal class Program
    {
        static void Main(string[] args)
        {

            // Определяем путь к корню репозитория
            string repoRoot = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..")
            );

            string stateFile = "";

            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
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
            
            var schedule = new FormerScheduleFromYasno().Get();


            //--------------------------------
            //   С М Е Н А    Г Р А Ф И К А 
            //--------------------------------

            Console.WriteLine("График старий:" + state.ScheduleHash);

            Console.WriteLine("График новий:"+ schedule.GetScheduleHash());

            if (string.IsNullOrEmpty(state.ScheduleHash) || !state.ScheduleHash.Contains(schedule.GetScheduleHash()))
            {
                if (schedule.ScheduleDate1 != null || schedule.ScheduleDate2 != null)
                {
                    var message = new StringBuilder();
                    // Отправить сообщение об изменении графика 
                    message.Append("⚡️<b>Оновлено графік відключення світла</b>\n");
                    message.Append("\n");
                    if (schedule.ScheduleDate1 != null)
                    {
                        message.Append($"📅 <b>{schedule.ScheduleDate1.GetCaptionDate()}</b>\n");
                        message.Append(schedule.ScheduleDate1.GetHtmlPeriod() + "\n");
                        message.Append("\n");
                    }
                    if (schedule.ScheduleDate2 != null)
                    {
                        message.Append($"📅 <b>{schedule.ScheduleDate2.GetCaptionDate()}</b>\n");
                        message.Append(schedule.ScheduleDate2.GetHtmlPeriod() + "\n");
                        message.Append("\n");
                    }
                    message.Append("<i>P.S. Оновлено на Yasno " + schedule.DateLastUpdate.ToString("dd.MM.yyyy HH:mm") + "</i>");

                    sendTelegramMessage(message.ToString());
                    Console.WriteLine("Сообщение об изменении графика отправлено");

                    // Сохраняем статус 
                    state.ScheduleHashDateSet = GetCurrentDateTimeUa();
                    state.ScheduleHash = schedule.GetScheduleHash();

                    AppState.SaveState(stateFile, state);
                }
                else
                {
                    Console.WriteLine("График по свету не изменился - 2");
                }
            }
            else
            {
                Console.WriteLine("График по свету не изменился - 1");
            }

            // Уведомления отправляем только по текущей дате. Определить, какой из графиков относится к текущей дате
            var dateTimeCurrent = GetCurrentDateTimeUa();
            ScheduleOneDay scheduleOneDay = null;
            if (schedule.ScheduleDate1 != null && dateTimeCurrent.Date == schedule.ScheduleDate1.Date)
            {
                scheduleOneDay = schedule.ScheduleDate1;
            }
            if (schedule.ScheduleDate2 != null && dateTimeCurrent.Date == schedule.ScheduleDate2.Date)
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

                if (1==1)
                {

                    foreach (var interval in scheduleOneDay.Periods)
                    {


                        var dateTimePowerOff = scheduleOneDay.Date + interval.Start;

                        Console.WriteLine($"  - Напоминание о включении света. Период {interval.GetPeriodString()}. Дата выключения: {dateTimeToStr(dateTimePowerOff)}. Текущая дата {dateTimeToStr(dateTimeCurrent)}  ");

                        if (dateTimePowerOff == state.DateTimePowerOffLastMessage)
                        {
                            Console.WriteLine($"      => уже сообщение было отправлено ранее");
                            continue;
                        }

                        // если сейчас ещё не началось отключение
                        if (dateTimeCurrent < dateTimePowerOff)
                        {
                            // время до начала отключения
                            TimeSpan diff = dateTimePowerOff - dateTimeCurrent;


                            // если осталось <= 30 минут
                            if (diff <= notifyBefore)
                            {

                                if (isPowerOn())
                                {
                                    var messageTimeOff = scheduleOneDay.GetHtmlPeriod(dateTimeCurrent.TimeOfDay);
                                    state.DateTimePowerOffLastMessage = dateTimePowerOff;
                                    isSendMessageOff = true;
                                    sendTelegramMessage("⚠️🔴 Світло може пропасти орієнтовно через <b>" + diff.Minutes.ToString() + $" хв.</b> в <b>{TimeRange.ConvertTimeToStr(dateTimePowerOff.TimeOfDay)}</b> \n" +
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

                        var dateTimePowerOn = scheduleOneDay.Date + interval.End;
                        // Если это конец дня, возможно на следующий день свет не планируется включаться
                        if (interval.End.Hours == 23 &&
                            interval.End.Minutes == 59 &&
                            schedule.ScheduleDate2.Date == scheduleOneDay.Date.AddDays(1)
                            )
                        {
                            var timeNextDay = schedule.ScheduleDate2.Periods.FirstOrDefault();
                            if (timeNextDay != null && timeNextDay.Start.Hours == 0 && timeNextDay.Start.Minutes == 0)
                            {
                                dateTimePowerOn = schedule.ScheduleDate2.Date + timeNextDay.End;
                            }
                        }

                        

                        Console.WriteLine($"  - Напоминание о включении света. Период {interval.GetPeriodString()}. Дата включения: {dateTimeToStr(dateTimePowerOn)}. Текущая дата {dateTimeToStr(dateTimeCurrent)}  ");


                        if (dateTimePowerOn == state.DateTimePowerOnLastMessage)
                        {
                            Console.WriteLine($"      => уже сообщение было отправлено ранее");
                            continue;
                        }

                        // если сейчас ещё не началось отключение
                        if (dateTimeCurrent < dateTimePowerOn)
                        {
                            // время до начала отключения
                            TimeSpan diff = dateTimePowerOn - dateTimeCurrent;

                            // если осталось <= 30 минут
                            if (diff <= notifyBefore)
                            {
                                if (!isPowerOn())
                                {
                                    state.DateTimePowerOnLastMessage = dateTimePowerOn;
                                    var messageTimeOff = scheduleOneDay.GetHtmlPeriod(dateTimeCurrent.TimeOfDay);

                                    sendTelegramMessage("⚠️🟢 Світло має з'явити орієнтовно через <b>" + diff.Minutes.ToString() + $" хв.</b> в <b>{TimeRange.ConvertTimeToStr(dateTimePowerOn.TimeOfDay)}</b> \n" +
                                        (!string.IsNullOrEmpty(messageTimeOff) ? "\nПланові відключення до кінця дня: \n" + messageTimeOff : "")
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
            
        }


        private static string dateTimeToStr(DateTime dateTime)
        {
            return dateTime.ToString("dd.MM.yyyy HH:mm");
        }



        public static DateTime GetCurrentDateTimeUa()
        {
            TimeZoneInfo kyiv = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, kyiv);
        }





        public static void sendTelegramMessage(string message)
        {
            string botToken = "8571725999:AAF29-E6SmsTp5JpLz1ZWIhQUOKnoWCB1kg";
            string chatId = "-1002275491172";

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
                        { "text", message },
                        { "parse_mode", "HTML"}
                    }
            ;

                using (var content = new FormUrlEncodedContent(data))
                {
                    // Синхронный POST
                    HttpResponseMessage response = httpClient.PostAsync(url, content).Result;

                    // Бросит исключение, если статус не 2xx
                    response.EnsureSuccessStatusCode();
                }
            }
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
        /// Получить период в виде HTML
        /// </summary>
        /// <param name="timeStartNext">Время начала отключения, после которого получить список</param>
        /// <returns></returns>
        public string GetHtmlPeriod(TimeSpan? timeStartNext = null)
        {
            if (Periods.Count==0)
            {
                return "🟢 Відключення не плануються";
            }

            return string.Join("\n", Periods.Where(t => timeStartNext == null ? true : t.Start > timeStartNext).Select(t => "🔴 " + t.GetPeriodString()));
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
            
            if (Date == Program.GetCurrentDateTimeUa().Date)
            {
                result = result + " " + "(сьогодні)";
            }
            else if (Date == Program.GetCurrentDateTimeUa().Date.AddDays(1))
            {
                result = result + " " + "(завтра)";
            }
            else if (Date == Program.GetCurrentDateTimeUa().Date.AddDays(-1))
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
            return string.Join(" => ", Periods.Select(t => t.GetPeriodString()));
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
        public TimeRange(TimeSpan start, TimeSpan end)
        {
            Start = start;
            End = end;
        }

        public string GetPeriodString()
        {
            return ConvertTimeToStr(Start) + " - " + ConvertTimeToStr(End);
        }

        /// <summary>
        /// Конвертировать время в строку
        /// </summary>
        public static string ConvertTimeToStr(TimeSpan time)
        {
            return time.Hours == 23 && time.Minutes == 59 ? "24:00" : time.Hours.ToString("D2") + ":" + time.Minutes.ToString("D2");
        }

    }




    

    /// <summary>
    /// Класс мини база данных 
    /// </summary>
    class AppState
    {
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

        // Считывание state.json
        public static AppState LoadState(string path)
        {
            if (!File.Exists(path))
            {
                return new AppState
                {
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


            //jsonYasnoTmp = jsonTmp();


            var jsonYasno = new Json(jsonYasnoTmp)["1.1"];
            var schedule = new Schedule();
            schedule.DateLastUpdate = getDateUa(jsonYasno["updatedOn"].GetValue<DateTimeOffset>());
            if (jsonYasno["today"]["status"].Value == "ScheduleApplies")
            {
                schedule.ScheduleDate1 = new ScheduleOneDay();
                schedule.ScheduleDate1.Date = getDateUa(jsonYasno["today"]["date"].GetValue<DateTimeOffset>()).Date;

                foreach (var item in jsonYasno["today"]["slots"].GetArray())
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

                    var timeEnd = hoursEnd == 24 ? new TimeSpan(23, 59, 0) : new TimeSpan(hoursEnd, minutesEnd, 0);

                    schedule.ScheduleDate1.Periods.Add(new TimeRange(timeStart, timeEnd));
                }
            }
            if (jsonYasno["tomorrow"]["status"].Value == "ScheduleApplies")
            {
                schedule.ScheduleDate2 = new ScheduleOneDay();
                schedule.ScheduleDate2.Date = getDateUa(jsonYasno["tomorrow"]["date"].GetValue<DateTimeOffset>()).Date;

                foreach (var item in jsonYasno["tomorrow"]["slots"].GetArray())
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

                    var timeEnd = hoursEnd == 24 ? new TimeSpan(23, 59, 0) : new TimeSpan(hoursEnd, minutesEnd, 0);

                    schedule.ScheduleDate2.Periods.Add(new TimeRange(timeStart, timeEnd));
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
                      ""start"": 0,
                      ""end"": 480,
                      ""type"": ""NotPlanned""
                    },
                    {
                      ""start"": 480,
                      ""end"": 570,
                      ""type"": ""Definite""
                    },
                    {
                      ""start"": 810,
                      ""end"": 1134,
                      ""type"": ""Definite""
                    },
                    {
                      ""start"": 1200,
                      ""end"": 1260,
                      ""type"": ""Definite""
                    }
                  ],
                  ""date"": ""2025-11-18T00:00:00+02:00"",
                  ""status"": ""ScheduleApplies""
                },
                ""tomorrow"": {
                  ""slots"": [
                    {
                      ""start"": 0,
                      ""end"": 150,
                      ""type"": ""Definite""
                    },
                    {
                      ""start"": 150,
                      ""end"": 510,
                      ""type"": ""NotPlanned""
                    },
                    {
                      ""start"": 510,
                      ""end"": 750,
                      ""type"": ""Definite""
                    },
                    {
                      ""start"": 750,
                      ""end"": 1440,
                      ""type"": ""NotPlanned""
                    }
                  ],
                  ""date"": ""2025-11-19T00:00:00+02:00"",
                  ""status"": ""ScheduleApplies""
                },
                ""updatedOn"": ""2025-11-18T04:31:02+00:00""
              }
            }
            ";
        }

    }


}
