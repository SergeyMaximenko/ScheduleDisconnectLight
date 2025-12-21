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

namespace ScheduleDisconnectLight
{
   
}
           


/*
            // 1. Получаем строки, которые НЕ обработаны
            var rows = GetUnprocessedRows(service, _spreadsheetId, _sheetName);

            Console.WriteLine($"Найдено необработанных строк: {rows.Count}");

            // 2. Обрабатываем коды (ваша логика)
            foreach (var row in rows)
            {
                Console.WriteLine($"Обрабатываю код: {row.Code}");

            }

            // 3. Проставляем "Да"
            markRowsAsProcessed(service, _spreadsheetId, _sheetName, rows);

            Console.WriteLine("Готово! Все строки помечены как обработанные.");


        }

        private static void markRowsAsProcessed(SheetsService service, string spreadsheetId, string sheetName, List<SheetRow> rows)
        {
            foreach (var row in rows)
            {
                string updateRange = $"{sheetName}!B{row.RowIndex}";

                var valueRange = new ValueRange
                {
                    Values = new List<IList<object>>
            {
                new List<object> { "Да" }
            }
                };

                var updateRequest = service.Spreadsheets.Values.Update(
                    valueRange,
                    spreadsheetId,
                    updateRange);

                updateRequest.ValueInputOption =
                    SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

                updateRequest.Execute();
            }
        }

        public static List<SheetRow> GetUnprocessedRows(
    SheetsService service,
    string spreadsheetId,
    string sheetName)
        {
            string range = $"{sheetName}!A:B";

            var request = service.Spreadsheets.Values.Get(spreadsheetId, range);
            var response = request.Execute();
            var values = response.Values;

            var result = new List<SheetRow>();

            if (values == null || values.Count == 0)
                return result;

            // Пропускаем заголовок
            for (int i = 1; i < values.Count; i++)
            {
                var row = values[i];

                string code = row.Count > 0 ? row[0].ToString() : "";
                string processed = row.Count > 1 ? row[1].ToString() : "";

                if (string.IsNullOrWhiteSpace(code))
                    continue;

                // Нужно только где НЕ обработано
                if (string.IsNullOrWhiteSpace(processed))
                {
                    result.Add(new SheetRow
                    {
                        Code = code,
                        RowIndex = i + 1
                    });
                }
            }

            return result;
        }

    }

    public class SheetRow
    {
        public string Code { get; set; }
        public int RowIndex { get; set; }
    }


*/