using System.Collections.Generic;
using TorontoDaycares.Models;

namespace TorontoDaycares.Exporters
{
    public interface IExporter
    {
        void Export(Options filter, Dictionary<ProgramType, List<(Daycare Daycare, DaycareProgram Program)>> items);
    }
}
