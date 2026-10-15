namespace CarbonBill.Modules.Recommendations.Loader;

public interface IDatasetLoader<T>
{
    Task<int> LoadAsync(Stream jsonStream, CancellationToken ct = default);
    Task<int> LoadFromFileAsync(string filePath, CancellationToken ct = default);
}
