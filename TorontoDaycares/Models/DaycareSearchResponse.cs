namespace TorontoDaycares.Models
{
    public class DaycareSearchResponse
    {
        public int TopN { get; set; }
        public List<TopProgramResult> TopPrograms { get; set; } = new List<TopProgramResult>();
    }

    public class TopProgramResult
    {
        public Daycare Daycare { get; set; } = null!;
        public DaycareProgram Program { get; set; } = null!;
    }
}
