using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using CompressedVideoService.entity;

namespace CompressedVideoService.repository
{
    internal class VideoRepository
    {
        private readonly string _dbConnectionString;

        public VideoRepository(string dbConnectionString)
        {
            _dbConnectionString = dbConnectionString;
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (var conn = new SQLiteConnection(_dbConnectionString))
            {
                conn.Open();
                const string createTableQuery =
                    @"CREATE TABLE IF NOT EXISTS VideoFiles (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT NOT NULL UNIQUE,
                    Hash TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    CompressedDate DATETIME,
                    ArchivedDate DATETIME,
                    OutputPath TEXT,
                    ErrorMessage TEXT,
                    LastModified DATETIME NOT NULL,
                    Version INTEGER NOT NULL DEFAULT 1
                )";
                new SQLiteCommand(createTableQuery, conn).ExecuteNonQuery();
            }
        }

        //private VideoFileEntity MapReaderToEntity(SQLiteDataReader reader)
        //{
        //    return new VideoFileEntity
        //    {
        //        Id = (int)reader["Id"],
        //        FilePath = (string)reader["FilePath"],
        //        Hash = (string)reader["Hash"],
        //        Status = (int)reader["Status"],
        //        CompressedDate = reader["CompressedDate"] as DateTime?,
        //        ArchivedDate = reader["ArchivedDate"] as DateTime?,
        //        OutputPath = reader["OutputPath"] as string,
        //        ErrorMessage = reader["ErrorMessage"] as string,
        //        LastModified = (DateTime)reader["LastModified"],
        //        Version = (int)reader["Version"]
        //    };
        //}

        private VideoFileEntity MapReaderToEntity(SQLiteDataReader reader)
        {
            // 预先获取所有列索引（提升性能）
            int idOrdinal = reader.GetOrdinal("Id");
            int filePathOrdinal = reader.GetOrdinal("FilePath");
            int hashOrdinal = reader.GetOrdinal("Hash");
            int statusOrdinal = reader.GetOrdinal("Status");
            int compressedDateOrdinal = reader.GetOrdinal("CompressedDate");
            int archivedDateOrdinal = reader.GetOrdinal("ArchivedDate");
            int outputPathOrdinal = reader.GetOrdinal("OutputPath");
            int errorMessageOrdinal = reader.GetOrdinal("ErrorMessage");
            int lastModifiedOrdinal = reader.GetOrdinal("LastModified");
            int versionOrdinal = reader.GetOrdinal("Version");

            return new VideoFileEntity
            {
                Id = reader.GetInt32(idOrdinal),
                FilePath = reader.GetString(filePathOrdinal),
                Hash = reader.GetString(hashOrdinal),
                Status = reader.GetInt32(statusOrdinal),
                // 显式转换 null 为可空类型
                CompressedDate = reader.IsDBNull(compressedDateOrdinal)
                    ? (DateTime?)null
                    : reader.GetDateTime(compressedDateOrdinal),
                ArchivedDate = reader.IsDBNull(archivedDateOrdinal)
                    ? (DateTime?)null
                    : reader.GetDateTime(archivedDateOrdinal),
                // 字符串类型无需转换（引用类型天然支持 null）
                OutputPath = reader.IsDBNull(outputPathOrdinal)
                    ? null
                    : reader.GetString(outputPathOrdinal),
                ErrorMessage = reader.IsDBNull(errorMessageOrdinal)
                    ? null
                    : reader.GetString(errorMessageOrdinal),
                LastModified = reader.GetDateTime(lastModifiedOrdinal),
                Version = reader.GetInt32(versionOrdinal)
            };
        }


