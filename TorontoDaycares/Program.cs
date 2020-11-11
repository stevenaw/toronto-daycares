using CommandLine;
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
        const int MaxConnections = 5;

        static HttpClient GetHttpClient(int maxConnections)
        {
            return HttpClientFactory.Create(new HttpClientHandler()
            {
                MaxConnectionsPerServer = maxConnections
            });
        }

        static async Task Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args);

            await result.WithParsedAsync(async options =>
            {
                var client = GetHttpClient(MaxConnections);

                var repo = new DaycareRepository(client);
                var daycares = await repo.GetDaycares();

                if (!options.WardList.Any())
                    options.WardList = new[] { 6, 8, 11, 18, 17, 16, 15 };
                if (!options.ProgramList.Any())
                    options.ProgramList = new[] { ProgramType.Infant, ProgramType.Toddler };

                var topPrograms = FindData(daycares, options);

                var exporter = GetExporter(options);
                exporter.Export(options, topPrograms);
            });
        }

        private static IExporter GetExporter(Options options)
        {
            if (!string.IsNullOrEmpty(options.OutputFile))
                return new ExcelExporter(options.OutputFile);
            return new ConsoleExporter();
        }

        private static Dictionary<ProgramType, List<(Daycare Daycare, DaycareProgram Program)>> FindData(IEnumerable<Daycare> daycares, Options filter)
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
