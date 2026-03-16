using ClosedXML.Excel;

namespace TorontoDaycares.Exporters
{
    public record class ExcelExporter(string fileName) : IExporter
    {
        private string FileName { get; } = fileName;

        public async Task ExportAsync(Models.DaycareSearchResponse response)
        {
            if (!Directory.Exists(Path.GetDirectoryName(FileName)))
            {
                throw new DirectoryNotFoundException($"Directory not found: {Path.GetDirectoryName(FileName)}");
            }

            var items = response.TopPrograms
                .GroupBy(x => x.Program.ProgramType)
                .ToDictionary(g => g.Key, g => g.Select(x => (x.Daycare, x.Program)).ToList());

            if (items.Count == 0)
            {
                throw new InvalidOperationException("No programs to export.");
            }

            using var workbook = new XLWorkbook();

            foreach (var programType in items)
            {
                var worksheet = workbook.Worksheets.Add(programType.Key.ToString());

                worksheet.Row(1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Value = "Name";
                worksheet.Cell(1, 2).Value = "Rating";
                worksheet.Cell(1, 3).Value = "Capacity";
                worksheet.Cell(1, 4).Value = "Vacancy";
                worksheet.Cell(1, 5).Value = "Address";
                worksheet.Cell(1, 6).Value = "Url";

                var row = 2;
                foreach (var item in programType.Value)
                {
                    worksheet.Cell(row, 1).Value = item.Daycare.Name;
                    worksheet.Cell(row, 2).Value = item.Program.Rating.Value;
                    worksheet.Cell(row, 3).Value = item.Program.Capacity;
                    if (item.Program.Vacancy.HasValue)
                        worksheet.Cell(row, 4).Value = item.Program.Vacancy.Value;
                    worksheet.Cell(row, 5).Value = item.Daycare.Address;
                    worksheet.Cell(row, 6).SetHyperlink(new XLHyperlink(item.Daycare.Uri));
                    row++;
                }

                worksheet.Columns().AdjustToContents();
            }

            await Task.Run(() => workbook.SaveAs(FileName));
        }
    }
}
