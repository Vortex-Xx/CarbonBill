using CarbonBill.SharedKernel.Contracts;

namespace CarbonBill.Modules.GapDetection.Fakes;

public class FakeExpectedDocRuleReader : IExpectedDocRuleReader
{
    private readonly List<ExpectedDocRuleDto> _rules = [];

    public void AddRule(ExpectedDocRuleDto rule)
    {
        _rules.Add(rule);
    }

    public void AddRules(IEnumerable<ExpectedDocRuleDto> rules)
    {
        _rules.AddRange(rules);
    }

    public void Clear()
    {
        _rules.Clear();
    }

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
