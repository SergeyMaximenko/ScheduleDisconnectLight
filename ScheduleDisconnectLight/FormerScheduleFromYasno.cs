using Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ScheduleDisconnectLight
{
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
                    Console.WriteLine("FormerScheduleFromYasno Ошибка: " + ex.Message);
                    jsonYasnoTmp = string.Empty;
                }
            }

            if (string.IsNullOrEmpty(jsonYasnoTmp))
            {
                return null;
            }

            // jsonYasnoTmp = jsonTmp();


            var jsonYasno = new Json(jsonYasnoTmp)[Api.CodeGroup];
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
            return $@"
{{
  ""{Api.CodeGroup}"": {{
    ""today"": {{
      ""slots"": [
        {{ ""start"": 480, ""end"": 575, ""type"": ""Definite"" }},
        {{ ""start"": 810, ""end"": 1134, ""type"": ""Definite"" }},
        {{ ""start"": 1150, ""end"": 1440, ""type"": ""Definite"" }}
      ],
      ""date"": ""2025-12-09T00:00:00+02:00"",
      ""status"": ""ScheduleApplies""
    }},
    ""tomorrow"": {{
      ""slots"": [
        {{ ""start"": 0, ""end"": 30, ""type"": ""Definite"" }},
        {{ ""start"": 520, ""end"": 750, ""type"": ""Definite"" }}
      ],
      ""date"": ""2025-12-10T00:00:00+02:00"",
      ""status"": ""ScheduleApplies""
    }},
    ""updatedOn"": ""2025-11-18T04:31:02+00:00""
  }}
}}
";
        }


    }
}
