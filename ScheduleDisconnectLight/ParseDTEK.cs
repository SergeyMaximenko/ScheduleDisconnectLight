using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.Remoting.Messaging;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;


namespace ScheduleDisconnectLight
{
    public class ParseDTEK
    {
        public string Get()
        {
            var url = "https://www.dtek-kem.com.ua/ua/shutdowns";

            try
            {
                var cookies = new CookieContainer();
                string jsonStr = "";
                using (var httpClient = new HttpClient(new HttpClientHandler
                {
                    AutomaticDecompression =
                        DecompressionMethods.GZip |
                        DecompressionMethods.Deflate,
                    CookieContainer = cookies,
                    UseCookies = true,
                    AllowAutoRedirect = true
                }))
                {

                    // UA як у Playwright
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");

                    // Мова (locale)
                    httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("uk-UA,uk;q=0.9,ru;q=0.8,en;q=0.7");

                    // Забороняємо br, бо у тебе немає Brotli-розпакування
                    httpClient.DefaultRequestHeaders.Remove("Accept-Encoding");
                    httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");

                    // Типові браузерні заголовки
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Pragma", "no-cache");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "none");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-User", "?1");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"128\", \"Not;A=Brand\";v=\"24\"");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");

                    // Не обов'язково, але інколи допомагає
                    httpClient.DefaultRequestHeaders.Referrer = new Uri(url);


                    string factJsonText = "";
                    int i = 0;
                    for (i = 1; i <= 5; i++)
                    {
                        var resp = httpClient.GetAsync(url).GetAwaiter().GetResult();
                        var html = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        factJsonText = extractJsAssignmentObject(html, "DisconSchedule.fact");
                        if (!string.IsNullOrEmpty(factJsonText))
                        {
                            break;
                        }


                        // ✅ ЛОГ СЮДИ (коли fact не знайшовся)
                        var enc = resp.Content.Headers.ContentEncoding != null
                            ? string.Join(",", resp.Content.Headers.ContentEncoding)
                            : "(none)";
                        var ct = resp.Content.Headers.ContentType?.ToString() ?? "(none)";
                        var finalUrl = resp.RequestMessage?.RequestUri?.ToString() ?? "(unknown)";

                        var message1 =
                            $"Attempt {i}: HTTP {(int)resp.StatusCode} {resp.StatusCode}, " +
                            $"len={html?.Length ?? 0}, ct={ct}, enc={enc}, url={finalUrl}";

                        Console.WriteLine(message1);
                        new SenderTelegram() { IsTest = true }.Send(message1);

                        if (!string.IsNullOrEmpty(html))
                        {
                            var message2 = $"Attempt {i}: HTML head: " + html.Substring(0, Math.Min(250, html.Length));
                            Console.WriteLine($"len={html.Length}");
                            Console.WriteLine($"has_fact={(html.Contains("DisconSchedule.fact") ? "YES" : "NO")}");
                            Console.WriteLine($"has_ajaxUrl={(html.Contains("meta name=\"ajaxUrl\"") ? "YES" : "NO")}");
                            Console.WriteLine($"has_cloudflare={(html.Contains("cf-chl") || html.Contains("cloudflare") ? "YES" : "NO")}");
                            Console.WriteLine($"has_datadome={(html.Contains("datadome") ? "YES" : "NO")}");
                            Console.WriteLine($"has_turnstile={(html.Contains("turnstile") ? "YES" : "NO")}");
                            Console.WriteLine($"has_recaptcha={(html.Contains("recaptcha") ? "YES" : "NO")}");

                            // new SenderTelegram() { IsTest = true }.Send(message2);
                        }



                        Thread.Sleep(1500);

                    }



                    if (string.IsNullOrEmpty(factJsonText))
                    {
                        Console.WriteLine("ParseDTEK: Не найдено 'DisconSchedule.fact = ...' в HTML.");
                        //Console.WriteLine("Проверь: страница реально содержит данные (антибот может отдавать заглушку).");
                        return string.Empty;
                    }

                    // Попытка привести JS-объект к JSON (на твоём скрине оно уже похоже на JSON)
                    var normalized = normalizeJsObjectToJson(factJsonText);
                    if (normalized == "null" || string.IsNullOrEmpty(normalized))
                    {
                        Console.WriteLine("ParseDTEK: В 'DisconSchedule.fact значение null");
                        return string.Empty;
                    }



                    // Проверим, что это валидный JSON
                    var doc = JsonDocument.Parse(normalized);

                    // Проверить, что есть дата 
                    if (!doc.RootElement.TryGetProperty("data", out var data))
                    {
                        Console.WriteLine($"ParseDTEK. Нет атрибута data");
                        return string.Empty;
                    }

                    var wrapper = new
                    {
                        fact = doc.RootElement
                    };


                    jsonStr = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });



                    //var outPath = Path.Combine(Environment.CurrentDirectory, "fact.json");
                    //File.WriteAllText(outPath, jsonStr);
                    //Console.WriteLine($"OK. Сохранено: " + outPath);
                }


                return jsonStr;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ParseDTEK, ошибка: " + ex.Message);
                return string.Empty;
            }
        }

        static void WriteLong(string text, int chunkSize = 1000)
        {
            for (int i = 0; i < text.Length; i += chunkSize)
            {
                Console.WriteLine(text.Substring(i, Math.Min(chunkSize, text.Length - i)));
            }
        }

        /// <summary>
        /// Достаёт объект справа от "varName = { ... }" до ; учитывая вложенные скобки.
        /// </summary>
        private static string extractJsAssignmentObject(string html, string varName)
        {
            // Находим позицию "DisconSchedule.fact"
            var idx = html.IndexOf(varName, StringComparison.Ordinal);
            if (idx < 0) return null;

            // Находим '='
            var eq = html.IndexOf('=', idx);
            if (eq < 0) return null;

            // Ищем начало объекта '{' после '='
            var start = html.IndexOf('{', eq);
            if (start < 0) return null;

            // Идём по символам и считаем баланс фигурных скобок
            int depth = 0;
            for (int i = start; i < html.Length; i++)
            {
                char c = html[i];

                if (c == '{') depth++;
                else if (c == '}') depth--;

                if (depth == 0)
                {
                    // i — позиция закрывающей '}'
                    var obj = html.Substring(start, i - start + 1);
                    return obj.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Мини-нормализация "JS object" -> "JSON".
        /// Если в объекте уже двойные кавычки и true/false/null — обычно ничего не ломает.
        /// </summary>
        private static string normalizeJsObjectToJson(string jsObject)
        {
            var s = jsObject.Trim();

            // Иногда встречается одинарная кавычка — заменим на двойную (упрощённо).
            // Если у тебя внутри текста есть апострофы — скажи, сделаю аккуратнее.
            s = s.Replace("'", "\"");

            // JS undefined -> JSON null
            s = Regex.Replace(s, @"\bundefined\b", "null");

            return s;
        }
    }
}


