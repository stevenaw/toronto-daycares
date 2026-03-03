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

            var html = await ProjectFiles.TestData.Daycare_TestData_html.ReadAllTextAsync();
            handler.SetResponseContent(html);

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

        [Test]
        public async Task Parse_LinkedProgram_HasExpectedProgramTypes()
        {
            using var handler = new NullHttpClientHandler();
            using var client = new HttpClient(handler);

            using var tempDir = new TempDirectory();
            var repo = new DaycareRepository(client)
            {
                HtmlCacheDirectory = tempDir
            };

            var html = await ProjectFiles.TestData.Daycare_LinkedProgram_html.ReadAllTextAsync();
            handler.SetResponseContent(html);

            var uri = new Uri("https://example.com/daycare/1288");

            var daycare = await repo.GetDaycare(uri, "1288", CancellationToken.None);

            Assert.That(daycare.Programs, Is.Not.Null);
            var types = daycare.Programs.Select(p => p.ProgramType).ToArray();
            Assert.That(types, Is.EquivalentTo([Models.ProgramType.Infant, Models.ProgramType.Toddler, Models.ProgramType.Preschool]));
        }

        [Test]
        public async Task Parse_UnlinkedProgram_HasExpectedProgramTypes()
        {
            using var handler = new NullHttpClientHandler();
            using var client = new HttpClient(handler);

            using var tempDir = new TempDirectory();
            var repo = new DaycareRepository(client)
            {
                HtmlCacheDirectory = tempDir
            };

            var html = await ProjectFiles.TestData.Daycare_UnlinkedProgram_html.ReadAllTextAsync();
            handler.SetResponseContent(html);

            var uri = new Uri("https://example.com/daycare/14687");

            var daycare = await repo.GetDaycare(uri, "14687", CancellationToken.None);

            Assert.That(daycare.Programs, Is.Not.Null);

            var types = daycare.Programs.Select(p => p.ProgramType).ToArray();
            Assert.That(types, Is.EqualTo([Models.ProgramType.Preschool]));
        }
    }
}
