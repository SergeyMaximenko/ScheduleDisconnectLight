using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ScheduleDisconnectLight
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Отправляем сообщение...");

            try
            {
                SendTelegramMessage().GetAwaiter().GetResult();
                Console.WriteLine("Готово!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }
        }

        static async Task SendTelegramMessage()
        {
            // Можно брать из переменных среды, а можно пока захардкодить
            string botToken = "7911836999:AAHeC6qjw-Kis9xwA332YTq2ns1YI1AMdMI";
            string chatId = "-1002275491172";

            if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
            {
                Console.WriteLine("BOT_TOKEN или CHAT_ID не заданы.");
                return;
            }

            string text = "1122Ping из C# (.NET Framework 4.7.2)";

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
                    var response = await httpClient.PostAsync(url, content);
                    response.EnsureSuccessStatusCode();
                }
            }
        }
    }
}
