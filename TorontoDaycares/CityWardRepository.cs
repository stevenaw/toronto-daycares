using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using TorontoDaycares.Models;

namespace TorontoDaycares
{
    public class CityWardRepository
    {
        private const string CityWardDataset = "https://ckan0.cf.opendata.inter.prod-toronto.ca/dataset/5e7a8234-f805-43ac-820f-03d7c360b588/resource/12a877e3-82ce-4334-ae1d-1c1f0ea3823f/download/City%20Wards%20Data%20-%204326.csv";

        private HttpClient Client { get; }
        private string CacheFileLocation { get; }
        private Dictionary<string, CityWard> WardsByName { get; set; } = [];
        private CityWard[] Wards { get; set; } = [];

        public CityWardRepository(HttpClient client)
        {
            Client = client;
            CacheFileLocation = Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory, "City Wards Data.csv");
        }
        public CityWardRepository(HttpClient client, string cacheFileLocation)
        {
            Client = client;
            CacheFileLocation = cacheFileLocation;
        }

        public class CityWardClassMap : ClassMap<CityWard>
        {
            public CityWardClassMap()
            {
                Map(m => m.Number).Name("AREA_LONG_CODE");
                Map(m => m.Name).Name("AREA_NAME");
            }
        }

        private async Task InitializeCache()
        {
            if (WardsByName is { Count: 0 })
            {
                if (!File.Exists(CacheFileLocation))
                {
                    var response = await Client.GetAsync(CityWardDataset);

                    response.EnsureSuccessStatusCode();

                    await using var result = await response.Content.ReadAsStreamAsync();
                    await using var newFile = File.Create(CacheFileLocation);
                    await result.CopyToAsync(newFile);
                }

                await using var file = File.OpenRead(CacheFileLocation);
                using var reader = new StreamReader(file);

                var csvParser = new CsvReader(reader, CultureInfo.InvariantCulture);
                csvParser.Context.RegisterClassMap<CityWardClassMap>();

                var wards = new Dictionary<string, CityWard>(25);
                var allWards = new CityWard[25];

                await foreach (var ward in csvParser.GetRecordsAsync<CityWard>())
                {
                    allWards[ward.Number-1] = ward;
                    wards.Add(ward.Name, ward);
                }

                WardsByName = wards;
                Wards = allWards;
            }
        }

        public async Task<CityWard?> GetWardByNameAsync(string wardName)
        {
            await InitializeCache();

            if (WardsByName.TryGetValue(wardName, out var ward))
                return ward;

            return default;
        }

        public async Task<CityWard[]> GetWardsAsync()
        {
            await InitializeCache();

            return Wards;
        }
    }
}
