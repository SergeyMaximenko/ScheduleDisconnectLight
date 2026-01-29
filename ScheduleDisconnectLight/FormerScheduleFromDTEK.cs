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
    /// Формирователь графика по DTEK
    /// </summary>
    public class FormerScheduleFromDTEK
    {
        public Schedule Get()
        {
            //string url = "https://github.com/Baskerville42/outage-data-ua/blob/main/data/kyiv.json";
            string url = "https://raw.githubusercontent.com/Baskerville42/outage-data-ua/main/data/kyiv.json";


            string jsonDtekTmp = new FormerScheduleFromDTEK_Parser().Get();

            if (string.IsNullOrEmpty(jsonDtekTmp))
            {
                new SenderTelegram() { SendType = SendType.OnlyTest }.Send("Ручний парсер сайту ДТЕК повернув пусте значення");



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
                        Console.WriteLine("FormerScheduleFromDTEK Ошибка: " + ex.Message);
                        jsonDtekTmp = string.Empty;
                    }
                }
                Console.WriteLine("Текущий график взят із https://github.com/Baskerville42");
            }
            else
            {
                Console.WriteLine("Текущий график взят напрямую из DTEK");
            }

            if (string.IsNullOrEmpty(jsonDtekTmp))
            {
                return null;
            }

            //  jsonDtekTmp = jsonTmp();


            var jsonDtek = new Json(jsonDtekTmp)["fact"];
            var schedule = new Schedule();
            schedule.DateLastUpdate = jsonDtek["update"].ValueDate;




            var scheduleFromYasno = new FormerScheduleFromYasno().Get();
            if (scheduleFromYasno!=null)
            {
                schedule.IsEmergencyShutdowns = scheduleFromYasno.IsEmergencyShutdowns;
            }
            



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
                foreach (var itemStatus in itemDates.Value["GPV37.1"].GetDictionary())
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
        ""GPV37.1"": {
          ""1"": ""no"",
          ""2"": ""no"",
          ""3"": ""yes"",
          ""4"": ""yes"",
          ""5"": ""no"",
          ""6"": ""yes"",
          ""7"": ""yes"",
          ""8"": ""no"",
          ""9"": ""yes"",
          ""10"": ""yes"",
          ""11"": ""yes"",
          ""12"": ""yes"",
          ""13"": ""no"",
          ""14"": ""yes"",
          ""15"": ""yes"",
          ""16"": ""no"",
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
        ""GPV37.1"": {
          ""1"": ""no"",
          ""2"": ""no"",
          ""3"": ""first"",
          ""4"": ""yes"",
          ""5"": ""first"",
          ""6"": ""yes"",
          ""7"": ""no"",
          ""8"": ""yes"",
          ""9"": ""second"",
          ""10"": ""second"",
          ""11"": ""no"",
          ""12"": ""no"",
          ""13"": ""yes"",
          ""14"": ""no"",
          ""15"": ""first"",
          ""16"": ""no"",
          ""17"": ""first"",
          ""18"": ""yes"",
          ""19"": ""yes"",
          ""20"": ""yes"",
          ""21"": ""no"",
          ""22"": ""yes"",
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
}
