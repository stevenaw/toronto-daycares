using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TorontoDaycares.Exporters;

namespace TorontoDaycares
{
    class Program
    {
        static HttpClient GetHttpClient(int maxConnections)
        {
            return HttpClientFactory.Create(new HttpClientHandler()
            {
                MaxConnectionsPerServer = maxConnections
            });
        }

        static async Task Main(string[] args)
        {
            const int maxConnections = 5;
            var client = GetHttpClient(maxConnections);

            var repo = new DaycareRepository(client);
            var daycares = await repo.GetDaycares();

            var filter = new DaycareFilter()
            {
                TopN = 50,
                WardList = new[] { 6, 8, 11, 18, 17, 16, 15 },
                ProgramList = new [] { ProgramType.Infant, ProgramType.Toddler }
            };

            var topPrograms = FindData(daycares, filter);

            var exporter = new ExcelExporter("output.xlsx");
            exporter.Export(filter, topPrograms);
        }

        private static Dictionary<ProgramType, List<(Daycare Daycare, DaycareProgram Program)>> FindData(IEnumerable<Daycare> daycares, DaycareFilter filter)
        {
            var allPrograms = daycares
                .SelectMany(o =>
                {
                    var result = o.Programs
                        .Where(o2 => o2.Rating.HasValue)
                        .Select(p => (Daycare: o, Program: p));

                    if (filter.WardList.Any())
                        result = result.Where(o2 => filter.WardList.Contains(o2.Daycare.WardNumber));
                    if (filter.ProgramList.Any())
                        result = result.Where(o2 => filter.ProgramList.Contains(o2.Program.ProgramType));

                    return result;
                })
                .ToArray();

            var topPrograms = allPrograms
                .GroupBy(o => o.Program.ProgramType)
                .ToDictionary(
                    o => o.Key,
                    vals =>
                    {
                        var result = vals
                                    .OrderByDescending(val => val.Program.Rating)
                                    .Select(val => val);

                        if (filter.TopN.HasValue)
                            result = result.Take(filter.TopN.Value);

                        return result.ToList();
                    }
                );

            return topPrograms;
        }
    }
}
