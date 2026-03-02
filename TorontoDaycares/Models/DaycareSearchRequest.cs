namespace TorontoDaycares.Models
{
    public class DaycareSearchRequest
    {
        public int TopN { get; set; } = 50;
        public int[]? Wards { get; set; }
        public ProgramType[]? Programs { get; set; }
        public string? NearAddress { get; set; }
        public DaycareSearchOptions Options { get; set; } = DaycareSearchOptions.None;
    }
}
