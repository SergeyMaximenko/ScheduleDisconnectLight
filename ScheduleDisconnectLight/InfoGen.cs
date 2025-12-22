using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static ScheduleDisconnectLight.Api;

namespace ScheduleDisconnectLight
{


    public class InfoGen
    {


        private bool _sendOnlyTestGroup = false;


        private SheetsService _service;
        private bool _isTest;
        public InfoGen()
        {
            _service = new SpreadSheet().Get();
            _isTest = Api.SendOnlyTestGroup(_sendOnlyTestGroup);

           /*
            new SenderTelegram()
            {
                SendOnlyTestGroup = _sendOnlyTestGroup,
                ReplyMarkupObj = GetReplyMarkup(_sendOnlyTestGroup)
            }.Send("_");
           */
        }


        public void Check()
        {


            var paramZP = new ParamLasZP(_sendOnlyTestGroup).GetParam();
            string messageStatus = "";
            if (paramZP == null)
            {
                Console.WriteLine("ParamLasZP вернул null. Последня заправка не заполнена");
                saveNote("");
                return;
            }
            else
            {
                messageStatus =
                    $"<b>Палива в генераторі:</b>\n" +
                    $"⏳ Вистачить на ~ <b>{paramZP.BalanceHours_Str}</b>\n" +
                    $"⛽️ Залишилось ~ <b>{paramZP.BalanceLiters} л</b>\n" +
                    $"📉 і це складає <b>{paramZP.BalancePercent}%</b>\n" +
                    "\n" +
                    $"<b>Остання заправка:</b>\n" +
                    $"📅 {Api.GetCaptionDate(paramZP.LastZP_DateTime) }\n" +
                    $"🕒 {Api.TimeToStr(paramZP.LastZP_DateTime)}\n" +
                    $"⚙️ Відпрацював <b>{paramZP.ExecHours_Str}</b>\n" +
                    $"🛢️ Спожито палива ~ <b>{paramZP.ExecLiters} л</b>\n" +
                    $"🙏 Заправляв <b>{paramZP.LastZP_UserName}</b>\n" +
                    (!string.IsNullOrEmpty(paramZP.LastZP_UserCode) ? $"👤 <b>@{paramZP.LastZP_UserCode}</b>" : "") +
                    (paramZP.IsBalanceEmpty
                    ? "\n\n🚫 <i>P.S. Залишки палива по нулям. Можливо ще не внесли інформацію про заправку генератора</i> "
                    : "");

                var messageStatusToExcelTmp = messageStatus;
                if (!string.IsNullOrEmpty(paramZP.LastZP_UserCode))
                {
                    var refHtml = $"<a href=\"https://t.me/{paramZP.LastZP_UserCode}\" target=\"_blank\">t.me/{paramZP.LastZP_UserCode}</a>";
                    messageStatusToExcelTmp = messageStatus.Replace($"@{paramZP.LastZP_UserCode}", refHtml);
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
           


            
            if (paramZP.BalanceHours >= 3) 
            {
                Console.WriteLine("Баланс палива. В нормі і складає " + paramZP.BalanceHours+ " Відправлений показник "+ balanceHoursOld);
                if (balanceHoursOld !=999)
                {
                    saveHours(999);
                }
                // Сообщение не нужно отправлять
            }
            else if (paramZP.BalanceHours >= (decimal)0.5)
            {
                if (balanceHoursOld - paramZP.BalanceHours >= 1) 
                {
                    Console.WriteLine("Баланс палива. Повідомлення  відправлено. Старий баланс - " + balanceHoursOld + ", поточний баланс - " + paramZP.BalanceHours);
                    // Отправить
                    saveHours(paramZP.BalanceHours);


                    var messageTelegram =
                        $"🆘 <b>Потрібна заправка генератора</b>\n\n" +
                        messageStatus;

                    new SenderTelegram()
                    {
                        SendOnlyTestGroup = _sendOnlyTestGroup,
                        ReplyMarkupObj = GetReplyMarkup(_sendOnlyTestGroup)
                    }.Send(messageTelegram);

                }
                else
                {
                    // Уже было отправлено
                    Console.WriteLine("Баланс палива. Повідомлення БУЛО відправлено раніше при балансі " + balanceHoursOld+", поточний баланс - " + paramZP.BalanceHours);
                }

            }
            else
            {
                Console.WriteLine("Баланс палива. Повідомлення не відправляємо - " + paramZP.BalanceHours);
            }


        }

        private void saveHours(decimal hourse)
        {
            
            if (_isTest)
            {
                SpreadSheet.SetValue(_service, "ЗаправкаСтатус", 2, 2, hourse.ToString());
            }
            else
            {
                SpreadSheet.SetValue(_service, "ЗаправкаСтатус", 2, 1, hourse.ToString());
            }

        }


        private void saveNote(string note)
        {


            if (_isTest)
            {
                SpreadSheet.AddNote(_service, "ЗаправкаСтатус", 1, 2, note);
            }
            else
            {
                SpreadSheet.AddNote(_service, "ЗаправкаСтатус", 1, 1, note);
            }

        }

        private decimal getOldHours()
        {

            if (_isTest)
            {
                return ParamLasZP.ConverValue<decimal>(SpreadSheet.GetValue(_service, "ЗаправкаСтатус", 2, 2));
            }
            else
            {
                return ParamLasZP.ConverValue<decimal>(SpreadSheet.GetValue(_service, "ЗаправкаСтатус", 2, 1));
            }


        }


        public static string GetReplyMarkup(bool sendOnlyTestGroup)
        {

            var connect = new ConnectParam(sendOnlyTestGroup);

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
    }

}
