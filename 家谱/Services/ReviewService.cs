using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using 家谱.DB;
using 家谱.Models.DTOs.Common;
using 家谱.Models.Entities;
using 家谱.Models.Enums;

namespace 家谱.Services
{
    public interface IReviewService
    {
        // 1. 统一提交入口：支持所有业务申请


        Task<Guid> SubmitAsync(SubmitReviewRequest dto, Guid submitterId);

        /// 2. 统一审批出口：通过 ActionCode 路由到具体的内部处理方法
        Task ApproveAsync(Guid taskId, Guid reviewerId, string notes, int action);
    }



    public class ReviewService : IReviewService
    {
        private readonly GenealogyDbContext _db;

        public ReviewService(GenealogyDbContext db, ILogger<ReviewService> logger)
        {
            _db = db;
        }

        /// <summary>
        /// 1. 统一提交入口：支持所有业务申请
        /// </summary>
        public async Task<Guid> SubmitAsync(SubmitReviewRequest dto, Guid submitterId)
        {

            var t = await _db.ReviewTasks.FirstOrDefaultAsync(t =>
                t.SubmitterID == submitterId &&
                t.TargetID == dto.TargetId &&
                t.ActionCode == dto.ActionCode &&
                t.Status == 0);

            if (t != null)
            {
                throw new Exception("您已提交过相同的申请，请等待审核结果");
            }

            var task = new ReviewTask
            {
                TreeID = dto.TreeId,
                SubmitterID = submitterId,// 从参数传入，确保安全,保证提交人与 Token 中一致，防止冒用他人 ID 提交
                ActionCode = dto.ActionCode, // 如 "Member.Update", "Tree.ApplyRole"
                TargetID = dto.TargetId,
                ChangeData = dto.ChangeData, // 直接存储 JSON 字符串
                ApplyReason = dto.Reason,
                Status = 0 // Pending
            };

            _db.ReviewTasks.Add(task);
            await _db.SaveChangesAsync();
            return task.TaskID;
        }

        /// <summary>
        /// 2. 统一审批出口：通过 ActionCode 路由到具体的内部处理方法
        /// </summary>
        public async Task ApproveAsync(Guid taskId, Guid reviewerId, string notes, int action)
        {
            var task = await _db.ReviewTasks.FirstOrDefaultAsync(t => t.TaskID == taskId);
            if (task == null || task.Status != 0) throw new Exception("任务不存在或已处理");

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // 路由：根据 ActionCode 分发逻辑
                switch (task.ActionCode)
                {
                    case ReviewActions.ApplyAdmin:
                        await HandleApplyAdminAsync(task, action);
                        break;


                    default:
                        throw new Exception($"未定义的业务操作: {task.ActionCode}");
                }

                // 更新任务状态
                task.Status = 1; // Approved
                task.ReviewerID = reviewerId;
                task.ReviewNotes = notes;
                task.ProcessedAt = DateTime.Now;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }



        #region 内部私有处理逻辑 (具体业务重放)

        /// 处理管理员权限申请
        private async Task HandleApplyAdminAsync(ReviewTask task, int Action)
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



        #endregion
    }

}
