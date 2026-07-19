using System;
using System.IO;
using Microsoft.Data.Sqlite;

var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var dbPath = Path.Combine(appData, "Lux Platform", "platform.db");

using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();
using var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT Username, MikroTikUserId, SyncStatus, IsDeleted, DeletedSource FROM Vouchers WHERE Username = 'test_del_7';";
using var reader = cmd.ExecuteReader();
if (reader.Read())
{
    Console.WriteLine($"Voucher: {reader.GetString(0)} | MikroTikUserId: {reader.GetValue(1)} | SyncStatus: {reader.GetInt32(2)} | IsDeleted: {reader.GetBoolean(3)} | DeletedSource: {reader.GetValue(4)}");
}
else
{
    Console.WriteLine("Voucher test_del_7 not found.");
}
