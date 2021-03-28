using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TorontoDaycares.Models;

namespace TorontoDaycares
{
    public class DaycareService
    {
        private GpsRepository GpsRepo { get; }
        private DaycareRepository DaycareRepo { get; }

        public DaycareService(DaycareRepository daycareRepo, GpsRepository gpsRepo)
        {
            DaycareRepo = daycareRepo;
            GpsRepo = gpsRepo;
        }

        private async Task<IEnumerable<Uri>> GetInvalidUrls()
        {
            var dataDir = Directory.CreateDirectory(Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory));
            var invalidFile = Path.Join(dataDir.FullName, FileResources.InvalidUrlsFile);

            if (!File.Exists(invalidFile))
                return Array.Empty<Uri>();

            var uris = new List<Uri>();
            using (var stream = File.OpenRead(invalidFile))
            {
                using (var reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        if (!string.IsNullOrWhiteSpace(line))
                            uris.Add(new Uri(line));
                    }
                }
            }

            return uris;
        }

        public async Task<IEnumerable<Daycare>> GetDaycares(CancellationToken cancellationToken = default)
        {
            var urls = await DaycareRepo.GetDaycareUrls(cancellationToken);
            var invalidUrls = await GetInvalidUrls();

            var daycares = new List<Daycare>();
            var parsedDir = Directory.CreateDirectory(Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory, FileResources.ParsedDataDirectory));
            var invalidFile = Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory, FileResources.InvalidUrlsFile);

            foreach (var url in urls.Except(invalidUrls))
            {
                try
                {
                    Daycare daycare = null;
                    var fileNameBase = Path.GetFileNameWithoutExtension(url.ToString());

                    var dataFile = Path.Join(parsedDir.FullName, fileNameBase + ".json");
                    if (File.Exists(dataFile))
                    {
                        using (var s = File.OpenRead(dataFile))
                        {
                            daycare = await JsonSerializer.DeserializeAsync<Daycare>(s, cancellationToken: cancellationToken);
                        }
                    }

                    if (daycare == null)
                    {
                        daycare = await DaycareRepo.GetDaycare(url, fileNameBase, cancellationToken);

                        if (daycare.Programs.Any())
                        {
                            if (daycare.GpsCoordinates == null)
                                daycare.GpsCoordinates = await GpsRepo.GetCoordinates(daycare.Address, cancellationToken);

                            using (var s = File.OpenWrite(dataFile))
                            {
                                await JsonSerializer.SerializeAsync(s, daycare, cancellationToken: cancellationToken);
                            }
                        }
                        else
                        {
                            using (var s = File.Open(invalidFile, FileMode.Append))
                            {
                                using (var writer = new StreamWriter(s))
                                {
                                    await writer.WriteLineAsync(url.ToString().AsMemory(), cancellationToken: cancellationToken);
                                }
                            }
                        }
                    }

                    daycares.Add(daycare);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error for url ({url}) : {e}");
                }
            }

            return daycares;
        }
    }
}
