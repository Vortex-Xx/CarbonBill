namespace CarbonBill.SharedKernel.Contracts;

public record ExpectedDocRuleDto(
    Guid Id,
    Guid OrgId,
    Guid AssetId,
    string AssetName,
    string AssetType,
    string DocType,
    string Frequency,
    int DueDayOfMonth,
    Guid? ResponsibleUserId,
    Guid? SiteId = null)
{
    public ExpectedDocRuleDto(
        Guid id,
        Guid orgId,
        Guid assetId,
        string assetName,
        string assetType,
        string docType,
        string frequency,
        int dueDayOfMonth,
        Guid? responsibleUserId) 
        : this(id, orgId, assetId, assetName, assetType, docType, frequency, dueDayOfMonth, responsibleUserId, null)
    {
    }
}

public interface IExpectedDocRuleReader
{
    Task<IReadOnlyList<ExpectedDocRuleDto>> GetRulesForOrgAsync(Guid orgId, CancellationToken ct = default);
    Task<IReadOnlyList<ExpectedDocRuleDto>> GetAllActiveRulesAsync(CancellationToken ct = default);
}
