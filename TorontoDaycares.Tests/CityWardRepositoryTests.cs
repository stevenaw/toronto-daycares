
namespace TorontoDaycares.Tests
{
    [TestFixture]
    public class CityWardRepositoryTests
    {
        [Test]
        public async Task GetWardsAsync_ReturnsAll()
        {
            var repo = GetRepository();

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
            var repo = GetRepository();

            var wards1 = await repo.GetWardsAsync();
            var wards2 = await repo.GetWardsAsync();

            Assert.That(wards1, Is.SameAs(wards2));
        }

        private static CityWardRepository GetRepository()
        {
            using var mockClient = new HttpClient(new ThrowingHttpClientHandler());
            var cacheFileLocation = Path.Join(Directory.GetCurrentDirectory(), "Data", "City Wards Data - 4326.csv");

            var repo = new CityWardRepository(mockClient, cacheFileLocation);

            return repo;
        }

        public class ThrowingHttpClientHandler : HttpClientHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
            protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
