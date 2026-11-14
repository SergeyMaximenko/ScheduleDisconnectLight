using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Service
{
    /// <summary>
    /// Що зараз записано в закладці DeyeСтатус (стовпець Прод або ТЕСТ залежно від режиму).
    /// Порожнє значення в таблиці = null.
    /// </summary>
    public class DeyeStored
    {
        public DateTime? RequestAt;
        public DateTime? MeasuredAt;

        public int? GridStatus;
        public DateTime? GridDate;
        public double? GridVoltage;

        public int? GenStatus;
        public DateTime? GenDate;
        public double? GenPower;
        public double? GenVoltage;
        /// <summary>Максимальна потужність генератора за поточний запуск (Вт). null, коли генератор не працює.</summary>
        public double? GenPeak;

        public int? InverterStatus;
        public DateTime? InverterDate;
        public double? LoadWatts;

        public double? Soc;
        public int? BatteryMode;
        public double? BatteryWatts;
    }

    /// <summary>Рядок журналу (закладка DeyeСтатистика)</summary>
    public class DeyeJournalRow
    {
        /// <summary>"Світло" або "Генератор"</summary>
        public string Object;
        public DateTime Date;
        /// <summary>1 - з'явилось/запустився, 0 - зникло/зупинився</summary>
        public int OnOff;
        /// <summary>Скільки годин працював генератор (пишемо при зупинці генератора)</summary>
        public double? HoursGenOn;
        /// <summary>Скільки годин не було світла (пишемо коли світло з'явилось)</summary>
        public double? HoursPowerOff;
        /// <summary>Максимальна потужність генератора за запуск, Вт (пишемо при зупинці генератора)</summary>
        public double? PeakW;
    }

    /// <summary>
    /// Коди параметрів у колонці A закладки DeyeСтатус. Рядок параметра програма знаходить саме за кодом.
    /// Коди НЕ МІНЯТИ (їх використовує код); назви й коментарі в таблиці можна змінювати як завгодно.
    /// </summary>
    public static class DeyeCode
    {
        public const string RequestTime = "REQUEST_TIME"; // Час запиту до Deye
        public const string MeasureTime = "MEASURE_TIME"; // Час вимірювання Deye
        public const string GridStatus = "GRID_STATUS";   // Мережа. Статус
        public const string GridDate = "GRID_DATE";       // Мережа. Дата статусу
        public const string GridVoltage = "GRID_VOLTAGE"; // Мережа. Напруга
        public const string GenStatus = "GEN_STATUS";     // Генератор. Статус
        public const string GenDate = "GEN_DATE";         // Генератор. Дата статусу
        public const string GenPower = "GEN_POWER";       // Генератор. Потужність
        public const string GenVoltage = "GEN_VOLTAGE";   // Генератор. Напруга
        public const string GenPeak = "GEN_PEAK";         // Генератор. Макс. потужність за запуск
        public const string InvStatus = "INV_STATUS";     // Інвертор. Статус
        public const string InvDate = "INV_DATE";         // Інвертор. Дата статусу
        public const string InvLoad = "INV_LOAD";         // Інвертор. Навантаження
        public const string BatSoc = "BAT_SOC";           // Батарея. Заряд
        public const string BatMode = "BAT_MODE";         // Батарея. Режим
        public const string BatPower = "BAT_POWER";       // Батарея. Потужність

        /// <summary>Усі коди, які обов'язково мають бути в таблиці</summary>
        public static readonly string[] All =
        {
            RequestTime, MeasureTime,
            GridStatus, GridDate, GridVoltage,
            GenStatus, GenDate, GenPower, GenVoltage, GenPeak,
            InvStatus, InvDate, InvLoad,
            BatSoc, BatMode, BatPower
        };
    }

    /// <summary>
    /// Робота із закладками DeyeСтатус (поточний стан) і DeyeСтатистика (журнал змін).
    ///
    /// ЯК ЗНАХОДИМО ПАРАМЕТРИ: за КОДОМ у колонці A (наприклад, GRID_STATUS), а не за номером рядка чи назвою.
    /// Тому рядки можна вставляти, видаляти й переставляти, а назви в колонці B змінювати.
    /// Єдине, що зафіксоване: колонка A = код, колонка C = "Значення Прод", колонка D = "Значення ТЕСТ".
    ///
    /// ПРО ТЕСТ: локальний запуск (не GitHub) = тест. Значення йдуть у колонку D ("Значення ТЕСТ"),
    /// у журналі в колонці F "Режим теста" ставиться "так". Запуск на GitHub пише в колонку C ("Значення Прод").
    /// </summary>
    public static class DeyeSheet
    {
        public const string StatusSheet = "DeyeСтатус";
        public const string JournalSheet = "DeyeСтатистика";

        // Колонки закладки DeyeСтатус (індекси з 0)
        private const int CodeColumnIndex = 0;  // A: код параметра
        private const int ProdColumnIndex = 2;  // C: Значення Прод
        private const int TestColumnIndex = 3;  // D: Значення ТЕСТ

        /// <summary>Скільки рядків закладки читаємо (з запасом на нові параметри)</summary>
        private const int MaxRows = 100;

        /// <summary>Індекс колонки значень: Прод на GitHub, ТЕСТ локально</summary>
        private static int ValueColumnIndex
        {
            get { return Api.IsRegTest() ? TestColumnIndex : ProdColumnIndex; }
        }

        private static readonly DateTime SheetsEpoch = new DateTime(1899, 12, 30);

        private static DeyeStored _cache;
        private static Dictionary<string, int> _rowByCode; // код -> номер рядка з 0
        private static Dictionary<string, int> _sheetIds;

        // ----------------------------------------------------------------------------------------
        // Читання
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Прочитати поточний стан із таблиці (один запит). Результат кешується до наступного запису.
        /// Заодно визначає, в якому рядку який код.
        /// </summary>
        public static DeyeStored ReadStored()
        {
            if (_cache != null)
            {
                return _cache;
            }

            // Спершу перевіряємо, що закладка існує: якщо назва в таблиці інша, отримаємо зрозуміле повідомлення
            SheetId(StatusSheet);

            var request = SpreadSheet.Service.Spreadsheets.Values.Get(SpreadSheet.SpreadsheetId, $"'{StatusSheet}'!A1:D{MaxRows}");
            // Беремо "сирі" значення: дати як числа, а не як текст у регіональному форматі
            request.ValueRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.UNFORMATTEDVALUE;
            request.DateTimeRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.DateTimeRenderOptionEnum.SERIALNUMBER;

            ValueRange response = null;
            GoogleExecuteRetry.Exec("Read " + StatusSheet, () => { response = request.Execute(); });

            var rows = response?.Values ?? new List<IList<object>>();
            _rowByCode = FindCodes(rows);

            int col = ValueColumnIndex;

            object cell(string code)
            {
                var r = rows[_rowByCode[code]];
                return col < r.Count ? r[col] : null;
            }

            var stored = new DeyeStored
            {
                RequestAt = ToDate(cell(DeyeCode.RequestTime)),
                MeasuredAt = ToDate(cell(DeyeCode.MeasureTime)),
                GridStatus = ToInt(cell(DeyeCode.GridStatus)),
                GridDate = ToDate(cell(DeyeCode.GridDate)),
                GridVoltage = ToDouble(cell(DeyeCode.GridVoltage)),
                GenStatus = ToInt(cell(DeyeCode.GenStatus)),
                GenDate = ToDate(cell(DeyeCode.GenDate)),
                GenPower = ToDouble(cell(DeyeCode.GenPower)),
                GenVoltage = ToDouble(cell(DeyeCode.GenVoltage)),
                GenPeak = ToDouble(cell(DeyeCode.GenPeak)),
                InverterStatus = ToInt(cell(DeyeCode.InvStatus)),
                InverterDate = ToDate(cell(DeyeCode.InvDate)),
                LoadWatts = ToDouble(cell(DeyeCode.InvLoad)),
                Soc = ToDouble(cell(DeyeCode.BatSoc)),
                BatteryMode = ToInt(cell(DeyeCode.BatMode)),
                BatteryWatts = ToDouble(cell(DeyeCode.BatPower))
            };

            _cache = stored;
            return stored;
        }

        /// <summary>
        /// Знаходить рядки за кодами в колонці A. Якщо якогось обов'язкового коду немає
        /// або код повторюється - кидає зрозумілу помилку (без запису "навмання").
        /// </summary>
        private static Dictionary<string, int> FindCodes(IList<IList<object>> rows)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var duplicates = new List<string>();

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.Count <= CodeColumnIndex || r[CodeColumnIndex] == null)
                {
                    continue;
                }
                var code = Convert.ToString(r[CodeColumnIndex], CultureInfo.InvariantCulture).Trim();
                if (code.Length == 0)
                {
                    continue;
                }
                if (map.ContainsKey(code))
                {
                    duplicates.Add(code);
                    continue;
                }
                map[code] = i;
            }

            var missing = DeyeCode.All.Where(c => !map.ContainsKey(c)).ToList();
            if (missing.Count > 0)
            {
                throw new Exception($"У закладці '{StatusSheet}' (колонка A) немає кодів: {string.Join(", ", missing)}");
            }
            if (duplicates.Count > 0)
            {
                throw new Exception($"У закладці '{StatusSheet}' (колонка A) коди повторюються: {string.Join(", ", duplicates)}");
            }
            return map;
        }

        // ----------------------------------------------------------------------------------------
        // Запис (один атомарний batchUpdate)
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Записати поточний стан і (якщо є) рядки журналу ОДНИМ запитом.
        /// Запит атомарний: або записується все, або нічого. Тому не може вийти, що журнал доданий, а статус не оновлений.
        /// Якщо запит не пройшов, наступний запуск побачить ту саму різницю й повторить запис (дублікатів не буде,
        /// бо порівняння йде зі станом у DeyeСтатус, а не з журналом).
        /// </summary>
        public static void WriteAll(DeyeStored state, IList<DeyeJournalRow> journal)
        {
            EnsureCodesLoaded();

            var requests = new List<Request>();

            // 1. Журнал: нові рядки додаємо в кінець
            if (journal != null && journal.Count > 0)
            {
                var isTest = Api.IsRegTest();
                var rows = new List<RowData>();
                foreach (var item in journal)
                {
                    rows.Add(new RowData
                    {
                        Values = new List<CellData>
                        {
                            TextCell(item.Object),
                            DateCell(item.Date),
                            NumberCell((double?)item.OnOff),
                            NumberCell(item.HoursGenOn, "0.000"),
                            NumberCell(item.HoursPowerOff, "0.000"),
                            // "Режим теста": "так" для тестових запусків
                            isTest ? TextCell("так") : EmptyCell(),
                            // G: макс. потужність генератора за запуск, Вт
                            NumberCell(item.PeakW, "0")
                        }
                    });
                }

                requests.Add(new Request
                {
                    AppendCells = new AppendCellsRequest
                    {
                        SheetId = SheetId(JournalSheet),
                        Rows = rows,
                        Fields = "userEnteredValue,userEnteredFormat.numberFormat"
                    }
                });
            }

            // 2. Поточний стан: кожне значення в свій рядок за кодом (інші комірки таблиці не чіпаємо)
            requests.Add(CellRequest(DeyeCode.RequestTime, DateCell(state.RequestAt)));
            requests.Add(CellRequest(DeyeCode.MeasureTime, DateCell(state.MeasuredAt)));
            requests.Add(CellRequest(DeyeCode.GridStatus, NumberCell(state.GridStatus)));
            requests.Add(CellRequest(DeyeCode.GridDate, DateCell(state.GridDate)));
            requests.Add(CellRequest(DeyeCode.GridVoltage, NumberCell(state.GridVoltage)));
            requests.Add(CellRequest(DeyeCode.GenStatus, NumberCell(state.GenStatus)));
            requests.Add(CellRequest(DeyeCode.GenDate, DateCell(state.GenDate)));
            requests.Add(CellRequest(DeyeCode.GenPower, NumberCell(state.GenPower)));
            requests.Add(CellRequest(DeyeCode.GenVoltage, NumberCell(state.GenVoltage)));
            requests.Add(CellRequest(DeyeCode.GenPeak, NumberCell(state.GenPeak)));
            requests.Add(CellRequest(DeyeCode.InvStatus, NumberCell(state.InverterStatus)));
            requests.Add(CellRequest(DeyeCode.InvDate, DateCell(state.InverterDate)));
            requests.Add(CellRequest(DeyeCode.InvLoad, NumberCell(state.LoadWatts)));
            requests.Add(CellRequest(DeyeCode.BatSoc, NumberCell(state.Soc)));
            requests.Add(CellRequest(DeyeCode.BatMode, NumberCell(state.BatteryMode)));
            requests.Add(CellRequest(DeyeCode.BatPower, NumberCell(state.BatteryWatts)));

            Execute(requests, "Write " + StatusSheet);

            _cache = state; // тепер у таблиці саме це
        }

        /// <summary>
        /// Deye не відповів або дані застарілі: оновлюємо ТІЛЬКИ "Час запиту до Deye" (щоб було видно, що застосунок живий).
        /// Усі інші значення лишаються як були.
        /// </summary>
        public static void WriteRequestTimeOnly(DateTime requestAt)
        {
            EnsureCodesLoaded();

            var requests = new List<Request> { CellRequest(DeyeCode.RequestTime, DateCell(requestAt)) };
            Execute(requests, "Write request time " + StatusSheet);

            if (_cache != null)
            {
                _cache.RequestAt = requestAt;
            }
        }

        /// <summary>Якщо ще не знаємо рядків кодів, читаємо таблицю (зазвичай уже прочитано в ReadStored)</summary>
        private static void EnsureCodesLoaded()
        {
            if (_rowByCode == null)
            {
                ReadStored();
            }
        }

        /// <summary>Запит на запис однієї комірки: рядок за кодом, колонка за режимом (Прод/ТЕСТ)</summary>
        private static Request CellRequest(string code, CellData cell)
        {
            return new Request
            {
                UpdateCells = new UpdateCellsRequest
                {
                    Start = new GridCoordinate
                    {
                        SheetId = SheetId(StatusSheet),
                        RowIndex = _rowByCode[code],
                        ColumnIndex = ValueColumnIndex
                    },
                    Rows = new List<RowData> { new RowData { Values = new List<CellData> { cell } } },
                    // Змінюємо тільки значення і формат числа. Рамки, колір, ширина не чіпаємо.
                    Fields = "userEnteredValue,userEnteredFormat.numberFormat"
                }
            };
        }

        private static void Execute(List<Request> requests, string info)
        {
            var batch = new BatchUpdateSpreadsheetRequest { Requests = requests };
            GoogleExecuteRetry.Exec(info, () =>
            {
                SpreadSheet.Service.Spreadsheets.BatchUpdate(batch, SpreadSheet.SpreadsheetId).Execute();
            });
        }

        /// <summary>Внутрішній номер аркуша (потрібен для batchUpdate). Один запит на запуск.</summary>
        private static int SheetId(string title)
        {
            if (_sheetIds == null)
            {
                var request = SpreadSheet.Service.Spreadsheets.Get(SpreadSheet.SpreadsheetId);
                request.Fields = "sheets.properties(sheetId,title)";
                Spreadsheet spreadsheet = null;
                GoogleExecuteRetry.Exec("Get sheets ids", () => { spreadsheet = request.Execute(); });

                _sheetIds = new Dictionary<string, int>();
                foreach (var sheet in spreadsheet.Sheets)
                {
                    _sheetIds[sheet.Properties.Title] = (int)sheet.Properties.SheetId.GetValueOrDefault();
                }
            }

            int id;
            if (!_sheetIds.TryGetValue(title, out id))
            {
                // Показуємо назви, які є в таблиці, у лапках (видно зайві пробіли) і з кодами літер
                // (видно, якщо замість кириличної літери стоїть схожа латинська).
                var existing = string.Join("; ", _sheetIds.Keys.Select(k => "'" + k + "' [" + string.Join(" ", k.Select(c => ((int)c).ToString("X"))) + "]"));
                throw new Exception($"Закладку '{title}' [{string.Join(" ", title.Select(c => ((int)c).ToString("X")))}] не знайдено в таблиці. Є такі: {existing}");
            }
            return id;
        }

        // ----------------------------------------------------------------------------------------
        // Комірки
        // ----------------------------------------------------------------------------------------

        private static CellData EmptyCell()
        {
            return new CellData();
        }

        private static CellData TextCell(string text)
        {
            return new CellData { UserEnteredValue = new ExtendedValue { StringValue = text } };
        }

        private static CellData NumberCell(double? value, string pattern = null)
        {
            if (!value.HasValue)
            {
                return EmptyCell();
            }
            var cell = new CellData { UserEnteredValue = new ExtendedValue { NumberValue = value.Value } };
            if (pattern != null)
            {
                cell.UserEnteredFormat = new CellFormat { NumberFormat = new NumberFormat { Type = "NUMBER", Pattern = pattern } };
            }
            return cell;
        }

        private static CellData NumberCell(int? value)
        {
            return NumberCell(value.HasValue ? (double?)value.Value : null);
        }

        /// <summary>Дата й час як справжня дата Google Sheets у форматі 2026-10-08 23:12:28</summary>
        private static CellData DateCell(DateTime? value)
        {
            if (!value.HasValue)
            {
                return EmptyCell();
            }
            return new CellData
            {
                UserEnteredValue = new ExtendedValue { NumberValue = (value.Value - SheetsEpoch).TotalDays },
                UserEnteredFormat = new CellFormat
                {
                    NumberFormat = new NumberFormat { Type = "DATE_TIME", Pattern = "yyyy-mm-dd hh:mm:ss" }
                }
            };
        }

        // ----------------------------------------------------------------------------------------
        // Розбір значень із таблиці
        // ----------------------------------------------------------------------------------------

        private static double? ToDouble(object value)
        {
            if (value == null) return null;
            if (value is double) return (double)value;
            if (value is string)
            {
                var text = ((string)value).Trim().Replace(" ", "").Replace(",", ".");
                double parsed;
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : (double?)null;
            }
            try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); } catch { return null; }
        }

        private static int? ToInt(object value)
        {
            var d = ToDouble(value);
            return d.HasValue ? (int)Math.Round(d.Value) : (int?)null;
        }

        private static DateTime? ToDate(object value)
        {
            if (value == null) return null;

            var serial = value as double?;
            if (serial.HasValue)
            {
                // Округлюємо до секунди, щоб прибрати похибку дробового числа
                var date = SheetsEpoch.AddDays(serial.Value);
                return new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second);
            }

            if (value is string)
            {
                // Якщо хтось вручну ввів дату як текст
                DateTime parsed;
                if (DateTime.TryParseExact(((string)value).Trim(), new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd H:mm:ss", "yyyy-MM-dd HH:mm" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                {
                    return parsed;
                }
            }
            return null;
        }

        // ----------------------------------------------------------------------------------------
        // Зручні методи для решти коду (замість старого OnOffСтатус)
        // ----------------------------------------------------------------------------------------

        /// <summary>Світло є за даними таблиці</summary>
        public static bool IsGridOn()
        {
            return ReadStored().GridStatus == 1;
        }

        /// <summary>Генератор працює за даними таблиці</summary>
        public static bool IsGeneratorOn()
        {
            return ReadStored().GenStatus == 1;
        }
    }
}
