using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbPath = Path.Combine(localAppData, "Lux Platform", "platform.db");
        var tempPath = Path.Combine(Path.GetTempPath(), "sqldb_43175f7d-366b-4227-9ae5-5e46d392743a.clean");

        using var platformConn = new SqliteConnection($"Data Source={dbPath}");
        platformConn.Open();

        using var snapConn = new SqliteConnection($"Data Source={tempPath}");
        snapConn.Open();

        var snapUsers = new List<string>();
        using (var cmd = snapConn.CreateCommand())
        {
            cmd.CommandText = "SELECT userName FROM [user]";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                snapUsers.Add(reader.IsDBNull(0) ? "" : reader.GetString(0));
            }
        }

        var dbUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = platformConn.CreateCommand())
        {
            cmd.CommandText = "SELECT Username FROM Vouchers";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                dbUsers.Add(reader.GetString(0));
            }
        }

        foreach (var u in snapUsers)
        {
            if (dbUsers.Contains(u))
            {
                Console.WriteLine($"Colliding Username found: '{u}'");
            }
        }
    }
}
