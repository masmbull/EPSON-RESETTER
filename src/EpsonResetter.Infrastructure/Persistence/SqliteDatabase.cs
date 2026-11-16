using Microsoft.Data.Sqlite;

namespace EpsonResetter.Infrastructure.Persistence;

public static class DatabasePaths
{
    public static string ApplicationDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EpsonResetter");

    public static string DatabasePath { get; } = Path.Combine(ApplicationDirectory, "epson-resetter.db");

    public static string LogDirectory { get; } = Path.Combine(ApplicationDirectory, "logs");
}

/// <summary>Creates the local SQLite database and its schema on first use.</summary>
public static class SqliteDatabase
{
    private static string _databasePath = DatabasePaths.DatabasePath;

    /// <summary>Database file in use. Production uses the per-user LocalAppData path.</summary>
    public static string CurrentDatabasePath
    {
        get => _databasePath;
        set => _databasePath = string.IsNullOrWhiteSpace(value) ? DatabasePaths.DatabasePath : value;
    }

    public static void ResetToDefaultPath() => _databasePath = DatabasePaths.DatabasePath;
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS printers (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            driver_name TEXT,
            driver_manufacturer TEXT,
            driver_version TEXT,
            driver_inf_name TEXT,
            port_name TEXT,
            connection_type INTEGER NOT NULL,
            ip_address TEXT,
            port_number INTEGER,
            condition INTEGER NOT NULL,
            work_offline INTEGER NOT NULL,
            is_default INTEGER NOT NULL,
            location TEXT,
            comment TEXT,
            manufacturer TEXT,
            model_name TEXT,
            detected_at_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS activity (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp_utc TEXT NOT NULL,
            printer_name TEXT NOT NULL,
            model_name TEXT,
            action TEXT NOT NULL,
            result INTEGER NOT NULL,
            details TEXT,
            error TEXT
        );

        CREATE INDEX IF NOT EXISTS IX_activity_timestamp ON activity (timestamp_utc DESC);

        CREATE TABLE IF NOT EXISTS settings (
            id INTEGER PRIMARY KEY CHECK (id = 1),
            json TEXT NOT NULL
        );
        """;

    public static SqliteConnection OpenConnection()
    {
        var directory = Path.GetDirectoryName(CurrentDatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = CurrentDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        }.ToString());

        connection.Open();
        EnsureSchema(connection);
        return connection;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Schema;
        command.ExecuteNonQuery();
    }
}