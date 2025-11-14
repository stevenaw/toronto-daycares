namespace TorontoDaycares.Tests
{
    [TestFixture]
    public class DaycareRepositoryTests
    {
        [Test]
        public async Task GetDaycare_WritesAndReadsCache()
        {
            using var handler = new NullHttpClientHandler();
            using var client = new HttpClient(handler);

            using var tempDir = new TempDirectory();
            var repo = new DaycareRepository(client)
            {
                HtmlCacheDirectory = tempDir
            };

            var sampleHtml = "<html><body><h1>Test Daycare</h1><div class='csd_opcrit_content_box'><h2>Test Daycare (123)</h2><header><p>123 Test St, Suite 5 <span class='ward-link'> Ward: 10</span></p></header></div><div class='csd_opcrit_content_box'><table><tbody><tr><td>Infant</td><td>10</td><td>Yes</td><td>4.5</td></tr></tbody></table></div></body></html>";

            handler.SetResponseContent(sampleHtml);

            var uri = new Uri("https://example.com/daycare/123");

            var daycare1 = await repo.GetDaycare(uri, "123", CancellationToken.None);

            // Ensure that content was fetched (handler recorded request)
            Assert.That(handler.Requests, Has.Count.EqualTo(1));

            // Second call should read from cache, not make another HTTP request
            var daycare2 = await repo.GetDaycare(uri, "123", CancellationToken.None);
            Assert.That(handler.Requests, Has.Count.EqualTo(1));

            Assert.That(daycare1.Name, Is.EqualTo("Test Daycare"));
            Assert.That(daycare1.Id, Is.EqualTo(123));
            Assert.That(daycare1.Programs, Has.Count.EqualTo(1));
            Assert.That(daycare2.Name, Is.EqualTo(daycare1.Name));
        }
    }
}
