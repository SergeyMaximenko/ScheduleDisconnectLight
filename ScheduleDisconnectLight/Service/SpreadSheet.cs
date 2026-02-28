using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using static Google.Apis.Sheets.v4.SpreadsheetsResource.ValuesResource;
using System.Text.RegularExpressions;



namespace Service
{
    public static class SpreadSheet
    {
        public static string SpreadsheetId = "1G20MV3_PX9OIu1vSaCB_vaOFJjeu9lnVg3ZR2QiPI2s";

        public const string SheetNameFuelStatistic = "ЗаправкаСтатистика";
        public const string SheetNameFuelStatus = "ЗаправкаСтатус";

        public const string SheetAvgRefuel = "СередніВитрати";
        
        public const string SheetParam = "Параметри";

        public const string SheetNameTehService = "ТОСтатистика";

        

        public const string SheetNameOnOffStatistic = "OnOffСтатистика";
        public const string SheetNameOnOffStatus = "OnOffСтатус";
        public const string SheetNameZvonokCall = "МодемЗвонки";

        // public const string SheetNameTOStatistic = "ТоСтатистика";


        private static SheetsService _service;
        

        public static SheetsService Service
        {
            get
            {
                if (_service != null)
                {
                    return _service;
                }

                GoogleCredential credential;
                if (Api.IsGitHub())
                {
                    credential = GoogleCredential.GetApplicationDefault();

                }
                else
                {

                    string repoRoot = Path.GetFullPath(
                       Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..")
                       );
                    string serviceAccountFile = Path.Combine(repoRoot, "nodal-reserve-445809-v0-a6ece564837c.json");


                    // Авторизация

                    using (var stream = new FileStream(serviceAccountFile, FileMode.Open, FileAccess.Read))
                    {
                        credential = GoogleCredential.FromStream(stream).CreateScoped(SheetsService.Scope.Spreadsheets);
                    }
                }
                _service = new SheetsService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Sheets Processor"
                });

