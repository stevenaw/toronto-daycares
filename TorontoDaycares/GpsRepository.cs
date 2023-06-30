using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using TorontoDaycares.Models;

namespace TorontoDaycares
{
    public class GpsRepository
    {
        private HttpClient Client { get; }
        private Dictionary<string, Coordinates>? Cache { get; set; }
        private string CacheFileLocation { get; }

        private DateTime lastCall = DateTime.MinValue;

        public GpsRepository(HttpClient client)
        {
            Client = client;
            CacheFileLocation = Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory, "gps.json");
        }

        public static void ConfigureClient(HttpClient client)
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org", UriKind.Absolute);

            client.DefaultRequestHeaders.Host = "nominatim.openstreetmap.org";
            client.DefaultRequestHeaders.Add("AcceptEncoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            client.DefaultRequestHeaders.Add("DNT", "1");
            client.DefaultRequestHeaders.Add("Pragma", "no-cache");
            client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:87.0) Gecko/20100101 Firefox/87.0");
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

            var addressParam = HttpUtility.UrlEncode(address);
            var url = $"/search?street={addressParam}&city=Toronto&state=ON&country=CA&format=json&limit=1";
            var item = await RequestFromOpenStreetMaps(url, cancellationToken);

            if (item.Length == 0)
            {
                url = $"/search?q={addressParam},Toronto,ON,CA&format=json&limit=1";
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
            var now = DateTime.Now;
            var elapsedSinceLastCall = now - lastCall;
            if (elapsedSinceLastCall.TotalSeconds < 2)
                await Task.Delay(TimeSpan.FromSeconds(2) - elapsedSinceLastCall, cancellationToken);

            lastCall = DateTime.Now;

            return await Client.GetFromJsonAsync<OpenStreetMapResponse[]>(url, cancellationToken);
        }

        private async Task InitializeCache()
        {
            if (Cache is null)
            {
                if (File.Exists(CacheFileLocation))
                {
                    await using var s = File.OpenRead(CacheFileLocation);
                    Cache = await JsonSerializer.DeserializeAsync<Dictionary<string, Coordinates>>(s);
                }
                else
                {
                    Cache = new Dictionary<string, Coordinates>();
                }
            }
        }

        private async Task PersistCache()
        {
            await using var s = File.OpenWrite(CacheFileLocation);
            await JsonSerializer.SerializeAsync(s, Cache);
        }

        private class OpenStreetMapResponse
        {
            public string lat { get; set; }
            public string lon { get; set; }
        }
    }
}
