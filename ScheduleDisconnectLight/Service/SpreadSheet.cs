using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service
{
    public class SpreadSheet
    {
        public static string SpreadsheetId = "1G20MV3_PX9OIu1vSaCB_vaOFJjeu9lnVg3ZR2QiPI2s";

        public const string SheetNameFuelStatistic = "ЗаправкаСтатистика";
        public const string SheetNameFuelStatus = "ЗаправкаСтатус";

        public const string SheetNameOnOffStatistic = "OnOffСтатистика";
        public const string SheetNameOnOffStatus = "OnOffСтатус";
        
        

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

            service.Spreadsheets
                .BatchUpdate(request, SpreadsheetId)
                .Execute();

        }


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

            updateRequest.Execute();

        }

        public static T GetValue<T>(SheetsService service, string pageExcel, int rowIndex, int columnIndex)
        {
            string columnLetter = columnIndexToLetter(columnIndex);
            string range = $"{pageExcel}!{columnLetter}{rowIndex + 1}";

            var request = service.Spreadsheets.Values.Get(
                SpreadsheetId,
                range
            );

            var response = request.Execute();

            if (response.Values == null || response.Values.Count == 0)
                return default(T);

            if (response.Values[0].Count == 0)
                return default(T); 

            return TypeTools.Convert<T>(response.Values[0][0]);
        }

    }

}


