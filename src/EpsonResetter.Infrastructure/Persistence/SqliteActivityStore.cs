using EpsonResetter.Core.Models;
using EpsonResetter.Core.Services;
using Microsoft.Data.Sqlite;

namespace EpsonResetter.Infrastructure.Persistence;

public sealed class SqliteActivityStore : IActivityStore
{
    public async Task AddAsync(ActivityRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await using var connection = SqliteDatabase.OpenConnection();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO activity (timestamp_utc, printer_name, model_name, action, result, details, error)
            VALUES ($timestamp, $printer, $model, $action, $result, $details, $error);
            """;

        command.Parameters.AddWithValue("$timestamp", ToStoredTime(record.TimestampUtc));
        command.Parameters.AddWithValue("$printer", record.PrinterName);
        command.Parameters.AddWithValue("$model", (object?)record.ModelName ?? DBNull.Value);
        command.Parameters.AddWithValue("$action", record.Action);
        command.Parameters.AddWithValue("$result", (int)record.Result);
        command.Parameters.AddWithValue("$details", (object?)record.Details ?? DBNull.Value);
        command.Parameters.AddWithValue("$error", (object?)record.Error ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        command.CommandText = "SELECT last_insert_rowid();";
        record.Id = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<ActivityRecord>> ListAsync(int maxRecords = 200, CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(maxRecords, 1, 5000);

        await using var connection = SqliteDatabase.OpenConnection();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT id, timestamp_utc, printer_name, model_name, action, result, details, error
            FROM activity
            ORDER BY id DESC
            LIMIT $limit;
            """;

        command.Parameters.AddWithValue("$limit", limit);

        var records = new List<ActivityRecord>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            records.Add(new ActivityRecord
            {
                Id = reader.GetInt64(0),
                TimestampUtc = FromStoredTime(reader.GetString(1)),
                PrinterName = reader.GetString(2),
                ModelName = reader.IsDBNull(3) ? null : reader.GetString(3),
                Action = reader.GetString(4),
                Result = (ActivityResult)reader.GetInt32(5),
                Details = reader.IsDBNull(6) ? null : reader.GetString(6),
                Error = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }

        return records;
    }

    public async Task<int> ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = SqliteDatabase.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM activity;";
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static string ToStoredTime(DateTime value) =>
        (value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime())
            .ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    internal static DateTime FromStoredTime(string value) =>
        DateTime.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : DateTime.UtcNow;
}