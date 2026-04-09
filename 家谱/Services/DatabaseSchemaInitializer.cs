using Microsoft.EntityFrameworkCore;
using 家谱.DB;

namespace 家谱.Services
{
    public interface IDatabaseSchemaInitializer
    {
        Task EnsureAsync();
    }

    public class DatabaseSchemaInitializer : IDatabaseSchemaInitializer
    {
        private readonly GenealogyDbContext _db;

        public DatabaseSchemaInitializer(GenealogyDbContext db)
        {
            _db = db;
        }

        public async Task EnsureAsync()
        {
            var sql = """
                IF OBJECT_ID(N'dbo.Geno_Tree_Permissions', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Geno_Tree_Permissions](
                        [PermissionID] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        [TreeID] UNIQUEIDENTIFIER NOT NULL,
                        [UserID] UNIQUEIDENTIFIER NOT NULL,
                        [RoleType] TINYINT NOT NULL,
                        [GrantedBy] UNIQUEIDENTIFIER NULL,
                        [GrantedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Tree_Permissions_GrantedAt DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Tree_Permissions_UpdatedAt DEFAULT SYSUTCDATETIME(),
                        [IsActive] BIT NOT NULL CONSTRAINT DF_Geno_Tree_Permissions_IsActive DEFAULT 1
                    );
                    CREATE UNIQUE INDEX IX_Geno_Tree_Permissions_TreeID_UserID ON [dbo].[Geno_Tree_Permissions]([TreeID], [UserID]);
                    CREATE INDEX IX_Geno_Tree_Permissions_UserID ON [dbo].[Geno_Tree_Permissions]([UserID]);
                END;

                IF OBJECT_ID(N'dbo.Sys_Data_Logs', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Sys_Data_Logs](
                        [LogID] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        [TaskID] UNIQUEIDENTIFIER NULL,
                        [TargetTable] NVARCHAR(50) NOT NULL,
                        [TargetID] UNIQUEIDENTIFIER NOT NULL,
                        [BeforeData] NVARCHAR(MAX) NOT NULL,
                        [OpType] NVARCHAR(20) NOT NULL,
                        [OpUser] UNIQUEIDENTIFIER NULL,
                        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Sys_Data_Logs_CreatedAt DEFAULT SYSUTCDATETIME()
                    );
                    CREATE INDEX IX_Sys_Data_Logs_TaskID ON [dbo].[Sys_Data_Logs]([TaskID]);
                    CREATE INDEX IX_Sys_Data_Logs_TargetID ON [dbo].[Sys_Data_Logs]([TargetID]);
                END;

                MERGE [dbo].[Geno_Tree_Permissions] AS target
                USING (
                    SELECT TreeID, OwnerID
                    FROM [dbo].[Geno_Trees]
                    WHERE IsDel = 0 AND OwnerID IS NOT NULL
                ) AS source
                ON target.TreeID = source.TreeID AND target.UserID = source.OwnerID
                WHEN MATCHED THEN
                    UPDATE SET target.RoleType = 1, target.IsActive = 1, target.UpdatedAt = SYSUTCDATETIME()
                WHEN NOT MATCHED BY TARGET THEN
                    INSERT (PermissionID, TreeID, UserID, RoleType, GrantedBy, GrantedAt, UpdatedAt, IsActive)
                    VALUES (NEWID(), source.TreeID, source.OwnerID, 1, source.OwnerID, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
                """;

            await _db.Database.ExecuteSqlRawAsync(sql);
        }
    }
}
