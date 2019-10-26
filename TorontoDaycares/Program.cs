using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace TorontoDaycares
{
    class Program
    {
        static HttpClient GetHttpClient(int maxConnections)
        {
            return HttpClientFactory.Create(new HttpClientHandler()
            {
                MaxConnectionsPerServer = maxConnections
            });
        }

        static async Task Main(string[] args)
        {
            const int maxConnections = 5;
            var client = GetHttpClient(maxConnections);

            var repo = new DaycareRepository(client);
            var daycares = await repo.GetDaycares();

            var filter = new DaycareFilter()
            {
                TopN = 25,
                WardList = new[] { 6, 8, 11, 18, 17, 16, 15 },
                ProgramList = new [] { ProgramType.Infant }
            };

            var allPrograms = daycares
                .SelectMany(o =>
                {
                    var result = o.Programs
                        .Where(o2 => o2.Rating.HasValue)
                        .Select(p => new
                        {
                            Daycare = o,
                            Program = p
                        });

                    if (filter.WardList.Any())
                        result = result.Where(o2 => filter.WardList.Contains(o2.Daycare.WardNumber));
                    if (filter.ProgramList.Any())
                        result = result.Where(o2 => filter.ProgramList.Contains(o2.Program.ProgramType));

                    return result;
                })
                .ToArray();

            var topPrograms = allPrograms
                .GroupBy(o => o.Program.ProgramType)
                .ToDictionary(
                    o => o.Key,
                    vals =>
                    {
                        var result = vals
                                    .OrderByDescending(val => val.Program.Rating)
                                    .Select(val => val);

                        if (filter.TopN.HasValue)
                            result = result.Take(filter.TopN.Value);

                        return result.ToList();
                    }
                );

            foreach(var programType in topPrograms)
            {
                var title = $"Top {filter.TopN.Value} {programType.Key} programs:";
                Console.WriteLine(title);
                Console.WriteLine(new string('-', title.Length));

                foreach(var item in programType.Value)
                {
                    Console.WriteLine($"- {item.Program.Rating.Value}/5 - {item.Daycare.Name} - {item.Daycare.Address}");
                }
                Console.WriteLine();
            }
        }
    }
}
