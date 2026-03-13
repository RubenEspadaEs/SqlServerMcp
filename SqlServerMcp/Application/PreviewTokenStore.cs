using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SqlServerMcp.Application;

/// <summary>
/// Manages preview tokens used to validate guarded data changes.
/// </summary>
public interface IPreviewTokenStore
{
    /// <summary>
    /// Creates a preview token for the current SQL statement and execution options.
    /// </summary>
    string Create(string connectionString, string normalizedSql, IReadOnlyDictionary<string, JsonElement>? parameters, bool allowAffectAllRows);

    /// <summary>
    /// Validates that a preview token still matches the requested SQL statement and execution options.
    /// </summary>
    bool Validate(string token, string connectionString, string normalizedSql, IReadOnlyDictionary<string, JsonElement>? parameters, bool allowAffectAllRows);
}

/// <summary>
/// Stores preview tokens in memory for the lifetime of the process.
/// </summary>
public sealed class InMemoryPreviewTokenStore : IPreviewTokenStore
{
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, PreviewTokenState> _tokens = new();

    /// <inheritdoc />
    public string Create(string connectionString, string normalizedSql, IReadOnlyDictionary<string, JsonElement>? parameters, bool allowAffectAllRows)
    {
        CleanupExpiredTokens();

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _tokens[token] = new PreviewTokenState(
            DateTimeOffset.UtcNow.Add(Expiration),
            ComputeFingerprint(connectionString, normalizedSql, parameters, allowAffectAllRows));

        return token;
    }

    /// <inheritdoc />
    public bool Validate(string token, string connectionString, string normalizedSql, IReadOnlyDictionary<string, JsonElement>? parameters, bool allowAffectAllRows)
    {
        CleanupExpiredTokens();
        if (!_tokens.TryRemove(token, out var state))
        {
            return false;
        }

        var currentFingerprint = ComputeFingerprint(connectionString, normalizedSql, parameters, allowAffectAllRows);
        return state.ExpiresAt >= DateTimeOffset.UtcNow &&
               StringComparer.Ordinal.Equals(state.Fingerprint, currentFingerprint);
    }

    private void CleanupExpiredTokens()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _tokens.Where(pair => pair.Value.ExpiresAt < now))
        {
            _tokens.TryRemove(pair.Key, out _);
        }
    }

    private static string ComputeFingerprint(string connectionString, string normalizedSql, IReadOnlyDictionary<string, JsonElement>? parameters, bool allowAffectAllRows)
    {
        var payload = JsonSerializer.Serialize(new
        {
            connectionString,
            normalizedSql,
            allowAffectAllRows,
            parameters = parameters?.OrderBy(pair => pair.Key, StringComparer.Ordinal)
        });

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private sealed record PreviewTokenState(DateTimeOffset ExpiresAt, string Fingerprint);
}
