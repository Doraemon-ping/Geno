namespace 家谱.Services.Common
{
    using Microsoft.EntityFrameworkCore;
    using System.Text.Json;
    using 家谱.DB;
    using 家谱.Models.DTOs.Common;
    using 家谱.Models.Entities;
    using 家谱.Models.Enums;

    public interface IHandleTasks
    {
        Task HandleApplyAdminAsync(ReviewTask task, int Action);
    }


    /// <summary>
    /// Defines the <see cref="HandleTasks" />
    /// 处理各种审核任务的具体逻辑实现类，提供给 ReviewService 内部调用
    /// </summary>
    public class HandleTasks : IHandleTasks
    {
        private readonly GenealogyDbContext _db;

        public HandleTasks(GenealogyDbContext db)
        {
            _db = db;
        }

        /// 处理管理员权限申请
        /// <summary>
        /// The HandleApplyAdminAsync
        /// </summary>
        /// <param name="task">The task<see cref="ReviewTask"/></param>
        /// <param name="Action">The Action<see cref="int"/></param>
        /// <returns>The <see cref="Task"/></returns>
        public async Task HandleApplyAdminAsync(ReviewTask task, int Action)
        {
            // 1. 解析 ChangeData 负载
            var payload = JsonSerializer.Deserialize<RoleApplyPayload>(task.ChangeData, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload == null) throw new Exception("无效的权限申请数据");

            // 2. 检查权限表中是否已存在该用户的记录
            // 3. 根据 Action（同意/拒绝）执行不同的逻辑
            //Task.TargetID 是须要修改权限的用户 ID，可能非本人提交
            var user = await _db.Users
                .FirstOrDefaultAsync(r => r.UserID == task.TargetID);

            string targetRoleName = ((RoleType)payload.NewRole).ToString();

            if (user == null)
            {
                throw new Exception("用户不存在");
            }

            if (user.RoleType == payload.NewRole)
            {
                throw new Exception($"用户已是{targetRoleName}，无需重复申请");
            }

            if (Action == 1)//同意
            {
                user.RoleType = payload.NewRole;
            }
            else if (Action == 2)//拒绝
            {
                // 不修改用户角色，仅记录审核结果
            }
            else
            {
                throw new Exception("无效的操作类型");
            }
        }
    }
}
