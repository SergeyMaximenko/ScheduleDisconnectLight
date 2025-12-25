using System;
using System.Collections.Generic;
using System.Net.Http;


namespace Service
{
    public class SenderTelegram
    {

        public SenderTelegram()
        {
            SendType = SendType.Auto;
        }
        public SendType SendType  { get; set; }

        /// <summary>
        /// Отправить сообщение в доп. ветку
        /// </summary>
        public bool SendInChatIdThreadAddition { get; set; }


        public string ReplyMarkupObj { get; set; }

        public void Send(string message)
        {
            var connect = new ConnectParam(SendType);



            using (var httpClient = new HttpClient())
            {
                string url = $"https://api.telegram.org/bot{connect.BotToken}/sendMessage";

                var data = new Dictionary<string, string>
                    {
                        { "chat_id", connect.ChatId },
                        { "message_thread_id", SendInChatIdThreadAddition ? connect.ChatIdThreadAdditional : connect.ChatIdThread },
                        { "text", message },
                        { "parse_mode", "HTML"}
                    };

                if (!string.IsNullOrEmpty(ReplyMarkupObj))
                {
                    data.Add("reply_markup", ReplyMarkupObj);
                }

                Console.WriteLine("START SEND TELEGRAM:");
                Console.WriteLine(message);
                Console.WriteLine("END SEND TELEGRAM:");



                using (var content = new FormUrlEncodedContent(data))
                {
                    // Синхронный POST
                    HttpResponseMessage response = httpClient.PostAsync(url, content).Result;




                    var responseString = response.Content.ReadAsStringAsync().Result;

                    if (response.IsSuccessStatusCode)
                    {
                        var idMessage = new Json(responseString)["result"]["message_id"].ValueInt;
                    }
                    else
                    {
                        Console.WriteLine(new Json(responseString)["description"].Value);

                        // Бросит исключение, если статус не 2xx
                        response.EnsureSuccessStatusCode();



                    }

                }
            }
        }


    }
}
