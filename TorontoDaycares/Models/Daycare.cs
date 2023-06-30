using System.Text.Json.Serialization;

namespace TorontoDaycares.Models
{
    public class Daycare
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Uri Uri { get; set; }

        public int WardNumber { get; set; }
        public string WardName { get; set; }

        public string Address { get; set; }
        public string Unit { get; set; }
        public string NearestIntersection { get; set; }

        public Coordinates GpsCoordinates { get; set; }

        public List<DaycareProgram> Programs { get; set; }
    }

    [JsonSerializable(typeof(Daycare[]))]
    internal partial class DaycareJsonContext : JsonSerializerContext
    {
    }
}
