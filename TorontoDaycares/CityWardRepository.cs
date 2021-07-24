using CsvHelper;
using CsvHelper.Configuration;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using TorontoDaycares.Models;

namespace TorontoDaycares
{
    public class CityWardRepository
    {
        private HttpClient Client { get; }
        private string CacheFileLocation { get; }

        private Dictionary<string, CityWard> Wards { get; set; }

        public CityWardRepository(HttpClient client)
        {
            Client = client;
            CacheFileLocation = Path.Join(Directory.GetCurrentDirectory(), FileResources.DataDirectory, "City Wards Data.csv");
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
            if (Wards == null)
            {
                if (!File.Exists(CacheFileLocation))
                {
                    var response = await Client.GetAsync("https://ckan0.cf.opendata.inter.prod-toronto.ca/download_resource/7672dac5-b383-4d7c-90ec-291dc69d37bf?format=csv&projection=4326");

                    response.EnsureSuccessStatusCode();

                    await using var result = await response.Content.ReadAsStreamAsync();
                    await using var newFile = File.Create(CacheFileLocation);
                    await result.CopyToAsync(newFile);
                }

                await using var file = File.OpenRead(CacheFileLocation);
                using var reader = new StreamReader(file);
                var csvParser = new CsvReader(reader, CultureInfo.InvariantCulture);

                var wards = new Dictionary<string, CityWard>();

                csvParser.Context.RegisterClassMap<CityWardClassMap>();
                await foreach (var ward in csvParser.GetRecordsAsync<CityWard>())
                    wards.Add(ward.Name, ward);

                Wards = wards;
            }
        }

        internal async Task<CityWard> GetWardByNameAsync(string wardName)
        {
            await InitializeCache();

            if (Wards.TryGetValue(wardName, out var ward))
                return ward;
            return default;
        }
    }
}
