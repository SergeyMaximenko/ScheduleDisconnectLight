using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Service
{
    /// <summary>
    /// Налаштування доступу до Deye Cloud (секрети).
    /// GitHub: змінні середовища DEYE_APP_ID, DEYE_APP_SECRET, DEYE_LOGIN, DEYE_PASSWORD.
    /// Локально: ті самі ключі в appsettings.Local.json (файл у .gitignore).
    /// </summary>
    public class DeyeSettings
    {
        public string AppId { get; private set; }
        public string AppSecret { get; private set; }
        /// <summary>E-mail (або логін) облікового запису Deye Cloud</summary>
        public string Login { get; private set; }
        public string Password { get; private set; }

        public static DeyeSettings Get()
        {
            var settings = new DeyeSettings();

            if (Api.IsGitHub())
            {
                settings.AppId = Environment.GetEnvironmentVariable("DEYE_APP_ID");
                settings.AppSecret = Environment.GetEnvironmentVariable("DEYE_APP_SECRET");
                settings.Login = Environment.GetEnvironmentVariable("DEYE_LOGIN");
                settings.Password = Environment.GetEnvironmentVariable("DEYE_PASSWORD");
            }
            else
            {
                string repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
                string localPath = Path.Combine(repoRoot, "appsettings.Local.json");
                if (!File.Exists(localPath))
                {
                    throw new Exception($"Файл {localPath} не знайдено!");
                }

                var json = new Json(File.ReadAllText(localPath));
                settings.AppId = json["DEYE_APP_ID"].Value;
                settings.AppSecret = json["DEYE_APP_SECRET"].Value;
                settings.Login = json["DEYE_LOGIN"].Value;
                settings.Password = json["DEYE_PASSWORD"].Value;
            }

            if (string.IsNullOrWhiteSpace(settings.AppId) ||
                string.IsNullOrWhiteSpace(settings.AppSecret) ||
                string.IsNullOrWhiteSpace(settings.Login) ||
                string.IsNullOrWhiteSpace(settings.Password))
            {
                throw new Exception("Не задано DEYE_APP_ID / DEYE_APP_SECRET / DEYE_LOGIN / DEYE_PASSWORD.");
            }
            return settings;
        }
    }

    /// <summary>
    /// Поточні показники одного інвертора, як їх повернув Deye (device/latest).
    /// </summary>
    public class DeyeReading
    {
        /// <summary>Серійний номер інвертора</summary>
        public string DeviceSn;

        /// <summary>
        /// Час, коли логер зафіксував ці дані (київський час).
        /// null - Deye не повернув час або його не вдалося розібрати.
        /// </summary>
        public DateTime? MeasuredAt;

        /// <summary>Показники: ключ Deye (наприклад, "SOC", "BatteryPower") → число (null, якщо не число)</summary>
        public Dictionary<string, double?> Points = new Dictionary<string, double?>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Клієнт Deye Cloud. Бере ТІЛЬКИ поточний стан (device/latest), історію не читає.
    ///
    /// Квота Deye - 100 000 викликів на місяць, тому:
    ///  - за один запуск робиться ОДИН виклик device/latest;
    ///  - токен зберігається у файлі (на GitHub цей файл відновлюється з кешу Actions між запусками),
    ///    новий токен запитується тільки коли старий спливає.
    /// </summary>
    public class DeyeClient
    {
        private const string BaseUrl = "https://eu1-developer.deyecloud.com/v1.0/";

        /// <summary>Скільки часу до завершення терміну дії токена ми вже вважаємо його непридатним</summary>
        private static readonly TimeSpan TokenReserve = TimeSpan.FromMinutes(10);

        private readonly DeyeSettings _settings;
        private readonly string _tokenFile;

        private string _token;
        private DateTime _tokenExpiresUtc;

        public DeyeClient(DeyeSettings settings)
        {
            _settings = settings;

            // На GitHub шлях до файлу задає workflow (DEYE_TOKEN_FILE), локально - файл у корені репозиторію.
            var fromEnv = Environment.GetEnvironmentVariable("DEYE_TOKEN_FILE");
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                _tokenFile = fromEnv;
            }
            else
            {
                string repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
                _tokenFile = Path.Combine(repoRoot, "deye-token.local.json");
            }
        }

        /// <summary>
        /// Поточні показники інвертора. Один HTTP-виклик (плюс запит токена, якщо старий спливає).
        /// </summary>
        public DeyeReading GetLatest(string deviceSn)
        {
            var body = new JObject { ["deviceList"] = new JArray(deviceSn) };
            var response = Post("device/latest", body, true);

            var list = response["deviceDataList"] as JArray;
            if (list == null)
            {
                throw new Exception("Deye не повернув показники інвертора (deviceDataList).");
            }

            foreach (var item in list)
            {
                if (!string.Equals((string)item["deviceSn"], deviceSn, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var reading = new DeyeReading { DeviceSn = deviceSn };
                reading.MeasuredAt = ParseDeyeTime(item["collectionTime"]);

                var dataList = item["dataList"] as JArray;
                if (dataList != null)
                {
                    foreach (var point in dataList)
                    {
                        var key = (string)point["key"];
                        if (string.IsNullOrEmpty(key))
                        {
                            continue;
                        }
                        reading.Points[key] = ToNumber(point["value"]);
                    }
                }
                return reading;
            }

            throw new Exception($"Deye не повернув дані для інвертора {deviceSn}.");
        }

        /*
        // ---------------------------------------------------------------------------------------------
        // ЯК ВИЗНАЧИТИ SN ІНВЕРТОРА (для довідки; в роботі не використовується, SN вказано явно в PowerMonitor).
        // Один раз викликаємо station/device і дивимось deviceSn у відповіді (deviceType = "INVERTER"):
        //
        //   var stations = Post("station/list", new JObject(), true);           // знаходимо stationId
        //   var devices  = Post("station/device", new JObject {
        //                       ["stationIds"] = new JArray(<stationId>) }, true);
        //   foreach (var d in (JArray)devices["deviceListItems"])
        //       Console.WriteLine((string)d["deviceType"] + " " + (string)d["deviceSn"]);
        // ---------------------------------------------------------------------------------------------
        */

        // ----------------------------------------------------------------------------------------
        // HTTP
        // ----------------------------------------------------------------------------------------

        private JObject Post(string endpoint, JObject body, bool authenticated, bool allowRetryOnAuthError = true)
        {
            if (authenticated)
            {
                EnsureToken();
            }

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) })
            {
                var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + endpoint)
                {
                    Content = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json")
                };
                if (authenticated)
                {
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _token);
                }

                HttpResponseMessage response;
                try
                {
                    response = http.SendAsync(request).Result;
                }
                catch (Exception ex)
                {
                    throw new Exception("Не вдалося підключитися до Deye Cloud: " + SafeMessage(ex.GetBaseException().Message));
                }

                using (response)
                {
                    // Токен міг бути відкликаний раніше строку: запитуємо новий і повторюємо виклик один раз
                    if (response.StatusCode == HttpStatusCode.Unauthorized && authenticated && allowRetryOnAuthError)
                    {
                        _token = null;
                        TryDeleteTokenFile();
                        return Post(endpoint, body, authenticated, false);
                    }

                    string text = response.Content.ReadAsStringAsync().Result;
                    JObject data;
                    try
                    {
                        data = JObject.Parse(text);
                    }
                    catch
                    {
                        throw new Exception("Deye повернув неочікувану відповідь (HTTP " + (int)response.StatusCode + ").");
                    }

                    var success = data["success"] != null && (bool?)data["success"] == true;
                    if (!response.IsSuccessStatusCode || !success)
                    {
                        var msg = (string)data["msg"] ?? (string)data["message"] ?? "помилка запиту";
                        throw new Exception("Deye: " + SafeMessage(msg) + " (HTTP " + (int)response.StatusCode + ", код " + SafeMessage((string)data["code"] ?? "-") + ").");
                    }
                    return data;
                }
            }
        }

        // ----------------------------------------------------------------------------------------
        // Токен
        // ----------------------------------------------------------------------------------------

        private void EnsureToken()
        {
            if (TokenIsValid())
            {
                return;
            }

            LoadTokenFile();
            if (TokenIsValid())
            {
                Console.WriteLine("Deye: токен взято з кешу");
                return;
            }

            Login();
        }

        private bool TokenIsValid()
        {
            return !string.IsNullOrEmpty(_token) && _tokenExpiresUtc > DateTime.UtcNow + TokenReserve;
        }

        private void Login()
        {
            Console.WriteLine("Deye: запитуємо новий токен");

            var body = new JObject
            {
                ["appSecret"] = _settings.AppSecret,
                // Deye приймає пароль як SHA-256 у нижньому регістрі
                ["password"] = Sha256Hex(_settings.Password)
            };
            body[_settings.Login.Contains("@") ? "email" : "username"] = _settings.Login;

            var data = Post("account/token?appId=" + Uri.EscapeDataString(_settings.AppId), body, false);

            var token = (string)data["accessToken"];
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new Exception("Deye не повернув токен доступу.");
            }

            // На всяк випадок прибираємо префікс "Bearer ", якщо він є
            _token = token.Trim();
            if (_token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                _token = _token.Substring(7).Trim();
            }

            double expiresInSeconds = 3600;
            double.TryParse((string)data["expiresIn"], NumberStyles.Float, CultureInfo.InvariantCulture, out expiresInSeconds);
            if (expiresInSeconds <= 0)
            {
                expiresInSeconds = 3600;
            }
            _tokenExpiresUtc = DateTime.UtcNow.AddSeconds(expiresInSeconds);

            SaveTokenFile();
        }

        private void LoadTokenFile()
        {
            try
            {
                if (!File.Exists(_tokenFile))
                {
                    return;
                }
                var json = JObject.Parse(File.ReadAllText(_tokenFile));
                _token = (string)json["token"];
                long unix = (long?)json["expiresAtUnix"] ?? 0;
                _tokenExpiresUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix);
            }
            catch (Exception ex)
            {
                // Пошкоджений файл не страшний: просто запитаємо новий токен
                Console.WriteLine("Deye: не вдалося прочитати кеш токена: " + ex.Message);
                _token = null;
            }
        }

        private void SaveTokenFile()
        {
            try
            {
                long unix = (long)(_tokenExpiresUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                var json = new JObject { ["token"] = _token, ["expiresAtUnix"] = unix };
                File.WriteAllText(_tokenFile, json.ToString(Newtonsoft.Json.Formatting.None));

                // Повідомляємо workflow, що токен оновлено: тільки тоді він збереже кеш (див. main.yml)
                var githubEnv = Environment.GetEnvironmentVariable("GITHUB_ENV");
                if (!string.IsNullOrEmpty(githubEnv))
                {
                    File.AppendAllText(githubEnv, "DEYE_TOKEN_REFRESHED=true" + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // Без кешу все працює, просто токен запитуватиметься частіше
                Console.WriteLine("Deye: не вдалося зберегти кеш токена: " + ex.Message);
            }
        }

        private void TryDeleteTokenFile()
        {
            try
            {
                if (File.Exists(_tokenFile))
                {
                    File.Delete(_tokenFile);
                }
            }
            catch
            {
                // нічого страшного
            }
        }

        // ----------------------------------------------------------------------------------------
        // Допоміжні
        // ----------------------------------------------------------------------------------------

        private static string Sha256Hex(string text)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>Прибираємо секрети з тексту помилки, щоб вони не потрапили в лог</summary>
        private string SafeMessage(string message)
        {
            var text = (message ?? "").Replace("\r", " ").Replace("\n", " ");
            if (text.Length > 500)
            {
                text = text.Substring(0, 500);
            }
            foreach (var secret in new[] { _settings.Password, _settings.AppSecret, _token })
            {
                if (!string.IsNullOrEmpty(secret))
                {
                    text = text.Replace(secret, "[приховано]");
                }
            }
            return text;
        }

        private static double? ToNumber(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Boolean)
            {
                return null;
            }
            var raw = token.ToString().Trim();
            if (raw.Length == 0)
            {
                return null;
            }
            double value;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value))
            {
                return value;
            }
            return null;
        }

        /// <summary>
        /// Deye повертає час збору даних або як Unix-час (секунди чи мілісекунди),
        /// або як рядок. Перетворюємо в київський час.
        /// </summary>
        public static DateTime? ParseDeyeTime(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }
            var raw = token.ToString().Trim();
            if (raw.Length == 0)
            {
                return null;
            }

            var kyiv = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");

            double number;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                // > 1e12 - це вже мілісекунди
                var seconds = number > 1e12 ? number / 1000.0 : number;
                if (seconds < 1e9 || seconds > 4102444800d)
                {
                    return null;
                }
                var utc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
                return TimeZoneInfo.ConvertTimeFromUtc(utc, kyiv);
            }

            // Рядок із часовим поясом (Z або +02:00)
            DateTimeOffset withZone;
            if ((raw.EndsWith("Z", StringComparison.OrdinalIgnoreCase) || System.Text.RegularExpressions.Regex.IsMatch(raw, @"[+-]\d{2}:\d{2}$")) &&
                DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out withZone))
            {
                return TimeZoneInfo.ConvertTimeFromUtc(withZone.UtcDateTime, kyiv);
            }

            // Рядок без пояса вважаємо київським часом
            DateTime local;
            string[] formats = { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss", "yyyy/MM/dd HH:mm:ss", "yyyy-MM-dd HH:mm" };
            if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out local))
            {
                return local;
            }
            return null;
        }
    }
}
