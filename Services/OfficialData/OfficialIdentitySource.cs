using GovernmentMiningApp.Models;

namespace GovernmentMiningApp.Services.OfficialData;

/// <summary>
/// Adapter contract for an officially authorized subscriber/identity source.
/// Implementations must use documented credentials, endpoints and authorization scopes supplied by the data owner.
/// No web scraping, credential bypass, database guessing or IP-to-person inference belongs here.
/// </summary>
public interface IOfficialIdentitySource
{
    string SourceName { get; }
    Task<OfficialIdentityRecord?> ResolveAsync(string ipAddress, DateTime observedAt, string operationCode, CancellationToken cancellationToken = default);
}

public sealed class OfficialIdentityRecord
{
    public string SourceRecordId { get; init; } = "";
    public string SubscriberName { get; init; } = "";
    public string PhoneNumber { get; init; } = "";
    public string Province { get; init; } = "";
    public string City { get; init; } = "";
    public string Street { get; init; } = "";
    public string PostalCode { get; init; } = "";
    public string ISP { get; init; } = "";
    public string OperatorName { get; init; } = "";
    public DateTime QueriedAt { get; init; }
    public string EvidenceReference { get; init; } = "";
}

public sealed class UnconfiguredOfficialIdentitySource : IOfficialIdentitySource
{
    public string SourceName => "Unconfigured";
    public Task<OfficialIdentityRecord?> ResolveAsync(string ipAddress, DateTime observedAt, string operationCode, CancellationToken cancellationToken = default)
        => Task.FromResult<OfficialIdentityRecord?>(null);
}
