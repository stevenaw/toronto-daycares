using CommandLine;
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
        static async Task Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args);
            await result.WithParsedAsync(async options =>
            {
                var host = BuildHost(options);

                using var serviceScope = host.Services.CreateScope();
                var services = serviceScope.ServiceProvider;

                Directory.CreateDirectory(Path.Join(Directory.GetCurrentDirectory(), TorontoDaycares.FileResources.DataDirectory));

                var req = new Models.DaycareSearchRequest()
                {
                    TopN = options.TopN,
                    Wards = options.WardList?.ToArray(),
                    Programs = options.ProgramList?.ToArray(),
                    NearAddress = options.Address,
                    Options = DaycareSearchOptions.None
                };

                var service = services.GetRequiredService<DaycareService>();
                var response = await service.SearchDaycares(req);

                var exporter = services.GetRequiredService<IExporter>();
                await exporter.ExportAsync(response);
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
    }
}
