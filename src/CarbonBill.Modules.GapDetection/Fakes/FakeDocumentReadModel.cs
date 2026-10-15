using CarbonBill.SharedKernel.Contracts;

namespace CarbonBill.Modules.GapDetection.Fakes;

public class FakeDocumentReadModel : IDocumentReadModel
{
    private readonly List<DocumentSummaryDto> _documents = [];

    public void AddDocument(DocumentSummaryDto doc)
    {
        _documents.Add(doc);
    }

    public void AddDocuments(IEnumerable<DocumentSummaryDto> docs)
    {
        _documents.AddRange(docs);
    }

    public void Clear()
    {
        _documents.Clear();
    }

    public Task<IReadOnlyList<DocumentSummaryDto>> GetDocumentsAsync(Guid orgId, string? billingPeriod = null, CancellationToken ct = default)
    {
        var query = _documents.Where(d => d.OrgId == orgId);
        if (!string.IsNullOrWhiteSpace(billingPeriod))
        {
            query = query.Where(d => string.Equals(d.BillingPeriod, billingPeriod, StringComparison.OrdinalIgnoreCase));
        }

        IReadOnlyList<DocumentSummaryDto> result = query.ToList();
        return Task.FromResult(result);
    }

    public Task<bool> HasDocumentAsync(Guid orgId, Guid? assetId, string? docType, string billingPeriod, CancellationToken ct = default)
    {
        var exists = _documents.Any(d =>
            d.OrgId == orgId &&
            (!assetId.HasValue || d.AssetId == assetId) &&
            (string.IsNullOrWhiteSpace(docType) || string.Equals(d.DocType, docType, StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(d.BillingPeriod, billingPeriod, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(exists);
    }
}
