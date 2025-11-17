using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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

            // Увеличиваем счётчик
            state.Counter++;

            // Ставим текущее время
            state.LastUpdated = DateTime.Now;

            // Сохраняем обратно
            SaveState(stateFile, state);

            Console.WriteLine("state.json обновлён по пути: " + stateFile);





            getScheduleYasno();

            Console.WriteLine("Отправляем сообщение...");

            sendTelegramMessage();
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
            schedule.DateUpdate = jsonYasno["updatedOn"].GetValue<DateTime>();
            if (jsonYasno["today"]["status"].Value == "ScheduleApplies")
            {
                schedule.ParamDisconnet1 = new ScheduleTimeDisconnet();
                schedule.ParamDisconnet1.Date = jsonYasno["today"]["date"].GetValue<DateTime>();

                foreach (var item in jsonYasno["today"]["slots"].GetArray())
                {
                    if (item["type"].Value == "Definite")
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
                schedule.ParamDisconnet2.Date = jsonYasno["tomorrow"]["date"].GetValue<DateTime>();

                foreach (var item in jsonYasno["tomorrow"]["slots"].GetArray())
                {
                    if (item["type"].Value == "Definite")
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


        public static void sendTelegramMessage()
        {
            string botToken = "7911836999:AAHeC6qjw-Kis9xwA332YTq2ns1YI1AMdMI";
            string chatId = "-1002275491172";

            if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
            {
                Console.WriteLine("BOT_TOKEN или CHAT_ID не заданы.");
                return;
            }

            string text = "11144__-555ёё221122Ping из C# (.NET Framework 4.7.2)";

            using (var httpClient = new HttpClient())
            {
                string url = $"https://api.telegram.org/bot{botToken}/sendMessage";

                var data = new Dictionary<string, string>
                    {
                        { "chat_id", chatId },
                        { "text", text }
                    };

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
                    LastUpdated = DateTime.Now,
                    Counter = 0
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
        public DateTime LastUpdated { get; set; }
        public int Counter { get; set; }
    }

    public class Schedule
    {
        public DateTime DateUpdate;
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

        public ScheduleTimeDisconnet()
        {
            PeriodDisconnet = new List<Tuple<TimeSpan, TimeSpan>>();
        }
    }



}
