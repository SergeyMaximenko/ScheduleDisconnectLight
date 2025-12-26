using Google.Apis.Sheets.v4;
using Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;


namespace ScheduleDisconnectLight
{


    public class GeneratorNotification
    {


        private SendType _sendType = SendType.Auto;

        private Schedule _schedule;
        private SheetsService _service;
        private bool _sendTestGroup;
        public GeneratorNotification(Schedule schedule)
        {
            _service = new SpreadSheet().GetService();
            _sendTestGroup = Api.SendTestGroup(_sendType);

            _schedule = schedule;

            // На всякий випадок, щоб не заспамити
            if (!Api.IsGitHub() && false)
            {
                var sendTypeTmp = SendType.OnlyTest;

                new SenderTelegram()
                {
                    SendInChatIdThreadAddition = true,
                    SendType = sendTypeTmp,
                    ReplyMarkupObj = GetReplyMarkup(sendTypeTmp, new[] { ReplyMarkup.ShowBonus })
                }.Send(
               "🔎 Натисніть кнопку нижче, щоб переглянути <b>нараховану винагороду</b> за заправку генератора\r\n\r\n" +
               "📌 <i>Ці дані завжди актуальні</i> ⬇️");

                new SenderTelegram()
                {
                    SendInChatIdThreadAddition = true,
                    SendType = sendTypeTmp,
                    ReplyMarkupObj = GetReplyMarkup(sendTypeTmp, new[] { ReplyMarkup.ShowIndicators })
                }.Send(
                "🔎 Натисніть кнопку нижче, щоб переглянути <b>залишки палива</b> та <b>прогноз його закінчення</b> відповідно до графіків відключення\r\n\r\n" +
                "📌 <i>Ці дані завжди актуальні</i> ⬇️");


                new SenderTelegram()
                {
                    SendInChatIdThreadAddition = true,
                    SendType = sendTypeTmp,
                    ReplyMarkupObj = GetReplyMarkup(sendTypeTmp, new[] { ReplyMarkup.SetIndicators })
                }.Send(
                "🔎 Натисніть кнопку нижче, щоб <b>внести</b> показники <b>заправки генератора</b>⬇️");

            }
          
        }


