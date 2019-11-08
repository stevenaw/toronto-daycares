using System;
using System.Collections.Generic;

namespace TorontoDaycares.Exporters
{
    public class ConsoleExporter : IExporter
    {
        public void Export(DaycareFilter filter, Dictionary<ProgramType, List<(Daycare Daycare, DaycareProgram Program)>> items)
        {
            foreach (var programType in items)
            {
                var title = $"Top {filter.TopN.Value} {programType.Key} programs:";
                Console.WriteLine(title);
                Console.WriteLine(new string('-', title.Length));

                foreach (var item in programType.Value)
                {
                    Console.WriteLine($"- {item.Program.Rating.Value}/5 - {item.Daycare.Name} - {item.Daycare.Address}");
                }
                Console.WriteLine();
            }
        }
    }
}
