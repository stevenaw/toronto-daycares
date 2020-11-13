using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TorontoDaycares
{
    public partial class DaycareRepository
    {
        private async Task<IEnumerable<Uri>> GetDaycareUrls(CancellationToken cancellationToken)
        {
            var dataDir = Directory.CreateDirectory(Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory));
            var dataFile = Path.Join(dataDir.FullName, FileResources.AllUrlsFile);

            Uri[] uris = Array.Empty<Uri>();

            if (File.Exists(dataFile))
                uris = File.ReadLines(dataFile).Select(line => new Uri(line)).ToArray();

            if (!uris.Any())
            {
                var alphaPages = await GetAlphaUrls(cancellationToken);
                uris = (await FetchDaycareUrls(alphaPages, cancellationToken)).ToArray();

                await File.WriteAllLinesAsync(dataFile, uris.Select(u => u.ToString()), cancellationToken);
            }

            return uris;
        }

        private async Task<IEnumerable<Uri>> FetchDaycareUrls(IEnumerable<Uri> pageUrls, CancellationToken cancellationToken)
        {
            List<Uri> daycareUrls = new List<Uri>();

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
            var response = await client.GetAsync(url, cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var page = new HtmlDocument();
            page.LoadHtml(html);

            return page;
        }
    }
}
