using Google.Apis.Sheets.v4;
using Service;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;


namespace ScheduleDisconnectLight
{


    public class GeneratorNotification
    {


        private bool _sendOnlyTestGroupParam = false;

        private Schedule _schedule;
        private SheetsService _service;
        private bool _sendTestGroup;
        public GeneratorNotification(Schedule schedule)
        {
            _service = new SpreadSheet().GetService();
            _sendTestGroup = Api.SendOnlyTestGroup(_sendOnlyTestGroupParam);

            _schedule = schedule;

            /*
             new SenderTelegram()
             {
                 SendOnlyTestGroup = _sendOnlyTestGroup,
                 ReplyMarkupObj = GetReplyMarkup(_sendOnlyTestGroup)
             }.Send("_");
            */
        }


        public void Form()
        {

        
            var statusGen = new GeneratorStatus(_sendOnlyTestGroupParam).GetParam();
            string messageStatus = "";
            if (statusGen == null)
            {
                Console.WriteLine("ParamLasZP вернул null. Последня заправка не заполнена");
                saveNote("");
                return;
            }
            else
            {
                var messageForecast = new StringBuilder();
                if (_schedule != null && statusGen.Balance_Hours != 0) //
                {

                    getTimeForecast(_schedule, statusGen.Balance_Hours, out DateTime dateStopGenStr, out string balanceTimeStr, out bool isCurrentDay);

                    if (dateStopGenStr != DateTime.MinValue)
                    {
                        messageForecast.Append(
                            "<b>Прогноз відповідно до графіків відключень:</b>\n" +
                               "⛔️ паливо скінчиться:\n" +
                              $"📅 {Api.GetCaptionDate(dateStopGenStr)}\n" +
                              $"🕒 ~ <b>{Api.TimeToStr(dateStopGenStr)}</b>\n");
                    }
                    else
                    {
                        messageForecast.Append(
                            "<b>Прогноз відповідно до графіків відключень:</b>\n" +
                           $"📅 <u>{(isCurrentDay ? "сьогодні" : "завтра")}</u>, в кінці дня, запас палива дозволить працювати генератору ще:\n " +
                           $"⏳ ~ <b>{balanceTimeStr}</b>\n");

                    }

                    messageForecast.Append("\n");

                    // Отправить сообщение об изменении графика 
                    messageForecast.Append("<b>Запланові відключення:</b>\n");
                    messageForecast.Append($"🗓️ {_schedule.ScheduleCurrentDay.GetCaptionDate()}\n");
                    messageForecast.Append(_schedule.ScheduleCurrentDay.GetPeriodStrForHtmlStatusGen() + "\n");
                    if (!_schedule.ScheduleNextDay.IsEmpty())
                    {
                        messageForecast.Append($"🗓️ {_schedule.ScheduleNextDay.GetCaptionDate()}\n");
                        messageForecast.Append(_schedule.ScheduleNextDay.GetPeriodStrForHtmlStatusGen() + "\n");
                    }

                    messageForecast.Append("\n");


                }

                messageStatus =
                    $"<b>Паливо в генераторі:</b>\n" +
                    $"⏳ вистачить на ~ <b>{statusGen.Balance_HoursStr}</b>\n" +
                    $"⛽️ залишилось ~ <b>{statusGen.Balance_LitersStr} л</b>\n" +
                    $"📉 і це складає <b>{statusGen.Balance_Percent}%</b>\n" +
                    "\n" +
                    messageForecast.ToString() +

                    $"<b>Остання заправка:</b>\n" +
                    $"📅 {Api.GetCaptionDate(statusGen.LastRefuel_DateTime)}\n" +
                    $"🕒 {Api.TimeToStr(statusGen.LastRefuel_DateTime)}\n" +
                    $"⚙️ відпрацював <b>{statusGen.AfterRefuel_HoursStr}</b>\n" +
                    $"🛢️ спожито палива ~ <b>{statusGen.AfterRefuel_LitersStr} л</b>\n" +
                    $"🙏 заправляв <b>{statusGen.LastRefuel_UserName}</b>\n" +
                    (!string.IsNullOrEmpty(statusGen.LastRefuel_UserCode) ? $"👤 <b>@{statusGen.LastRefuel_UserCode}</b>" : "") +
                    (statusGen.IsBalanceEmpty
                    ? "\n\n🚫 <i>P.S. Залишки палива по нулям. Можливо ще не внесли інформацію про заправку генератора</i> "
                    : "");

                var messageStatusToExcelTmp = messageStatus;
                if (!string.IsNullOrEmpty(statusGen.LastRefuel_UserCode))
                {
                    var refHtml = $"<a href=\"https://t.me/{statusGen.LastRefuel_UserCode}\" target=\"_blank\">t.me/{statusGen.LastRefuel_UserCode}</a>";
                    messageStatusToExcelTmp = messageStatus.Replace($"@{statusGen.LastRefuel_UserCode}", refHtml);
                }

                var messageToExcel =
                    $"<b>Показники станом на:</b>\n" +
                    $"📅 {Api.GetCaptionDate(Api.DateTimeUaCurrent)}\n " +
                    $"🕒 {Api.TimeToStr(Api.DateTimeUaCurrent)}\n" +
                    $"\n" +
                    messageStatusToExcelTmp;
                saveNote(messageToExcel);
            }

            decimal balanceHoursOld = getOldHours();




            if (statusGen.Balance_Hours >= 3)
            {
                Console.WriteLine("Баланс палива. В нормі і складає " + statusGen.Balance_Hours + " Відправлений показник " + balanceHoursOld);
                if (balanceHoursOld != 999)
                {
                    saveHours(999);
                }
                // Сообщение не нужно отправлять
            }
            else if (statusGen.Balance_Hours >= (decimal)0.5)
            {
                if (balanceHoursOld - statusGen.Balance_Hours >= 1)
                {
                    Console.WriteLine("Баланс палива. Повідомлення  відправлено. Старий баланс - " + balanceHoursOld + ", поточний баланс - " + statusGen.Balance_Hours);
                    // Отправить
                    saveHours(statusGen.Balance_Hours);


                    var messageTelegram =
                        $"🆘 <b>Потрібна заправка генератора</b>\n\n" +
                        messageStatus;

                    new SenderTelegram()
                    {
                        SendOnlyTestGroupParam = _sendOnlyTestGroupParam,
                        ReplyMarkupObj = GetReplyMarkup(_sendOnlyTestGroupParam)
                    }.Send(messageTelegram);

                }
                else
                {
                    // Уже было отправлено
                    Console.WriteLine("Баланс палива. Повідомлення БУЛО відправлено раніше при балансі " + balanceHoursOld + ", поточний баланс - " + statusGen.Balance_Hours);
                }

            }
            else
            {
                Console.WriteLine("Баланс палива. Повідомлення НЕ відправляємо - " + statusGen.Balance_Hours);
            }


        }

