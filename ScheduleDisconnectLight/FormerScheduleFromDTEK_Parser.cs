//using Microsoft.Playwright;

using Service;
using System;
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




    public class FormerScheduleFromDTEK_Parser
    {
        public string Get()
        {

          //return "";


            var url = "https://www.dtek-kem.com.ua/ua/shutdowns";

            try
            {



                // иногда на net48 полезно явно включить TLS 1.2
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

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
                    httpClient.Timeout = TimeSpan.FromSeconds(25);

                    // UA як у Playwright
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");

                    // Мова (locale)
                    httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("uk-UA,uk;q=0.9,ru;q=0.8,en;q=0.7");

                    // Забороняємо br, бо у тебе немає Brotli-розпакування
                    httpClient.DefaultRequestHeaders.Remove("Accept-Encoding");
                    httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");

                    // ✅ ВАЖНО: Accept
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                        "Accept",
                        "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");

                    // Типові браузерні заголовки
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

                    var rnd = new Random();
                    string factJsonText = "";
                    int i = 0;
                    for (i = 1; i <= 10; i++)
                    {
                        var respAsync = httpClient.GetAsync(url);
                        var respAwaiter = respAsync.GetAwaiter();
                        var resp = respAwaiter.GetResult();

                        //var resp = httpClient.GetAsync(url).GetAwaiter().GetResult();


                        var htmlContent = resp.Content;
                        var htmlStringAsync = htmlContent.ReadAsStringAsync();
                        var htmlAwaiter = htmlStringAsync.GetAwaiter();
                        var html = htmlAwaiter.GetResult();

                        factJsonText = extractJsAssignmentObject(html, "DisconSchedule.fact");
                        
                        if (!string.IsNullOrEmpty(factJsonText))
                        {
                            break;
                        }
        
                        Thread.Sleep(1000 + rnd.Next(0, 500));
                    }

                    if (i > 1)
                    {
                        if (string.IsNullOrEmpty(factJsonText))
                        {
                            new SenderTelegram() { SendType = SendType.OnlyTest }.Send("НЕ подключено с " + i + " попытки");
                        }
                        else
                        {
                            //new SenderTelegram() { SendOnlyTestGroup = true }.Send("Подключено с " + i + " попытки");
                        }
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
                Console.WriteLine("❌ ParseDTEK, ошибка: " + ex.Message);
                Console.WriteLine("❌ Стек: " + ex.StackTrace);
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