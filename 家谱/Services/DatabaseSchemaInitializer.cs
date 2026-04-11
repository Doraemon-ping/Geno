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
                IF OBJECT_ID(N'dbo.Sys_Users', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.Sys_Users', N'AvatarUrl') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Users]
                    ADD [AvatarUrl] NVARCHAR(500) NULL;
                END;

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

                IF OBJECT_ID(N'dbo.Geno_Events', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Geno_Events](
                        [EventID] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        [TreeID] UNIQUEIDENTIFIER NULL,
                        [EventTitle] NVARCHAR(200) NOT NULL,
                        [EventType] TINYINT NOT NULL,
                        [IsGlobal] BIT NOT NULL CONSTRAINT DF_Geno_Events_IsGlobal DEFAULT 0,
                        [IsPublic] BIT NOT NULL CONSTRAINT DF_Geno_Events_IsPublic DEFAULT 0,
                        [EventDate] DATE NULL,
                        [DateRaw] NVARCHAR(100) NULL,
                        [LocationID] UNIQUEIDENTIFIER NULL,
                        [Description] NVARCHAR(MAX) NULL,
                        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Events_CreatedAt DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Events_UpdatedAt DEFAULT SYSUTCDATETIME(),
                        [IsDel] BIT NOT NULL CONSTRAINT DF_Geno_Events_IsDel DEFAULT 0
                    );
                END;

                IF OBJECT_ID(N'dbo.Geno_Event_Participants', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Geno_Event_Participants](
                        [EventID] UNIQUEIDENTIFIER NOT NULL,
                        [MemberID] UNIQUEIDENTIFIER NOT NULL,
                        [RoleDescription] NVARCHAR(100) NULL,
                        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Event_Participants_CreatedAt DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Event_Participants_UpdatedAt DEFAULT SYSUTCDATETIME(),
                        [IsDel] BIT NOT NULL CONSTRAINT DF_Geno_Event_Participants_IsDel DEFAULT 0,
                        CONSTRAINT PK_Geno_Event_Participants PRIMARY KEY ([EventID], [MemberID])
                    );
                END;

                IF OBJECT_ID(N'dbo.Sys_Media_Files', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Sys_Media_Files](
                        [MediaID] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        [TreeID] UNIQUEIDENTIFIER NULL,
                        [OwnerType] NVARCHAR(50) NOT NULL,
                        [OwnerID] UNIQUEIDENTIFIER NULL,
                        [FileName] NVARCHAR(260) NOT NULL,
                        [FileExt] NVARCHAR(20) NULL,
                        [MimeType] NVARCHAR(100) NULL,
                        [FileSize] BIGINT NOT NULL,
                        [StoragePath] NVARCHAR(500) NOT NULL,
                        [PublicUrl] NVARCHAR(500) NULL,
                        [HashValue] NVARCHAR(128) NULL,
                        [Caption] NVARCHAR(200) NULL,
                        [SortOrder] INT NOT NULL CONSTRAINT DF_Sys_Media_Files_SortOrder DEFAULT 1,
                        [UploadUserID] UNIQUEIDENTIFIER NOT NULL,
                        [ReviewTaskID] UNIQUEIDENTIFIER NULL,
                        [Status] TINYINT NOT NULL CONSTRAINT DF_Sys_Media_Files_Status DEFAULT 0,
                        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Sys_Media_Files_CreatedAt DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Sys_Media_Files_UpdatedAt DEFAULT SYSUTCDATETIME(),
                        [IsDel] BIT NOT NULL CONSTRAINT DF_Sys_Media_Files_IsDel DEFAULT 0
                    );
                END;

                IF OBJECT_ID(N'dbo.Geno_Comments', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Geno_Comments](
                        [CommentID] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        [TreeID] UNIQUEIDENTIFIER NULL,
                        [OwnerType] NVARCHAR(50) NOT NULL,
                        [OwnerID] UNIQUEIDENTIFIER NOT NULL,
                        [UserID] UNIQUEIDENTIFIER NOT NULL,
                        [Content] NVARCHAR(1000) NOT NULL,
                        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Comments_CreatedAt DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Comments_UpdatedAt DEFAULT SYSUTCDATETIME(),
                        [IsDel] BIT NOT NULL CONSTRAINT DF_Geno_Comments_IsDel DEFAULT 0
                    );
                END;

                IF OBJECT_ID(N'dbo.Geno_Space_Posts', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Geno_Space_Posts](
                        [PostID] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        [TreeID] UNIQUEIDENTIFIER NOT NULL,
                        [UserID] UNIQUEIDENTIFIER NOT NULL,
                        [PostTitle] NVARCHAR(200) NULL,
                        [Content] NVARCHAR(MAX) NOT NULL,
                        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Space_Posts_CreatedAt DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Space_Posts_UpdatedAt DEFAULT SYSUTCDATETIME(),
                        [IsDel] BIT NOT NULL CONSTRAINT DF_Geno_Space_Posts_IsDel DEFAULT 0
                    );
                END;

                IF OBJECT_ID(N'dbo.Sys_Announcements', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Sys_Announcements](
                        [AnnouncementID] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        [Title] NVARCHAR(200) NOT NULL,
                        [Content] NVARCHAR(MAX) NOT NULL,
                        [Category] NVARCHAR(50) NOT NULL CONSTRAINT DF_Sys_Announcements_Category DEFAULT N'系统公告',
                        [Status] TINYINT NOT NULL CONSTRAINT DF_Sys_Announcements_Status DEFAULT 1,
                        [IsPinned] BIT NOT NULL CONSTRAINT DF_Sys_Announcements_IsPinned DEFAULT 0,
                        [CreatedBy] UNIQUEIDENTIFIER NOT NULL,
                        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Sys_Announcements_CreatedAt DEFAULT SYSUTCDATETIME(),
                        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Sys_Announcements_UpdatedAt DEFAULT SYSUTCDATETIME(),
                        [PublishedAt] DATETIME2 NULL,
                        [IsDel] BIT NOT NULL CONSTRAINT DF_Sys_Announcements_IsDel DEFAULT 0
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

                IF COL_LENGTH(N'dbo.Geno_Events', N'IsGlobal') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Events]
                    ADD [IsGlobal] BIT NOT NULL CONSTRAINT DF_Geno_Events_IsGlobal DEFAULT 0;
                END;

                IF COL_LENGTH(N'dbo.Geno_Events', N'IsPublic') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Events]
                    ADD [IsPublic] BIT NOT NULL CONSTRAINT DF_Geno_Events_IsPublic DEFAULT 0;
                END;

                IF COL_LENGTH(N'dbo.Geno_Events', N'DateRaw') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Events]
                    ADD [DateRaw] NVARCHAR(100) NULL;
                END;

                IF COL_LENGTH(N'dbo.Geno_Events', N'LocationID') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Events]
                    ADD [LocationID] UNIQUEIDENTIFIER NULL;
                END;

                IF COL_LENGTH(N'dbo.Geno_Events', N'Description') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Events]
                    ADD [Description] NVARCHAR(MAX) NULL;
                END;

                IF COL_LENGTH(N'dbo.Geno_Events', N'CreatedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Events]
                    ADD [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Events_CreatedAt DEFAULT SYSUTCDATETIME();
                END;

                IF COL_LENGTH(N'dbo.Geno_Events', N'UpdatedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Events]
                    ADD [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Events_UpdatedAt DEFAULT SYSUTCDATETIME();
                END;

                IF COL_LENGTH(N'dbo.Geno_Events', N'IsDel') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Events]
                    ADD [IsDel] BIT NOT NULL CONSTRAINT DF_Geno_Events_IsDel DEFAULT 0;
                END;

                IF COL_LENGTH(N'dbo.Geno_Event_Participants', N'RoleDescription') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Event_Participants]
                    ADD [RoleDescription] NVARCHAR(100) NULL;
                END;

                IF COL_LENGTH(N'dbo.Geno_Event_Participants', N'CreatedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Event_Participants]
                    ADD [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Event_Participants_CreatedAt DEFAULT SYSUTCDATETIME();
                END;

                IF COL_LENGTH(N'dbo.Geno_Event_Participants', N'UpdatedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Event_Participants]
                    ADD [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Event_Participants_UpdatedAt DEFAULT SYSUTCDATETIME();
                END;

                IF COL_LENGTH(N'dbo.Geno_Event_Participants', N'IsDel') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Event_Participants]
                    ADD [IsDel] BIT NOT NULL CONSTRAINT DF_Geno_Event_Participants_IsDel DEFAULT 0;
                END;

                IF COL_LENGTH(N'dbo.Sys_Media_Files', N'PublicUrl') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Media_Files]
                    ADD [PublicUrl] NVARCHAR(500) NULL;
                END;

                IF COL_LENGTH(N'dbo.Sys_Media_Files', N'HashValue') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Media_Files]
                    ADD [HashValue] NVARCHAR(128) NULL;
                END;

                IF COL_LENGTH(N'dbo.Sys_Media_Files', N'Caption') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Media_Files]
                    ADD [Caption] NVARCHAR(200) NULL;
                END;

                IF COL_LENGTH(N'dbo.Sys_Media_Files', N'SortOrder') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Media_Files]
                    ADD [SortOrder] INT NOT NULL CONSTRAINT DF_Sys_Media_Files_SortOrder DEFAULT 1;
                END;

                IF COL_LENGTH(N'dbo.Sys_Media_Files', N'ReviewTaskID') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Media_Files]
                    ADD [ReviewTaskID] UNIQUEIDENTIFIER NULL;
                END;

                IF COL_LENGTH(N'dbo.Sys_Media_Files', N'Status') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Media_Files]
                    ADD [Status] TINYINT NOT NULL CONSTRAINT DF_Sys_Media_Files_Status DEFAULT 0;
                END;

                IF COL_LENGTH(N'dbo.Sys_Media_Files', N'CreatedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Media_Files]
                    ADD [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Sys_Media_Files_CreatedAt DEFAULT SYSUTCDATETIME();
                END;

                IF COL_LENGTH(N'dbo.Sys_Media_Files', N'UpdatedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Media_Files]
                    ADD [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Sys_Media_Files_UpdatedAt DEFAULT SYSUTCDATETIME();
                END;

                IF COL_LENGTH(N'dbo.Sys_Media_Files', N'IsDel') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Media_Files]
                    ADD [IsDel] BIT NOT NULL CONSTRAINT DF_Sys_Media_Files_IsDel DEFAULT 0;
                END;

                IF COL_LENGTH(N'dbo.Geno_Comments', N'TreeID') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Comments]
                    ADD [TreeID] UNIQUEIDENTIFIER NULL;
                END;

                IF COL_LENGTH(N'dbo.Geno_Comments', N'ParentCommentID') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Comments]
                    ADD [ParentCommentID] UNIQUEIDENTIFIER NULL;
                END;

                IF COL_LENGTH(N'dbo.Geno_Comments', N'UpdatedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Comments]
                    ADD [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Geno_Comments_UpdatedAt DEFAULT SYSUTCDATETIME();
                END;

                IF COL_LENGTH(N'dbo.Geno_Comments', N'IsDel') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Geno_Comments]
                    ADD [IsDel] BIT NOT NULL CONSTRAINT DF_Geno_Comments_IsDel DEFAULT 0;
                END;

                IF COL_LENGTH(N'dbo.Sys_Announcements', N'Category') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Announcements]
                    ADD [Category] NVARCHAR(50) NOT NULL CONSTRAINT DF_Sys_Announcements_Category DEFAULT N'系统公告';
                END;

                IF COL_LENGTH(N'dbo.Sys_Announcements', N'Status') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Announcements]
                    ADD [Status] TINYINT NOT NULL CONSTRAINT DF_Sys_Announcements_Status DEFAULT 1;
                END;

                IF COL_LENGTH(N'dbo.Sys_Announcements', N'IsPinned') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Announcements]
                    ADD [IsPinned] BIT NOT NULL CONSTRAINT DF_Sys_Announcements_IsPinned DEFAULT 0;
                END;

                IF COL_LENGTH(N'dbo.Sys_Announcements', N'PublishedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Announcements]
                    ADD [PublishedAt] DATETIME2 NULL;
                END;

                IF COL_LENGTH(N'dbo.Sys_Announcements', N'UpdatedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Announcements]
                    ADD [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Sys_Announcements_UpdatedAt DEFAULT SYSUTCDATETIME();
                END;

                IF COL_LENGTH(N'dbo.Sys_Announcements', N'IsDel') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Sys_Announcements]
                    ADD [IsDel] BIT NOT NULL CONSTRAINT DF_Sys_Announcements_IsDel DEFAULT 0;
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

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Events_TreeID_IsGlobal_EventDate_IsDel' AND object_id = OBJECT_ID(N'dbo.Geno_Events'))
                    CREATE INDEX IX_Geno_Events_TreeID_IsGlobal_EventDate_IsDel ON [dbo].[Geno_Events]([TreeID], [IsGlobal], [EventDate], [IsDel]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Events_TreeID_IsGlobal_IsPublic_EventDate_IsDel' AND object_id = OBJECT_ID(N'dbo.Geno_Events'))
                    CREATE INDEX IX_Geno_Events_TreeID_IsGlobal_IsPublic_EventDate_IsDel ON [dbo].[Geno_Events]([TreeID], [IsGlobal], [IsPublic], [EventDate], [IsDel]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Events_IsGlobal_EventDate_IsDel' AND object_id = OBJECT_ID(N'dbo.Geno_Events'))
                    CREATE INDEX IX_Geno_Events_IsGlobal_EventDate_IsDel ON [dbo].[Geno_Events]([IsGlobal], [EventDate], [IsDel]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Events_EventType_EventDate' AND object_id = OBJECT_ID(N'dbo.Geno_Events'))
                    CREATE INDEX IX_Geno_Events_EventType_EventDate ON [dbo].[Geno_Events]([EventType], [EventDate]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Event_Participants_EventID_IsDel' AND object_id = OBJECT_ID(N'dbo.Geno_Event_Participants'))
                    CREATE INDEX IX_Geno_Event_Participants_EventID_IsDel ON [dbo].[Geno_Event_Participants]([EventID], [IsDel]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Event_Participants_MemberID' AND object_id = OBJECT_ID(N'dbo.Geno_Event_Participants'))
                    CREATE INDEX IX_Geno_Event_Participants_MemberID ON [dbo].[Geno_Event_Participants]([MemberID]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Media_Files_OwnerType_OwnerID_IsDel' AND object_id = OBJECT_ID(N'dbo.Sys_Media_Files'))
                    CREATE INDEX IX_Sys_Media_Files_OwnerType_OwnerID_IsDel ON [dbo].[Sys_Media_Files]([OwnerType], [OwnerID], [IsDel]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Media_Files_TreeID_Status_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Sys_Media_Files'))
                    CREATE INDEX IX_Sys_Media_Files_TreeID_Status_CreatedAt ON [dbo].[Sys_Media_Files]([TreeID], [Status], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Media_Files_UploadUserID_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Sys_Media_Files'))
                    CREATE INDEX IX_Sys_Media_Files_UploadUserID_CreatedAt ON [dbo].[Sys_Media_Files]([UploadUserID], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Media_Files_ReviewTaskID' AND object_id = OBJECT_ID(N'dbo.Sys_Media_Files'))
                    CREATE INDEX IX_Sys_Media_Files_ReviewTaskID ON [dbo].[Sys_Media_Files]([ReviewTaskID]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Media_Files_HashValue' AND object_id = OBJECT_ID(N'dbo.Sys_Media_Files'))
                    CREATE INDEX IX_Sys_Media_Files_HashValue ON [dbo].[Sys_Media_Files]([HashValue]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Comments_OwnerType_OwnerID_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Geno_Comments'))
                    CREATE INDEX IX_Geno_Comments_OwnerType_OwnerID_CreatedAt ON [dbo].[Geno_Comments]([OwnerType], [OwnerID], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Comments_ParentCommentID_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Geno_Comments'))
                    CREATE INDEX IX_Geno_Comments_ParentCommentID_CreatedAt ON [dbo].[Geno_Comments]([ParentCommentID], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Comments_TreeID_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Geno_Comments'))
                    CREATE INDEX IX_Geno_Comments_TreeID_CreatedAt ON [dbo].[Geno_Comments]([TreeID], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Comments_UserID_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Geno_Comments'))
                    CREATE INDEX IX_Geno_Comments_UserID_CreatedAt ON [dbo].[Geno_Comments]([UserID], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Space_Posts_TreeID_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Geno_Space_Posts'))
                    CREATE INDEX IX_Geno_Space_Posts_TreeID_CreatedAt ON [dbo].[Geno_Space_Posts]([TreeID], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Geno_Space_Posts_UserID_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Geno_Space_Posts'))
                    CREATE INDEX IX_Geno_Space_Posts_UserID_CreatedAt ON [dbo].[Geno_Space_Posts]([UserID], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Announcements_Status_IsPinned_PublishedAt_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Sys_Announcements'))
                    CREATE INDEX IX_Sys_Announcements_Status_IsPinned_PublishedAt_CreatedAt ON [dbo].[Sys_Announcements]([Status], [IsPinned], [PublishedAt], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Announcements_Category_Status_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Sys_Announcements'))
                    CREATE INDEX IX_Sys_Announcements_Category_Status_CreatedAt ON [dbo].[Sys_Announcements]([Category], [Status], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sys_Announcements_CreatedBy_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Sys_Announcements'))
                    CREATE INDEX IX_Sys_Announcements_CreatedBy_CreatedAt ON [dbo].[Sys_Announcements]([CreatedBy], [CreatedAt]);

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
