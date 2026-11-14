using Google.Apis.Sheets.v4;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScheduleDisconnectLight;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Service
{
    /// <summary>
    /// Передача даних на Cloudflare Worker (проєкт C13PowerDashboard) для дашборда і звіту генератора.
    /// Формат запитів: C13PowerDashboard/INGEST.md (schemaVersion 1).
    ///
    /// Алгоритм (кожен запуск):
    ///  1. Читаємо журнал (DeyeСтатистика) і заправки (ЗаправкаСтатистика), рахуємо їх хеші.
    ///  2. POST /api/ingest: невеликий знімок поточного стану + хеші.
    ///  3. Worker відповідає, чи потрібні повні блоки (needJournal / needRefuels): коли його хеш відрізняється.
    ///  4. Лише тоді шлемо повний журнал / заправки. Ручні правки в таблиці теж потрапляють на Worker самі.
    ///
    /// Налаштування: WORKER_URL і INGEST_SECRET (GitHub - змінні середовища / secrets,
    /// локально - appsettings.Local.json). Якщо не задані, передача пропускається.
    /// У тестовому режимі (локальний запуск) передача теж пропускається, щоб тестові дані не потрапили на сайт;
    /// виняток - "WORKER_SYNC_FORCE": true у appsettings.Local.json (або змінна середовища WORKER_SYNC_FORCE=1).
    /// </summary>
    public static class WorkerSync
    {
        private const int SchemaVersion = 1;

        // Колонки закладки ЗаправкаСтатистика (з 0): див. GeneratorStatus.cs
        private const int RefuelDateCol = 1, RefuelLitersCol = 2, RefuelCodeCol = 4, RefuelNameCol = 5, RefuelTestCol = 7, RefuelIgnoreCol = 10;

        private static readonly DateTime SheetsEpoch = new DateTime(1899, 12, 30);
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static void Run()
        {
            // Тестовий режим (локальний запуск, не GitHub): тестові дані на сайт не передаємо.
            // Свідомо перевірити передачу локально можна: "WORKER_SYNC_FORCE": true у appsettings.Local.json
            // (або змінна середовища WORKER_SYNC_FORCE=1).
            if (Api.IsRegTest() && !IsForced())
            {
                Console.WriteLine("WorkerSync: тестовий режим, передачу на сайт пропущено");
                return;
            }

            string url, secret;
            if (!TryGetSettings(out url, out secret))
            {
                Console.WriteLine("WorkerSync: WORKER_URL / INGEST_SECRET не задано, передачу пропущено");
                return;
            }
            url = url.TrimEnd('/');

            var stored = DeyeSheet.ReadStored();
            var journal = ReadJournal();
            var refuels = ReadRefuels();
            var journalHash = Hash(journal);
            var refuelsHash = Hash(refuels);

            var body = new JObject
            {
                ["schemaVersion"] = SchemaVersion,
                ["snapshot"] = BuildSnapshot(stored, GeneratorNotification.LastIndicatorsHtml, GeneratorNotification.LastIndicatorsAt),
                ["journalHash"] = journalHash,
                ["refuelsHash"] = refuelsHash
            };
            var answer = Post(url + "/api/ingest", secret, body);
            var needJournal = answer.Value<bool?>("needJournal") == true;
            var needRefuels = answer.Value<bool?>("needRefuels") == true;
            Console.WriteLine($"WorkerSync: знімок передано, потрібен журнал={needJournal}, заправки={needRefuels}");

            if (needJournal)
            {
                Post(url + "/api/ingest/journal", secret, new JObject
                {
                    ["schemaVersion"] = SchemaVersion, ["hash"] = journalHash, ["rows"] = journal
                });
                Console.WriteLine($"WorkerSync: журнал передано ({journal.Count} рядків)");
            }
            if (needRefuels)
            {
                Post(url + "/api/ingest/refuels", secret, new JObject
                {
                    ["schemaVersion"] = SchemaVersion, ["hash"] = refuelsHash, ["rows"] = refuels
                });
                Console.WriteLine($"WorkerSync: заправки передано ({refuels.Count} рядків)");
            }
        }

        // ------------------------------------------------------------------------------------
        // Налаштування
        // ------------------------------------------------------------------------------------

        private static JObject ReadLocalSettings()
        {
            try
            {
                var path = Path.Combine(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..")), "appsettings.Local.json");
                return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("WorkerSync: не вдалося прочитати appsettings.Local.json: " + ex.Message);
                return null;
            }
        }

        // Локальний запуск передає дані лише свідомо: WORKER_SYNC_FORCE у appsettings.Local.json (true / "1") або змінна середовища = 1.
        private static bool IsForced()
        {
            if (Environment.GetEnvironmentVariable("WORKER_SYNC_FORCE") == "1") return true;
            var value = ReadLocalSettings()?["WORKER_SYNC_FORCE"];
            if (value == null) return false;
            if (value.Type == JTokenType.Boolean) return (bool)value;
            var text = value.ToString();
            return text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetSettings(out string url, out string secret)
        {
            if (Api.IsGitHub())
            {
                url = Environment.GetEnvironmentVariable("WORKER_URL");
                secret = Environment.GetEnvironmentVariable("INGEST_SECRET");
            }
            else
            {
                url = null;
                secret = null;
                var json = ReadLocalSettings();
                if (json != null)
                {
                    url = (string)json["WORKER_URL"];
                    secret = (string)json["INGEST_SECRET"];
                }
            }
            return !string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(secret);
        }

        // ------------------------------------------------------------------------------------
        // Знімок
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Знімок: поточний стан Deye + текст показників генератора (indicatorsHtml) і час, на який він пораховано.
        /// Усе йде одним JSON, а Worker зберігає його одним ключем KV (один запис на запуск).
        /// indicatorsHtml = null, якщо показники цього разу не сформовано (Worker тоді лишає попередні).
        /// </summary>
        private static JObject BuildSnapshot(DeyeStored s, string indicatorsHtml, DateTime? indicatorsAt)
        {
            string mode = null;
            if (s.BatteryMode.HasValue)
            {
                mode = s.BatteryMode.Value == (int)BatteryMode.Charging ? "charging"
                     : s.BatteryMode.Value == (int)BatteryMode.Discharging ? "discharging" : "idle";
            }
            return new JObject
            {
                ["requestAt"] = Ms(s.RequestAt),
                ["measuredAt"] = Ms(s.MeasuredAt),
                ["gridStatus"] = s.GridStatus, ["gridDate"] = Ms(s.GridDate), ["gridVoltage"] = s.GridVoltage,
                ["genStatus"] = s.GenStatus, ["genDate"] = Ms(s.GenDate), ["genPower"] = s.GenPower, ["genVoltage"] = s.GenVoltage, ["genPeak"] = s.GenPeak,
                ["inverterStatus"] = s.InverterStatus, ["inverterDate"] = Ms(s.InverterDate), ["loadWatts"] = s.LoadWatts,
                ["soc"] = s.Soc, ["batteryMode"] = mode, ["batteryWatts"] = s.BatteryWatts,
                ["indicatorsHtml"] = indicatorsHtml,
                ["indicatorsAt"] = indicatorsHtml == null ? null : Ms(indicatorsAt)
            };
        }

        // ------------------------------------------------------------------------------------
        // Журнал і заправки з таблиці
        // ------------------------------------------------------------------------------------

        private static IList<IList<object>> ReadRange(string range)
        {
            var request = SpreadSheet.Service.Spreadsheets.Values.Get(SpreadSheet.SpreadsheetId, range);
            request.ValueRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.UNFORMATTEDVALUE;
            request.DateTimeRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.DateTimeRenderOptionEnum.SERIALNUMBER;
            Google.Apis.Sheets.v4.Data.ValueRange response = null;
            GoogleExecuteRetry.Exec("Read " + range, () => { response = request.Execute(); });
            return response.Values ?? new List<IList<object>>();
        }

        /// <summary>Журнал: A=Об'єкт, B=Дата, C=1/0, D=годин генератора, E=годин без світла, F=Режим теста, G=макс. потужність, Вт</summary>
        private static JArray ReadJournal()
        {
            var result = new JArray();
            foreach (var row in ReadRange(DeyeSheet.JournalSheet + "!A:G"))
            {
                var obj = Text(row, 0);
                var date = Date(row, 1);
                var onOff = Number(row, 2);
                if ((obj != "Світло" && obj != "Генератор") || !date.HasValue || (onOff != 0 && onOff != 1))
                {
                    continue; // заголовок або сміття
                }
                result.Add(new JObject
                {
                    ["object"] = obj, ["at"] = Ms(date), ["onOff"] = onOff,
                    ["hoursPowerOff"] = Number(row, 4), ["hoursGenOn"] = Number(row, 3), ["peakW"] = Number(row, 6),
                    ["test"] = Text(row, 5).ToLowerInvariant() == "так"
                });
            }
            return result;
        }

        /// <summary>Заправки. Тестові рядки і рядки "Не враховувати" не передаємо. Ім'я: "Користувач Ім'я", інакше "Користувач Код".</summary>
        private static JArray ReadRefuels()
        {
            var result = new JArray();
            foreach (var row in ReadRange(SpreadSheet.SheetFuelStatistic + "!A:K"))
            {
                var date = Date(row, RefuelDateCol);
                if (!date.HasValue)
                {
                    continue;
                }
                if (Text(row, RefuelTestCol).ToLowerInvariant() == "так" || Text(row, RefuelIgnoreCol).ToLowerInvariant() == "так")
                {
                    continue;
                }
                var by = Text(row, RefuelNameCol);
                if (by == "")
                {
                    by = Text(row, RefuelCodeCol);
                }
                result.Add(new JObject { ["at"] = Ms(date), ["liters"] = Number(row, RefuelLitersCol), ["by"] = by == "" ? null : by });
            }
            return result;
        }

        // ------------------------------------------------------------------------------------
        // Допоміжні
        // ------------------------------------------------------------------------------------

        private static string Text(IList<object> row, int index)
        {
            return index < row.Count && row[index] != null ? Convert.ToString(row[index]).Trim() : "";
        }

        private static double? Number(IList<object> row, int index)
        {
            if (index >= row.Count || row[index] == null) return null;
            if (row[index] is double) return (double)row[index];
            double parsed;
            var text = Convert.ToString(row[index]).Trim().Replace(" ", "").Replace(',', '.');
            return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed)
                ? parsed : (double?)null;
        }

        /// <summary>Дата-число Google Sheets (київський час) -> DateTime. Текст не приймаємо.</summary>
        private static DateTime? Date(IList<object> row, int index)
        {
            if (index >= row.Count || !(row[index] is double)) return null;
            var d = SheetsEpoch.AddDays((double)row[index]);
            return new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second);
        }

        /// <summary>Київський час -> мілісекунди Unix (UTC)</summary>
        private static long? Ms(DateTime? kyivTime)
        {
            if (!kyivTime.HasValue) return null;
            var kyiv = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            var local = DateTime.SpecifyKind(kyivTime.Value, DateTimeKind.Unspecified);
            // Неіснуюча година (перехід на літній час): беремо як є зі зсувом зимового часу
            var utc = kyiv.IsInvalidTime(local) ? local.AddHours(-2) : TimeZoneInfo.ConvertTimeToUtc(local, kyiv);
            return (long)(DateTime.SpecifyKind(utc, DateTimeKind.Utc) - UnixEpoch).TotalMilliseconds;
        }

        /// <summary>Хеш вмісту блоку: SHA-256 від компактного JSON, перші 32 символи hex</summary>
        private static string Hash(JArray rows)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rows.ToString(Formatting.None)));
                return string.Concat(bytes.Select(b => b.ToString("x2"))).Substring(0, 32);
            }
        }

        private static JObject Post(string url, string secret, JObject body)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("X-Ingest-Secret", secret);
                var response = client.SendAsync(request).Result;
                var text = response.Content.ReadAsStringAsync().Result;
                if (!response.IsSuccessStatusCode)
                {
                    // Секрет у повідомлення не потрапляє, лише код і відповідь Worker
                    throw new Exception($"Worker {(int)response.StatusCode}: {text}");
                }
                return JObject.Parse(text);
            }
        }
    }
}
