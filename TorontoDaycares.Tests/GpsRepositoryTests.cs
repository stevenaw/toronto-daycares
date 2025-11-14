using System.Text;
using System.Text.Json;
using TorontoDaycares.Models;

namespace TorontoDaycares.Tests
{
    [TestFixture]
    public class GpsRepositoryTests
    {
        [Test]
        public void ConfigureClient_SetsBaseAddressAndHeaders()
        {
            using var client = new HttpClient();

            GpsRepository.ConfigureClient(client);

            Assert.That(client.BaseAddress, Is.EqualTo(new Uri("https://nominatim.openstreetmap.org")));
            Assert.That(client.DefaultRequestHeaders.Host, Is.EqualTo("nominatim.openstreetmap.org"));
            Assert.That(client.DefaultRequestHeaders.Contains("User-Agent"), Is.True);
            var ua = string.Join(";", client.DefaultRequestHeaders.GetValues("User-Agent"));
            Assert.That(ua, Does.Contain("Firefox").Or.Contain("Mozilla"));
        }

        [Test]
        public async Task GetCoordinates_ReturnsCachedValue_WhenCacheFileExists()
        {
            using var handler = new ThrowIfCalledHandler();
            using var client = new HttpClient(handler);

            using var cacheFile = new TempFile("json");
            var repo = new GpsRepository(client, cacheFile);

            // create a temp cache file with a mapping as JSON
            var address = "1 Test St";
            var json = $"{{ \"{address}\": {{ \"Latitute\": 12.34, \"Longitude\": 56.78 }} }}";

            await File.WriteAllTextAsync(cacheFile, json);

            var coords = await repo.GetCoordinates(address);

            Assert.That(coords, Is.Not.Null);
            Assert.That(coords.Latitute, Is.EqualTo(12.34));
            Assert.That(coords.Longitude, Is.EqualTo(56.78));
        }

        [Test]
        public async Task GetCoordinates_FetchesFromHttpAndPersistsCache()
        {
            const string address = "123 Example Ave";

            // Handler returns a non-empty JSON array for the search request
            var handler = new TestHandler(request =>
            {
                var json = "[{\"lat\": \"1.1\", \"lon\": \"2.2\"}]";
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            });

            using var client = new HttpClient(handler);
            GpsRepository.ConfigureClient(client);

            using var cacheFile = new TempFile("json");
            var repo = new GpsRepository(client, cacheFile);

            var coords = await repo.GetCoordinates(address);

            Assert.That(coords, Is.Not.Null);
            Assert.That(coords.Latitute, Is.EqualTo(1.1));
            Assert.That(coords.Longitude, Is.EqualTo(2.2));

            // cache file should have been created
            Assert.That(File.Exists(cacheFile), Is.True);

            // read file and ensure it contains the address
            await using var s = File.OpenRead(cacheFile);
            var dict = await JsonSerializer.DeserializeAsync<Dictionary<string, Coordinates>>(s);
            Assert.That(dict, Contains.Key(address));
            Assert.That(dict[address].Latitute, Is.EqualTo(1.1));
        }

        [Test]
        public async Task GetCoordinates_UsesFallbackQuery_WhenFirstSearchReturnsEmpty()
        {
            const string address = "Fallback St";

            // Handler returns [] for street=... and a value for q=...
            var handler = new TestHandler(request =>
            {
                var q = request.RequestUri?.Query ?? string.Empty;
                if (q.Contains("street="))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent("[]", Encoding.UTF8, "application/json")
                    };
                }

                if (q.Contains("q="))
                {
                    var json = "[{\"lat\": \"9.9\", \"lon\": \"8.8\"}]";
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                };
            });

            using var client = new HttpClient(handler);
            GpsRepository.ConfigureClient(client);

            using var cacheFile = new TempFile("json");
            var repo = new GpsRepository(client, cacheFile);

            var coords = await repo.GetCoordinates(address);

            Assert.That(coords, Is.Not.Null);
            Assert.That(coords.Latitute, Is.EqualTo(9.9));
            Assert.That(coords.Longitude, Is.EqualTo(8.8));

            // ensure two requests were made (one for street=, one for q=)
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(handler.Requests[0].RequestUri?.Query, Does.Contain("street="));
            Assert.That(handler.Requests[1].RequestUri?.Query, Does.Contain("q="));
        }

        // Helper handler that records requests and uses a responder func
        private class TestHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public List<HttpRequestMessage> Requests { get; } = [];

            public TestHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                _responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(_responder(request));
            }
        }

        // Handler that fails if any HTTP call is made
        private class ThrowIfCalledHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Assert.Fail("HttpClient should not be called when cache file exists");
                throw new InvalidOperationException();
            }
        }
    }
}
