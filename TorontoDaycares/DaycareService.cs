using System.Text.Json;
using TorontoDaycares.Models;

namespace TorontoDaycares
{
    public class DaycareService
    {
        private GpsRepository GpsRepo { get; }
        private DaycareRepository DaycareRepo { get; }
        private CityWardRepository CityWardRepo { get; }

        private static DirectoryInfo ParsedDir { get; } = Directory.CreateDirectory(Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory, FileResources.ParsedDataDirectory));
        private static string InvalidFile { get; } = Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory, FileResources.InvalidUrlsFile);

        public DaycareService(DaycareRepository daycareRepo, GpsRepository gpsRepo, CityWardRepository cityWardRepo)
        {
            DaycareRepo = daycareRepo;
            GpsRepo = gpsRepo;
            CityWardRepo = cityWardRepo;
        }

        private static async Task<IEnumerable<Uri>> GetInvalidUrls(CancellationToken cancellationToken)
        {
            var dataDir = Directory.CreateDirectory(Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory));
            var invalidFile = Path.Join(dataDir.FullName, FileResources.InvalidUrlsFile);

            if (!File.Exists(invalidFile))
                return Array.Empty<Uri>();

            var uris = new List<Uri>();
            await foreach (var line in File.ReadLinesAsync(invalidFile, cancellationToken))
                if (!string.IsNullOrWhiteSpace(line))
                    uris.Add(new Uri(line));

            return uris;
        }

        public async Task<IEnumerable<Daycare>> GetDaycares(DaycareSearchOptions options, CancellationToken cancellationToken = default)
        {
            var urls = await DaycareRepo.GetDaycareUrls(cancellationToken);
            var invalidUrls = await GetInvalidUrls(cancellationToken);

            var dataFile = Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory, "daycares.json");

            Dictionary<Uri, Daycare> daycares = new Dictionary<Uri, Daycare>();
            if (File.Exists(dataFile))
            {
                await using var fs = File.OpenRead(dataFile);
                var items = JsonSerializer.DeserializeAsyncEnumerable<Daycare>(fs, cancellationToken: cancellationToken);

                await foreach (var item in items)
                    daycares.Add(item!.Uri, item);
            }

            var newDaycares = 0;
            foreach (var url in urls.Except(invalidUrls))
            {
                if (!daycares.ContainsKey(url))
                {
                    try
                    {
                        daycares[url] = await GetDaycare(options, url, cancellationToken);
                        newDaycares++;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error for url ({url}) : {e}");
                    }
                }
            }

            if (newDaycares > 0)
            {
                await using var fs = File.OpenWrite(dataFile);
                await JsonSerializer.SerializeAsync(fs, daycares.Values, cancellationToken: cancellationToken);
            }

            return daycares.Values;
        }

        private async Task<Daycare> GetDaycare(DaycareSearchOptions options, Uri url, CancellationToken cancellationToken)
        {
            Daycare? daycare = null;
            var fileNameBase = Path.GetFileNameWithoutExtension(url.ToString());

            var dataFile = Path.Join(ParsedDir.FullName, fileNameBase + ".json");
            if (File.Exists(dataFile))
            {
                await using var s = File.OpenRead(dataFile);
                daycare = await JsonSerializer.DeserializeAsync<Daycare>(s, cancellationToken: cancellationToken);
            }

            if (daycare is null)
            {
                daycare = await DaycareRepo.GetDaycare(url, fileNameBase, cancellationToken);

                if (daycare.Programs.Any())
                {
                    if (daycare.GpsCoordinates is null && options.HasFlag(DaycareSearchOptions.IncludeGps))
                    {
                        daycare.GpsCoordinates = await GpsRepo.GetCoordinates(daycare.Address, cancellationToken);
                        if (daycare.GpsCoordinates is null)
                            Console.WriteLine($"Could not find GPS info for address {daycare.Address} in url {url}");
                    }

                    if (daycare.WardNumber == default && !string.IsNullOrWhiteSpace(daycare.WardName))
                    {
                        var ward = await CityWardRepo.GetWardByNameAsync(daycare.WardName);
                        daycare.WardNumber = ward?.Number ?? default;

                        if (daycare.WardNumber == default)
                            Console.WriteLine($"Could not find ward number for ward name {daycare.WardName}, address {daycare.Address} in url {url}");
                    }

                    using var s = File.OpenWrite(dataFile);
                    await JsonSerializer.SerializeAsync(s, daycare, cancellationToken: cancellationToken);
                }
                else
                {
                    await using var s = File.Open(InvalidFile, FileMode.Append);
                    using var writer = new StreamWriter(s);
                    await writer.WriteLineAsync(url.ToString().AsMemory(), cancellationToken: cancellationToken);
                }
            }

            return daycare;
        }
    }
}
