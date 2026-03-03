namespace TorontoDaycares.Exporters
{
    public class ConsoleExporter : IExporter
    {
        // TODO: Async
        public Task ExportAsync(Models.DaycareSearchResponse response)
        {
            var items = response.TopPrograms.GroupBy(x => x.Program.ProgramType).ToDictionary(g => g.Key, g => g.Select(x => (x.Daycare, x.Program)).ToList());

            foreach (var programType in items)
            {
                var title = $"Top {response.TopN} {programType.Key} programs:";
                Console.WriteLine(title);
                Console.WriteLine(new string('-', title.Length));

                foreach (var item in programType.Value)
                {
                    Console.Write($"{item.Program.Rating.Value,5:0.00} / 5 - {item.Daycare.Name} - {item.Daycare.Address}");
                    if (!string.IsNullOrEmpty(item.Daycare.NearestIntersection))
                        Console.Write($" ({item.Daycare.NearestIntersection})");
                    Console.WriteLine();
                }

                Console.WriteLine();
            }

            return Task.CompletedTask;
        }
    }
}
