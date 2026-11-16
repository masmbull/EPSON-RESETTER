using EpsonResetter.Core.Models;
using EpsonResetter.Core.Services;
using Microsoft.Data.Sqlite;

namespace EpsonResetter.Infrastructure.Persistence;

public sealed class SqlitePrinterCacheStore : IPrinterCacheStore
{
    public async Task SaveAsync(IReadOnlyList<PrinterDevice> printers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(printers);

        await using var connection = SqliteDatabase.OpenConnection();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM printers;";
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var printer in printers)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR REPLACE INTO printers (
                    id, name, driver_name, driver_manufacturer, driver_version, driver_inf_name,
                    port_name, connection_type, ip_address, port_number, condition, work_offline,
                    is_default, location, comment, manufacturer, model_name, detected_at_utc
                ) VALUES (
                    $id, $name, $driver, $drivermanufacturer, $driverversion, $driverinf,
                    $port, $connection, $ip, $portnumber, $condition, $offline,
                    $isdefault, $location, $comment, $manufacturer, $model, $detected
                );
                """;

            command.Parameters.AddWithValue("$id", printer.Id);
            command.Parameters.AddWithValue("$name", printer.Name);
            command.Parameters.AddWithValue("$driver", (object?)printer.DriverName ?? string.Empty);
            command.Parameters.AddWithValue("$drivermanufacturer", (object?)printer.DriverManufacturer ?? DBNull.Value);
            command.Parameters.AddWithValue("$driverversion", (object?)printer.DriverVersion ?? DBNull.Value);
            command.Parameters.AddWithValue("$driverinf", (object?)printer.DriverInfName ?? DBNull.Value);
            command.Parameters.AddWithValue("$port", (object?)printer.PortName ?? string.Empty);
            command.Parameters.AddWithValue("$connection", (int)printer.ConnectionType);
            command.Parameters.AddWithValue("$ip", (object?)printer.IpAddress ?? DBNull.Value);
            command.Parameters.AddWithValue("$portnumber", (object?)printer.PortNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("$condition", (int)printer.Condition);
            command.Parameters.AddWithValue("$offline", printer.WorkOffline ? 1 : 0);
            command.Parameters.AddWithValue("$isdefault", printer.IsDefault ? 1 : 0);
            command.Parameters.AddWithValue("$location", (object?)printer.Location ?? DBNull.Value);
            command.Parameters.AddWithValue("$comment", (object?)printer.Comment ?? DBNull.Value);
            command.Parameters.AddWithValue("$manufacturer", printer.Manufacturer);
            command.Parameters.AddWithValue("$model", (object?)printer.ModelName ?? string.Empty);
            command.Parameters.AddWithValue("$detected", SqliteActivityStore.ToStoredTime(printer.DetectedAtUtc));

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PrinterDevice>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = SqliteDatabase.OpenConnection();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT id, name, driver_name, driver_manufacturer, driver_version, driver_inf_name,
                   port_name, connection_type, ip_address, port_number, condition, work_offline,
                   is_default, location, comment, manufacturer, model_name, detected_at_utc
            FROM printers
            ORDER BY name;
            """;

        var printers = new List<PrinterDevice>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            printers.Add(new PrinterDevice
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                DriverName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                DriverManufacturer = reader.IsDBNull(3) ? null : reader.GetString(3),
                DriverVersion = reader.IsDBNull(4) ? null : reader.GetString(4),
                DriverInfName = reader.IsDBNull(5) ? null : reader.GetString(5),
                PortName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                ConnectionType = (PrinterConnectionType)reader.GetInt32(7),
                IpAddress = reader.IsDBNull(8) ? null : reader.GetString(8),
                PortNumber = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                Condition = (PrinterCondition)reader.GetInt32(10),
                WorkOffline = reader.GetInt32(11) != 0,
                IsDefault = reader.GetInt32(12) != 0,
                Location = reader.IsDBNull(13) ? null : reader.GetString(13),
                Comment = reader.IsDBNull(14) ? null : reader.GetString(14),
                Manufacturer = reader.IsDBNull(15) ? "Unknown" : reader.GetString(15),
                ModelName = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                DetectedAtUtc = SqliteActivityStore.FromStoredTime(reader.GetString(17))
            });
        }

        return printers;
    }
}