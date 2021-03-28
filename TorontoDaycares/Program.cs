using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TorontoDaycares.Exporters;
using TorontoDaycares.Models;

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
                var gpsRepo = new GpsRepository(client);
                var daycareRepo = new DaycareRepository(client);
                var repo = new DaycareService(daycareRepo, gpsRepo);

                if (!string.IsNullOrEmpty(options.Address))
                    options.AddressCoordinates = await gpsRepo.GetCoordinates(options.Address);

                var searchOptions = DaycareSearchOptions.None;
                if (options.AddressCoordinates != null)
                    searchOptions |= DaycareSearchOptions.IncludeGps;

                var daycares = await repo.GetDaycares(searchOptions);
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

                    if (filter.AddressCoordinates != null)
                        result = result.Where(o2 =>
                            o2.Daycare.GpsCoordinates != null &&
                            GreatCircleDistance(o2.Daycare.GpsCoordinates, filter.AddressCoordinates) < 15
                        );

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

        private static double GreatCircleDistance(Coordinates a, Coordinates b)
        {
            const double EarthRadius = 6371; // Radius of the Earth in km.

            var lat1 = DegreesToRadians(a.Latitute);
            var lon1 = DegreesToRadians(a.Longitude);
            var lat2 = DegreesToRadians(b.Latitute);
            var lon2 = DegreesToRadians(b.Longitude);

            var diffLat = lat2 - lat1;
            var diffLon = lon2 - lon1;

            double h = Math.Sin(diffLat / 2) * Math.Sin(diffLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(diffLon / 2) * Math.Sin(diffLon / 2);

            return 2 * EarthRadius * Math.Asin(Math.Sqrt(h));

            static double DegreesToRadians(double degrees)
            {
                return degrees * Math.PI / 180.0;
            }
        }
    }
}
