using Google.Apis.Sheets.v4;
using ScheduleDisconnectLight;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace Service
{



    public static class ZvonokClient
    {

        private static bool isNowInTimeRange(TimeSpan timeFrom, TimeSpan timeTo)
        {
            var now = Api.DateTimeUaCurrent.TimeOfDay;

            // обычный диапазон: 08:00–20:00
            if (timeFrom <= timeTo)
                return now >= timeFrom && now <= timeTo;

            // диапазон через полночь: 22:00–06:00
            return now >= timeFrom || now <= timeTo;
        }


        public static void MakeCall(ModemParam modemParam)
        {
            var isTest = false;

            if (string.IsNullOrEmpty(SpreadSheet.GetValue<string>(SpreadSheet.SheetParam, 3, 1)))
            {
                Console.WriteLine($"Звонок на модем. Це прод-режим");
                isTest = false;
                // Це промисловий режим
                if (modemParam.Percent >= 95)
                {
                    Console.WriteLine($"Звонок на модем. Не здійснено. Процент {modemParam.Percent}");
                    return;
                }
                if (modemParam.Status == StatusModemBattery.Charging)
                {
                    Console.WriteLine($"Звонок на модем. Не здійснено. Статус {modemParam.Status}");
                    return;
                }

                var timeCall = SpreadSheet.GetValue<int>(SpreadSheet.SheetParam, 1, 1);
                if (timeCall == 0)
                {
                    Console.WriteLine($"Звонок на модем. Не здійснено. Час здійнення не задано ");
                    return;
                }
                if (isNowInTimeRange(new TimeSpan(23, 30, 0), new TimeSpan(06, 00, 0)))
                {
                    Console.WriteLine($"Звонок на модем. Не здійснено. В ночі не потрібно будити ");
                    return;
                }
                var lastCall = SpreadSheet.GetValue<DateTime>(SpreadSheet.SheetNameOnOffStatus, 8, 1);

                if (lastCall.AddMinutes(timeCall) > Api.DateTimeUaCurrent)
                {
                    Console.WriteLine($"Звонок на модем. Не здійснено. Ще не пройшло {timeCall} хв ");
                    return;
                }

                if (!Api.IsGenOn() && !Api.IsPowerOn())
                {
                    Console.WriteLine($"Звонок на модем. Не здійснено. Генератор не працює і світла немає ");
                    return;
                }

            }
            else
            {
                Console.WriteLine($"Звонок на модем. Це тест-режим");
                isTest = true;
            }


            
            var resultCall = call();
            Console.WriteLine($"Звонок на модем здійснено {resultCall}");

            SpreadSheet.AppendRow(SpreadSheet.SheetNameZvonokCall, new List<SpreadSheet.CellValueNote>
            {
                new SpreadSheet.CellValueNote { Value = Api.DateTimeUaCurrent.ToString("yyyy-MM-dd HH:mm:ss"), Note = null }, //.ToString("yyyy-MM-dd HH:mm:ss")
                new SpreadSheet.CellValueNote { Value = resultCall, Note = null },
                new SpreadSheet.CellValueNote { Value = "<тут статус>", Note = modemParam.Message },
                new SpreadSheet.CellValueNote { Value = isTest ? "так" : "", Note = null },
            });

            if (!isTest)
            {
                SpreadSheet.SetValue(SpreadSheet.SheetNameOnOffStatus, 8, 1, Api.DateTimeUaCurrent.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            


        }




        private static string call()
        {
            var url = "https://zvonok.com/manager/cabapi_external/api/v1/phones/call/";

            try
            {
                using (var http = new HttpClient())
                {
                    var phone = "+38" + Text.Replace(Text.Replace(SpreadSheet.GetValue<string>(SpreadSheet.SheetParam, 2, 1), " ", ""), " ", "").Trim();
                    

                    var form = new FormUrlEncodedContent(new[]
                    {
                new KeyValuePair<string,string>("public_key", KeyParam.Get().ZvonokKey),
                new KeyValuePair<string,string>("phone",phone ),
                new KeyValuePair<string,string>("campaign_id", "22242650")
            });

                    var response = http.PostAsync(url, form).Result;

                    string body = response.Content.ReadAsStringAsync().Result;

                    // если HTTP ошибка — вернуть текст ответа API
                    if (!response.IsSuccessStatusCode)
                    {
                        return $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {body}";
                    }

                    return body;
                }
            }
            catch (HttpRequestException ex)
            {
                return "HTTP ERROR: " + ex.Message;
            }
            catch (WebException ex)
            {
                return "WEB ERROR: " + ex.Message;
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

    }
}
