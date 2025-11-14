using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using TorontoDaycares.Models;

namespace TorontoDaycares
{
    public class DaycareRepository
    {
        private HttpClient Client { get; }
        public DirectoryInfo HtmlCacheDirectory { get; init; }

        public DaycareRepository(HttpClient client)
        {
            Client = client;
            HtmlCacheDirectory = Directory.CreateDirectory(Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory, FileResources.RawDataDirectory));
        }

        public async Task<Daycare> GetDaycare(Uri url, string id, CancellationToken cancellationToken)
        {
            HtmlDocument html;
            var rawFile = Path.Join(HtmlCacheDirectory.FullName, $"{id}.html");

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

            var daycare = HtmlDaycareParser.ParseDaycare(url, html);
            return daycare;
        }

        public async Task<IEnumerable<Uri>> GetDaycareUrls(CancellationToken cancellationToken)
        {
            var dataDir = Directory.CreateDirectory(Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory));
            var dataFile = Path.Join(dataDir.FullName, FileResources.AllUrlsFile);

            Uri[] uris = [];

            if (File.Exists(dataFile))
                uris = File.ReadLines(dataFile).Select(line => new Uri(line)).ToArray();

            if (uris.Length == 0)
            {
                var alphaPages = await GetAlphaUrls(cancellationToken);
                uris = (await FetchDaycareUrls(alphaPages, cancellationToken)).ToArray();

                await File.WriteAllLinesAsync(dataFile, uris.Select(u => u.ToString()), cancellationToken);
            }

            return uris;
        }

        private async Task<IEnumerable<Uri>> FetchDaycareUrls(IEnumerable<Uri> pageUrls, CancellationToken cancellationToken)
        {
            List<Uri> daycareUrls = [];

            foreach (var url in pageUrls)
            {
                var page = await FetchHtml(url, cancellationToken);

                var anchors = page.QuerySelectorAll("div.pfrPrdListing tbody tr td:first-child a");
                var urls = anchors.Select(a => new Uri(url, a.Attributes["href"].Value));

                daycareUrls.AddRange(urls);
            }

            return daycareUrls;
        }

        private async Task<IEnumerable<Uri>> GetAlphaUrls(CancellationToken cancellationToken)
        {
            var startUrl = new Uri("https://www.toronto.ca/data/children/dmc/a2z/a2za.html");
            var page = await FetchHtml(startUrl, cancellationToken);

            var anchors = page.QuerySelectorAll("#pfrNavAlpha2 li a");
            return anchors.Select(a =>
            {
                var href = a.Attributes["href"];
                return new Uri(startUrl, href.Value);
            }).ToArray();
        }

        private async Task<HtmlDocument> FetchHtml(Uri url, CancellationToken cancellationToken)
        {
            var response = await Client.GetAsync(url, cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var page = new HtmlDocument();
            page.LoadHtml(html);

            return page;
        }

        private static class HtmlDaycareParser
        {
            public static Daycare ParseDaycare(Uri uri, HtmlDocument page)
            {
                var daycare = new Daycare();

                // Get name of the daycare
                var name = page.QuerySelector("h1").InnerText;

                var infoBoxes = page.QuerySelectorAll(".csd_opcrit_content_box").ToArray();

                var topInfoBox = infoBoxes[0];

                // Get ID of the daycare
                var header = topInfoBox.QuerySelector("h2").InnerText.AsSpan();
                var idText = header.Slice(name.Length + 2).Trim(')');

                daycare.Id = int.Parse(idText);
                daycare.Name = name;

                // Get address
                var addressBox = topInfoBox.QuerySelector("header p");
                var addressSpan = addressBox.GetDirectInnerText()
                    .Replace('\n', ' ')
                    .Replace('\t', ' ')
                    .Replace("&nbsp;", " ")
                    .AsSpan()
                    .Trim();

                // Checks to trim off intersection (if present)
                int openParenthesisIdx, closeParenthesisIdx = addressSpan.LastIndexOf(')');
                if (closeParenthesisIdx != -1 && (openParenthesisIdx = addressSpan.Slice(0, closeParenthesisIdx).LastIndexOf('(')) != -1)
                {
                    daycare.NearestIntersection = addressSpan.Slice(openParenthesisIdx + 1, closeParenthesisIdx - openParenthesisIdx - 1).Trim().ToString();
                    addressSpan = addressSpan.Slice(0, openParenthesisIdx).Trim();
                }

                int firstCommaIdx;
                if ((firstCommaIdx = addressSpan.IndexOf(',')) == -1)
                    daycare.Address = addressSpan.ToString();
                else
                {
                    daycare.Unit = addressSpan.Slice(firstCommaIdx + 1).Trim().ToString();
                    daycare.Address = addressSpan.Slice(0, firstCommaIdx).Trim().ToString();
                }

                var wardContainer = addressBox.QuerySelector(".ward-link");
                var ward = wardContainer.InnerText
                    .AsSpan()
                    .Slice("Ward:".Length + 1)
                    .Trim();

                if (int.TryParse(ward, out var wardNumber))
                    daycare.WardNumber = wardNumber;
                else
                    daycare.WardName = ward.ToString();

                daycare.Uri = uri;

                var programBox = infoBoxes[1];
                daycare.Programs = [];

                var programTable = programBox.QuerySelector("table");
                var programRows = programTable is null ? Array.Empty<HtmlNode>() : programTable.QuerySelectorAll("tbody tr");

                foreach (var row in programRows)
                {
                    var cells = row.QuerySelectorAll("td");

                    var type = GetCellContents(cells[0]);
                    var capacity = GetCellContents(cells[1]);
                    var vacancy = GetCellContents(cells[2]);
                    var quality = GetCellContents(cells[3]);

                    var program = new DaycareProgram()
                    {
                        Capacity = int.Parse(capacity),
                        Vacancy = ConvertVacancy(vacancy),
                        Rating = InvalidRating(quality) ? null : double.Parse(quality)
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
                var text = link is null ? node.InnerText : link.InnerText;
                return text.Trim();
            }

            private static bool? ConvertVacancy(string s)
            {
                if (string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase))
                    return true;
                else if (string.Equals(s, "no", StringComparison.OrdinalIgnoreCase))
                    return false;
                return null;
            }

            private static bool InvalidRating(string rating)
            {
                return rating == "-" || rating == "Not yet available";
            }
        }
    }
}
