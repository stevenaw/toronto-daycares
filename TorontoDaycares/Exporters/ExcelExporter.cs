using OfficeOpenXml;
using System.Collections.Generic;
using System.IO;

namespace TorontoDaycares.Exporters
{
    public class ExcelExporter : IExporter
    {
        private string FileName { get; set; }

        public ExcelExporter(string fileName)
        {
            FileName = fileName;
        }

        public void Export(DaycareFilter filter, Dictionary<ProgramType, List<(Daycare Daycare, DaycareProgram Program)>> items)
        {
            using (var package = new ExcelPackage())
            {
                foreach (var programType in items)
                {
                    var worksheet = package.Workbook.Worksheets.Add(programType.Key.ToString());

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
                        worksheet.Cells[row, 6].Value = item.Daycare.Uri;
                        row++;
                    }

                    worksheet.Cells.AutoFitColumns(0);
                }

                using (var file = File.OpenWrite(FileName))
                {
                    package.SaveAs(file);
                }
            }
        }
    }
}
