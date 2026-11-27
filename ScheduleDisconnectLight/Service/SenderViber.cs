using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Service
{
    /// <summary>
    /// Відправка повідомлення у Viber-чат через власне API черги (проєкт "Чавдар 13 / viber-queue"),
    /// а не через Viber-бота: реальне надсилання виконує Viber Desktop на сервері.
    ///
    /// ВАЖЛИВО: відповідь 202 означає лише "додано в чергу", а не "надіслано". Сервер бере повідомлення
    /// у роботу з паузою (до ~15 с і довше), тому тут результат не чекаємо. Статус дивитись у
    /// GET {VIBER_API_URL}/messages/{id} (id пишеться в лог). sent також не гарантує доставку одержувачу.
    ///
    /// Налаштування (як WORKER_URL / INGEST_SECRET у WorkerSync): VIBER_API_URL і VIBER_API_TOKEN
    /// (GitHub - змінні середовища / secrets, локально - appsettings.Local.json). Якщо не задані - помилка в консоль.
    /// Токен у лог і в тексти помилок не потрапляє. URL має бути https, інакше токен не відправляється.
    ///
    /// Повідомлення готуються для Telegram (HTML), а Viber HTML не розуміє: &lt;b&gt;/&lt;i&gt;/&lt;s&gt; перетворюються
    /// на розмітку Viber (*жирний*, _курсив_, ~закреслений~), посилання - на "текст (адреса)", інші теги прибираються.
    /// </summary>
    public class SenderViber
    {
        /// <summary>Назва чату для тестового режиму. Має бути в allowlist сервера (GET /chats).</summary>
        public const string ChatTest = "Тест";

        /// <summary>
        /// Назва робочого чату. Має бути в allowlist сервера (GET /chats): підключена 27.11.2026.
        /// </summary>
        public const string ChatProd = "Робоча група Чавдар";

        private const int MaxTextLength = 8000;   // ліміт API, UTF-16 units
        private const int MaxAttempts = 3;

        public SenderViber()
        {
            SendType = SendType.Auto;
        }

        public SendType SendType { get; set; }

        /// <summary>
        /// Ідентифікатор повідомлення в черзі ([A-Za-z0-9_.:-], до 100 символів). Той самий id з тим самим текстом
        /// повторно не додається (захист від дублів). Недопустимі символи замінюються на "_" (з записом у консоль).
        /// Якщо не заданий - створюється новий для кожного Send; повтори всередині одного Send завжди йдуть з одним id.
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// Відправка у Viber ніколи не кидає виняток: збій Viber (немає налаштувань, API недоступне, відмова сервера)
        /// не повинен зупиняти основну роботу програми. Помилка пишеться в консоль.
        /// Повертає true, якщо повідомлення прийнято в чергу, false - якщо ні (або текст порожній).
        /// </summary>
        public bool Send(string message)
        {
            try
            {
                return SendCore(message);
            }
            catch (Exception ex)
            {
                var inner = ex is AggregateException && ex.InnerException != null ? ex.InnerException : ex;
                Console.WriteLine("### VIBER.ERROR: повідомлення не відправлено: " + inner.Message + " ###");
                return false;
            }
        }

        private bool SendCore(string message)
        {
            var text = ToPlainText(message);
            if (text.Length == 0)
            {
                Console.WriteLine("### VIBER: порожнє повідомлення, пропущено ###");
                return false;
            }

            var chat = Api.IsRegTest(SendType) ? ChatTest : ChatProd;
            var id = NormalizeId(MessageId);

            string url, token;
            GetSettings(out url, out token);

            var body = new JObject
            {
                ["messages"] = new JArray(new JObject { ["id"] = id, ["chat"] = chat, ["text"] = text })
            }.ToString(Formatting.None);

            Console.WriteLine("### VIBER.START: " + chat + " / " + id + " ###");
            Console.WriteLine(text);
            Console.WriteLine("### VIBER.END ###");

            Post(url + "/messages", token, body, id);
            return true;
        }

        /// <summary>
        /// Приводить id до формату API ([A-Za-z0-9_.:-], 1-100 символів). Винятку не кидає: недопустимі символи
        /// замінюються на "_", надто довгий id обрізається, про зміну пишеться в консоль.
        /// Порожній id - створюється новий. Обрізання/заміна детерміновані, тож той самий вхідний id
        /// завжди дає той самий результат (захист від дублів зберігається).
        /// </summary>
        private static string NormalizeId(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                return "sdl-" + Guid.NewGuid().ToString("D");
            }

            var id = Regex.Replace(messageId, @"[^A-Za-z0-9_.:-]", "_");
            if (id.Length > 100)
            {
                id = id.Substring(0, 100);
            }
            if (id != messageId)
            {
                Console.WriteLine("Viber: MessageId містив недопустимі символи або був задовгим, використано '" + id + "'");
            }
            return id;
        }

        // ------------------------------------------------------------------------------------
        // Налаштування (так само, як у WorkerSync)
        // ------------------------------------------------------------------------------------

        private static void GetSettings(out string url, out string token)
        {
            if (Api.IsGitHub())
            {
                url = Environment.GetEnvironmentVariable("VIBER_API_URL");
                token = Environment.GetEnvironmentVariable("VIBER_API_TOKEN");
            }
            else
            {
                url = null;
                token = null;
                var json = ReadLocalSettings();
                if (json != null)
                {
                    url = (string)json["VIBER_API_URL"];
                    token = (string)json["VIBER_API_TOKEN"];
                }
            }

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
            {
                throw new Exception("VIBER_API_URL / VIBER_API_TOKEN не знайдено (змінні середовища GitHub або appsettings.Local.json)");
            }
            url = url.Trim().TrimEnd('/');
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("VIBER_API_URL має починатися з https://: токен не відправляється по незахищеному каналу");
            }
            token = token.Trim();
        }

        private static JObject ReadLocalSettings()
        {
            var path = Path.Combine(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..")), "appsettings.Local.json");
            if (!File.Exists(path))
            {
                throw new Exception("Файл " + path + " не знайдено!");
            }
            return JObject.Parse(File.ReadAllText(path));
        }

        // ------------------------------------------------------------------------------------
        // Запит
        // ------------------------------------------------------------------------------------

        private static void Post(string url, string token, string body, string id)
        {
            for (int attempt = 1; ; attempt++)
            {
                string error;
                bool retry;
                try
                {
                    using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, url)
                        {
                            // Content-Type: application/json; charset=utf-8
                            Content = new StringContent(body, Encoding.UTF8, "application/json")
                        };
                        request.Headers.Add("Authorization", "Bearer " + token);

                        using (var response = client.SendAsync(request).Result)
                        {
                            var text = response.Content.ReadAsStringAsync().Result;
                            if (response.IsSuccessStatusCode)
                            {
                                LogQueued(text, id);
                                return;
                            }

                            var code = (int)response.StatusCode;
                            error = "Viber API " + code + ": " + text;
                            // 5xx і 429 - тимчасові; 400/401/404/409/422 повтор не виправить
                            retry = code >= 500 || code == 429;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Мережа / таймаут. Повтор безпечний: id той самий, дубль сервер не додасть.
                    var inner = ex is AggregateException && ex.InnerException != null ? ex.InnerException : ex;
                    error = "Viber API: " + inner.GetType().Name + ": " + inner.Message;
                    retry = true;
                }

                if (!retry || attempt >= MaxAttempts)
                {
                    throw new Exception(error);
                }
                Console.WriteLine("Viber: спроба " + attempt + " з " + MaxAttempts + " не вдалася (" + error + "), повтор з тим самим id");
                Thread.Sleep(TimeSpan.FromSeconds(2 * attempt));
            }
        }

        private static void LogQueued(string responseText, string id)
        {
            try
            {
                var created = JObject.Parse(responseText)["messages"]?[0]?.Value<bool?>("created");
                Console.WriteLine(created == false
                    ? "Viber: повідомлення " + id + " уже було в черзі (повтор), дубль не додано"
                    : "Viber: повідомлення " + id + " додано в чергу (надсилання виконає worker)");
            }
            catch (Exception)
            {
                Console.WriteLine("Viber: повідомлення " + id + " прийнято API");
            }
        }

        // ------------------------------------------------------------------------------------
        // Текст
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Замінює теги за шаблоном на розмітку Viber. Viber не розпізнає маркер, якщо він стоїть упритул до
        /// іншого символу (наприклад, смужки з квадратиків перед "*22%*"), тому за потреби додається пробіл
        /// перед відкривальним і після закривального маркера.
        /// </summary>
        private static string ReplaceMarked(string input, string pattern, string mark)
        {
            return Regex.Replace(input, pattern, m =>
            {
                var result = ViberMark(mark, m.Groups[2].Value);
                if (m.Index > 0)
                {
                    var before = input[m.Index - 1];
                    if (!char.IsWhiteSpace(before) && "([{>".IndexOf(before) < 0) result = " " + result;
                }
                var end = m.Index + m.Length;
                if (end < input.Length)
                {
                    var after = input[end];
                    if (char.IsLetterOrDigit(after) || char.IsSurrogate(after)) result += " ";
                }
                return result;
            }, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        /// <summary>
        /// Обгортає текст маркером розмітки Viber. Правила Viber: маркер впритул до тексту (без пробілів всередині)
        /// і не через рядки, тому кожен непорожній рядок обгортається окремо, а пробіли залишаються ззовні.
        /// Теги всередині (вкладені) тут ще не видалені: це робить наступний крок.
        /// </summary>
        private static string ViberMark(string mark, string inner)
        {
            var lines = inner.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var core = line.Trim();
                if (core.Length == 0) continue;
                var start = line.IndexOf(core, StringComparison.Ordinal);
                lines[i] = line.Substring(0, start) + mark + core + mark + line.Substring(start + core.Length);
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Telegram-HTML -> текст для Viber: &lt;b&gt; у *жирний*, &lt;i&gt; у _курсив_, &lt;s&gt; у ~закреслений~,
        /// &lt;br&gt; у переноси рядків, посилання "текст (адреса)", інші теги видаляються, сутності декодуються,
        /// керівні символи (крім LF і tab) прибираються, довжина обмежується лімітом API.
        /// </summary>
        public static string ToPlainText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";

            var text = html;
            text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<a\s[^>]*href\s*=\s*[""']([^""']*)[""'][^>]*>(.*?)</a>",
                m =>
                {
                    var label = Regex.Replace(m.Groups[2].Value, "<[^>]+>", "").Trim();
                    var link = m.Groups[1].Value.Trim();
                    return label.Length == 0 || label == link ? link : label + " (" + link + ")";
                },
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            // Viber не розуміє HTML, але має власну розмітку: *жирний*, _курсив_, ~закреслений~.
            text = ReplaceMarked(text, @"<(b|strong)>(.*?)</\1>", "*");
            text = ReplaceMarked(text, @"<(i|em)>(.*?)</\1>", "_");
            text = ReplaceMarked(text, @"<(s|strike|del)>(.*?)</\1>", "~");
            text = Regex.Replace(text, "<[^>]+>", "");
            text = WebUtility.HtmlDecode(text);

            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            text = Regex.Replace(text, @"[\u0000-\u0008\u000B-\u001F\u007F]", "");
            text = text.Trim();

            if (text.Length > MaxTextLength)
            {
                text = text.Substring(0, MaxTextLength - 1) + "…";
            }
            return text;
        }
    }
}
