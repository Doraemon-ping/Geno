namespace 家谱.DB
{
    using Microsoft.EntityFrameworkCore;
    using System.Collections.Generic;
    using System.Reflection.Emit;
    using 家谱.Models.Entities;

    public class GenealogyDbContext : DbContext
    {
        //数据库上下文类，负责与数据库进行交互

        public GenealogyDbContext(DbContextOptions<GenealogyDbContext> options)
            : base(options)
        {
        }

        // 定义数据集，对应数据库中的表

        public DbSet<SysUser> Users { get; set; } = null!;

        // 以后增加的表也写在这里，例如：
        // public DbSet<GenoMember> Members { get; set; }

        public DbSet<AuditRequest> AuditRequests { get; set; } = null!;

        public DbSet<GenoGenerationPoem> GenoGenerationPoems { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 可以在这里进行更精细的 Fluent API 配置
            modelBuilder.Entity<SysUser>(entity =>
            {
                entity.ToTable("Sys_Users"); // 映射到真实的表名
                entity.HasKey(e => e.UserID);

                // 确保 Username 和 Email 的唯一性约束
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();

                // 设置默认值（如果数据库没设，EF 也会尝试处理）
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });
        }
    }
}
