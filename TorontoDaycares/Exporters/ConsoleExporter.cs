using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TorontoDaycares.Models;

namespace TorontoDaycares.Exporters
{
    public class ConsoleExporter : IExporter
    {
        // TODO: Async

        public Task ExportAsync(Options filter, Dictionary<ProgramType, List<(Daycare Daycare, DaycareProgram Program)>> items)
        {
            foreach (var programType in items)
            {
                var title = $"Top {filter.TopN.Value} {programType.Key} programs:";
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
