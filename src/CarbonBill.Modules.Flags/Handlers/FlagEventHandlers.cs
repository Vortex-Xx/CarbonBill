using CarbonBill.Modules.Flags.Services;
using CarbonBill.SharedKernel.Events;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Flags.Handlers;

public class DocumentConfirmedEventHandler(
    IFlagEngine flagEngine,
    ILogger<DocumentConfirmedEventHandler> logger) : IDomainEventHandler<DocumentConfirmedEvent>
{
    private readonly IFlagEngine _flagEngine = flagEngine;
    private readonly ILogger<DocumentConfirmedEventHandler> _logger = logger;

    public async Task HandleAsync(DocumentConfirmedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling DocumentConfirmedEvent for Doc {DocId}, Org {OrgId}",
            domainEvent.DocumentId, domainEvent.OrgId);

        var period = domainEvent.Period ?? DateTime.UtcNow.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        await _flagEngine.EvaluateOrgPeriodAsync(domainEvent.OrgId, period, domainEvent.SiteId, cancellationToken);
    }
}

public class EmissionCalculatedEventHandler(
    IFlagEngine flagEngine,
    ILogger<EmissionCalculatedEventHandler> logger) : IDomainEventHandler<EmissionCalculatedEvent>
{
    private readonly IFlagEngine _flagEngine = flagEngine;
    private readonly ILogger<EmissionCalculatedEventHandler> _logger = logger;

    public async Task HandleAsync(EmissionCalculatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling EmissionCalculatedEvent for Org {OrgId}, Period {Period}",
            domainEvent.OrgId, domainEvent.Period);

        await _flagEngine.EvaluateOrgPeriodAsync(domainEvent.OrgId, domainEvent.Period, domainEvent.SiteId, cancellationToken);
    }
}
