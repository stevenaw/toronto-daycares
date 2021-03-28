using System;
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
        private readonly HttpClient client;

        private DateTime lastCall = DateTime.MinValue;

        public GpsRepository(HttpClient client)
        {
            this.client = client;
        }

        public async Task<Coordinates> GetCoordinates(string address, CancellationToken cancellationToken = default)
        {
            // TODO: Thread-safety ??
            // TODO: Cache of previously-fetched addresses
            // //  - Single json file mapping address to coords
            // //  - Mirrored in memory as a Dictionary<string, Coordinates>

            var now = DateTime.Now;
            var elapsedSinceLastCall = now - lastCall;
            if (elapsedSinceLastCall.TotalSeconds < 2)
                await Task.Delay(TimeSpan.FromSeconds(2) - elapsedSinceLastCall, cancellationToken);

            lastCall = DateTime.Now;


            // Docs: https://nominatim.org/release-docs/latest/api/Search/
            // Usage Policy: https://operations.osmfoundation.org/policies/nominatim/
            // TODO: Include a citation of licensing

            var addressParam = HttpUtility.UrlEncode(address);
            var url = $"https://nominatim.openstreetmap.org/search?street={addressParam}&city=Toronto&state=ON&country=CA&format=json&limit=1";

            var msg = new HttpRequestMessage(HttpMethod.Get, url);

            msg.Headers.Host = "nominatim.openstreetmap.org";
            msg.Headers.Add("AcceptEncoding", "gzip, deflate, br");
            msg.Headers.Add("Accept","text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            msg.Headers.Add("Accept-Language", "en-US,en;q=0.5");
            msg.Headers.Add("Cache-Control", "no-cache");
            msg.Headers.Add("Connection", "keep-alive");
            msg.Headers.Add("DNT", "1");
            msg.Headers.Add("Pragma", "no-cache");
            msg.Headers.Add("Upgrade-Insecure-Requests", "1");
            msg.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:87.0) Gecko/20100101 Firefox/87.0");

            using var resp = await client.SendAsync(msg, cancellationToken);

            resp.EnsureSuccessStatusCode();

            using var result = await resp.Content.ReadAsStreamAsync(cancellationToken);
            var item = await JsonSerializer.DeserializeAsync<OpenStreetMapResponse[]>(result, cancellationToken: cancellationToken);

            // TODO: Error check for count, also use tryparse
            return new Coordinates()
            {
                Latitute = decimal.Parse(item[0].lat),
                Longitude = decimal.Parse(item[0].lon)
            };
        }

        public class OpenStreetMapResponse
        {
            public string lat { get; set; }
            public string lon { get; set; }
        }
    }
}
