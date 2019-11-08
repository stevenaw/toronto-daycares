using System;
using System.Collections.Generic;
using System.Text;

namespace TorontoDaycares.Exporters
{
    public interface IExporter
    {
        void Export(DaycareFilter filter, Dictionary<ProgramType, List<(Daycare Daycare, DaycareProgram Program)>> items);
    }
}
