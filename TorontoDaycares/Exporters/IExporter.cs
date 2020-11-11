﻿using System;
using System.Collections.Generic;
using System.Text;
using TorontoDaycares.Models;

namespace TorontoDaycares.Exporters
{
    public interface IExporter
    {
        void Export(Options filter, Dictionary<ProgramType, List<(Daycare Daycare, DaycareProgram Program)>> items);
    }
}
