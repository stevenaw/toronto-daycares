
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

            var repo = new CityWardRepository(mockClient)
            {
                CacheFileLocation = cacheFileLocation
            };

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

            var repo = new CityWardRepository(mockClient)
            {
                CacheFileLocation = cacheFileLocation
            };

            var wards1 = await repo.GetWardsAsync();
            var wards2 = await repo.GetWardsAsync();

            Assert.That(wards1, Is.SameAs(wards2));
        }

        [Test]
        public async Task GetWardsAsync_FetchesWhenNotCached()
        {
            var handler = new NullHttpClientHandler();
            var mockClient = new HttpClient(handler);

            var cacheFileLocation = new TempFile("csv");
            var repo = new CityWardRepository(mockClient)
            {
                CacheFileLocation = cacheFileLocation
            };

            _ = await repo.GetWardsAsync();

            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(handler.Requests[0].RequestUri?.ToString(), Does.StartWith("https://ckan0.cf.opendata.inter.prod-toronto.ca"));
        }
    }
}