        public int AddVideo(VideoFileEntity entity)
        {
            using (var conn = new SQLiteConnection(_dbConnectionString))
            {
                conn.Open();
                const string query =
                    @"INSERT INTO VideoFiles
                (FilePath, Hash, Status, CompressedDate, ArchivedDate, 
                 OutputPath, ErrorMessage, LastModified)
              VALUES 
                (@FilePath, @Hash, @Status, @CompressedDate, @ArchivedDate, 
                 @OutputPath, @ErrorMessage, @LastModified);
              SELECT last_insert_rowid();";

                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FilePath", entity.FilePath);
                    cmd.Parameters.AddWithValue("@Hash", entity.Hash);
                    cmd.Parameters.AddWithValue("@Status", (int)entity.Status);
                    cmd.Parameters.AddWithValue("@CompressedDate", entity.CompressedDate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ArchivedDate", entity.ArchivedDate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@OutputPath", entity.OutputPath ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ErrorMessage", entity.ErrorMessage ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LastModified", DateTime.Now);

                    // 执行并处理返回值
                    object result = cmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                    {
                        throw new SQLiteException("插入记录失败，未返回有效ID");
                    }

                    var newId = Convert.ToInt32(result);

                    // 初始化版本号为 1 (根据数据库 DEFAULT 值)
                    entity.Id = newId;
                    entity.Version = 1;

                    return newId;
                }
            }
        }
        public void DeleteVideo(VideoFileEntity videoFileEntity)
        {
            using (var conn = new SQLiteConnection(_dbConnectionString))
            {
                conn.Open();
                const string query =
                    @"DELETE FROM VideoFiles 
            WHERE Id = @Id AND Version = @Version;
            SELECT changes() AS RowsAffected;";

                using (var cmd = new SQLiteCommand(query, conn))
                {
                    // 主键参数
                    cmd.Parameters.AddWithValue("@Id", videoFileEntity.Id);

                    // 乐观锁参数（根据版本号）
                    cmd.Parameters.AddWithValue("@Version", videoFileEntity.Version);

                    // 执行并验证影响行数
                    int rowsAffected = Convert.ToInt32(cmd.ExecuteScalar());

                    if (rowsAffected == 0)
                    {
                        throw new DBConcurrencyException(
                            $"删除失败：视频ID {videoFileEntity.Id} 不存在或版本号 {videoFileEntity.Version} 已过期");
                    }
                }
            }
        }


        public void UpdateVideo(VideoFileEntity entity)
        {
            using (var conn = new SQLiteConnection(_dbConnectionString))
            {
                conn.Open();
                const string query =
                    @"UPDATE VideoFiles SET
                FilePath = @FilePath,
                Hash = @Hash,
                Status = @Status,
                CompressedDate = @CompressedDate,
                ArchivedDate = @ArchivedDate,
                OutputPath = @OutputPath,
                ErrorMessage = @ErrorMessage,
                LastModified = @LastModified,
                Version = Version + 1
            WHERE Id = @Id
                AND Version = @CurrentVersion";

                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", entity.Id);
                    cmd.Parameters.AddWithValue("@FilePath", entity.FilePath);
                    cmd.Parameters.AddWithValue("@Hash", entity.Hash);
                    cmd.Parameters.AddWithValue("@Status", (int)entity.Status);
                    cmd.Parameters.AddWithValue("@CompressedDate", entity.CompressedDate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ArchivedDate", entity.ArchivedDate ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@OutputPath", entity.OutputPath ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ErrorMessage", entity.ErrorMessage ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LastModified", DateTime.Now);
                    cmd.Parameters.AddWithValue("@CurrentVersion", entity.Version);  // 使用当前版本号作为条件

                    int affectedRows = cmd.ExecuteNonQuery();
                    if (affectedRows == 0)
                    {
                        throw new SQLiteException("更新失败：数据版本冲突或记录不存在");
                    }
                    entity.Version++;
                }
            }
        }

        public void UpdateVideoByFilePath(VideoFileEntity entity)
        {
            using (var conn = new SQLiteConnection(_dbConnectionString))
            {
                conn.Open();
                const string updateQuery = @"
            UPDATE VideoFiles
            SET
                Hash = @Hash,
                Status = @Status,
                CompressedDate = @CompressedDate,
                ArchivedDate = @ArchivedDate,
                OutputPath = @OutputPath,
                ErrorMessage = @ErrorMessage,
                LastModified = @LastModified,
                Version = Version + 1
            WHERE FilePath = @FilePath
                AND Version = @CurrentVersion;";

                using (var cmd = new SQLiteCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Hash", entity.Hash);
                    cmd.Parameters.AddWithValue("@Status", (int)entity.Status);
                    cmd.Parameters.AddWithValue("@CompressedDate", entity.CompressedDate.HasValue ? (object)entity.CompressedDate.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ArchivedDate", entity.ArchivedDate.HasValue ? (object)entity.ArchivedDate.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@OutputPath", !string.IsNullOrEmpty(entity.OutputPath) ? (object)entity.OutputPath : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ErrorMessage", !string.IsNullOrEmpty(entity.ErrorMessage) ? (object)entity.ErrorMessage : DBNull.Value);
                    cmd.Parameters.AddWithValue("@LastModified", DateTime.Now);
                    cmd.Parameters.AddWithValue("@FilePath", entity.FilePath);
                    cmd.Parameters.AddWithValue("@CurrentVersion", entity.Version);

                    int affectedRows = cmd.ExecuteNonQuery();
                    if (affectedRows == 0)
                    {
                        throw new SQLiteException("No rows updated. The record may have been modified or deleted by another process.");
                    }
                    entity.Version++;
                }
            }
        }

        public int UpdateStatus(VideoFileEntity entity, int status, string errorMessage = null)
        {
            using (var conn = new SQLiteConnection(_dbConnectionString))
            {
                conn.Open();
                const string query = @"UPDATE VideoFiles SET
                                Status = @Status,
                                ErrorMessage = @ErrorMessage,
                                LastModified = @LastModified,
                                Version = Version + 1
                              WHERE Id = @Id 
                                AND Version = @CurrentVersion";  // 版本检查

                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", entity.Id);
                    cmd.Parameters.AddWithValue("@Status", status);
                    cmd.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@LastModified", DateTime.Now);  // 更新时间戳
                    cmd.Parameters.AddWithValue("@CurrentVersion", entity.Version);  // 当前版本号

                    int affectedRows = cmd.ExecuteNonQuery();
                    if (affectedRows == 0)
                    {
                        throw new SQLiteException($"更新失败：ID为 {entity.Id} 的记录版本冲突或不存在");
                    }
                    entity.Version++;
                    return affectedRows;
                }
            }
        }


        public IEnumerable<VideoFileEntity> GetPendingCompression()
        {
            var results = new List<VideoFileEntity>();
            using (var conn = new SQLiteConnection(_dbConnectionString))
            {
                conn.Open();
                const string query = "SELECT * FROM VideoFiles WHERE Status = 0 ORDER BY Id";

                using (var cmd = new SQLiteCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(MapReaderToEntity(reader));
                    }
                }
            }
            return results;
        }

        public VideoFileEntity FindByFilePath(string filePath)
        {
            using (var conn = new SQLiteConnection(_dbConnectionString))
            {
                conn.Open();
                const string query = "SELECT * FROM VideoFiles WHERE FilePath = @FilePath";

                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FilePath", filePath);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return MapReaderToEntity(reader);
                        }
                    }
                }
            }
            return null;
        }

        public IEnumerable<VideoFileEntity> GetAll()
        {
            var results = new List<VideoFileEntity>();
            using (var conn = new SQLiteConnection(_dbConnectionString))
            {
                conn.Open();
                const string query = "SELECT * FROM VideoFiles ORDER BY Id";

                using (var cmd = new SQLiteCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(MapReaderToEntity(reader));
                    }
                }
            }
            return results;
        }
    }
}
