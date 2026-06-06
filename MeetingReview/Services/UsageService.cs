using Microsoft.Data.Sqlite;
using MeetingReview.Models;

namespace MeetingReview.Services;

public sealed class UsageService : IUsageService
{
    private static readonly string DbPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "MeetingReview", "usage.db");

    private readonly string _cs = $"Data Source={DbPath}";
    private Dictionary<string, ModelRate> _rateCache = new();

    public async Task InitAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        await using var conn = new SqliteConnection(_cs);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ApiUsage (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CalledAt TEXT NOT NULL,
                ModelVersion TEXT NOT NULL,
                PromptTokens INTEGER NOT NULL,
                CandidateTokens INTEGER NOT NULL,
                TotalTokens INTEGER NOT NULL,
                EstimatedCostUsd REAL NOT NULL
            );
            CREATE TABLE IF NOT EXISTS ModelRates (
                ModelPattern TEXT PRIMARY KEY,
                InputRatePerMillion REAL NOT NULL,
                OutputRatePerMillion REAL NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);

        cmd.CommandText = "SELECT COUNT(*) FROM ModelRates;";
        var count = (long)(await cmd.ExecuteScalarAsync(ct) ?? 0L);
        if (count == 0)
        {
            cmd.CommandText = """
                INSERT INTO ModelRates VALUES ('gemini-1.5-flash', 0.075,  0.30);
                INSERT INTO ModelRates VALUES ('gemini-1.5-pro',   1.25,   5.00);
                INSERT INTO ModelRates VALUES ('gemini-2.0-flash', 0.10,   0.40);
                INSERT INTO ModelRates VALUES ('gemini-2.5-flash', 0.15,   0.60);
                INSERT INTO ModelRates VALUES ('gemini-2.5-pro',   1.25,   10.00);
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await RefreshCacheAsync(conn, ct);
    }

    public async Task SaveUsageAsync(ApiUsageRecord record, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ApiUsage (CalledAt, ModelVersion, PromptTokens, CandidateTokens, TotalTokens, EstimatedCostUsd)
            VALUES ($at, $model, $prompt, $cand, $total, $cost);
            """;
        cmd.Parameters.AddWithValue("$at",     record.CalledAt.ToString("O"));
        cmd.Parameters.AddWithValue("$model",  record.ModelVersion);
        cmd.Parameters.AddWithValue("$prompt", record.PromptTokens);
        cmd.Parameters.AddWithValue("$cand",   record.CandidateTokens);
        cmd.Parameters.AddWithValue("$total",  record.TotalTokens);
        cmd.Parameters.AddWithValue("$cost",   record.EstimatedCostUsd);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ApiUsageRecord>> QueryUsageAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, CalledAt, ModelVersion, PromptTokens, CandidateTokens, TotalTokens, EstimatedCostUsd
            FROM ApiUsage
            WHERE CalledAt >= $from AND CalledAt < $to
            ORDER BY CalledAt DESC;
            """;
        cmd.Parameters.AddWithValue("$from", from.ToString("O"));
        cmd.Parameters.AddWithValue("$to",   to.ToString("O"));

        var records = new List<ApiUsageRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            records.Add(new ApiUsageRecord
            {
                Id              = reader.GetInt32(0),
                CalledAt        = DateTime.Parse(reader.GetString(1)),
                ModelVersion    = reader.GetString(2),
                PromptTokens    = reader.GetInt32(3),
                CandidateTokens = reader.GetInt32(4),
                TotalTokens     = reader.GetInt32(5),
                EstimatedCostUsd = reader.GetDouble(6)
            });
        return records;
    }

    public async Task<IReadOnlyList<ModelRate>> GetRatesAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_cs);
        await conn.OpenAsync(ct);
        return await ReadRatesAsync(conn, ct);
    }

    public async Task ReplaceAllRatesAsync(IEnumerable<ModelRate> rates, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_cs);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var del = conn.CreateCommand())
        {
            del.Transaction = (SqliteTransaction)tx;
            del.CommandText = "DELETE FROM ModelRates;";
            await del.ExecuteNonQueryAsync(ct);
        }

        foreach (var r in rates)
        {
            await using var ins = conn.CreateCommand();
            ins.Transaction = (SqliteTransaction)tx;
            ins.CommandText = "INSERT INTO ModelRates VALUES ($p, $i, $o);";
            ins.Parameters.AddWithValue("$p", r.ModelPattern);
            ins.Parameters.AddWithValue("$i", r.InputRatePerMillion);
            ins.Parameters.AddWithValue("$o", r.OutputRatePerMillion);
            await ins.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        await RefreshCacheAsync(conn, ct);
    }

    public double CalculateCost(string modelVersion, int promptTokens, int candidateTokens)
    {
        var key = _rateCache.Keys.FirstOrDefault(k => modelVersion.Contains(k, StringComparison.OrdinalIgnoreCase));
        if (key == null) return 0;
        var rate = _rateCache[key];
        return ((double)promptTokens    / 1_000_000) * rate.InputRatePerMillion
             + ((double)candidateTokens / 1_000_000) * rate.OutputRatePerMillion;
    }

    private async Task RefreshCacheAsync(SqliteConnection conn, CancellationToken ct)
    {
        var rates = await ReadRatesAsync(conn, ct);
        _rateCache = rates.ToDictionary(r => r.ModelPattern, r => r);
    }

    private static async Task<List<ModelRate>> ReadRatesAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ModelPattern, InputRatePerMillion, OutputRatePerMillion FROM ModelRates ORDER BY ModelPattern;";
        var list = new List<ModelRate>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(new ModelRate
            {
                ModelPattern         = reader.GetString(0),
                InputRatePerMillion  = reader.GetDouble(1),
                OutputRatePerMillion = reader.GetDouble(2)
            });
        return list;
    }
}
