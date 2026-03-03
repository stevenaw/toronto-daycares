namespace TorontoDaycares.Exporters
{
    public interface IExporter
    {
        Task ExportAsync(Models.DaycareSearchResponse response);
    }
}
