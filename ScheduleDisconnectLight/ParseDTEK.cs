//using Microsoft.Playwright;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;





namespace ScheduleDisconnectLight
{

    /*
    public class ParseDTEK
    {
        public string Get()
        {
            var url = "https://www.dtek-kem.com.ua/ua/shutdowns";

            IPlaywright playwright = null;
            IBrowser browser = null;
            IBrowserContext context = null;

            try
            {
                playwright = Playwright.CreateAsync().GetAwaiter().GetResult();

                browser = playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" }
                }).GetAwaiter().GetResult();

                context = browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36",
                    Locale = "uk-UA",
                    TimezoneId = "Europe/Kyiv"
                }).GetAwaiter().GetResult();

                context.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
                {
                    ["Accept-Language"] = "uk-UA,uk;q=0.9,ru;q=0.8,en;q=0.7"
                }).GetAwaiter().GetResult();

                string factJsonText = "";
                int i;

                for (i = 1; i <= 5; i++)
                {
                    IPage page = null;
                    try
                    {
                        page = context.NewPageAsync().GetAwaiter().GetResult();

                        page.GotoAsync(url, new PageGotoOptions
                        {
                            WaitUntil = WaitUntilState.NetworkIdle,
                            Timeout = 120_000
                        }).GetAwaiter().GetResult();

                        page.WaitForTimeoutAsync(500).GetAwaiter().GetResult();

                        var html = page.ContentAsync().GetAwaiter().GetResult();

                        factJsonText = extractJsAssignmentObject(html, "DisconSchedule.fact");
                        if (!string.IsNullOrEmpty(factJsonText))
                            break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ParseDTEK-1, ошибка: " + ex.Message);
                        return string.Empty;
                    }
                    finally
                    {
                        if (page != null)
                        {
                            page.CloseAsync().GetAwaiter().GetResult();
                        }
                            
                    }

                    Thread.Sleep(1500);
                }

                if (i > 1)
                {
                    if (string.IsNullOrEmpty(factJsonText))
                    {
                        new SenderTelegram() { IsTest = true }.Send("НЕ Подключено с " + i + " попытки");
                    }
                    else
                    {
                        new SenderTelegram() { IsTest = true }.Send("Подключено с " + i + " попытки");
                    }
                }

                if (string.IsNullOrEmpty(factJsonText))
                {
                    Console.WriteLine("ParseDTEK: Не найдено 'DisconSchedule.fact = ...' в HTML.");
                    return string.Empty;
                }

                var normalized = normalizeJsObjectToJson(factJsonText);
                if (normalized == "null" || string.IsNullOrEmpty(normalized))
                {
                    Console.WriteLine("ParseDTEK: В 'DisconSchedule.fact' значение null");
                    return string.Empty;
                }

                using (var doc = JsonDocument.Parse(normalized))
                {
                    if (!doc.RootElement.TryGetProperty("data", out _))
                    {
                        Console.WriteLine("ParseDTEK: Нет атрибута data");
                        return string.Empty;
                    }

                    var wrapper = new { fact = doc.RootElement };

                    var jsonStr = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    return jsonStr;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ParseDTEK, ошибка: " + ex.Message);
                return string.Empty;
            }
            finally
            {
                try { if (context != null) context.CloseAsync().GetAwaiter().GetResult(); } catch { }
                try { if (browser != null) browser.CloseAsync().GetAwaiter().GetResult(); } catch { }
                try { if (playwright != null) playwright.Dispose(); } catch { }
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


    */




    public class ParseDTEK
    {
        public string Get()
        {


            var baseUrl = "https://www.dtek-kem.com.ua";
            var warmupUrl = baseUrl + "/ua/";
            var url = baseUrl + "/ua/shutdowns";

            try
            {
                // NET48: иногда полезно явно включить TLS 1.2
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var cookies = new CookieContainer();
                string jsonStr = "";

                using (var handler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    CookieContainer = cookies,
                    UseCookies = true,
                    AllowAutoRedirect = true
                })
                using (var httpClient = new HttpClient(handler))
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(25);

                    // Базовый набор заголовков (без "Sec-Fetch" и "sec-ch-ua" — они часто палят бота)
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");

                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                        "Accept",
                        "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");

                    httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("uk-UA,uk;q=0.9,ru;q=0.8,en;q=0.7");

                    // br НЕ просим, т.к. Brotli на net48 обычно нет
                    httpClient.DefaultRequestHeaders.Remove("Accept-Encoding");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");

                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Connection", "keep-alive");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");

                    // Нормальный referrer (не "сам на себя")
                    httpClient.DefaultRequestHeaders.Referrer = new Uri(warmupUrl);



                    var rnd = new Random();
                    string factJsonText = "";
                    int attempt;

                    for (attempt = 1; attempt <= 10; attempt++)
                    {
                        // 1) Прогрев (получить базовые cookies)
                        try
                        {
                            var warm = httpClient.GetAsync(warmupUrl).GetAwaiter().GetResult();
                            warm.Dispose();
                        }
                        catch
                        {
                            // прогрев не критичен — идем дальше
                        }

                        // 2) Основной запрос
                        var resp = httpClient.GetAsync(url).GetAwaiter().GetResult();
                        var html = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();


                        factJsonText = extractJsAssignmentObject(html, "DisconSchedule.fact");
                        if (!string.IsNullOrEmpty(factJsonText))
                        {
                            break;
                        }

          

                        Thread.Sleep(1000 + rnd.Next(0, 500));
                    }

                    if (attempt > 1)
                    {
                        if (string.IsNullOrEmpty(factJsonText))
                        {
                            new SenderTelegram() { IsTest = true }.Send("НЕ подключено (до 10 попыток).");
                        }
                        else
                        {
                            new SenderTelegram() { IsTest = true }.Send("Подключено с " + attempt + " попытки");
                        }
                    }

                    if (string.IsNullOrEmpty(factJsonText))
                    {
                        Console.WriteLine("ParseDTEK: Не найдено 'DisconSchedule.fact = ...' в HTML.");
                        return string.Empty;
                    }

                    // Приводим JS-объект к JSON
                    var normalized = normalizeJsObjectToJson(factJsonText);
                    if (normalized == "null" || string.IsNullOrWhiteSpace(normalized))
                    {
                        Console.WriteLine("ParseDTEK: В 'DisconSchedule.fact' значение null/empty.");
                        return string.Empty;
                    }

                    // Проверка JSON
                    var doc = JsonDocument.Parse(normalized);

                    // Проверка что есть data
                    if (!doc.RootElement.TryGetProperty("data", out var _))
                    {
                        Console.WriteLine("ParseDTEK: Нет атрибута data");
                        return string.Empty;
                    }

                    var wrapper = new { fact = doc.RootElement };

                    jsonStr = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    return jsonStr;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ParseDTEK, ошибка: " + ex.Message);
                return string.Empty;
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