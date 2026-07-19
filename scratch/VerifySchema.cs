using System;
using Microsoft.Data.Sqlite;
using System.IO;

class Program
{
    static void Main()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbPath = Path.Combine(localAppData, "LuxCard", "luxcard.db");

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        var tables = new[] { "Vouchers", "Batches", "Profiles", "Agents", "TemplateConfigs" };

        foreach (var table in tables)
        {
            Console.WriteLine($"\n--- {table} Schema ---");
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({table});";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(1);
                var type = reader.GetString(2);
                var notNull = reader.GetInt32(3) == 1;
                
                if (name == "RouterId")
                {
                    Console.WriteLine($"Column: {name} | Type: {type} | Required (NOT NULL): {notNull}");
                }
            }

            using var indexCommand = connection.CreateCommand();
            indexCommand.CommandText = $"PRAGMA index_list({table});";
            using var indexReader = indexCommand.ExecuteReader();
            while (indexReader.Read())
            {
                var indexName = indexReader.GetString(1);
                if (indexName.Contains("RouterId"))
                {
                    Console.WriteLine($"Index Found: {indexName}");
                }
            }
        }
    }
}
