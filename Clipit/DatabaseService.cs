using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace Clipit
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        public string ImagesFolder { get; }

        public DatabaseService()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Clipit");
            Directory.CreateDirectory(folder);

            ImagesFolder = Path.Combine(folder, "Images");
            Directory.CreateDirectory(ImagesFolder);

            string dbPath = Path.Combine(folder, "clipit.db");
            _connectionString = $"Data Source={dbPath}";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ClipboardItems (
                    Id TEXT PRIMARY KEY,
                    Content TEXT NOT NULL,
                    ImagePath TEXT,
                    ContentHash TEXT,
                    CopiedAt TEXT NOT NULL,
                    IsPinned INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );
            ";
            cmd.ExecuteNonQuery();

            // 기존 DB 호환을 위한 컬럼 자동 추가
            TryAddColumn(conn, "ALTER TABLE ClipboardItems ADD COLUMN ImagePath TEXT;");
            TryAddColumn(conn, "ALTER TABLE ClipboardItems ADD COLUMN ContentHash TEXT;");
        }

        private void TryAddColumn(SqliteConnection conn, string sql)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public List<ClipboardItem> LoadAll()
        {
            var list = new List<ClipboardItem>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Content, ImagePath, ContentHash, CopiedAt, IsPinned
                FROM ClipboardItems
                ORDER BY IsPinned DESC, CopiedAt DESC;
            ";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ClipboardItem
                {
                    Id = reader.GetString(0),
                    Content = reader.GetString(1),
                    ImagePath = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ContentHash = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CopiedAt = DateTime.Parse(reader.GetString(4)),
                    IsPinned = reader.GetInt32(5) == 1
                });
            }
            return list;
        }

        public void Upsert(ClipboardItem item)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ClipboardItems (Id, Content, ImagePath, ContentHash, CopiedAt, IsPinned)
                VALUES ($id, $content, $imagePath, $contentHash, $copiedAt, $isPinned)
                ON CONFLICT(Id) DO UPDATE SET
                    Content = excluded.Content,
                    ImagePath = excluded.ImagePath,
                    ContentHash = excluded.ContentHash,
                    CopiedAt = excluded.CopiedAt,
                    IsPinned = excluded.IsPinned;
            ";
            cmd.Parameters.AddWithValue("$id", item.Id);
            cmd.Parameters.AddWithValue("$content", item.Content);
            cmd.Parameters.AddWithValue("$imagePath", (object?)item.ImagePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$contentHash", (object?)item.ContentHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$copiedAt", item.CopiedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$isPinned", item.IsPinned ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        public void Delete(ClipboardItem item)
        {
            if (item.IsImage && !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
            {
                try { File.Delete(item.ImagePath); } catch { }
            }

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ClipboardItems WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", item.Id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteAllUnpinned(IEnumerable<ClipboardItem> unpinnedItems)
        {
            foreach (var item in unpinnedItems)
            {
                if (item.IsImage && !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
                {
                    try { File.Delete(item.ImagePath); } catch { }
                }
            }

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ClipboardItems WHERE IsPinned = 0;";
            cmd.ExecuteNonQuery();
        }

        public string GetSetting(string key, string defaultValue)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar()?.ToString() ?? defaultValue;
        }

        public void SaveSetting(string key, string value)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Settings (Key, Value) VALUES ($key, $value)
                ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            ";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.ExecuteNonQuery();
        }
    }
}