namespace TorontoDaycares.Tests
{
    using System.IO;
    using System.Linq;
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

            var sampleHtml = "<html><body><h1>Test Daycare</h1><div class='csd_opcrit_content_box'><h2>Test Daycare (123)</h2><header><p>123 Test St, Suite 5 <span class='ward-link'> Ward: 10</span></p></header></div><div class='csd_opcrit_content_box'><header><h2 class=\"csd_title\">Program Offerings   and Quality Ratings </h2><table><tbody><tr><td>Infant</td><td>10</td><td>Yes</td><td>4.5</td></tr></tbody></table></div></body></html>";

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
