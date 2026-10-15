using CarbonBill.Modules.Recommendations.Loader;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Recommendations.Seeds;

public interface IRecommendationsSeedLoader
{
    Task SeedDefaultMeasuresAsync(CancellationToken ct = default);
}

public class RecommendationsSeedLoader(
    MeasureDatasetLoader datasetLoader,
    ILogger<RecommendationsSeedLoader> logger) : IRecommendationsSeedLoader
{
    public async Task SeedDefaultMeasuresAsync(CancellationToken ct = default)
    {
        try
        {
            var baseDirs = new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../"))
            };

            string? sourcesPath = null;
            string? sampleLibraryPath = null;

            foreach (var baseDir in baseDirs)
            {
                var candidateSources = Path.Combine(baseDir, "seeds", "measures", "measure_sources_TEMPLATE.json");
                if (File.Exists(candidateSources) && sourcesPath == null)
                {
                    sourcesPath = candidateSources;
                }

                var candidateLibrary = Path.Combine(baseDir, "seeds", "measures", "measure_library_SAMPLE.json");
                if (File.Exists(candidateLibrary) && sampleLibraryPath == null)
                {
                    sampleLibraryPath = candidateLibrary;
                }
            }

            if (sourcesPath != null)
            {
                int sourcesLoaded = await datasetLoader.LoadSourcesFromFileAsync(sourcesPath, ct);
                logger.LogInformation("Loaded {Count} sources from {Path}", sourcesLoaded, sourcesPath);
            }
            else
            {
                logger.LogWarning("Measure sources seed file not found in search paths.");
            }

            if (sampleLibraryPath != null)
            {
                int measuresLoaded = await datasetLoader.LoadFromFileAsync(sampleLibraryPath, ct);
                logger.LogInformation("Loaded {Count} measures from {Path}", measuresLoaded, sampleLibraryPath);
            }
            else
            {
                logger.LogWarning("Measure library seed file not found in search paths.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while seeding recommendations dataset.");
        }
    }
}
