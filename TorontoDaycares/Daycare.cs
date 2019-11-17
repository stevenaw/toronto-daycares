using System;
using System.Collections.Generic;

namespace TorontoDaycares
{
    public class Daycare
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Uri Uri { get; set; }
        public int WardNumber { get; set; }

        public string Address { get; set; }

        public List<DaycareProgram> Programs { get; set; }
    }

    public class DaycareProgram
    {
        public ProgramType ProgramType { get; set; }
        public int Capacity { get; set; }
        public bool? Vacancy { get; set; }
        public double? Rating { get; set; }
    }

    public enum ProgramType
    {
        Infant,
        Toddler,
        Preschool
    }
}
