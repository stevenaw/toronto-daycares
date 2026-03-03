using CommandLine;
using TorontoDaycares.Models;

namespace TorontoDaycares
{
    public class Options
    {
        [Option('n', "topN", Required = false, Default = 50)]
        public int TopN { get; set; }

        [Option('w', "wards", Required = false, Separator = ',')]
        public IEnumerable<int> WardList { get; set; } = Array.Empty<int>();

        [Option('a', "address", Required = false)]
        public string? Address { get; set; }
        public Coordinates? AddressCoordinates { get; set; }


        [Option('p', "programs", Required = false, Separator = ',')]
        public IEnumerable<ProgramType> ProgramList { get; set; } = Array.Empty<ProgramType>();

        [Option('o', "output", Required = false, Default = "")]
        public string OutputFile { get; set; } = "";
    }
}
