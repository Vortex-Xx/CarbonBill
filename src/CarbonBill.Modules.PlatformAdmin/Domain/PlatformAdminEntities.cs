using CarbonBill.SharedKernel.Domain;

namespace CarbonBill.Modules.PlatformAdmin.Domain;

public class DatasetVersion : BaseEntity
{
    public string Kind { get; set; } = "Factors"; // Factors, Benchmarks, Templates
    public string Version { get; set; } = "2024.1";
    public string Description { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
