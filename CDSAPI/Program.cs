
class Program
{

    static async Task Main() {

        string _dataDir = Path.Combine(Directory.GetCurrentDirectory(), "data");

        var dataset = "reanalysis-era5-pressure-levels-monthly-means";
        var filename = Path.Combine(_dataDir, "era5.grib");
        var request = new Dictionary<string,object>() {
            {"data_format", "grib"},
            {"product_type", "monthly_averaged_reanalysis"},
            {"variable", "divergence"},
            {"pressure_level", "1"},
            {"year","2020"},
            {"month","06"},
            {"area", new double [] {90,-180,-90,180}},
            {"time","00:00"}
        };

        // Act
        CDSAPI.SetCredentials("https://cds.climate.copernicus.eu/api","00112233-4455-6677-c899-aabbccddeeff");
        var response = await CDSAPI.Retrieve(dataset, request, filename);
    }
}
