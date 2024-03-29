﻿using OfficeOpenXml;
using TorontoDaycares.Models;

namespace TorontoDaycares.Exporters
{
    public class ExcelExporter : IExporter
    {
        private string FileName { get; }

        static ExcelExporter()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public ExcelExporter(string fileName)
        {
            FileName = fileName;
        }

        public async Task ExportAsync(Options filter, Dictionary<ProgramType, List<(Daycare Daycare, DaycareProgram Program)>> items)
        {
            using var package = new ExcelPackage();

            foreach (var programType in items)
            {
                var worksheet = package.Workbook.Worksheets.Add(programType.Key.ToString());

                worksheet.Row(1).Style.Font.Bold = true;
                worksheet.Cells[1, 1].Value = "Name";
                worksheet.Cells[1, 2].Value = "Rating";
                worksheet.Cells[1, 3].Value = "Capacity";
                worksheet.Cells[1, 4].Value = "Vacancy";
                worksheet.Cells[1, 5].Value = "Address";
                worksheet.Cells[1, 6].Value = "Url";

                var row = 2;
                foreach (var item in programType.Value)
                {
                    worksheet.Cells[row, 1].Value = item.Daycare.Name;
                    worksheet.Cells[row, 2].Value = item.Program.Rating.Value;
                    worksheet.Cells[row, 3].Value = item.Program.Capacity;
                    worksheet.Cells[row, 4].Value = item.Program.Vacancy;
                    worksheet.Cells[row, 5].Value = item.Daycare.Address;
                    worksheet.Cells[row, 6].Hyperlink = item.Daycare.Uri;
                    row++;
                }

                worksheet.Cells.AutoFitColumns(0);
            }

            await using var file = File.OpenWrite(FileName);
            await package.SaveAsAsync(file);
        }
    }
}
