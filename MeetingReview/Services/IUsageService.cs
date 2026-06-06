using MeetingReview.Models;

namespace MeetingReview.Services;

public interface IUsageService
{
    Task InitAsync(CancellationToken ct = default);
    Task SaveUsageAsync(ApiUsageRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<ApiUsageRecord>> QueryUsageAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<ModelRate>> GetRatesAsync(CancellationToken ct = default);
    Task ReplaceAllRatesAsync(IEnumerable<ModelRate> rates, CancellationToken ct = default);
    double CalculateCost(string modelVersion, int promptTokens, int candidateTokens);
}
