using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TorontoDaycares.Models;

namespace TorontoDaycares
{
    public partial class DaycareRepository
    {
        private readonly HttpClient client;

        private static class FileResources
        {
            public const string DataDirectory = "data";
            public const string RawDataDirectory = "raw";
            public const string ParsedDataDirectory = "parsed";

            public const string AllUrlsFile = "urls.txt";
            public const string InvalidUrlsFile = "invalid.txt";
        }

        public DaycareRepository(HttpClient client)
        {
            this.client = client;
        }

        private static bool InvalidRating(string rating)
        {
            return rating == "-" || rating == "Not yet available";
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

        public async Task<IEnumerable<Daycare>> GetDaycares(CancellationToken cancellationToken = default(CancellationToken))
        {
            var urls = await GetDaycareUrls(cancellationToken);
            var invalidUrls = await GetInvalidUrls();

            var daycares = new List<Daycare>();
            var parsedDir = Directory.CreateDirectory(Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory, FileResources.ParsedDataDirectory));
            var rawDir = Directory.CreateDirectory(Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory, FileResources.RawDataDirectory));
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
                        HtmlDocument html = null;
                        var rawFile = Path.Join(rawDir.FullName, fileNameBase + ".html");

                        if (!File.Exists(rawFile))
                        {
                            html = await FetchHtml(url, cancellationToken);
                            await File.WriteAllTextAsync(rawFile, html.ParsedText, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            html = new HtmlDocument();
                            html.LoadHtml(await File.ReadAllTextAsync(rawFile, cancellationToken: cancellationToken));
                        }

                        daycare = ParseDaycare(url, html);
                        if (daycare.Programs.Any())
                        {
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

        private static Daycare ParseDaycare(Uri uri, HtmlDocument page)
        {
            var daycare = new Daycare();

            // Get name of the daycare
            var name = page.QuerySelector("h1").InnerText;

            var infoBoxes = page.QuerySelectorAll(".csd_opcrit_content_box").ToArray();

            var topInfoBox = infoBoxes[0];

            // Get ID of the daycare
            var header = topInfoBox.QuerySelector("h2").InnerText.AsSpan();
            var idText = header.Slice(name.Length + 2).Trim(')');

            daycare.Id = Int32.Parse(idText);
            daycare.Name = name;

            // Get address
            var addressBox = topInfoBox.QuerySelector("header p");
            daycare.Address = addressBox.GetDirectInnerText()
                .Replace('\n', ' ')
                .Replace('\t', ' ')
                .Replace("&nbsp;", " ")
                .Trim();

            var wardContainer = addressBox.QuerySelector(".ward-link");
            var wardNumber = wardContainer.InnerText
                .AsSpan()
                .Slice("Ward:".Length + 1)
                .Trim();
            daycare.WardNumber = int.Parse(wardNumber);

            daycare.Uri = uri;

            var programBox = infoBoxes[1];
            daycare.Programs = new List<DaycareProgram>();

            var programTable = programBox.QuerySelector("table");
            var programRows = programTable == null ? Array.Empty<HtmlNode>() : programTable.QuerySelectorAll("tbody tr");

            foreach (var row in programRows)
            {
                var cells = row.QuerySelectorAll("td");

                var type = GetCellContents(cells[0]);
                var capacity = GetCellContents(cells[1]);
                var vacancy = GetCellContents(cells[2]);
                var quality = GetCellContents(cells[3]);
                
                var program = new DaycareProgram()
                {
                    Capacity = Int32.Parse(capacity),
                    Vacancy = vacancy.ToLower() switch
                    {
                        "yes" => true,
                        "no" => false,
                        _ => (bool?)null
                    },
                    Rating = InvalidRating(quality) ? null : (double?)Double.Parse(quality)
                };

                if (Enum.TryParse<ProgramType>(type, out var programType))
                {
                    program.ProgramType = programType;
                    daycare.Programs.Add(program);
                }
                else
                {
                    Console.WriteLine($"Unknown program type: {type}");
                }
            }

            return daycare;
        }

        private static string GetCellContents(HtmlNode node)
        {
            var link = node.QuerySelector("a");
            var text = link == null ? node.InnerText : link.InnerText;
            return text.Trim();
        }
    }

    
}
