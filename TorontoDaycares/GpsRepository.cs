using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TorontoDaycares.Models;

namespace TorontoDaycares
{
    public class GpsRepository
    {
        private HttpClient Client { get; }
        private Dictionary<string, Coordinates> Cache { get; set; }
        private string CacheFileLocation { get; }


        private DateTime lastCall = DateTime.MinValue;

        public GpsRepository(HttpClient client)
        {
            Client = client;
            CacheFileLocation = Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory, "gps.json");
        }

        public async Task<Coordinates> GetCoordinates(string address, CancellationToken cancellationToken = default)
        {
            await InitializeCache();

            if (!Cache.TryGetValue(address, out var coords))
            {
                coords = await FetchCoordinates(address, cancellationToken);
                Cache.Add(address, coords);
                await PersistCache();
            }

            return coords;
        }

        private async Task<Coordinates> FetchCoordinates(string address, CancellationToken cancellationToken = default)
        {
            // Docs: https://nominatim.org/release-docs/latest/api/Search/
            // Usage Policy: https://operations.osmfoundation.org/policies/nominatim/
            // TODO: Include a citation of licensing

            var addressParam = HttpUtility.UrlEncode(address);
            var url = $"https://nominatim.openstreetmap.org/search?street={addressParam}&city=Toronto&state=ON&country=CA&format=json&limit=1";
            var item = await RequestFromOpenStreetMaps(url, cancellationToken);

            if (item.Length == 0)
            {
                url = $"https://nominatim.openstreetmap.org/search?q={addressParam},Toronto,ON,CA&format=json&limit=1";
                item = await RequestFromOpenStreetMaps(url, cancellationToken);
            }

            if (item.Length == 0)
                return null;

            return new Coordinates()
            {
                Latitute = double.Parse(item[0].lat),
                Longitude = double.Parse(item[0].lon)
            };
        }

        private async Task<OpenStreetMapResponse[]> RequestFromOpenStreetMaps(string url, CancellationToken cancellationToken)
        {
            // TODO: Thread-safety ??

            var now = DateTime.Now;
            var elapsedSinceLastCall = now - lastCall;
            if (elapsedSinceLastCall.TotalSeconds < 2)
                await Task.Delay(TimeSpan.FromSeconds(2) - elapsedSinceLastCall, cancellationToken);

            lastCall = DateTime.Now;

            var msg = new HttpRequestMessage(HttpMethod.Get, url);

            msg.Headers.Host = "nominatim.openstreetmap.org";
            msg.Headers.Add("AcceptEncoding", "gzip, deflate, br");
            msg.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            msg.Headers.Add("Accept-Language", "en-US,en;q=0.5");
            msg.Headers.Add("Cache-Control", "no-cache");
            msg.Headers.Add("Connection", "keep-alive");
            msg.Headers.Add("DNT", "1");
            msg.Headers.Add("Pragma", "no-cache");
            msg.Headers.Add("Upgrade-Insecure-Requests", "1");
            msg.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:87.0) Gecko/20100101 Firefox/87.0");

            var resp = await Client.SendAsync(msg, cancellationToken);
            resp.EnsureSuccessStatusCode();

            using var result = await resp.Content.ReadAsStreamAsync(cancellationToken);
            var item = await JsonSerializer.DeserializeAsync<OpenStreetMapResponse[]>(result, cancellationToken: cancellationToken);

            return item;
        }

        private async Task InitializeCache()
        {
            if (Cache == null)
            {
                if (File.Exists(CacheFileLocation))
                {
                    using (var s = File.OpenRead(CacheFileLocation))
                    {
                        Cache = await JsonSerializer.DeserializeAsync<Dictionary<string, Coordinates>>(s);
                    }
                }
                else
                {
                    Cache = new Dictionary<string, Coordinates>();
                }
            }
        }

        private async Task PersistCache()
        {
            using (var s = File.OpenWrite(CacheFileLocation))
            {
                await JsonSerializer.SerializeAsync(s, Cache);
            }
        }

        private class OpenStreetMapResponse
        {
            public string lat { get; set; }
            public string lon { get; set; }
        }
    }
}
