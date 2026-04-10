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
                END;

                IF OBJECT_ID(N'dbo.Geno_Unions', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Geno_Unions](
                        [UnionID] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        [Partner1ID] UNIQUEIDENTIFIER NOT NULL,
                        [Partner2ID] UNIQUEIDENTIFIER NOT NULL,
                        [UnionType] TINYINT NOT NULL CONSTRAINT DF_Geno_Unions_UnionType DEFAULT 1,
                        [SortOrder] INT NOT NULL CONSTRAINT DF_Geno_Unions_SortOrder DEFAULT 1,
                        [MarriageDate] DATE NULL,
                        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Unions_CreatedAt DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Unions_UpdatedAt DEFAULT SYSUTCDATETIME(),
                        [IsDel] BIT NOT NULL CONSTRAINT DF_Geno_Unions_IsDel DEFAULT 0
                    );
                END;

                IF OBJECT_ID(N'dbo.Geno_Union_Members', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Geno_Union_Members](
                        [UnionID] UNIQUEIDENTIFIER NOT NULL,
                        [MemberID] UNIQUEIDENTIFIER NOT NULL,
                        [RelType] TINYINT NOT NULL,
                        [ChildOrder] INT NOT NULL CONSTRAINT DF_Geno_Union_Members_ChildOrder DEFAULT 1,
                        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Union_Members_CreatedAt DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Union_Members_UpdatedAt DEFAULT SYSUTCDATETIME(),
                        [IsDel] BIT NOT NULL CONSTRAINT DF_Geno_Union_Members_IsDel DEFAULT 0,
                        CONSTRAINT PK_Geno_Union_Members PRIMARY KEY ([UnionID], [MemberID])
                    );
                END;

                IF OBJECT_ID(N'dbo.Sys_Data_Logs', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Sys_Data_Logs](
                        [LogID] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        [TaskID] UNIQUEIDENTIFIER NULL,
                        [TargetTable] NVARCHAR(50) NOT NULL,
                        [TargetID] UNIQUEIDENTIFIER NOT NULL,
                        [BeforeData] NVARCHAR(MAX) NOT NULL,
                        [AfterData] NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Sys_Data_Logs_AfterData DEFAULT N'{{}}',
                        [OpType] NVARCHAR(20) NOT NULL,
                        [OpUser] UNIQUEIDENTIFIER NULL,
                        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Sys_Data_Logs_CreatedAt DEFAULT SYSUTCDATETIME()
                    );
                END;

                IF COL_LENGTH(N'dbo.Sys_Data_Logs', N'AfterData') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Data_Logs]
                    ADD [AfterData] NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Sys_Data_Logs_AfterData DEFAULT N'{{}}';
                END;

                IF COL_LENGTH(N'dbo.Geno_Unions', N'CreatedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Unions]
                    ADD [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Unions_CreatedAt DEFAULT SYSUTCDATETIME();
                END;

                IF COL_LENGTH(N'dbo.Geno_Unions', N'UpdatedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Unions]
                    ADD [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Unions_UpdatedAt DEFAULT SYSUTCDATETIME();
                END;

                IF COL_LENGTH(N'dbo.Geno_Unions', N'IsDel') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Unions]
                    ADD [IsDel] BIT NOT NULL CONSTRAINT DF_Geno_Unions_IsDel DEFAULT 0;
                END;

                IF COL_LENGTH(N'dbo.Geno_Union_Members', N'ChildOrder') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Union_Members]
                    ADD [ChildOrder] INT NOT NULL CONSTRAINT DF_Geno_Union_Members_ChildOrder DEFAULT 1;
                END;

                IF COL_LENGTH(N'dbo.Geno_Union_Members', N'CreatedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Union_Members]
                    ADD [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Union_Members_CreatedAt DEFAULT SYSUTCDATETIME();
                END;

                IF COL_LENGTH(N'dbo.Geno_Union_Members', N'UpdatedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Union_Members]
                    ADD [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Union_Members_UpdatedAt DEFAULT SYSUTCDATETIME();
                END;

                IF COL_LENGTH(N'dbo.Geno_Union_Members', N'IsDel') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Union_Members]
                    ADD [IsDel] BIT NOT NULL CONSTRAINT DF_Geno_Union_Members_IsDel DEFAULT 0;
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Tree_Permissions_TreeID_UserID' AND object_id = OBJECT_ID(N'dbo.Geno_Tree_Permissions'))
                    CREATE UNIQUE INDEX IX_Geno_Tree_Permissions_TreeID_UserID ON [dbo].[Geno_Tree_Permissions]([TreeID], [UserID]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Tree_Permissions_UserID' AND object_id = OBJECT_ID(N'dbo.Geno_Tree_Permissions'))
                    CREATE INDEX IX_Geno_Tree_Permissions_UserID ON [dbo].[Geno_Tree_Permissions]([UserID]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Tree_Permissions_UserID_IsActive' AND object_id = OBJECT_ID(N'dbo.Geno_Tree_Permissions'))
                    CREATE INDEX IX_Geno_Tree_Permissions_UserID_IsActive ON [dbo].[Geno_Tree_Permissions]([UserID], [IsActive]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Unions_Partner1ID' AND object_id = OBJECT_ID(N'dbo.Geno_Unions'))
                    CREATE INDEX IX_Geno_Unions_Partner1ID ON [dbo].[Geno_Unions]([Partner1ID]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Unions_Partner2ID' AND object_id = OBJECT_ID(N'dbo.Geno_Unions'))
                    CREATE INDEX IX_Geno_Unions_Partner2ID ON [dbo].[Geno_Unions]([Partner2ID]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Union_Members_MemberID' AND object_id = OBJECT_ID(N'dbo.Geno_Union_Members'))
                    CREATE INDEX IX_Geno_Union_Members_MemberID ON [dbo].[Geno_Union_Members]([MemberID]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Union_Members_UnionID_ChildOrder' AND object_id = OBJECT_ID(N'dbo.Geno_Union_Members'))
                    CREATE INDEX IX_Geno_Union_Members_UnionID_ChildOrder ON [dbo].[Geno_Union_Members]([UnionID], [ChildOrder]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Data_Logs_TaskID' AND object_id = OBJECT_ID(N'dbo.Sys_Data_Logs'))
                    CREATE INDEX IX_Sys_Data_Logs_TaskID ON [dbo].[Sys_Data_Logs]([TaskID]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Data_Logs_TargetID' AND object_id = OBJECT_ID(N'dbo.Sys_Data_Logs'))
                    CREATE INDEX IX_Sys_Data_Logs_TargetID ON [dbo].[Sys_Data_Logs]([TargetID]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Data_Logs_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Sys_Data_Logs'))
                    CREATE INDEX IX_Sys_Data_Logs_CreatedAt ON [dbo].[Sys_Data_Logs]([CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Data_Logs_TargetTable_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Sys_Data_Logs'))
                    CREATE INDEX IX_Sys_Data_Logs_TargetTable_CreatedAt ON [dbo].[Sys_Data_Logs]([TargetTable], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Data_Logs_OpUser_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Sys_Data_Logs'))
                    CREATE INDEX IX_Sys_Data_Logs_OpUser_CreatedAt ON [dbo].[Sys_Data_Logs]([OpUser], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Members_TreeID_GenerationNum' AND object_id = OBJECT_ID(N'dbo.Geno_Members'))
                    CREATE INDEX IX_Geno_Members_TreeID_GenerationNum ON [dbo].[Geno_Members]([TreeID], [GenerationNum]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Members_TreeID_PoemID' AND object_id = OBJECT_ID(N'dbo.Geno_Members'))
                    CREATE INDEX IX_Geno_Members_TreeID_PoemID ON [dbo].[Geno_Members]([TreeID], [PoemID]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Members_TreeID_LastName_FirstName' AND object_id = OBJECT_ID(N'dbo.Geno_Members'))
                    CREATE INDEX IX_Geno_Members_TreeID_LastName_FirstName ON [dbo].[Geno_Members]([TreeID], [LastName], [FirstName]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Members_TreeID_Gender_GenerationNum' AND object_id = OBJECT_ID(N'dbo.Geno_Members'))
                    CREATE INDEX IX_Geno_Members_TreeID_Gender_GenerationNum ON [dbo].[Geno_Members]([TreeID], [Gender], [GenerationNum]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Members_TreeID_IsLiving_GenerationNum' AND object_id = OBJECT_ID(N'dbo.Geno_Members'))
                    CREATE INDEX IX_Geno_Members_TreeID_IsLiving_GenerationNum ON [dbo].[Geno_Members]([TreeID], [IsLiving], [GenerationNum]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Members_TreeID_SysUserId' AND object_id = OBJECT_ID(N'dbo.Geno_Members'))
                    CREATE INDEX IX_Geno_Members_TreeID_SysUserId ON [dbo].[Geno_Members]([TreeID], [SysUserId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Generation_Poems_TreeID_GenerationNum' AND object_id = OBJECT_ID(N'dbo.Geno_Generation_Poems'))
                    CREATE INDEX IX_Geno_Generation_Poems_TreeID_GenerationNum ON [dbo].[Geno_Generation_Poems]([TreeID], [GenerationNum]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Generation_Poems_TreeID_Word' AND object_id = OBJECT_ID(N'dbo.Geno_Generation_Poems'))
                    CREATE INDEX IX_Geno_Generation_Poems_TreeID_Word ON [dbo].[Geno_Generation_Poems]([TreeID], [Word]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Trees_OwnerID_IsDel' AND object_id = OBJECT_ID(N'dbo.Geno_Trees'))
                    CREATE INDEX IX_Geno_Trees_OwnerID_IsDel ON [dbo].[Geno_Trees]([OwnerID], [IsDel]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Trees_IsPublic_IsDel' AND object_id = OBJECT_ID(N'dbo.Geno_Trees'))
                    CREATE INDEX IX_Geno_Trees_IsPublic_IsDel ON [dbo].[Geno_Trees]([IsPublic], [IsDel]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Review_Tasks_Status_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Sys_Review_Tasks'))
                    CREATE INDEX IX_Sys_Review_Tasks_Status_CreatedAt ON [dbo].[Sys_Review_Tasks]([Status], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Review_Tasks_TreeID_Status_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Sys_Review_Tasks'))
                    CREATE INDEX IX_Sys_Review_Tasks_TreeID_Status_CreatedAt ON [dbo].[Sys_Review_Tasks]([TreeID], [Status], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Review_Tasks_ReviewerID_Status' AND object_id = OBJECT_ID(N'dbo.Sys_Review_Tasks'))
                    CREATE INDEX IX_Sys_Review_Tasks_ReviewerID_Status ON [dbo].[Sys_Review_Tasks]([ReviewerID], [Status]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Review_Tasks_SubmitterID_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Sys_Review_Tasks'))
                    CREATE INDEX IX_Sys_Review_Tasks_SubmitterID_CreatedAt ON [dbo].[Sys_Review_Tasks]([SubmitterID], [CreatedAt]);

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
