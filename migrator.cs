using System;
using Microsoft.Data.Sqlite;
using System.IO;

class Program
{
    static void Main()
    {
        string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LuxCard", "luxcard.db");
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = "CREATE TABLE IF NOT EXISTS TemplateConfigs (Id TEXT PRIMARY KEY, Name TEXT, IsDefault INTEGER, Columns INTEGER, Rows INTEGER, CardWidth REAL, CardHeight REAL, BackgroundImagePath TEXT, ShowUsername INTEGER, UsernameX REAL, UsernameY REAL, ShowPassword INTEGER, PasswordX REAL, PasswordY REAL, ShowPrice INTEGER, PriceX REAL, PriceY REAL, ShowQr INTEGER, QrX REAL, QrY REAL, QrSize REAL, FontSize REAL, FontColorHex TEXT, CreatedAt TEXT, UpdatedAt TEXT, IsDeleted INTEGER, RowVersion BLOB);";
        cmd1.ExecuteNonQuery();

        try
        {
            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = "ALTER TABLE Profiles ADD COLUMN TemplateId TEXT;";
            cmd2.ExecuteNonQuery();
        }
        catch { } // Ignore if column already exists

        Console.WriteLine("Done");
    }
}
