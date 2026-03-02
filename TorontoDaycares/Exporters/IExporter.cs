namespace TorontoDaycares.Exporters
{
    public interface IExporter
    {
        Task ExportAsync(Options filter, Models.DaycareSearchResponse response);
    }
}
