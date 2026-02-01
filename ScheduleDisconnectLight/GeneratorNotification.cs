using Google.Apis.Sheets.v4;
using Service;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Text.Json;
using static ScheduleDisconnectLight.GeneratorStatus;


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
            if (!Api.IsGitHub() && 1 == 0) 
            {
                var sendTypeTmp = SendType.OnlyTest;






                
                new SenderTelegram()
                {
                    SendInChatIdThreadAddition = true,
                    SendType = sendTypeTmp,
                    ReplyMarkupObj = GetReplyMarkup(sendTypeTmp, new[] { ReplyMarkup.Moto })
                }.Send(
                "🔎 Натисніть кнопку нижче, щоб <b>актуалізувати мотогодини</b> генератора⬇️");

                new SenderTelegram()
                {
                    SendInChatIdThreadAddition = true,
                    SendType = sendTypeTmp,
                    ReplyMarkupObj = GetReplyMarkup(sendTypeTmp, new[] { ReplyMarkup.TehService })
                }.Send(
                "🔎 Натисніть кнопку нижче, щоб <b>внести</b> показники <b>ТО</b>⬇️");

             

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
                    ReplyMarkupObj = GetReplyMarkup(sendTypeTmp, new[] { ReplyMarkup.Refuel })
                }.Send(
                "🔎 Натисніть кнопку нижче, щоб <b>внести</b> показники <b>заправки генератора</b>⬇️");

                new SenderTelegram()
                {
                    SendInChatIdThreadAddition = true,
                    SendType = sendTypeTmp,
                    ReplyMarkupObj = GetReplyMarkup(sendTypeTmp, new[] { ReplyMarkup.ShowIndicators })
                }.Send(
                 "🔎 Натисніть кнопку нижче, щоб переглянути <b>залишки палива</b>, <b>прогноз його закінчення</b> та <b>показники ТО</b>\r\n\r\n" +
                 "📌 <i>Ці дані завжди актуальні</i> ⬇️");
                
                new SenderTelegram()
                {
                    SendInChatIdThreadAddition = true,
                    SendType = sendTypeTmp,
                    ReplyMarkupObj = GetReplyMarkup(sendTypeTmp, new[] { ReplyMarkup.AvgRefuel })
                }.Send(
 "🔎 Натисніть кнопку нижче, щоб встановити <b>середній прогнозний</b> показник <b>витрат палива</b> ⬇️");


            }

        }


        public void Form()
        {

            var generatorStatus = new GeneratorStatus(_sendType);
            var statusGenRefuel = generatorStatus.GetParamRefuel();
            var statusGenTehService = generatorStatus.GetParamTehService();


            string messageToTgRefuel = "";
            string messageToTgTehService = "";

            var datePower = SpreadSheet.GetValue<DateTime>(_service, SpreadSheet.SheetNameOnOffStatus, 1, 1);
            var dateGen = SpreadSheet.GetValue<DateTime>(_service, SpreadSheet.SheetNameOnOffStatus, 2, 1);
            var isPower = SpreadSheet.GetValue<int>(_service, SpreadSheet.SheetNameOnOffStatus, 1, 2) == 1;
            var isGen = SpreadSheet.GetValue<int>(_service, SpreadSheet.SheetNameOnOffStatus, 2, 2) == 1;



            var messageForecastTmp = "";
            var messageSchedule = new StringBuilder();
            var messageBalanceGen = new StringBuilder();
            var messageLastRefuelExec = new StringBuilder();
            var messageTehService = new StringBuilder();
            var messageLastRefuel = new StringBuilder();
            var messageStatusPower = new StringBuilder();
            var messageStatusGen = new StringBuilder();
            var messageStatusPowerGen = new StringBuilder();
            var messagePS = new StringBuilder();
            var messageDateIndicator = new StringBuilder();

            var messageSetParam = new StringBuilder();

            bool hasForecast = false;

            if (_schedule != null && statusGenRefuel != null && statusGenRefuel.Refuel_Balance_Hours != 0 && !_schedule.IsEmergencyShutdowns) //
            {

                getTimeForecast(_schedule, statusGenRefuel.Refuel_Balance_Hours, out hasForecast, out DateTime dateStopGenStr, out string balanceTimeStr, out bool isCurrentDay);

                //-----
                // ПРОГНОЗ
                //-----
                if (dateStopGenStr != DateTime.MinValue)
                {
                    //
                    messageForecastTmp = $"⛔️ якщо враховувати наявні графіки відключення, паливо скінчиться ~ <b>{Api.GetCaptionDateTimeShort(dateStopGenStr)}</b>\n";
                }
                else
                {
                    //
                    messageForecastTmp = $"⏳ якщо враховувати наявні графіки відключення, {(isCurrentDay ? "сьогодні" : "завтра")}, в кінці дня, запасу палива вистачить ще на ~ <b>{balanceTimeStr}</b>\n";

                }


                //-----
                // ГРАФИКИ
                //-----
                messageSchedule.Append(
                    "<b>Запланові відключення:</b>\n" +
                    $"🗓️ <b>{_schedule.ScheduleCurrentDay.GetCaptionDate()}</b>\n" +
                    _schedule.ScheduleCurrentDay.GetPeriodStrForHtmlStatusGen() + "\n");

                if (!_schedule.ScheduleNextDay.IsEmpty())
                {
                    messageSchedule.Append(
                        $"🗓️ <b>{_schedule.ScheduleNextDay.GetCaptionDate()}</b>\n" +
                        _schedule.ScheduleNextDay.GetPeriodStrForHtmlStatusGen() + "\n");
                }

            }



            if (statusGenRefuel != null)
            {
                messageSetParam.Append(
                    $"<b>Параметри прогнозу:</b>\n" +
                    $"📈 середній розхід - <b>{ParamRefuel._liter1Horse.ToString("0.##")} л/год</b>\n" +
                    $"⛽️ об'єм бака - <b>{ParamRefuel._totalLitersInGenerator.ToString("0.##")} л</b>\n" +
                    $"⏳ повного бака вистачить ~ <b>{Api.GetTimeHours(ParamRefuel._totalLitersInGenerator / ParamRefuel._liter1Horse, true)}</b>\n");


                string messageNonStopTmp= "";
                if (isGen && statusGenRefuel != null && statusGenRefuel.Refuel_Balance_Hours >= 1)
                {
                    var dateTimeStopGen = Api.DateTimeUaCurrent.AddHours((double)Math.Round(statusGenRefuel.Refuel_Balance_Hours, 3));
                    
                    messageNonStopTmp = $"⛔️ якщо генератор буде працювати без зупинок, паливо скінчиться ~ <b>{Api.GetCaptionDateTimeShort(dateTimeStopGen)}</b>\n";
                }

                
                messageBalanceGen.Append(
                    $"<b>Коли скінчиться паливо:</b>\n" +
                    $"⏳ вистачить на ~ <b>{statusGenRefuel.Refuel_Balance_HoursStr}</b>\n" +
                    messageNonStopTmp +
                    messageForecastTmp +
                    $"⛽️ залишок у баку ~ <b>{statusGenRefuel.Refuel_Balance_LitersStr} л (≈ {statusGenRefuel.Refuel_Balance_Percent}%)</b>\n");



                messageLastRefuelExec.Append(
                    $"<b>Після останньої заправки:</b>\n" +
                    $"⚙️ відпрацював - <b>{statusGenRefuel.Refuel_ExecAfter_HoursStr}</b>\n" +
                    $"⛽️ спожито палива ~ <b>{statusGenRefuel.Refuel_ExecAfter_LitersStr} л</b>\n");
                    
                    


                messageLastRefuel.Append(
                    $"<b>Остання заправка:</b>\n" +
                    $"📅 {Api.GetCaptionDate(statusGenRefuel.Refuel_Last_DateTime)}\n" +
                    $"🕒 {Api.TimeToStr(statusGenRefuel.Refuel_Last_DateTime)}\n" +
                    $"🙏 заправляв <b>{statusGenRefuel.Refuel_Last_UserName}</b>\n" +
                    (!string.IsNullOrEmpty(statusGenRefuel.Refuel_Last_UserCode) ? $"👤 <b>@{statusGenRefuel.Refuel_Last_UserCode}</b>\n" : ""));



                if (statusGenRefuel.Refuel_Balance_IsEmptyHours)
                {
                    messagePS.Append("🚫 <i>P.S. Залишки палива по нулям. Можливо ще не внесли інформацію про заправку генератора</i>");

                }
            }


            if (statusGenTehService != null)
            {

                messageTehService.Append(
                    $"<b>Показники по ТО:</b>\n" +
                    (statusGenTehService.TehService_Balance_Hours < 10 ? "🆘 УВАГА! Критичний рівень залишку годин для ТО\n" : "") +
                    $"⏳ всього мотогодин <b>{statusGenTehService.TehService_ExecAll_HoursStr}</b>\n" +
                    $"⚙️ відпрацював після ТО <b>{statusGenTehService.TehService_ExecAfter_HoursStr}</b>\n" +
                    $"⚖️ норма для ТО <b>{Api.GetTimeHours(statusGenTehService._totalHoursTehService, true)}</b>\n" +
                    $"⏳ до наступного ТО ~ <b>{statusGenTehService.TehService_Balance_HoursStr}</b> (≈ {statusGenTehService.TehService_Balance_Percent}%)\n");
                    
                    

                messageTehService.Append("\n");

                messageTehService.Append(
                    $"<b>Останнє ТО:</b>\n" +
                    $"📅 {Api.GetCaptionDate(statusGenTehService.TehService_Last_DateTime)}\n" +
                    $"🕒 {Api.TimeToStr(statusGenTehService.TehService_Last_DateTime)}\n" +
                    $"🙏 контролював <b>{statusGenTehService.TehService_Last_UserName}</b>\n" +
                    (!string.IsNullOrEmpty(statusGenTehService.TehService_Last_UserCode) ? $"👤 <b>@{statusGenTehService.TehService_Last_UserCode}</b>\n" : ""));
            }

            string replaceUserToHtml(StringBuilder message)
            {
                var messageResult = message.ToString();
                if (statusGenRefuel != null && !string.IsNullOrEmpty(statusGenRefuel.Refuel_Last_UserCode))
                {
                    var refHtml = $"<a href=\"https://t.me/{statusGenRefuel.Refuel_Last_UserCode}\" target=\"_blank\">t.me/{statusGenRefuel.Refuel_Last_UserCode}</a>";
                    messageResult = messageResult.Replace($"@{statusGenRefuel.Refuel_Last_UserCode}", refHtml);
                }

                if (statusGenTehService != null && !string.IsNullOrEmpty(statusGenTehService.TehService_Last_UserCode))
                {
                    var refHtml = $"<a href=\"https://t.me/{statusGenTehService.TehService_Last_UserCode}\" target=\"_blank\">t.me/{statusGenTehService.TehService_Last_UserCode}</a>";
                    messageResult = messageResult.ToString().Replace($"@{statusGenTehService.TehService_Last_UserCode}", refHtml);
                }

                return messageResult.ToString();


            }




            messageStatusPower.Append(
                 (isPower
                 ? "✅💡 <b>Світло є</b>\n" +
                   "🕒 було включено в <b>" + Api.TimeToStr(datePower) + "</b>\n"
                 : "❌💡 <b>Світло відсутнє</b>\n" +
                   "🕒 було виключено в <b>" + Api.TimeToStr(datePower) + "</b>\n") +
                "📅 " + Api.GetCaptionDate(datePower) + "\n");

            messageStatusGen.Append(
                 (isGen
                 ? "✅🔋 <b>Генератор працює</b>\n" +
                   "🕒 запустився в <b>" + Api.TimeToStr(dateGen) + "</b>\n"
                 : "❌🔋 <b>Генератор відпочиває</b>\n" +
                   "🕒 зупинився в <b>" + Api.TimeToStr(dateGen) + "</b>\n") +
                "📅 " + Api.GetCaptionDate(dateGen) + "\n");



            messageDateIndicator.Append(
                 $"<b>Показники станом на:</b>\n" +
                 $"📅 {Api.GetCaptionDate(Api.DateTimeUaCurrent)}\n " +
                 $"🕒 {Api.TimeToStr(Api.DateTimeUaCurrent)}\n");


            var messageSaveIndicatorsToExcel = concatMessage(
                messageDateIndicator.ToString(),
                messageSetParam.ToString(),
                messageBalanceGen.ToString(),
                messageSchedule.ToString(),
                messageLastRefuelExec.ToString(),
                replaceUserToHtml(messageLastRefuel).ToString(),
                messageStatusPower.ToString(),
                messageStatusGen.ToString(),
                replaceUserToHtml(messageTehService).ToString(),
                messagePS.ToString());

            saveNote(messageSaveIndicatorsToExcel);

            messageToTgRefuel = concatMessage(
                messageBalanceGen.ToString(),
                messageLastRefuel.ToString());

            messageToTgTehService = concatMessage(
                messageTehService.ToString()); 




            if (statusGenRefuel != null)
            {
                decimal balanceHoursOld = getHoursRefuel();

                if (statusGenRefuel.Refuel_Balance_Hours >= 3)
                {
                    Console.WriteLine("Баланс палива на заправку. В нормі і складає " + statusGenRefuel.Refuel_Balance_Hours + " Відправлений показник " + balanceHoursOld);
                    if (balanceHoursOld != 999)
                    {
                        saveHoursRefuel(999);
                    }
                    // Сообщение не нужно отправлять
                }
                else if (statusGenRefuel.Refuel_Balance_Hours >= (decimal)0.5)
                {
                    if (balanceHoursOld - statusGenRefuel.Refuel_Balance_Hours >= 1)
                    {
                        Console.WriteLine("Баланс палива на заправку. Повідомлення  відправлено. Старий баланс - " + balanceHoursOld + ", поточний баланс - " + statusGenRefuel.Refuel_Balance_Hours);
                        // Отправить
                        saveHoursRefuel(statusGenRefuel.Refuel_Balance_Hours);


                        var messageTelegram =
                            $"🆘 <b>Потрібна заправка генератора</b>\n\n" +
                            messageToTgRefuel;

                        new SenderTelegram()
                        {
                            SendType = _sendType,
                            ReplyMarkupObj = GetReplyMarkup(_sendType, new[] { ReplyMarkup.Refuel, ReplyMarkup.ShowIndicators })
                        }.Send(messageTelegram);

                    }
                    else
                    {
                        // Уже было отправлено
                        Console.WriteLine("Баланс палива на заправку. Повідомлення БУЛО відправлено раніше при балансі " + balanceHoursOld + ", поточний баланс - " + statusGenRefuel.Refuel_Balance_Hours);
                    }

                }
                else
                {
                    Console.WriteLine("Баланс палива на заправку. Повідомлення НЕ відправляємо. Залишок палива " + statusGenRefuel.Refuel_Balance_Hours);
                }
            }

            if (statusGenTehService != null)
            {
                decimal balanceHoursOld = getHoursTehService();

                if (statusGenTehService.TehService_Balance_Hours >= 75)
                {
                    Console.WriteLine("Баланс годин на ТО. В нормі і складає " + statusGenTehService.TehService_Balance_Hours + " Відправлений показник " + balanceHoursOld);
                    if (balanceHoursOld != 999)
                    {
                        saveHoursTehService(999);
                    }
                    // Сообщение не нужно отправлять
                }
                else if (statusGenTehService.TehService_Balance_Hours >= 1)
                {
                    if (balanceHoursOld - statusGenTehService.TehService_Balance_Hours >= 25)
                    {
                        Console.WriteLine("Баланс палива на ТО. Повідомлення  відправлено. Старий баланс - " + balanceHoursOld + ", поточний баланс - " + statusGenTehService.TehService_Balance_Hours);
                        // Отправить
                        saveHoursTehService(statusGenTehService.TehService_Balance_Hours);


                        var messageTelegram =
                            $"⚠️ <b>Потрібно планувати тех.обслуговування генератора</b>\n\n" +
                            messageToTgTehService;

                        new SenderTelegram()
                        {
                            SendType = _sendType,
                            ReplyMarkupObj = GetReplyMarkup(_sendType, new[] { ReplyMarkup.TehService, ReplyMarkup.ShowIndicators })
                        }.Send(messageTelegram);

                    }
                    else
                    {
                        // Уже было отправлено
                        Console.WriteLine("Баланс палива на ТО. Повідомлення БУЛО відправлено раніше при балансі " + balanceHoursOld + ", поточний баланс - " + statusGenTehService.TehService_Balance_Hours);
                    }

                }
                else
                {
                    Console.WriteLine("Баланс палива на ТО. Повідомлення НЕ відправляємо - " + statusGenTehService.TehService_Balance_Hours);
                }
            }


        }

        private string concatMessage(params string[] message)
        {
            return string.Join("\n", message.Where(t => !string.IsNullOrEmpty(t.ToString())));
        }


        private void saveHoursRefuel(decimal hourse)
        {
            SpreadSheet.SetValue(_service, SpreadSheet.SheetNameFuelStatus, 2, _sendTestGroup ? 2 : 1, hourse.ToString());

        }
        private decimal getHoursRefuel()
        {
            return SpreadSheet.GetValue<decimal>(_service, SpreadSheet.SheetNameFuelStatus, 2, _sendTestGroup ? 2 : 1);
        }


        private void saveHoursTehService(decimal hourse)
        {
            SpreadSheet.SetValue(_service, SpreadSheet.SheetNameFuelStatus, 3, _sendTestGroup ? 2 : 1, hourse.ToString());

        }
        private decimal getHoursTehService()
        {
            return SpreadSheet.GetValue<decimal>(_service, SpreadSheet.SheetNameFuelStatus, 3, _sendTestGroup ? 2 : 1);
        }


        private void saveNote(string note)
        {
            SpreadSheet.AddNote(_service, SpreadSheet.SheetNameFuelStatus, 1, _sendTestGroup ? 2 : 1, note);

        }




        public static string GetReplyMarkup(SendType sendType, ReplyMarkup[] replyMarkups)
        {

            var connect = new ConnectParam(sendType);
            string payload = Uri.EscapeDataString("IsTest=" + (connect.SendInTestGroup  ? "Yes" : "No"));

            string miniAppLink1 = $"https://t.me/{connect.BotUsername}//?startapp={payload}";
            string miniAppLink2 = $"https://t.me/{connect.BotUsername}/onlinestatus/?startapp={payload}";
            string miniAppLink3 = $"https://t.me/{connect.BotUsername}/bonus/?startapp={payload}";
            string miniAppLink4 = $"https://t.me/{connect.BotUsername}/tehservice/?startapp={payload}_IsTO=Yes";
            string miniAppLink5 = $"https://t.me/{connect.BotUsername}/setmoto/?startapp={payload}";
            string miniAppLink6 = $"https://t.me/{connect.BotUsername}/avgrefuel/?startapp={payload}";

            var inline_keyboard = new List<object>();

            if (replyMarkups.Contains(ReplyMarkup.Refuel))
            {
                inline_keyboard.Add(
                    new[]
                            {
                                new
                                {
                                    text = "⛽️ Заправка генератора",
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
            if (replyMarkups.Contains(ReplyMarkup.TehService))
            {
                inline_keyboard.Add(
                    new[]
                            {
                                new
                                {
                                    text = "🔧 Тех.обслуговування",
                                    url = miniAppLink4   // ✅ ВАЖНО: url, НЕ web_app
                                }
                            }
                    );
            }
            if (replyMarkups.Contains(ReplyMarkup.Moto))
            {
                inline_keyboard.Add(
                    new[]
                            {
                                new
                                {
                                    text = "🔄 Актуалізація мотогодин",
                                    url = miniAppLink5   // ✅ ВАЖНО: url, НЕ web_app
                                }
                            }
                    );
            }
            if (replyMarkups.Contains(ReplyMarkup.AvgRefuel))
            {
                inline_keyboard.Add(
                    new[]
                            {
                                new
                                {
                                    text = "📈 Встановити прогноз витрат на паливо",
                                    url = miniAppLink6   // ✅ ВАЖНО: url, НЕ web_app
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

        private static void getTimeForecast(Schedule schedule, decimal hours, out bool hasForecast, out DateTime dateStopGenStr, out string balanceTimeStr, out bool isCurrentDay)
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


                     /// Ось це відкрити, якщо будуть працювати графіки з 6 до 23-00
                    //dateTimeFrom = new[] { new DateTime(scheduleDay.Date.Year, scheduleDay.Date.Month, scheduleDay.Date.Day, 6, 0, 0), dateTimeFrom }.Max();
                    //dateTimeTo = new[] { new DateTime(scheduleDay.Date.Year, scheduleDay.Date.Month, scheduleDay.Date.Day, 23, 0, 0), dateTimeTo }.Min();


                    var diff = (decimal)(dateTimeTo - dateTimeFrom).TotalHours;

                    if (diff < 0)
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
        Refuel,
        ShowIndicators,
        ShowBonus,
        TehService,
        Moto,
        AvgRefuel
    }

}