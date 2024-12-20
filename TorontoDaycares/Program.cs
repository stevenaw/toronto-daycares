﻿using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TorontoDaycares.Exporters;
using TorontoDaycares.Models;

namespace TorontoDaycares
{
    /*
     * TODO: Use OpenData dataset instead of page scraping
     * https://open.toronto.ca/dataset/licensed-child-care-centres/
     * CSV + WGS84
     * https://ckan0.cf.opendata.inter.prod-toronto.ca/dataset/059d37c6-d88b-42fb-b230-ec6a5ec74c24/resource/74eb5418-42c8-49d3-a62f-69941f0161f3/download/Child%20care%20centres%20-%204326.csv
     */
    class Program
    {
        const int MaxDrivingDistanceKm = 15;

        static async Task Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args);
            await result.WithParsedAsync(async options =>
            {
                var host = BuildHost(options);

                using var serviceScope = host.Services.CreateScope();
                var services = serviceScope.ServiceProvider;

                Directory.CreateDirectory(Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory));

                if (!string.IsNullOrEmpty(options.Address))
                {
                    var gpsRepo = services.GetRequiredService<GpsRepository>();
                    options.AddressCoordinates = await gpsRepo.GetCoordinates(options.Address);
                }

                var searchOptions = DaycareSearchOptions.None;
                if (options.AddressCoordinates is not null)
                    searchOptions |= DaycareSearchOptions.IncludeGps;

                var service = services.GetRequiredService<DaycareService>();

                var daycares = await service.GetDaycares(searchOptions);
                var topPrograms = FindData(daycares, options);

                var exporter = services.GetRequiredService<IExporter>();
                await exporter.ExportAsync(options, topPrograms);
            });
        }

        private static IHost BuildHost(Options options)
        {
            var builder = new HostBuilder()
               .ConfigureServices((hostContext, services) =>
               {
                   services.AddHttpClient<GpsRepository>()
                       .ConfigureHttpClient(client => GpsRepository.ConfigureClient(client))
                       .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                       {
                           MaxConnectionsPerServer = 1
                       });

                   services.AddHttpClient<CityWardRepository>()
                       .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                       {
                           MaxConnectionsPerServer = 2
                       });

                   services.AddHttpClient<DaycareRepository>()
                       .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                       {
                           MaxConnectionsPerServer = 5
                       });

                   services.AddTransient<DaycareService>();

                   if (!string.IsNullOrEmpty(options.OutputFile))
                       services.AddTransient<IExporter>(builder => new ExcelExporter(options.OutputFile));
                   else
                       services.AddTransient<IExporter>(builder => new ConsoleExporter());
               }).UseConsoleLifetime();

            return builder.Build();
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

                    if (filter.AddressCoordinates is not null)
                        result = result.Where(o2 =>
                            o2.Daycare.GpsCoordinates is not null &&
                            GreatCircleDistance(o2.Daycare.GpsCoordinates, filter.AddressCoordinates) < MaxDrivingDistanceKm
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
                                    .Take(filter.TopN)
                                    .ToList();

                        return result;
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
