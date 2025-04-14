using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Xunit;
using System.Collections;
using System.Collections.Generic;

public class CDSAPITests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Directory.GetCurrentDirectory(), "data");

    public void Dispose()
    {
        // Cleanup después de cada test
        var filesToDelete = new[] { "era5.grib", "sea_ice_type.zip", "unreachable" };
        foreach (var file in filesToDelete)
        {
            var path = Path.Combine(_dataDir, file);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ERA5MonthlyPressureData()
    {
        // Arrange
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
        var response = await CDSAPI.Retrieve(
            "reanalysis-era5-pressure-levels-monthly-means",
            request,
            filename
        );

        // Assert
        Assert.IsType<Dictionary<string, object>>(response);
        Assert.True(File.Exists(filename));
    }

    [Fact]
    public async Task SeaIceTypeData()
    {
        // Arrange
        var filename = Path.Combine(_dataDir, "sea_ice_type.zip");
        var request = new Dictionary<string,object>() {
            {"variable", "sea_ice_type"},
            {"region", "northern_hemisphere"},
            {"cdr_type", "cdr"},
            {"year","1979"},
            {"month","01"},
            {"day", "02"},
            {"version","3_0"},
            {"data_format","zip"}
        };

        // Act
        var response = await CDSAPI.Retrieve(
            "satellite-sea-ice-edge-type",
            request,
            filename
        );

        // Assert
        Assert.IsType<Dictionary<string, object>>(response);
        Assert.True(File.Exists(filename));

        // Extraer contenido ZIP
        string extractedFile = null;
        using (var archive = ZipFile.OpenRead(filename))
        {
            var entry = archive.Entries[0];
            extractedFile = Path.Combine(_dataDir, entry.Name);
            entry.ExtractToFile(extractedFile, true);
        }

        // Limpieza
        File.Delete(extractedFile);
    }

    [Fact]
    public async Task BadRequestsErrorsAreCaught()
    {
        // Arrange
        var goodName = "reanalysis-era5-single-levels";
        var badName = "bad-dataset";
        var badRequest = new Dictionary<string,object>() {
            {"badRequest", "is"},
            {"a", "bad"},
            {"re", "quest"}
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => CDSAPI.Retrieve(goodName, badRequest, "unreachable"));        
        await Assert.ThrowsAsync<ArgumentException>(() => CDSAPI.Retrieve(badName,  badRequest, "unreachable"));
    }

    [Fact]
    public async Task CredentialsWithScopedValues()
    {
        // Arrange
        var filename = Path.Combine(_dataDir, "sea_ice_type.zip");
        var dataset = "satellite-sea-ice-edge-type";
        var request = new Dictionary<string,object>() {
            {"variable", "sea_ice_type"},
            {"region", "northern_hemisphere"},
            {"cdr_type", "cdr"},
            {"year","1979"},
            {"month","01"},
            {"day", "02"},
            {"version","3_0"},
            {"data_format","zip"}
        };
        
        // Act
        var (url, key) = CDSAPI.GetCredentials();
        CDSAPI.SetCredentials(url, key);
        var response = await CDSAPI.Retrieve(dataset, request, filename);

        // Assert
        Assert.IsType<Dictionary<string, object>>(response);
    }
}