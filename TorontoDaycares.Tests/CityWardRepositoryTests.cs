
namespace TorontoDaycares.Tests
{
    [TestFixture]
    public class CityWardRepositoryTests
    {
        [Test]
        public async Task GetWardsAsync_ReturnsAll()
        {
            using var mockClient = new HttpClient(new NullHttpClientHandler());
            var cacheFileLocation = Path.Join(Directory.GetCurrentDirectory(), "Data", "City Wards Data - 4326.csv");

            var repo = new CityWardRepository(mockClient, cacheFileLocation);

            var wards = await repo.GetWardsAsync();

            Assert.That(wards, Has.Length.EqualTo(25));

            foreach (var ward in wards)
            {
                Assert.That(ward.Name, Is.Not.Null);
                Assert.That(ward.Number, Is.GreaterThan(0));
            }
        }

        [Test]
        public async Task GetWardsAsync_CachesResults()
        {
            using var mockClient = new HttpClient(new NullHttpClientHandler());
            var cacheFileLocation = Path.Join(Directory.GetCurrentDirectory(), "Data", "City Wards Data - 4326.csv");

            var repo = new CityWardRepository(mockClient, cacheFileLocation);

            var wards1 = await repo.GetWardsAsync();
            var wards2 = await repo.GetWardsAsync();

            Assert.That(wards1, Is.SameAs(wards2));
        }

        [Test]
        public async Task GetWardsAsync_FetchesWhenNotCached()
        {
            var handler = new NullHttpClientHandler();
            var mockClient = new HttpClient(handler);

            var cacheFileLocation = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");

            try
            {
                var repo = new CityWardRepository(mockClient, cacheFileLocation);

                _ = await repo.GetWardsAsync();

                Assert.That(handler.Requests, Has.Count.EqualTo(1));
                Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Get));
                Assert.That(handler.Requests[0].RequestUri?.ToString(), Does.StartWith("https://ckan0.cf.opendata.inter.prod-toronto.ca"));

            }
            finally
            {
                File.Delete(cacheFileLocation);
            }
        }

        internal class NullHttpClientHandler : HttpClientHandler
        {
            public List<HttpRequestMessage> Requests { get; set; } = new List<HttpRequestMessage>();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Send(request, cancellationToken));
            }

            protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);

                return new HttpResponseMessage()
                {
                    StatusCode = System.Net.HttpStatusCode.OK
                };
            }
        }
    }
}