                return _service;
            }

        }

        

        public static void AddNote(string pageExcel, int rowIndex, int columnIndex, string note)
        {

            var spreadsheet = Service.Spreadsheets.Get(SpreadsheetId).Execute();

            var sheet = spreadsheet.Sheets
                .FirstOrDefault(s => s.Properties.Title == pageExcel);

            if (sheet == null)
            {
                Console.WriteLine($"Лист '{pageExcel}' не знайдено");
                return;

            }

            var sheetId = (int)sheet.Properties.SheetId;

            var request = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request>
                {
                    new Request
                    {
                        UpdateCells = new UpdateCellsRequest
                        {
                            Start = new GridCoordinate
                            {
                                SheetId = sheetId,   // ID листа!
                                RowIndex = rowIndex,        // 0-based
                                ColumnIndex = columnIndex      // 0-based
                            },
                            Rows = new List<RowData>
                            {
                                new RowData
                                {
                                    Values = new List<CellData>
                                    {
                                        new CellData
                                        {
                                            Note = note
                                        }
                                    }
                                }
                            },
                            Fields = "note"
                        }
                    }
                }
            };

            


            GoogleExecuteRetry.Exec("SetNote: " + pageExcel ,
             () =>
             {
                 Service.Spreadsheets.BatchUpdate(request, SpreadsheetId).Execute();
             });


        }

        /*
        public static string GetNote(SheetsService service, string spreadsheetId, string pageExcel, int rowIndex, int columnIndex)
        {
            var request = service.Spreadsheets.Get(spreadsheetId);

            // Беремо ТІЛЬКИ note — швидко і без зайвого
            request.Fields =
                "sheets(data(rowData(values(note))))";


            var response = request.Execute();

            var spreadsheet = service.Spreadsheets.Get(SpreadsheetId).Execute();

            var sheet = spreadsheet.Sheets
                .FirstOrDefault(s => s.Properties.Title == pageExcel);

            if (sheet == null)
            {
                Console.WriteLine($"Лист '{pageExcel}' не знайдено");
                return string.Empty;

            }



            var cell = sheet.Data[0]
                .RowData[rowIndex]
                .Values[columnIndex];

            return cell.Note;
        }
        */


        private static string columnIndexToLetter(int columnIndex)
        {
            // 0 -> A, 1 -> B, 25 -> Z, 26 -> AA
            columnIndex++; // переводимо в 1-based
            string columnLetter = "";

            while (columnIndex > 0)
            {
                int mod = (columnIndex - 1) % 26;
                columnLetter = (char)('A' + mod) + columnLetter;
                columnIndex = (columnIndex - mod) / 26;
            }

            return columnLetter;
        }

        

    private static (int rowIndex0, int colIndex0) ParseA1StartCell(string a1Range)
    {
        // Примеры:
        // "Лист!A12:Z12"
        // "Лист!B7"
        // "'Лист 1'!A1:C1"
        var s = a1Range;
        var excl = s.LastIndexOf('!');
        if (excl >= 0) s = s.Substring(excl + 1);

        // берём только стартовую часть до ":" (если есть)
        var colon = s.IndexOf(':');
        if (colon >= 0) s = s.Substring(0, colon);

        // A12
        var m = Regex.Match(s, @"^(?<col>[A-Z]+)(?<row>\d+)$", RegexOptions.IgnoreCase);
        if (!m.Success) throw new FormatException("Cannot parse A1: " + a1Range);

        var colLetters = m.Groups["col"].Value.ToUpperInvariant();
        var row1 = int.Parse(m.Groups["row"].Value);

        int col1 = 0;
        foreach (var ch in colLetters)
            col1 = col1 * 26 + (ch - 'A' + 1);

        return (row1 - 1, col1 - 1); // to 0-based
    }

    public static void SetValue(string pageExcel, int rowIndex, int columnIndex, object value)
        {
            string columnLetter = columnIndexToLetter(columnIndex);


            // RowIndex предполагается 1-based (как у тебя сейчас)
            string updateRange = $"{pageExcel}!{columnLetter}{rowIndex + 1}";

            var valueRange = new ValueRange
            {
                Values = new List<IList<object>>
                    {
                        new List<object> { value }
                    }
            };

            var updateRequest = Service.Spreadsheets.Values.Update(
                valueRange,
                SpreadsheetId,
                updateRange);

            updateRequest.ValueInputOption =
                SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;


            GoogleExecuteRetry.Exec("SetValue: "+pageExcel + " " + updateRange,
                () =>
                {
                    updateRequest.Execute();
                });


        }

        public static void AppendRow(string pageExcel, List<object> values)
        {
            if (Service == null) throw new ArgumentNullException(nameof(Service));
            if (string.IsNullOrWhiteSpace(pageExcel)) throw new ArgumentException("pageExcel is empty", nameof(pageExcel));
            if (values == null) throw new ArgumentNullException(nameof(values));

            // Диапазон-«якорь»: достаточно указать лист, API само добавит строку в конец таблицы/диапазона.
            // Важно: если на листе есть "таблица" (связный диапазон данных), Append добавит ниже неё.
            // Если данных нет — добавит в первую строку.
            string range = $"{pageExcel}!A:Z"; // можно расширить, но это безопасный якорь

            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { values }
            };

            var appendRequest = Service.Spreadsheets.Values.Append(valueRange, SpreadsheetId, range);

            // USERENTERED = как будто ввели руками (формулы, даты и т.п. будут распознаны)
            appendRequest.ValueInputOption =
                SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

            // INSERT_ROWS = реально вставлять новые строки (а не перетирать)
            appendRequest.InsertDataOption =
                SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;

            // Опционально: что возвращать (можно оставить по умолчанию)
            // appendRequest.IncludeValuesInResponse = false;

            GoogleExecuteRetry.Exec($"AppendRow: {pageExcel}",
                () =>
                {
                    appendRequest.Execute();
                });
        }

        public static void AppendRow(string pageExcel, List<CellValueNote> values)
        {
            if (Service == null) throw new ArgumentNullException(nameof(Service));
            if (string.IsNullOrWhiteSpace(pageExcel)) throw new ArgumentException("pageExcel is empty", nameof(pageExcel));
            if (values == null) throw new ArgumentNullException(nameof(values));

            // 1) Append значений
            string range = $"{pageExcel}!A:Z";

            var rowValues = values.Select(v => v?.Value).Cast<object>().ToList();

            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { rowValues }
            };

            var appendRequest = Service.Spreadsheets.Values.Append(valueRange, SpreadsheetId, range);
            appendRequest.ValueInputOption =
                SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            appendRequest.InsertDataOption =
                SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;

            // Чтобы узнать строку, куда реально добавило
            appendRequest.IncludeValuesInResponse = false;

            AppendValuesResponse appendResp = null;

            GoogleExecuteRetry.Exec($"AppendRow: {pageExcel}",
                () => { appendResp = appendRequest.Execute(); });

            // 2) Если нет ни одного комментария — выходим
            var hasAnyNote = values.Any(v => !string.IsNullOrWhiteSpace(v?.Note));
            if (!hasAnyNote) return;

            // 3) Находим rowIndex, куда вставило
            // appendResp.Updates.UpdatedRange обычно вида "Лист!A123:Z123"
            var updatedRange = appendResp?.Updates?.UpdatedRange;
            if (string.IsNullOrWhiteSpace(updatedRange))
                throw new Exception("Append succeeded but UpdatedRange is empty. Can't set notes.");

            var (rowIndex0, colIndex0) = ParseA1StartCell(updatedRange);

            // 4) Получаем sheetId для pageExcel
            var spreadsheet = Service.Spreadsheets.Get(SpreadsheetId).Execute();
            var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == pageExcel);
            if (sheet == null) throw new Exception($"Лист '{pageExcel}' не знайдено");

            var sheetId = (int)sheet.Properties.SheetId;

            // 5) BatchUpdate на notes только в этой строке
            // Важно: задаём CellData по всем колонкам, но Note ставим только где он есть.
            // Для остальных Note оставляем null — в новой строке это безвредно.
            var cellDatas = new List<CellData>();
            for (int i = 0; i < values.Count; i++)
            {
                var note = values[i]?.Note;
                cellDatas.Add(new CellData
                {
                    Note = string.IsNullOrWhiteSpace(note) ? null : note
                });
            }

            var batch = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request>
        {
            new Request
            {
                UpdateCells = new UpdateCellsRequest
                {
                    Start = new GridCoordinate
                    {
                        SheetId = sheetId,
                        RowIndex = rowIndex0,
                        ColumnIndex = colIndex0 // обычно 0 (A), но парсер универсальный
                    },
                    Rows = new List<RowData>
                    {
                        new RowData
                        {
                            Values = cellDatas
                        }
                    },
                    Fields = "note"
                }
            }
        }
            };

            GoogleExecuteRetry.Exec($"AppendRowNotes: {pageExcel} row={rowIndex0 + 1}",
                () => { Service.Spreadsheets.BatchUpdate(batch, SpreadsheetId).Execute(); });
        }


        public sealed class CellValueNote
        {
            public object Value { get; set; }
            public string Note { get; set; } // null/"" => не добавлять
        }


        public static T GetValue<T>(string pageExcel, int rowIndex, int columnIndex)
        {
            string columnLetter = columnIndexToLetter(columnIndex);
            string range = $"{pageExcel}!{columnLetter}{rowIndex + 1}";

            var request = Service.Spreadsheets.Values.Get(
                SpreadsheetId,
                range
            );

            //TResponse response;
            ValueRange response = null;

            GoogleExecuteRetry.Exec("GetValue: " + pageExcel + " " + range,
                () =>
                {
                    response = request.Execute();
                });

            
         

            if (response == null || response.Values == null || response.Values.Count == 0)
            {
                return default(T);
            }
                

            if (response.Values[0].Count == 0)
            {
                return default(T);
            }
                

            return TypeTools.Convert<T>(response.Values[0][0]);
        }

    }

    public class GoogleExecuteRetry
    {
        public string Info;
        public Action Action;

        public GoogleExecuteRetry(string info, Action action)
        {
            Info = info;
            Action = action;
        }

        public void Exec()
        {


            void logError(int attempt,GoogleApiException ex)
            {
                Console.WriteLine("Попытка подключения " + attempt);

                Console.WriteLine("--------------");
                Console.WriteLine($"G.Sheets ERROR CODE => {(int)ex.HttpStatusCode} {ex.HttpStatusCode}");
                Console.WriteLine($"G.Sheets INFO => {Info}");
                Console.WriteLine($"G.Sheets MESSAGE => {ex.Error?.Message}");

                if (ex.Error?.Errors != null)
                {
                    foreach (var e in ex.Error.Errors)
                    {
                        Console.WriteLine($"G.Sheets => {e.Reason}: {e.Message}");
                    }

                }
                Console.WriteLine($"G.Sheets STACK => {ex.StackTrace}");
                Console.WriteLine("--------------");
            }

            var rnd = new Random();
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    Action();
                    if (attempt > 1)
                    {
                        new SenderTelegram() { SendType = SendType.OnlyTest }.Send("Подключено к G.Sheets  ("+ Info + ") с " + attempt+" попытки");
                    }

                    break; // успіх
                }
                catch (GoogleApiException ex) when (
                    ex.HttpStatusCode == HttpStatusCode.InternalServerError ||   // 500
                    ex.HttpStatusCode == HttpStatusCode.ServiceUnavailable ||    // 503
                    (int)ex.HttpStatusCode == 429)                               // 429
                {
                    logError(attempt, ex);

                    if (attempt == 5)
                    {
                        // Вче попытки исчерпаны. Выдать ошибку 
                        throw;
                    }
                    

                    // експоненційне зростання по требованиям Google
                    int delayMs = (int)(400 * Math.Pow(2, attempt - 1));
                    delayMs += rnd.Next(0, 250); // jitter
                    Thread.Sleep(delayMs);
                }
                catch (GoogleApiException ex) // ← ВСІ ІНШІ Google API помилки
                {
                    logError(attempt, ex);

                    throw;
                }

                catch (Exception ex)
                {
                    Console.WriteLine($"G.Sheets INFO (ошибка типу Exception ) => {Info}");
                    throw;
                }
            }
        }

        public static void Exec(string info, Action action)
        {
            new GoogleExecuteRetry(info, action).Exec();
        }
    }

}


