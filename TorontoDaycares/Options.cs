﻿using CommandLine;
using System.Collections.Generic;

namespace TorontoDaycares
{
    public class Options
    {
        [Option('n', "topN", Required = false, Default = 50)]
        public int? TopN { get; set; }

        [Option('w', "wards", Required = false, Separator = ',')]
        public IEnumerable<int> WardList { get; set; }

        [Option('p', "programs", Required = false, Separator = ',')]
        public IEnumerable<ProgramType> ProgramList { get; set; }

        [Option('o', "output", Required = false, Default = "")]
        public string OutputFile { get; set; }
    }
}