using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MikroTikVoucherPrinter.Infrastructure.Data;

/// <summary>
/// ترقية مخطط SQLite تدريجية للأجهزة التي تعتمد على <see cref="DatabaseFacade.EnsureCreatedAsync"/>
/// مع جدول <c>SchemaVersions</c>.
/// </summary>
public static class LuxCardSqliteSchemaUpgrade
{
    private const int CurrentSchemaVersion = 14;

    public static async Task ApplyAsync(LuxCardDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        var conn = db.Database.GetDbConnection();
        var ownConnection = conn.State == ConnectionState.Closed;
        if (ownConnection)
            await conn.OpenAsync(cancellationToken);

        try
        {
            await EnsureSchemaVersionsTableAsync(conn, cancellationToken);

            var applied = await GetMaxAppliedVersionAsync(conn, cancellationToken);
            if (applied >= CurrentSchemaVersion)
                return;

            await EnsureTemplateConfigsTableAsync(conn, ct: cancellationToken);
            await EnsureAgentsTableAsync(conn, ct: cancellationToken);
            await EnsureNetworkDevicesTableAsync(conn, ct: cancellationToken); // v6

            await EnsureProfileColumnsAsync(conn, logger, cancellationToken);
            await EnsureTemplateConfigColumnsAsync(conn, logger, cancellationToken);

            // v7 — LuxTemplate Engine tables
            await EnsureLuxTemplatesTableAsync(conn, cancellationToken);
            await EnsurePrintJobsTableAsync(conn, cancellationToken);

            // v8 — PrintJob system upgrades
            await EnsurePrintJobColumnsAsync(conn, logger, cancellationToken);
            await EnsureVoucherColumnsAsync(conn, logger, cancellationToken);
            await EnsurePrintJobEventsTableAsync(conn, cancellationToken);

            // v9 — Voucher Import Engine columns (VoucherSource, ImportDate, CreatedBy, Comment)
            if (applied < 9)
            {
                await EnsureVoucherColumnsAsync(conn, logger, cancellationToken);
            }

            // v10 — Voucher Sync V5 Architecture columns (IsDisabled, BytesUsed, UptimeUsedSeconds, MikroTikProfileId)
            if (applied < 10)
            {
                await EnsureProfileColumnsAsync(conn, logger, cancellationToken);
                await EnsureVoucherColumnsAsync(conn, logger, cancellationToken);
            }

            // v11 — Voucher Sync V6 Recycle Bin columns (DeletedDate, DeletedSource)
            if (applied < 11)
            {
                await EnsureVoucherColumnsAsync(conn, logger, cancellationToken);
            }

            // v12 — Add DownloadUsedBytes and UploadUsedBytes columns
            if (applied < 12)
            {
                await EnsureVoucherColumnsAsync(conn, logger, cancellationToken);
            }

            // v13 — Add RouterId to Vouchers, Batches, Profiles and Agents for backward compatibility of existing databases
            if (applied < 13)
            {
                await EnsureVoucherColumnsAsync(conn, logger, cancellationToken);
                await EnsureProfileColumnsAsync(conn, logger, cancellationToken);
                await EnsureBatchColumnsAsync(conn, logger, cancellationToken);
                await EnsureAgentColumnsAsync(conn, logger, cancellationToken);
            }

            // v14 — Add SystemType column to Profiles (was missing from EnsureProfileColumnsAsync,
            //        causing "no such column: p.SystemType" on customer machines with existing databases)
            if (applied < 14)
            {
                await EnsureProfileColumnsAsync(conn, logger, cancellationToken);
            }

            await InsertVersionRowAsync(conn, CurrentSchemaVersion, cancellationToken);
            logger.LogInformation("LuxCard DB schema upgraded to version {Version}", CurrentSchemaVersion);
        }
        finally
        {
            if (ownConnection && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    public static async Task PreMigrateBootstrapAsync(DbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        var conn = db.Database.GetDbConnection();
        var ownConnection = conn.State == ConnectionState.Closed;
        if (ownConnection)
            await conn.OpenAsync(cancellationToken);

        try
        {
            if (await TableExistsAsync(conn, "TemplateConfigs", cancellationToken))
            {
                var cols = await GetColumnNamesForAnyTableAsync(conn, "TemplateConfigs", cancellationToken);
                if (!cols.Contains("RouterId"))
                {
                    logger.LogInformation("TemplateConfigs is missing RouterId. Adding RouterId column manually.");
                    await using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "ALTER TABLE \"TemplateConfigs\" ADD COLUMN \"RouterId\" TEXT NOT NULL DEFAULT '';";
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                    logger.LogInformation("RouterId column added to TemplateConfigs.");
                }
            }

            if (await TableExistsAsync(conn, "Vouchers", cancellationToken))
            {
                var cols = await GetColumnNamesForAnyTableAsync(conn, "Vouchers", cancellationToken);
                if (cols.Contains("BytesUsed"))
                {
                    logger.LogInformation("Voucher usage columns already exist in sqlite. Bootstrapping EFMigrationsHistory to avoid duplicate column errors.");

                    await using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = """
                            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                                "ProductVersion" TEXT NOT NULL
                            );
                            """;
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    var migrationsToSeed = new List<string>
                    {
                        "20260709020311_AddVoucherUsageCounters",
                        "20260709020515_AddProfileMikroTikId",
                        "20260710001524_AddVoucherSoftDeleteMetadata"
                    };

                    foreach (var migrationId in migrationsToSeed)
                    {
                        bool exists = false;
                        await using (var checkCmd = conn.CreateCommand())
                        {
                            checkCmd.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @id;";
                            var p = checkCmd.CreateParameter();
                            p.ParameterName = "@id";
                            p.Value = migrationId;
                            checkCmd.Parameters.Add(p);
                            var count = await checkCmd.ExecuteScalarAsync(cancellationToken);
                            exists = Convert.ToInt32(count ?? 0) > 0;
                        }

                        if (!exists)
                        {
                            await using (var insertCmd = conn.CreateCommand())
                            {
                                insertCmd.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@id, '8.0.0');";
                                var p = insertCmd.CreateParameter();
                                p.ParameterName = "@id";
                                p.Value = migrationId;
                                insertCmd.Parameters.Add(p);
                                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
                            }
                            logger.LogInformation("Marked migration {MigrationId} as applied in __EFMigrationsHistory.", migrationId);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PreMigrateBootstrap failed. EF MigrateAsync might fail if there are schema conflicts.");
        }
        finally
        {
            if (ownConnection && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    private static async Task EnsureSchemaVersionsTableAsync(DbConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS SchemaVersions (
                Version INTEGER PRIMARY KEY NOT NULL,
                AppliedAt TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> GetMaxAppliedVersionAsync(DbConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT IFNULL(MAX(Version), 0) FROM SchemaVersions;";
        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar is long l ? (int)l : Convert.ToInt32(scalar ?? 0);
    }

    private static async Task InsertVersionRowAsync(DbConnection conn, int version, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO SchemaVersions (Version, AppliedAt) VALUES (@v, @dt)";
        var pV = cmd.CreateParameter(); pV.ParameterName = "@v"; pV.Value = version;
        var pDt = cmd.CreateParameter(); pDt.ParameterName = "@dt"; pDt.Value = DateTime.UtcNow.ToString("O");
        cmd.Parameters.Add(pV);
        cmd.Parameters.Add(pDt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsureNetworkDevicesTableAsync(DbConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS NetworkDevices (
                Id TEXT PRIMARY KEY NOT NULL,
                Name TEXT NOT NULL,
                Vendor INTEGER NOT NULL,
                Model TEXT NOT NULL,
                IpAddress TEXT NOT NULL,
                MacAddress TEXT NOT NULL,
                FirmwareVersion TEXT NOT NULL,
                Status INTEGER NOT NULL,
                LastSeen TEXT,
                Username TEXT,
                Password TEXT,
                Metadata TEXT,
                CreatedAt TEXT NOT NULL,
                CreatedBy TEXT,
                UpdatedAt TEXT,
                UpdatedBy TEXT,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                DeletedAt TEXT,
                DeletedBy TEXT,
                RowVersion TEXT
            );
            CREATE INDEX IF NOT EXISTS IX_NetworkDevices_IpAddress ON NetworkDevices (IpAddress);
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsureProfileColumnsAsync(DbConnection conn, ILogger logger, CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "Profiles", ct))
            return;

        var cols = await GetColumnNamesForAnyTableAsync(conn, "Profiles", ct);
        await TryAddColumnAsync(conn, cols, "Profiles", "TemplateId",       "TEXT NULL",                    logger, ct);
        await TryAddColumnAsync(conn, cols, "Profiles", "MikroTikProfileId","TEXT NULL",                    logger, ct);
        await TryAddColumnAsync(conn, cols, "Profiles", "RouterId",         "TEXT NOT NULL DEFAULT ''",     logger, ct);
        // SystemType was added to the domain model but was never added here — this caused
        // "no such column: p.SystemType" on customer machines that had pre-existing databases.
        await TryAddColumnAsync(conn, cols, "Profiles", "SystemType",       "TEXT NULL",                    logger, ct);
    }

    private static async Task EnsureTemplateConfigColumnsAsync(DbConnection conn, ILogger logger, CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "TemplateConfigs", ct))
            return;

        var cols = await GetColumnNamesAsync(conn, "TemplateConfigs", ct);

        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "Kind", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "IsSystemTemplate", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "LegacyRendererKey", "TEXT NULL", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "ThermalPrintableWidthMm", "REAL NULL", logger, ct);

        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "MarginX", "REAL NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "MarginY", "REAL NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "LogoImagePath", "TEXT NULL", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "LinkedProfileName", "TEXT NULL", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "FrameSize", "REAL NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "FrameColorHex", "TEXT NOT NULL DEFAULT '#000000'", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "FontFamily", "TEXT NOT NULL DEFAULT 'Arial'", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "IsBold", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "IsItalic", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "ShowValidity", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "ValidityX", "REAL NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "ValidityY", "REAL NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "ShowTime", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "TimeX", "REAL NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "TimeY", "REAL NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "ShowSerialNumber", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "SerialNumberX", "REAL NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "SerialNumberY", "REAL NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "ShowPrintDate", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "PrintDateX", "REAL NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "PrintDateY", "REAL NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "ShowBarcode", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "BarcodeX", "REAL NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "BarcodeY", "REAL NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "BarcodeSize", "REAL NOT NULL DEFAULT 0", logger, ct);

        // Missing columns that were added later to TemplateConfig entity
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "CardWidth", "REAL NOT NULL DEFAULT 70", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "CardHeight", "REAL NOT NULL DEFAULT 40", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "FontSize", "REAL NOT NULL DEFAULT 10", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "FontColorHex", "TEXT NOT NULL DEFAULT '#000000'", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "ShowUsername", "INTEGER NOT NULL DEFAULT 1", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "UsernameX", "REAL NOT NULL DEFAULT 35", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "UsernameY", "REAL NOT NULL DEFAULT 25", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "ShowPassword", "INTEGER NOT NULL DEFAULT 1", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "PasswordX", "REAL NOT NULL DEFAULT 35", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "PasswordY", "REAL NOT NULL DEFAULT 15", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "ShowPrice", "INTEGER NOT NULL DEFAULT 1", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "PriceX", "REAL NOT NULL DEFAULT 10", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "PriceY", "REAL NOT NULL DEFAULT 20", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "ShowQr", "INTEGER NOT NULL DEFAULT 1", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "QrX", "REAL NOT NULL DEFAULT 50", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "QrY", "REAL NOT NULL DEFAULT 15", logger, ct);
        await TryAddColumnAsync(conn, cols, "TemplateConfigs", "QrSize", "REAL NOT NULL DEFAULT 20", logger, ct);
    }

    private static async Task<bool> TableExistsAsync(DbConnection conn, string table, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@t;";
        var p = cmd.CreateParameter();
        p.ParameterName = "@t";
        p.Value = table;
        cmd.Parameters.Add(p);
        var n = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(n ?? 0) > 0;
    }

    private static async Task<HashSet<string>> GetColumnNamesAsync(DbConnection conn, string table, CancellationToken ct)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = conn.CreateCommand();
        // أسماء الجداول ثابتة من الكود (لا يُمرَّر إدخال المستخدم).
        cmd.CommandText = table switch
        {
            "Profiles" => "PRAGMA table_info('Profiles');",
            "TemplateConfigs" => "PRAGMA table_info('TemplateConfigs');",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            set.Add(reader.GetString(1));
        return set;
    }

    private static async Task TryAddColumnAsync(
        DbConnection conn,
        HashSet<string> cols,
        string table,
        string column,
        string ddlSuffix,
        ILogger logger,
        CancellationToken ct)
    {
        if (cols.Contains(column))
            return;

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {ddlSuffix};";
            await cmd.ExecuteNonQueryAsync(ct);
            cols.Add(column);
            logger.LogDebug("Added column {Table}.{Column}", table, column);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not add column {Table}.{Column}", table, column);
        }
    }

    private static async Task EnsureTemplateConfigsTableAsync(DbConnection conn, CancellationToken ct)
    {
        if (await TableExistsAsync(conn, "TemplateConfigs", ct)) return;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE "TemplateConfigs" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_TemplateConfigs" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "IsDefault" INTEGER NOT NULL,
                "Columns" INTEGER NOT NULL,
                "Rows" INTEGER NOT NULL,
                "BackgroundImagePath" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                "IsDeleted" INTEGER NOT NULL,
                "RowVersion" BLOB NULL,
                "Kind" INTEGER NOT NULL DEFAULT 0,
                "IsSystemTemplate" INTEGER NOT NULL DEFAULT 0,
                "LegacyRendererKey" TEXT NULL,
                "ThermalPrintableWidthMm" REAL NULL,
                "MarginX" REAL NOT NULL DEFAULT 0,
                "MarginY" REAL NOT NULL DEFAULT 0,
                "LogoImagePath" TEXT NULL,
                "LinkedProfileName" TEXT NULL,
                "FrameSize" REAL NOT NULL DEFAULT 0,
                "FrameColorHex" TEXT NOT NULL DEFAULT '#000000',
                "FontFamily" TEXT NOT NULL DEFAULT 'Arial',
                "IsBold" INTEGER NOT NULL DEFAULT 0,
                "IsItalic" INTEGER NOT NULL DEFAULT 0,
                "ShowValidity" INTEGER NOT NULL DEFAULT 0,
                "ValidityX" REAL NOT NULL DEFAULT 0,
                "ValidityY" REAL NOT NULL DEFAULT 0,
                "ShowTime" INTEGER NOT NULL DEFAULT 0,
                "TimeX" REAL NOT NULL DEFAULT 0,
                "TimeY" REAL NOT NULL DEFAULT 0,
                "ShowSerialNumber" INTEGER NOT NULL DEFAULT 0,
                "SerialNumberX" REAL NOT NULL DEFAULT 0,
                "SerialNumberY" REAL NOT NULL DEFAULT 0,
                "ShowPrintDate" INTEGER NOT NULL DEFAULT 0,
                "PrintDateX" REAL NOT NULL DEFAULT 0,
                "PrintDateY" REAL NOT NULL DEFAULT 0,
                "ShowBarcode" INTEGER NOT NULL DEFAULT 0,
                "BarcodeX" REAL NOT NULL DEFAULT 0,
                "BarcodeY" REAL NOT NULL DEFAULT 0,
                "BarcodeSize" REAL NOT NULL DEFAULT 0,
                "CardWidth" REAL NOT NULL DEFAULT 70,
                "CardHeight" REAL NOT NULL DEFAULT 40,
                "FontSize" REAL NOT NULL DEFAULT 10,
                "FontColorHex" TEXT NOT NULL DEFAULT '#000000',
                "ShowUsername" INTEGER NOT NULL DEFAULT 1,
                "UsernameX" REAL NOT NULL DEFAULT 35,
                "UsernameY" REAL NOT NULL DEFAULT 25,
                "ShowPassword" INTEGER NOT NULL DEFAULT 1,
                "PasswordX" REAL NOT NULL DEFAULT 35,
                "PasswordY" REAL NOT NULL DEFAULT 15,
                "ShowPrice" INTEGER NOT NULL DEFAULT 1,
                "PriceX" REAL NOT NULL DEFAULT 10,
                "PriceY" REAL NOT NULL DEFAULT 20,
                "ShowQr" INTEGER NOT NULL DEFAULT 1,
                "QrX" REAL NOT NULL DEFAULT 50,
                "QrY" REAL NOT NULL DEFAULT 15,
                "QrSize" REAL NOT NULL DEFAULT 20
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsureAgentsTableAsync(DbConnection conn, CancellationToken ct)
    {
        if (await TableExistsAsync(conn, "Agents", ct)) return;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE "Agents" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Agents" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "Phone" TEXT NOT NULL,
                "Notes" TEXT NULL,
                "CommissionRate" REAL NOT NULL,
                "Balance" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                "IsDeleted" INTEGER NOT NULL,
                "RowVersion" BLOB NULL,
                "RouterId" TEXT NOT NULL DEFAULT ''
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);
        
        // Also ensure AgentId exists on Vouchers table since they are linked
        if (await TableExistsAsync(conn, "Vouchers", ct))
        {
            var cols = await GetColumnNamesForAnyTableAsync(conn, "Vouchers", ct);
            if (!cols.Contains("AgentId"))
            {
                await using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE \"Vouchers\" ADD COLUMN \"AgentId\" TEXT NULL;";
                await alterCmd.ExecuteNonQueryAsync(ct);
            }
        }
    }

    private static async Task EnsureAgentColumnsAsync(DbConnection conn, ILogger logger, CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "Agents", ct))
            return;

        var cols = await GetColumnNamesForAnyTableAsync(conn, "Agents", ct);
        await TryAddColumnAsync(conn, cols, "Agents", "RouterId", "TEXT NOT NULL DEFAULT ''", logger, ct);
    }

    private static async Task EnsureBatchColumnsAsync(DbConnection conn, ILogger logger, CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "Batches", ct))
            return;

        var cols = await GetColumnNamesForAnyTableAsync(conn, "Batches", ct);
        await TryAddColumnAsync(conn, cols, "Batches", "RouterId", "TEXT NOT NULL DEFAULT ''", logger, ct);
    }

    private static async Task<HashSet<string>> GetColumnNamesForAnyTableAsync(DbConnection conn, string table, CancellationToken ct)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{table}');";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            set.Add(reader.GetString(1));
        return set;
    }
    private static async Task EnsureLuxTemplatesTableAsync(DbConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "LuxTemplates" (
                "Id"                  TEXT NOT NULL CONSTRAINT "PK_LuxTemplates" PRIMARY KEY,
                "Name"               TEXT NOT NULL,
                "Description"        TEXT,
                "Category"           INTEGER NOT NULL DEFAULT 0,
                "OutputType"         INTEGER NOT NULL DEFAULT 0,
                "Orientation"        INTEGER NOT NULL DEFAULT 0,
                "PageWidthMm"        REAL    NOT NULL DEFAULT 210.0,
                "PageHeightMm"       REAL    NOT NULL DEFAULT 297.0,
                "CardsPerRow"        INTEGER NOT NULL DEFAULT 3,
                "CardsPerColumn"     INTEGER NOT NULL DEFAULT 7,
                "CardWidthMm"        REAL    NOT NULL DEFAULT 63.0,
                "CardHeightMm"       REAL    NOT NULL DEFAULT 38.0,
                "HorizontalGapMm"    REAL    NOT NULL DEFAULT 0.0,
                "VerticalGapMm"      REAL    NOT NULL DEFAULT 0.0,
                "MarginTopMm"        REAL    NOT NULL DEFAULT 5.0,
                "MarginBottomMm"     REAL    NOT NULL DEFAULT 5.0,
                "MarginLeftMm"       REAL    NOT NULL DEFAULT 5.0,
                "MarginRightMm"      REAL    NOT NULL DEFAULT 5.0,
                "BackgroundType"     INTEGER NOT NULL DEFAULT 0,
                "BackgroundColorHex" TEXT,
                "BackgroundImagePath" TEXT,
                "ElementsJson"       TEXT    NOT NULL DEFAULT '[]',
                "LinkedProfileName"  TEXT,
                "Version"            INTEGER NOT NULL DEFAULT 1,
                "IsSystemTemplate"   INTEGER NOT NULL DEFAULT 0,
                "IsDefault"          INTEGER NOT NULL DEFAULT 0,
                "IsDeleted"          INTEGER NOT NULL DEFAULT 0,
                "RouterId"           TEXT    NOT NULL DEFAULT '',
                "CreatedAt"          TEXT    NOT NULL DEFAULT '',
                "UpdatedAt"          TEXT,
                "RowVersion"         BLOB
            );
            CREATE INDEX IF NOT EXISTS "IX_LuxTemplates_RouterId" ON "LuxTemplates" ("RouterId");
            CREATE INDEX IF NOT EXISTS "IX_LuxTemplates_Category" ON "LuxTemplates" ("Category");
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsurePrintJobsTableAsync(DbConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "PrintJobs" (
                "Id"             TEXT    NOT NULL CONSTRAINT "PK_PrintJobs" PRIMARY KEY,
                "TemplateId"     TEXT    NOT NULL,
                "PrintedAt"      TEXT    NOT NULL,
                "CardCount"      INTEGER NOT NULL DEFAULT 0,
                "BatchId"        TEXT,
                "OutputFormat"   INTEGER NOT NULL DEFAULT 0,
                "Status"         INTEGER NOT NULL DEFAULT 0,
                "OutputFilePath" TEXT,
                "ErrorMessage"   TEXT,
                "RouterId"       TEXT    NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS "IX_PrintJobs_TemplateId" ON "PrintJobs" ("TemplateId");
            CREATE INDEX IF NOT EXISTS "IX_PrintJobs_RouterId"   ON "PrintJobs" ("RouterId");
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsurePrintJobColumnsAsync(DbConnection conn, ILogger logger, CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "PrintJobs", ct))
            return;

        var cols = await GetColumnNamesForAnyTableAsync(conn, "PrintJobs", ct);

        await TryAddColumnAsync(conn, cols, "PrintJobs", "JobParametersJson", "TEXT NULL", logger, ct);
        await TryAddColumnAsync(conn, cols, "PrintJobs", "CurrentStep", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "PrintJobs", "ReservedCount", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "PrintJobs", "SyncedCount", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "PrintJobs", "PdfCount", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "PrintJobs", "PrintedCount", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "PrintJobs", "JobVersion", "INTEGER NOT NULL DEFAULT 1", logger, ct);
        await TryAddColumnAsync(conn, cols, "PrintJobs", "TemplateVersion", "INTEGER NOT NULL DEFAULT 1", logger, ct);
    }

    private static async Task EnsureVoucherColumnsAsync(DbConnection conn, ILogger logger, CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "Vouchers", ct))
            return;

        var cols = await GetColumnNamesForAnyTableAsync(conn, "Vouchers", ct);

        await TryAddColumnAsync(conn, cols, "Vouchers", "PrintStatus", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "Vouchers", "VoucherSource", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "Vouchers", "ImportDate", "TEXT NULL", logger, ct);
        await TryAddColumnAsync(conn, cols, "Vouchers", "CreatedBy", "TEXT NULL", logger, ct);
        await TryAddColumnAsync(conn, cols, "Vouchers", "Comment", "TEXT NULL", logger, ct);
        await TryAddColumnAsync(conn, cols, "Vouchers", "IsDisabled", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "Vouchers", "BytesUsed", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "Vouchers", "UptimeUsedSeconds", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "Vouchers", "DeletedDate", "TEXT NULL", logger, ct);
        await TryAddColumnAsync(conn, cols, "Vouchers", "DeletedSource", "INTEGER NULL", logger, ct);
        await TryAddColumnAsync(conn, cols, "Vouchers", "DownloadUsedBytes", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "Vouchers", "UploadUsedBytes", "INTEGER NOT NULL DEFAULT 0", logger, ct);
        await TryAddColumnAsync(conn, cols, "Vouchers", "RouterId", "TEXT NOT NULL DEFAULT ''", logger, ct);
    }

    private static async Task EnsurePrintJobEventsTableAsync(DbConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "PrintJobEvents" (
                "Id"         TEXT NOT NULL CONSTRAINT "PK_PrintJobEvents" PRIMARY KEY,
                "JobId"      TEXT NOT NULL,
                "Timestamp"  TEXT NOT NULL,
                "Level"      TEXT NOT NULL,
                "Message"    TEXT NOT NULL,
                "Details"    TEXT NULL,
                "IsDeleted"  INTEGER NOT NULL DEFAULT 0,
                "CreatedAt"  TEXT NOT NULL DEFAULT '',
                "UpdatedAt"  TEXT NULL,
                "RowVersion" BLOB NULL,
                "RouterId"   TEXT NOT NULL DEFAULT '',
                CONSTRAINT "FK_PrintJobEvents_PrintJobs_JobId" FOREIGN KEY ("JobId") REFERENCES "PrintJobs" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_PrintJobEvents_JobId" ON "PrintJobEvents" ("JobId");
            CREATE INDEX IF NOT EXISTS "IX_PrintJobEvents_RouterId" ON "PrintJobEvents" ("RouterId");
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
