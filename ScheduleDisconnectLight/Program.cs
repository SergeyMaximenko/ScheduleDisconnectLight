using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

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

            // Полный путь к state.json
            string stateFile = Path.Combine(repoRoot, "state.json");

            // Загружаем состояние
            AppState state = LoadState(stateFile);


           
          





            var schedule = getScheduleYasno();
            var message = "";
            if (1==1 || schedule.LastUpdatedYasno != state.LastUpdatedYasno)
            {
                // Изменился график

                // Записать дату последнего обновления в Yasno
                state.LastUpdatedYasno = schedule.LastUpdatedYasno;


                if (schedule.ParamDisconnet1 != null || schedule.ParamDisconnet2 != null)
                {
                    var messageTmp = new StringBuilder();
                    // Отправить сообщение об изменении графика 
                    messageTmp.Append("⚡️<b>2Увага!</b> Новий графік <b>відсутності</b> світла\n");
                    messageTmp.Append("\n");
                    if (schedule.ParamDisconnet1 != null)
                    {
                        messageTmp.Append($"📅 <b>{schedule.ParamDisconnet1.Date.ToString("dd.MM.yyyy")} {getNameDay(schedule.ParamDisconnet1.Date)}</b>\n");
                        messageTmp.Append(schedule.ParamDisconnet1.GetHtmlTime()+"\n");
                        messageTmp.Append("\n");
                    }
                    if (schedule.ParamDisconnet2 != null)
                    {
                        messageTmp.Append($"📅 <b>{schedule.ParamDisconnet2.Date.ToString("dd.MM.yyyy")} {getNameDay(schedule.ParamDisconnet2.Date)}</b>\n");
                        messageTmp.Append(schedule.ParamDisconnet2.GetHtmlTime() + "\n");
                        messageTmp.Append("\n");
                    }
                    messageTmp.Append("<i>P.S. Оновлено на Yasno " + state.LastUpdatedYasno.ToString("dd.MM.yyyy HH:mm") + "</i>");
                    message = messageTmp.ToString();
                }
                
            }
            else
            {
                

            }
            


            Console.WriteLine("Отправляем сообщение...");

            sendTelegramMessage(message);

            
            // Сохраняем обратно
            state.LastUpdatedFile = getCurrentDateUa();
            SaveState(stateFile, state);


        }


        private static Schedule getScheduleYasno()
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
            

                


            var jsonYasno = new Json(jsonYasnoTmp)["1.1"];
            var schedule = new Schedule();
            schedule.LastUpdatedYasno = getDateUa(jsonYasno["updatedOn"].GetValue<DateTimeOffset>());
            if (jsonYasno["today"]["status"].Value == "ScheduleApplies")
            {
                schedule.ParamDisconnet1 = new ScheduleTimeDisconnet();
                schedule.ParamDisconnet1.Date = getDateUa(jsonYasno["today"]["date"].GetValue<DateTimeOffset>()).Date;

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

                    schedule.ParamDisconnet1.PeriodDisconnet.Add(Tuple.Create(timeStart, timeEnd));
                }
            }
            if (jsonYasno["tomorrow"]["status"].Value == "ScheduleApplies")
            {
                schedule.ParamDisconnet2 = new ScheduleTimeDisconnet();
                schedule.ParamDisconnet2.Date = getDateUa(jsonYasno["tomorrow"]["date"].GetValue<DateTimeOffset>()).Date;

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

                    schedule.ParamDisconnet2.PeriodDisconnet.Add(Tuple.Create(timeStart, timeEnd));
                }
            }
            return schedule;


        }

        private static string getNameDay(DateTime date)
        {
            var text = date.ToString("ddd", new CultureInfo("uk-UA"));
            if (date == getCurrentDateUa().Date)
            {
                text = text + " (сьогодні)";
            }
            else if (getCurrentDateUa().Date.AddDays(1) == date)
            {
                text = text + " (завтра)";
            }
            else if (getCurrentDateUa().Date.AddDays(-1) == date)
            {
                text = text + " (вчора)";
            }
            return text;
        }

        private static DateTime getCurrentDateUa()
        {
            TimeZoneInfo kyiv = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, kyiv);
        }

        private static DateTime getDateUa(DateTimeOffset date)
        {
            TimeZoneInfo kyiv = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            // Конвертируем "как задумано" в киевский часовой пояс
            return TimeZoneInfo.ConvertTime(date, kyiv).DateTime;
        }



        public static void sendTelegramMessage(string message)
        {
            string botToken = "7911836999:AAHeC6qjw-Kis9xwA332YTq2ns1YI1AMdMI";
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

        // Считывание state.json
        static AppState LoadState(string path)
        {
            if (!File.Exists(path))
            {
                return new AppState
                {
                    LastUpdatedFile = DateTime.MinValue,
                    LastUpdatedYasno = DateTime.MinValue
                };
            }

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<AppState>(json);
        }

        // Сохранение state.json
        static void SaveState(string path, AppState state)
        {
            string json = JsonConvert.SerializeObject(state, Formatting.Indented);
            File.WriteAllText(path, json);
        }

    }

    // Класс "мини-базы"
    class AppState
    {
        public DateTime LastUpdatedFile { get; set; }
        
        public DateTime LastUpdatedYasno { get; set; }
    
    }

    public class Schedule
    {
        public DateTime LastUpdatedYasno;
        public ScheduleTimeDisconnet ParamDisconnet1;
        public ScheduleTimeDisconnet ParamDisconnet2;
        public Schedule()
        {
           
        }
    }

    public class ScheduleTimeDisconnet
    {
        public DateTime Date;
        public List<Tuple<TimeSpan,TimeSpan>> PeriodDisconnet;

        public string GetHtmlTime()
        {
            if (PeriodDisconnet.Count==0)
            {
                return "🟢 Відключення не плануються";
            }

            return string.Join("\n", PeriodDisconnet.Select(t => "🔴 " + t.Item1.Hours.ToString("D2") + ":" + t.Item1.Minutes.ToString("D2")+ " - "+ ( t.Item2.Hours == 23 && t.Item2.Minutes ==59 ? "24:00" : t.Item2.Hours.ToString("D2") + ":" + t.Item2.Minutes.ToString("D2"))));
        }

        public ScheduleTimeDisconnet()
        {
            PeriodDisconnet = new List<Tuple<TimeSpan, TimeSpan>>();
        }
    }



}
