using CarbonBill.SharedKernel.Contracts;

namespace CarbonBill.Modules.Flags.Fakes;

public class FakeExpectedDocRuleReader : IExpectedDocRuleReader
{
    private readonly List<ExpectedDocRuleDto> _rules = [];

    public void AddRule(ExpectedDocRuleDto rule) => _rules.Add(rule);

    public void Clear() => _rules.Clear();

    public Task<IReadOnlyList<ExpectedDocRuleDto>> GetRulesForOrgAsync(Guid orgId, CancellationToken ct = default)
    {
        IReadOnlyList<ExpectedDocRuleDto> matching = _rules
            .Where(r => r.OrgId == orgId)
            .ToList();
        return Task.FromResult(matching);
    }

    public Task<IReadOnlyList<ExpectedDocRuleDto>> GetAllActiveRulesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ExpectedDocRuleDto> all = _rules.ToList();
        return Task.FromResult(all);
    }
}

public class FakeDocumentReadModel : IDocumentReadModel
{
    private readonly List<DocumentSummaryDto> _documents = [];

    public void AddDocument(DocumentSummaryDto doc) => _documents.Add(doc);

    public void Clear() => _documents.Clear();

    public Task<IReadOnlyList<DocumentSummaryDto>> GetDocumentsAsync(Guid orgId, string? billingPeriod = null, CancellationToken ct = default)
    {
        var list = _documents.Where(d => d.OrgId == orgId);
        if (!string.IsNullOrEmpty(billingPeriod))
            list = list.Where(d => d.BillingPeriod == billingPeriod);

        return Task.FromResult<IReadOnlyList<DocumentSummaryDto>>(list.ToList());
    }

    public Task<bool> HasDocumentAsync(Guid orgId, Guid? assetId, string? docType, string billingPeriod, CancellationToken ct = default)
    {
        var exists = _documents.Any(d =>
            d.OrgId == orgId &&
            d.AssetId == assetId &&
            d.DocType == docType &&
            d.BillingPeriod == billingPeriod);

        return Task.FromResult(exists);
    }
}