        private void saveHours(decimal hourse)
        {
            SpreadSheet.SetValue(_service, SpreadSheet.SheetNameFuelStatus, 2, _sendTestGroup ? 2 : 1, hourse.ToString());

        }


        private void saveNote(string note)
        {
            SpreadSheet.AddNote(_service, SpreadSheet.SheetNameFuelStatus, 1, _sendTestGroup ? 2 : 1, note);

        }

        private decimal getOldHours()
        {
           return SpreadSheet.GetValue<decimal>(_service, SpreadSheet.SheetNameFuelStatus, 2, _sendTestGroup ? 2 : 1);
        }


        public static string GetReplyMarkup(bool sendOnlyTestGroupParam)
        {

            var connect = new ConnectParam(sendOnlyTestGroupParam);

            string payload = Uri.EscapeDataString("IsTest=" + (connect.SendInTestGroup ? "Yes" : "No"));

            string miniAppLink1 = $"https://t.me/{connect.BotUsername}//?startapp={payload}";
            string miniAppLink2 = $"https://t.me/{connect.BotUsername}/onlinestatus/?startapp={payload}";

            var replyMarkupObj = new
            {
                inline_keyboard = new[]
                {
                            new[]
                            {
                                new
                                {
                                    text = "✍️ Внести показники",
                                    url = miniAppLink1   // ✅ ВАЖНО: url, НЕ web_app
                                }
                            },
                            new[]
                            {
                                new
                                {
                                    text = "📊 Online показники",
                                    url = miniAppLink2   // ✅ ВАЖНО: url, НЕ web_app
                                }
                            },
                        }
            };

            return JsonSerializer.Serialize(replyMarkupObj);

        }

        private static void getTimeForecast(Schedule schedule, decimal hours, out DateTime dateStopGenStr, out string balanceTimeStr, out bool isCurrentDay)
        {
            var hoursCuurent = hours;
            dateStopGenStr = DateTime.MinValue;
            balanceTimeStr = string.Empty;
            isCurrentDay = false;

            var dateTimeToResult = DateTime.MinValue;
            foreach (var scheduleDay in new[] { schedule.ScheduleCurrentDay, schedule.ScheduleNextDay })
            {
                foreach (var item in scheduleDay.Times)
                {
                    var dateTimeTo = scheduleDay.Date + item.End;
                    var dateTimeFrom = scheduleDay.Date + item.Start;
                    if (Api.DateTimeUaCurrent > dateTimeTo)
                    {
                        continue;
                    }

                    if (Api.DateTimeUaCurrent >= scheduleDay.Date + item.Start)
                    {
                        dateTimeFrom = Api.DateTimeUaCurrent;
                    }

                    dateTimeFrom = new[] { new DateTime(scheduleDay.Date.Year, scheduleDay.Date.Month, scheduleDay.Date.Day, 6, 0, 0), dateTimeFrom }.Max();
                    dateTimeTo = new[] { new DateTime(scheduleDay.Date.Year, scheduleDay.Date.Month, scheduleDay.Date.Day, 23, 0, 0), dateTimeTo }.Min();


                    var diff = (decimal)(dateTimeTo - dateTimeFrom).TotalHours;

                    if (diff<0)
                    {
                        continue;
                    }

                    if (hoursCuurent <= diff)
                    {
                        dateTimeToResult = dateTimeFrom + TimeSpan.FromHours((double)hoursCuurent);
                        break;
                    }
                    else
                    {
                        hoursCuurent = hoursCuurent - diff;
                    }
                }

                if (dateTimeToResult != DateTime.MinValue)
                {
                    break;
                }
            }

            if (dateTimeToResult != DateTime.MinValue)
            {
                dateStopGenStr = dateTimeToResult;
                // З Врахуванням графік відключень, генератор зупиниться в  dateTimeToResult
            }
            else
            {


                balanceTimeStr = Api.GetTimeHours(hoursCuurent, true);

                if (schedule.ScheduleNextDay.IsEmpty())
                {
                    isCurrentDay = true;
                    // З врахуванням графіку відключень, на кінець потого дня в залишку паливу в генераторі вистачить на balanceTimeStr 
                }
                else
                {
                    isCurrentDay = false;

                    // З врахуванням графіку відключень, на кінець завтрашнього дня в залишку паливу в генераторі вистачить на balanceTimeStr
                }

            }

        }


    }

}