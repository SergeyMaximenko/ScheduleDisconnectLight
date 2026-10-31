using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Service
{
    public class SenderTelegram
    {
        public SenderTelegram() { SendType = SendType.Auto; }
        public SendType SendType { get; set; }
        public bool SendInChatIdThreadAddition { get; set; }
        public string ReplyMarkupObj { get; set; }
        public byte[] Photo { get; set; }

        public void Send(string message)
        {
            if (Photo != null && Photo.Length == 0)
                throw new ArgumentException("Photo must contain PNG bytes.");

            var connect = new ConnectParam(SendType);
            var chat_id = "";
            var message_thread_id = "";
            if (SendType == SendType.ServiceGroup)
            {
                chat_id = connect.ChatIdServiceGroup;
                message_thread_id = "";
            }
            else
            {
                chat_id = connect.ChatId;
                message_thread_id = SendInChatIdThreadAddition ? connect.ChatIdThreadAdditional : connect.ChatIdThread;
            }

            bool hasPhoto = Photo != null;
            using (var client = new HttpClient())
            {

                {
                    string text = message;
                    var data = new Dictionary<string, string> { { "chat_id", chat_id } };
                    if (!string.IsNullOrEmpty(message_thread_id))
                    {
                        data.Add("message_thread_id", message_thread_id);
                    }
                    if (!string.IsNullOrEmpty(text))
                    {
                        data.Add(hasPhoto ? "caption" : "text", text);
                        data.Add("parse_mode", "HTML");
                    }

                    if (!hasPhoto)
                    {
                        data.Add("link_preview_options", "{\"is_disabled\":true}");
                    }

                    if (!string.IsNullOrEmpty(ReplyMarkupObj))
                    {
                        data.Add("reply_markup", ReplyMarkupObj);
                    }

                    HttpContent content;
                    if (hasPhoto)
                    {
                        var form = new MultipartFormDataContent();
                        foreach (var field in data) form.Add(new StringContent(field.Value), field.Key);
                        var photo = new ByteArrayContent(Photo);
                        photo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                        form.Add(photo, "photo", "schedule.png");
                        content = form;
                    }
                    else
                    {
                        content = new FormUrlEncodedContent(data);
                    }

                    Console.WriteLine("### TELEGRAM.START: ###");
                    Console.WriteLine(text);
                    Console.WriteLine("### TELEGRAM.END ###");

                    using (content)
                    using (var response = client.PostAsync(
                        "https://api.telegram.org/bot" + connect.KeyParam.BotToken +
                        (hasPhoto ? "/sendPhoto" : "/sendMessage"), content).Result)
                    {
                        string responseString = response.Content.ReadAsStringAsync().Result;
                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine(new Json(responseString)["description"].Value);
                            response.EnsureSuccessStatusCode();
                        }
                    }
                }
            }
        }
    }
}