        public void Form()
        {

        
            var statusGen = new GeneratorStatus(_sendType).GetParam();
            string messageToTg = "";
            if (statusGen == null)
            {
                Console.WriteLine("ParamLasZP вернул null. Последня заправка не заполнена");
                saveNote("");
                return;
            }
            else
            {
                var datePower = SpreadSheet.GetValue<DateTime>(_service, SpreadSheet.SheetNameOnOffStatus, 1, 1);
                var dateGen = SpreadSheet.GetValue<DateTime>(_service, SpreadSheet.SheetNameOnOffStatus, 2, 1);
                var isPower = SpreadSheet.GetValue<int>(_service, SpreadSheet.SheetNameOnOffStatus, 1, 2) == 1;
                var isGen = SpreadSheet.GetValue<int>(_service, SpreadSheet.SheetNameOnOffStatus, 2, 2) == 1;


                var messageForecast = new StringBuilder();
                var messageSchedule = new StringBuilder();
                var messageBalanceGen = new StringBuilder();
                var messageLastRefuel = new StringBuilder();
                var messageStatusPower = new StringBuilder();
                var messageStatusGen = new StringBuilder();
                var messageStatusPowerGen = new StringBuilder();
                var messagePS = new StringBuilder();
                var messageDateIndicator = new StringBuilder();

                bool hasForecast = false;

                if (_schedule != null && statusGen.Balance_Hours != 0) //
                {

                    getTimeForecast(_schedule, statusGen.Balance_Hours, out hasForecast, out DateTime dateStopGenStr, out string balanceTimeStr, out bool isCurrentDay);

                    //-----
                    // ПРОГНОЗ
                    //-----
                    if (dateStopGenStr != DateTime.MinValue)
                    {
                        messageForecast.Append(
                               $"<b>Прогноз відповідно до графіків відключень:</b>\n" +
                               $"⛔️ паливо скінчиться:\n" +
                               $"🕒 ~ <b>{Api.TimeToStr(dateStopGenStr)}</b>\n" +
                               $"📅 {Api.GetCaptionDate(dateStopGenStr)}\n");

                    }
                    else
                    {
                        messageForecast.Append(
                           $"<b>Прогноз відповідно до графіків відключень:</b>\n" +
                           $"📅 <u>{(isCurrentDay ? "сьогодні" : "завтра")}</u>, в кінці дня, запас палива дозволить працювати генератору ще:\n" +
                           $"⏳ ~ <b>{balanceTimeStr}</b>\n");

                    }


                    //-----
                    // ГРАФИКИ
                    //-----
                    messageSchedule.Append(
                        "<b>Запланові відключення:</b>\n" +
                        $"🗓️ {_schedule.ScheduleCurrentDay.GetCaptionDate()}\n" +
                        _schedule.ScheduleCurrentDay.GetPeriodStrForHtmlStatusGen() + "\n");

                    if (!_schedule.ScheduleNextDay.IsEmpty())
                    {
                        messageSchedule.Append(
                            $"🗓️ {_schedule.ScheduleNextDay.GetCaptionDate()}\n" +
                            _schedule.ScheduleNextDay.GetPeriodStrForHtmlStatusGen() + "\n");
                    }


                }

                messageBalanceGen.Append(
                    $"<b>Паливо в генераторі:</b>\n" +
                    $"⏳ вистачить на ~ <b>{statusGen.Balance_HoursStr}</b>\n" +
                    $"⛽️ залишилось ~ <b>{statusGen.Balance_LitersStr} л</b>\n" +
                    $"📉 і це складає <b>{statusGen.Balance_Percent}%</b>\n");



                messageLastRefuel.Append(
                    $"<b>Остання заправка:</b>\n" +
                    $"📅 {Api.GetCaptionDate(statusGen.LastRefuel_DateTime)}\n" +
                    $"🕒 {Api.TimeToStr(statusGen.LastRefuel_DateTime)}\n" +
                    $"⚙️ відпрацював <b>{statusGen.AfterRefuel_HoursStr}</b>\n" +
                    $"🛢️ спожито палива ~ <b>{statusGen.AfterRefuel_LitersStr} л</b>\n" +
                    $"🙏 заправляв <b>{statusGen.LastRefuel_UserName}</b>\n" +
                    (!string.IsNullOrEmpty(statusGen.LastRefuel_UserCode) ? $"👤 <b>@{statusGen.LastRefuel_UserCode}</b>\n" : ""));



                if (statusGen.IsBalanceEmpty)
                {
                    messagePS.Append("🚫 <i>P.S. Залишки палива по нулям. Можливо ще не внесли інформацію про заправку генератора</i>");

                }

                string replaceUserToHtml(StringBuilder message)
                {
                    if (string.IsNullOrEmpty(statusGen.LastRefuel_UserCode))
                    {
                        return message.ToString();
                    }
                    var refHtml = $"<a href=\"https://t.me/{statusGen.LastRefuel_UserCode}\" target=\"_blank\">t.me/{statusGen.LastRefuel_UserCode}</a>";
                    return message.ToString().Replace($"@{statusGen.LastRefuel_UserCode}", refHtml);
                }


 

                messageStatusPower.AppendLine(
                     (isPower
                     ? "✅💡 <b>Світло є</b>\n"+
                       "🕒 було включено в <b>" + Api.TimeToStr(datePower) + "</b>\n"
                     : "❌💡 <b>Світло відсутнє</b>\n" +
                       "🕒 було виключено в <b>" + Api.TimeToStr(datePower) + "</b>\n") +
                    "📅 " + Api.GetCaptionDate(datePower)+ "\n");

                messageStatusGen.AppendLine(
                     (isGen
                     ? "✅🔋 <b>Генератор працює</b>\n" +
                       "🕒 запустився в <b>" + Api.TimeToStr(dateGen) + "</b>\n"
                     : "❌🔋 <b>Генератор зупинений</b>\n" +
                       "🕒 зупинився в <b>" + Api.TimeToStr(dateGen) + "</b>\n") +
                    "📅 " + Api.GetCaptionDate(dateGen) + "\n");



                messageDateIndicator.Append(
                     $"<b>Показники станом на:</b>\n" +
                     $"📅 {Api.GetCaptionDate(Api.DateTimeUaCurrent)}\n " +
                     $"🕒 {Api.TimeToStr(Api.DateTimeUaCurrent)}\n");


                var messageToExcel = concatMessage(
                    messageDateIndicator, 
                    messageBalanceGen, 
                    messageForecast,
                    messageStatusPower,
                    messageStatusGen,
                    messageSchedule,
                    replaceUserToHtml(messageLastRefuel), 
                    messagePS);

                saveNote(messageToExcel);

                messageToTg = concatMessage(
                    messageBalanceGen, 
                    hasForecast ? messageForecast.ToString() : string.Empty,
                    hasForecast ? messageSchedule.ToString() : string.Empty,
                    messageLastRefuel);
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
                        messageToTg;

                    new SenderTelegram()
                    {
                        SendType = _sendType,
                        ReplyMarkupObj = GetReplyMarkup(_sendType, new[] {ReplyMarkup.SetIndicators, ReplyMarkup.ShowIndicators})
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

        private string concatMessage(params object[] message)
        {
            return string.Join("\n", message.Where(t => !string.IsNullOrEmpty(t.ToString())));
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


        public static string GetReplyMarkup(SendType sendType, ReplyMarkup[] replyMarkups)
        {

            var connect = new ConnectParam(sendType);
            string payload = Uri.EscapeDataString("IsTest=" + (connect.SendInTestGroup ? "Yes" : "No"));

            string miniAppLink1 = $"https://t.me/{connect.BotUsername}//?startapp={payload}";
            string miniAppLink2 = $"https://t.me/{connect.BotUsername}/onlinestatus/?startapp={payload}";
            string miniAppLink3 = $"https://t.me/{connect.BotUsername}/bonus/?startapp={payload}";


            var inline_keyboard = new List<object>();

            if (replyMarkups.Contains(ReplyMarkup.SetIndicators))
            {
                inline_keyboard.Add(
                    new[]
                            {
                                new
                                {
                                    text = "✍️ Внести показники",
                                    url = miniAppLink1   // ✅ ВАЖНО: url, НЕ web_app
                                }
                            }
                    );
            }

            if (replyMarkups.Contains(ReplyMarkup.ShowIndicators))
            {
                inline_keyboard.Add(
                    new[]
                            {
                                new
                                {
                                    text = "📊 Online показники",
                                    url = miniAppLink2   // ✅ ВАЖНО: url, НЕ web_app
                                }
                            }
                    );
            }
            if (replyMarkups.Contains(ReplyMarkup.ShowBonus))
            {
                inline_keyboard.Add(
                    new[]
                            {
                                new
                                {
                                    text = "💰 Винагорода",
                                    url = miniAppLink3   // ✅ ВАЖНО: url, НЕ web_app
                                }
                            }
                    );
            }


            var replyMarkupObj = new
            {
                inline_keyboard
            };

            return JsonSerializer.Serialize(replyMarkupObj);

        }

        private static void getTimeForecast(Schedule schedule, decimal hours, out bool hasForecast,  out DateTime dateStopGenStr, out string balanceTimeStr, out bool isCurrentDay)
        {
            hasForecast = false;
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
                        hasForecast = true;
                        dateTimeToResult = dateTimeFrom + TimeSpan.FromHours((double)hoursCuurent);
                        break;
                    }
                    else
                    {
                        hasForecast = true;
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
    public enum ReplyMarkup
    {
        SetIndicators,
        ShowIndicators,
        ShowBonus
    }

}