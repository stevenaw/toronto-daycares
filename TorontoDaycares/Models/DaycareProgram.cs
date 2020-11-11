namespace TorontoDaycares.Models
{
    public class DaycareProgram
    {
        public ProgramType ProgramType { get; set; }
        public int Capacity { get; set; }
        public bool? Vacancy { get; set; }
        public double? Rating { get; set; }
    }
}
