using GovernmentMiningApp.Models;

namespace GovernmentMiningApp.Services;

/// <summary>
/// Contract for resolving a network observation against an explicitly authorized
/// official data source. This component deliberately does not scrape websites,
/// infer identities from IP addresses, or fabricate missing personal data.
/// </summary>
public interface IOfficialIdentitySource
{
    string SourceName { get; }
    Task<IdentityResolutionResult> ResolveAsync(string ipAddress, DateTime observedAt, CancellationToken cancellationToken = default);
}

public sealed class IdentityResolutionResult
{
    public string MatchStatus { get; init; } = "Unresolved";
    public string DataSource { get; init; } = "";
    public string SourceRecordId { get; init; } = "";
    public DateTime? SourceQueriedAt { get; init; }
    public string SubscriberName { get; init; } = "";
    public string PhoneNumber { get; init; } = "";
    public string Province { get; init; } = "";
    public string City { get; init; } = "";
    public string Street { get; init; } = "";
    public string PostalCode { get; init; } = "";
    public string Notes { get; init; } = "";
}

/// <summary>Production-safe default: no identity is asserted without an official adapter.</summary>
public sealed class UnconfiguredOfficialIdentitySource : IOfficialIdentitySource
{
    public string SourceName => "NotConfigured";

    public Task<IdentityResolutionResult> ResolveAsync(string ipAddress, DateTime observedAt, CancellationToken cancellationToken = default)
        => Task.FromResult(new IdentityResolutionResult
        {
            MatchStatus = "Unresolved",
            DataSource = SourceName,
            SourceQueriedAt = DateTime.UtcNow,
            Notes = "منبع رسمی هویت برای این محیط پیکربندی نشده است؛ هیچ اطلاعات هویتی استنتاج یا تولید نشد."
        });
}
