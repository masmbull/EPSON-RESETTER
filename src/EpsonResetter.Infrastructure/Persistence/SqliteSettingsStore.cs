using System.Text.Json;
using EpsonResetter.Core.Services;
using EpsonResetter.Core.Settings;
using Microsoft.Data.Sqlite;

namespace EpsonResetter.Infrastructure.Persistence;

public sealed class SqliteSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = SqliteDatabase.OpenConnection();
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT json FROM settings WHERE id = 1;";
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        if (result is null or DBNull)
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>((string)result, JsonOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await using var connection = SqliteDatabase.OpenConnection();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO settings (id, json) VALUES (1, $json)
            ON CONFLICT(id) DO UPDATE SET json = excluded.json;
            """;

        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(settings, JsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}