using System.Text.Json;
using TorontoDaycares.Models;

namespace TorontoDaycares
{
    public class DaycareService
    {
        const int MaxDrivingDistanceKm = 15;

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

        public async Task<Models.DaycareSearchResponse> SearchDaycares(Models.DaycareSearchRequest request, CancellationToken cancellationToken = default)
        {
            var topN = request?.TopN ?? 50;

            DaycareSearchOptions options = request?.Options ?? DaycareSearchOptions.None;

            // If proximity requested, ensure GPS info is included when retrieving daycares
            Coordinates? addressCoordinates = null;
            if (!string.IsNullOrWhiteSpace(request?.NearAddress))
            {
                options |= DaycareSearchOptions.IncludeGps;
                addressCoordinates = await GpsRepo.GetCoordinates(request!.NearAddress!, cancellationToken);
            }

            var daycares = (await GetDaycares(options, cancellationToken)).ToList();

            // Apply ward filter
            if (request?.Wards != null && request.Wards.Any())
                daycares = daycares.Where(d => request.Wards.Contains(d.WardNumber)).ToList();

            // If proximity was requested, compute distances and filter out daycares beyond MaxDrivingDistanceKm
            if (addressCoordinates is not null)
            {
                // Compute distance for each daycare (only if GPS available)
                daycares = daycares.Select(d =>
                {
                    if (d.GpsCoordinates is null)
                    {
                        d.DistanceKm = null;
                    }
                    else
                    {
                        d.DistanceKm = GreatCircleDistance(d.GpsCoordinates, addressCoordinates);
                    }
                    return d;
                }).ToList();

                // Keep only daycares within MaxDrivingDistanceKm
                daycares = daycares.Where(d => d.DistanceKm.HasValue && d.DistanceKm.Value <= MaxDrivingDistanceKm).ToList();
            }

            // Build a ranked list of individual programs (exclude programs with missing ratings)
            var programEntries = daycares
                .SelectMany(d => d.Programs ?? Enumerable.Empty<DaycareProgram>(), (d, p) => new { Daycare = d, Program = p })
                // Exclude programs without ratings
                .Where(x => x.Program.Rating.HasValue)
                // If a program filter was provided in the request, apply it here
                .Where(x => request?.Programs == null || request.Programs.Length == 0 || request.Programs.Contains(x.Program.ProgramType))
                // Order by rating descending
                .OrderByDescending(x => x.Program.Rating!.Value)
                .Take(topN)
                .Select(x => new Models.TopProgramResult { Daycare = x.Daycare, Program = x.Program })
                .ToList();

            return new Models.DaycareSearchResponse { TopPrograms = programEntries };
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

            Dictionary<Uri, Daycare> daycares = [];
            if (File.Exists(dataFile))
            {
                await using var fs = File.OpenRead(dataFile);

                var items = JsonSerializer.DeserializeAsyncEnumerable(fs, DaycareJsonContext.Default.Daycare, cancellationToken: cancellationToken);

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
                await JsonSerializer.SerializeAsync(fs, daycares.Values.ToArray(), DaycareJsonContext.Default.DaycareArray, cancellationToken: cancellationToken);
            }

            return daycares.Values;
        }

        private async Task<Daycare> GetDaycare(DaycareSearchOptions options, Uri url, CancellationToken cancellationToken)
        {
            Daycare? daycare = null;
            var fileNameBase = Path.GetFileNameWithoutExtension(url.ToString());

            var dataFile = Path.Join(ParsedDir.FullName, $"{fileNameBase}.json");
            if (File.Exists(dataFile))
            {
                await using var s = File.OpenRead(dataFile);
                daycare = await JsonSerializer.DeserializeAsync(s, DaycareJsonContext.Default.Daycare, cancellationToken: cancellationToken);
            }

            if (daycare is null)
            {
                daycare = await DaycareRepo.GetDaycare(url, fileNameBase, cancellationToken);

                if (daycare.Programs.Count != 0)
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
                    await JsonSerializer.SerializeAsync(s, daycare, DaycareJsonContext.Default.Daycare, cancellationToken: cancellationToken);
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
