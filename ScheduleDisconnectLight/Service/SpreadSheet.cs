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
using System.Threading;
using static Google.Apis.Sheets.v4.SpreadsheetsResource.ValuesResource;




namespace Service
{
    public class SpreadSheet
    {
        public static string SpreadsheetId = "1G20MV3_PX9OIu1vSaCB_vaOFJjeu9lnVg3ZR2QiPI2s";

        public const string SheetNameFuelStatistic = "ЗаправкаСтатистика";
        public const string SheetNameFuelStatus = "ЗаправкаСтатус";

        public const string SheetAvgRefuel = "СередніВитрати";

        public const string SheetNameTehService = "ТОСтатистика";


        public const string SheetNameOnOffStatistic = "OnOffСтатистика";
        public const string SheetNameOnOffStatus = "OnOffСтатус";


       // public const string SheetNameTOStatistic = "ТоСтатистика";
        

        public SheetsService GetService()
        {
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
            return new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Sheets Processor"
            });

        }


        public static void AddNote(SheetsService service, string pageExcel, int rowIndex, int columnIndex, string note)
        {

            var spreadsheet = service.Spreadsheets.Get(SpreadsheetId).Execute();

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
                 service.Spreadsheets.BatchUpdate(request, SpreadsheetId).Execute();
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

        public static void SetValue(SheetsService service, string pageExcel, int rowIndex, int columnIndex, object value)
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

            var updateRequest = service.Spreadsheets.Values.Update(
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

        public static T GetValue<T>(SheetsService service, string pageExcel, int rowIndex, int columnIndex)
        {
            string columnLetter = columnIndexToLetter(columnIndex);
            string range = $"{pageExcel}!{columnLetter}{rowIndex + 1}";

            var request = service.Spreadsheets.Values.Get(
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


