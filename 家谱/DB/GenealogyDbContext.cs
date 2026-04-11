using Microsoft.EntityFrameworkCore;
using 家谱.Models.Entities;

namespace 家谱.DB
{
    public class GenealogyDbContext : DbContext
    {
        public GenealogyDbContext(DbContextOptions<GenealogyDbContext> options)
            : base(options)
        {
        }

        public DbSet<SysUser> Users { get; set; } = null!;
        public DbSet<GenoGenerationPoem> GenoGenerationPoems { get; set; } = null!;
        public DbSet<GenoTree> GenoTrees { get; set; } = null!;
        public DbSet<GenoTreePermission> TreePermissions { get; set; } = null!;
        public DbSet<GenoMember> GenoMembers { get; set; } = null!;
        public DbSet<GenoUnion> GenoUnions { get; set; } = null!;
        public DbSet<GenoUnionMember> GenoUnionMembers { get; set; } = null!;
        public DbSet<GenoEvent> GenoEvents { get; set; } = null!;
        public DbSet<GenoEventParticipant> GenoEventParticipants { get; set; } = null!;
        public DbSet<SysMediaFile> MediaFiles { get; set; } = null!;
        public DbSet<GenoSpacePost> SpacePosts { get; set; } = null!;
        public DbSet<GenoComment> GenoComments { get; set; } = null!;
        public DbSet<SysAnnouncement> Announcements { get; set; } = null!;
        public DbSet<DataLog> DataLogs { get; set; } = null!;
        public DbSet<ReviewTask> ReviewTasks { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SysUser>(entity =>
            {
                entity.ToTable("Sys_Users");
                entity.HasKey(item => item.UserID);
                entity.HasIndex(item => item.Username).IsUnique();
                entity.HasIndex(item => item.Email).IsUnique();
                entity.Property(item => item.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<GenoTree>(entity =>
            {
                entity.HasMany(tree => tree.Poems)
                    .WithOne()
                    .HasForeignKey("TreeID");

                entity.HasQueryFilter(tree => !tree.IsDel);
                entity.HasIndex(tree => new { tree.OwnerID, tree.IsDel });
                entity.HasIndex(tree => new { tree.IsPublic, tree.IsDel });
            });

            modelBuilder.Entity<GenoGenerationPoem>(entity =>
            {
                entity.HasQueryFilter(poem => !poem.IsDel);
                entity.HasIndex(poem => new { poem.TreeID, poem.GenerationNum });
                entity.HasIndex(poem => new { poem.TreeID, poem.Word });
            });

            modelBuilder.Entity<GenoMember>(entity =>
            {
                entity.HasQueryFilter(member => member.IsDel != true);
                entity.HasIndex(member => new { member.TreeID, member.GenerationNum });
                entity.HasIndex(member => new { member.TreeID, member.PoemID });
                entity.HasIndex(member => new { member.TreeID, member.LastName, member.FirstName });
                entity.HasIndex(member => new { member.TreeID, member.Gender, member.GenerationNum });
                entity.HasIndex(member => new { member.TreeID, member.IsLiving, member.GenerationNum });
                entity.HasIndex(member => new { member.TreeID, member.SysUserId });
            });

            modelBuilder.Entity<GenoUnion>(entity =>
            {
                entity.HasQueryFilter(union => !union.IsDel);
                entity.HasIndex(union => union.Partner1ID);
                entity.HasIndex(union => union.Partner2ID);
            });

            modelBuilder.Entity<GenoUnionMember>(entity =>
            {
                entity.HasKey(item => new { item.UnionID, item.MemberID });
                entity.HasQueryFilter(item => !item.IsDel);
                entity.HasIndex(item => new { item.UnionID, item.ChildOrder });
                entity.HasIndex(item => item.MemberID);
            });

            modelBuilder.Entity<GenoEvent>(entity =>
            {
                entity.HasQueryFilter(item => !item.IsDel);
                entity.HasIndex(item => new { item.TreeID, item.IsGlobal, item.IsPublic, item.EventDate, item.IsDel });
                entity.HasIndex(item => new { item.IsGlobal, item.EventDate, item.IsDel });
                entity.HasIndex(item => new { item.EventType, item.EventDate });
            });

            modelBuilder.Entity<GenoEventParticipant>(entity =>
            {
                entity.HasKey(item => new { item.EventID, item.MemberID });
                entity.HasQueryFilter(item => !item.IsDel);
                entity.HasIndex(item => new { item.EventID, item.IsDel });
                entity.HasIndex(item => item.MemberID);
            });

            modelBuilder.Entity<SysMediaFile>(entity =>
            {
                entity.HasQueryFilter(item => !item.IsDel);
                entity.HasIndex(item => new { item.OwnerType, item.OwnerID, item.IsDel });
                entity.HasIndex(item => new { item.TreeID, item.Status, item.CreatedAt });
                entity.HasIndex(item => new { item.UploadUserID, item.CreatedAt });
                entity.HasIndex(item => item.ReviewTaskID);
                entity.HasIndex(item => item.HashValue);
            });

            modelBuilder.Entity<GenoSpacePost>(entity =>
            {
                entity.HasQueryFilter(item => !item.IsDel);
                entity.HasIndex(item => new { item.TreeID, item.CreatedAt });
                entity.HasIndex(item => new { item.UserID, item.CreatedAt });
            });

            modelBuilder.Entity<GenoComment>(entity =>
            {
                entity.HasQueryFilter(item => !item.IsDel);
                entity.HasIndex(item => new { item.OwnerType, item.OwnerID, item.CreatedAt });
                entity.HasIndex(item => new { item.ParentCommentID, item.CreatedAt });
                entity.HasIndex(item => new { item.TreeID, item.CreatedAt });
                entity.HasIndex(item => new { item.UserID, item.CreatedAt });
            });

            modelBuilder.Entity<SysAnnouncement>(entity =>
            {
                entity.HasQueryFilter(item => !item.IsDel);
                entity.HasIndex(item => new { item.Status, item.IsPinned, item.PublishedAt, item.CreatedAt });
                entity.HasIndex(item => new { item.Category, item.Status, item.CreatedAt });
                entity.HasIndex(item => new { item.CreatedBy, item.CreatedAt });
            });

            modelBuilder.Entity<GenoTreePermission>(entity =>
            {
                entity.HasIndex(item => new { item.TreeID, item.UserID }).IsUnique();
                entity.HasIndex(item => new { item.UserID, item.IsActive });
            });

            modelBuilder.Entity<DataLog>(entity =>
            {
                entity.HasIndex(log => log.CreatedAt);
                entity.HasIndex(log => new { log.TargetTable, log.CreatedAt });
                entity.HasIndex(log => new { log.OpUser, log.CreatedAt });
                entity.HasIndex(log => log.TargetID);
                entity.HasIndex(log => log.TaskID);
            });

            modelBuilder.Entity<ReviewTask>(entity =>
            {
                entity.HasIndex(task => new { task.Status, task.CreatedAt });
                entity.HasIndex(task => new { task.TreeID, task.Status, task.CreatedAt });
                entity.HasIndex(task => new { task.ReviewerID, task.Status });
                entity.HasIndex(task => new { task.SubmitterID, task.CreatedAt });
            });
        }
    }
}
