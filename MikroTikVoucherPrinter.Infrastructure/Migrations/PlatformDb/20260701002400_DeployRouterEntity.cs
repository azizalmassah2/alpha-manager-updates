using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MikroTikVoucherPrinter.Infrastructure.Migrations.PlatformDb
{
    /// <inheritdoc />
    public partial class DeployRouterEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SyncQueue_Devices_DeviceId",
                table: "SyncQueue");

            migrationBuilder.DropTable(
                name: "ConnectionCredentials");

            migrationBuilder.DropTable(
                name: "ConnectionProfiles");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.RenameColumn(
                name: "DeviceId",
                table: "SyncQueue",
                newName: "RouterId");

            migrationBuilder.RenameIndex(
                name: "IX_SyncQueue_DeviceId",
                table: "SyncQueue",
                newName: "IX_SyncQueue_RouterId");

            migrationBuilder.CreateTable(
                name: "OperationAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RouterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    OperationType = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetRole = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetRouterIds = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Progress = table.Column<double>(type: "REAL", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResultMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Routers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    EncryptedPassword = table.Column<string>(type: "TEXT", nullable: false),
                    EncryptedCredentialsReference = table.Column<string>(type: "TEXT", nullable: true),
                    RouterIdentity = table.Column<string>(type: "TEXT", nullable: true),
                    RouterBoard = table.Column<string>(type: "TEXT", nullable: true),
                    RouterOSVersion = table.Column<string>(type: "TEXT", nullable: true),
                    SoftwareId = table.Column<string>(type: "TEXT", nullable: true),
                    SerialNumber = table.Column<string>(type: "TEXT", nullable: true),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: true),
                    LastConnectedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RouterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    RuleName = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertCandidates_Routers_RouterId",
                        column: x => x.RouterId,
                        principalTable: "Routers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceTelemetry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RouterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CpuUsage = table.Column<double>(type: "REAL", nullable: false),
                    MemoryUsed = table.Column<long>(type: "INTEGER", nullable: false),
                    MemoryTotal = table.Column<long>(type: "INTEGER", nullable: false),
                    Uptime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Temperature = table.Column<double>(type: "REAL", nullable: true),
                    HealthStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTelemetry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceTelemetry_Routers_RouterId",
                        column: x => x.RouterId,
                        principalTable: "Routers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterfaceTelemetry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RouterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InterfaceName = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RxBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    TxBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    RxPackets = table.Column<long>(type: "INTEGER", nullable: false),
                    TxPackets = table.Column<long>(type: "INTEGER", nullable: false),
                    Running = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterfaceTelemetry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterfaceTelemetry_Routers_RouterId",
                        column: x => x.RouterId,
                        principalTable: "Routers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertCandidates_RouterId_Timestamp",
                table: "AlertCandidates",
                columns: new[] { "RouterId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTelemetry_RouterId_Timestamp",
                table: "DeviceTelemetry",
                columns: new[] { "RouterId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_InterfaceTelemetry_RouterId_Timestamp",
                table: "InterfaceTelemetry",
                columns: new[] { "RouterId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Routers_MacAddress",
                table: "Routers",
                column: "MacAddress",
                unique: true,
                filter: "\"MacAddress\" IS NOT NULL AND \"MacAddress\" != ''");

            migrationBuilder.CreateIndex(
                name: "IX_Routers_SerialNumber",
                table: "Routers",
                column: "SerialNumber",
                unique: true,
                filter: "\"SerialNumber\" IS NOT NULL AND \"SerialNumber\" != ''");

            migrationBuilder.CreateIndex(
                name: "IX_Routers_SoftwareId",
                table: "Routers",
                column: "SoftwareId",
                unique: true,
                filter: "\"SoftwareId\" IS NOT NULL AND \"SoftwareId\" != ''");

            migrationBuilder.AddForeignKey(
                name: "FK_SyncQueue_Routers_RouterId",
                table: "SyncQueue",
                column: "RouterId",
                principalTable: "Routers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.InsertData(
                table: "Routers",
                columns: new[] { "Id", "DisplayName", "Host", "Port", "Username", "EncryptedPassword", "CreatedAt", "IsDeleted", "RowVersion", "IsFavorite" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), "Legacy Router", "192.168.88.1", 8728, "admin", "", DateTime.UtcNow, false, new byte[0], false }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SyncQueue_Routers_RouterId",
                table: "SyncQueue");

            migrationBuilder.DropTable(
                name: "AlertCandidates");

            migrationBuilder.DropTable(
                name: "DeviceTelemetry");

            migrationBuilder.DropTable(
                name: "InterfaceTelemetry");

            migrationBuilder.DropTable(
                name: "OperationAuditRecords");

            migrationBuilder.DropTable(
                name: "OperationJobs");

            migrationBuilder.DropTable(
                name: "Routers");

            migrationBuilder.RenameColumn(
                name: "RouterId",
                table: "SyncQueue",
                newName: "DeviceId");

            migrationBuilder.RenameIndex(
                name: "IX_SyncQueue_RouterId",
                table: "SyncQueue",
                newName: "IX_SyncQueue_DeviceId");

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CurrentConnectionProfileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DeviceType = table.Column<string>(type: "TEXT", nullable: false),
                    Identity = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MacAddress = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SerialNumber = table.Column<string>(type: "TEXT", nullable: true),
                    SoftwareId = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConnectionProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApiPort = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfileName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Protocol = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SslPort = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UseSsl = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectionProfiles_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConnectionCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectionProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EncryptedPassword = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectionCredentials_ConnectionProfiles_ConnectionProfileId",
                        column: x => x.ConnectionProfileId,
                        principalTable: "ConnectionProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionCredentials_ConnectionProfileId",
                table: "ConnectionCredentials",
                column: "ConnectionProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionProfiles_DeviceId",
                table: "ConnectionProfiles",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_CurrentConnectionProfileId",
                table: "Devices",
                column: "CurrentConnectionProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_MacAddress",
                table: "Devices",
                column: "MacAddress",
                unique: true,
                filter: "\"MacAddress\" IS NOT NULL AND \"MacAddress\" != ''");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ProjectId",
                table: "Devices",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices",
                column: "SerialNumber",
                unique: true,
                filter: "\"SerialNumber\" IS NOT NULL AND \"SerialNumber\" != ''");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SoftwareId",
                table: "Devices",
                column: "SoftwareId",
                unique: true,
                filter: "\"SoftwareId\" IS NOT NULL AND \"SoftwareId\" != ''");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SyncQueue_Devices_DeviceId",
                table: "SyncQueue",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
