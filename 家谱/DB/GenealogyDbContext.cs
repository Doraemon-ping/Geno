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
        public DbSet<DataLog> DataLogs { get; set; } = null!;
        public DbSet<ReviewTask> ReviewTasks { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SysUser>(entity =>
            {
                entity.ToTable("Sys_Users");
                entity.HasKey(e => e.UserID);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<GenoTree>()
                .HasMany(t => t.Poems)
                .WithOne()
                .HasForeignKey("TreeID");

            modelBuilder.Entity<GenoTree>()
                .HasQueryFilter(t => !t.IsDel);

            modelBuilder.Entity<GenoGenerationPoem>()
                .HasQueryFilter(p => !p.IsDel);

            modelBuilder.Entity<GenoMember>()
                .HasQueryFilter(m => m.IsDel != true);

            modelBuilder.Entity<GenoUnion>()
                .HasQueryFilter(union => !union.IsDel);

            modelBuilder.Entity<GenoUnionMember>(entity =>
            {
                entity.HasKey(item => new { item.UnionID, item.MemberID });
                entity.HasQueryFilter(item => !item.IsDel);
                entity.HasIndex(item => new { item.UnionID, item.ChildOrder });
            });

            modelBuilder.Entity<GenoTreePermission>()
                .HasIndex(p => new { p.TreeID, p.UserID })
                .IsUnique();
        }
    }
}
