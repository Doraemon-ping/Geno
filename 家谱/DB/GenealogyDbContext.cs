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

        public DbSet<SysUser> Users { get; set; } = null!; // 系统用户表，包含管理员和普通用户
        // 以后增加的表也写在这里，例如：
        // public DbSet<GenoMember> Members { get; set; }
        public DbSet<GenoGenerationPoem> GenoGenerationPoems { get; set; } = null!;// 字辈诗表，关联家谱树
        public DbSet<GenoTree> GenoTrees { get; set; } = null!;// 家谱树表，包含树的基本信息和权限控制字段
        public DbSet<GenoMember> GenoMembers { get; set; } = null!;// 家谱成员表，包含成员的基本信息和与树的关联
        public DbSet<DataLog> DataLogs { get; set; } = null!; // 数据变更日志表，记录所有审核通过的修改历史（快照审计）
        public DbSet<ReviewTask> ReviewTasks { get; set; } = null!; // 审核任务表，记录待审核的修改请求和审核状态




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

                modelBuilder.Entity<GenoTree>()
        .HasMany(t => t.Poems)      // 树有多个字辈
        .WithOne()                  // 字辈对应一个树（这里留空，因为你删了导航属性）
        .HasForeignKey("TreeID");   // 明确告诉 EF，外键字段名叫 TreeID

            });
        }
    }
}
