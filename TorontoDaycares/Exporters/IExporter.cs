﻿using TorontoDaycares.Models;

namespace TorontoDaycares.Exporters
{
    public interface IExporter
    {
        Task ExportAsync(Options filter, Dictionary<ProgramType, List<(Daycare Daycare, DaycareProgram Program)>> items);
    }
}
