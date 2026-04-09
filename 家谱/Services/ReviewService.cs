namespace 家谱.Services
{
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.Threading.Tasks;
    using 家谱.DB;
    using 家谱.Models.DTOs;
    using 家谱.Models.Entities;
    using 家谱.Models.Enums;
    using 家谱.Services.Common;

    /// <summary>
    /// Defines the <see cref="IReviewService" />
    /// </summary>
    public interface IReviewService
    {
        /// <summary>
        /// The SubmitAsync 统一提交入口：支持所有业务申请
        /// </summary>
        /// <param name="dto">The dto<see cref="SubmitReviewRequest"/></param>
        /// <param name="submitterId">The submitterId<see cref="Guid"/></param>
        /// <returns>The <see cref="Task{Guid}"/></returns>
        Task<Guid> SubmitAsync(SubmitReviewRequest dto, Guid submitterId);

        /// <summary>
        /// The ApproveAsync 统一审批出口：通过 ActionCode 路由到具体的内部处理方法
        /// </summary>
        /// <param name="taskId">The taskId<see cref="Guid"/></param>
        /// <param name="reviewerId">The reviewerId<see cref="Guid"/></param>
        /// <param name="notes">The notes<see cref="string"/></param>
        /// <param name="action">The action<see cref="int"/></param>
        /// <returns>The <see cref="Task"/></returns>
        Task ApproveAsync(Guid taskId, Guid reviewerId, string notes, int action);


        /// <summary>
        /// The GetTaskList 获取用户提交或审核的任务列表，方便前端展示
        /// </summary>
        /// <param name="userId">The userId<see cref="Guid"/></param>
        /// <returns>The <see cref="Task{List{SubmitReviewRequest}}"/></returns>
        Task<List<TaskDtos>> GetTaskList(Guid userId);

        /// <summary>
        /// The GetAll 获取所有待审核的任务列表，供管理员审核使用
        /// </summary>
        /// <param name="userId">The userId<see cref="Guid"/></param>
        /// <returns>The <see cref="Task{List{TaskDtos}}"/></returns>
        Task<List<TaskDtos>> GetAll(Guid userId);
    }

    /// <summary>
    /// Defines the <see cref="ReviewService" />
    /// </summary>
    public class ReviewService : IReviewService
    {
        /// <summary>
        /// Defines the _db
        /// </summary>
        private readonly GenealogyDbContext _db;

        /// <summary>
        /// Defines the _handler
        /// </summary>
        private readonly IHandleTasks _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReviewService"/> class.
        /// </summary>
        /// <param name="db">The db<see cref="GenealogyDbContext"/></param>
        /// <param name="logger">The logger<see cref="ILogger{ReviewService}"/></param>
        /// <param name="handleTasks">The handleTasks<see cref="IHandleTasks"/></param>
        public ReviewService(GenealogyDbContext db, ILogger<ReviewService> logger, IHandleTasks handleTasks)
        {
            _db = db;
            _handler = handleTasks;
        }

        /// <summary>
        /// 1. 统一提交入口：支持所有业务申请
        /// </summary>
        /// <param name="dto">The dto<see cref="SubmitReviewRequest"/></param>
        /// <param name="submitterId">The submitterId<see cref="Guid"/></param>
        /// <returns>The <see cref="Task{Guid}"/></returns>
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
        /// <param name="taskId">The taskId<see cref="Guid"/></param>
        /// <param name="reviewerId">The reviewerId<see cref="Guid"/></param>
        /// <param name="notes">The notes<see cref="string"/></param>
        /// <param name="action">The action<see cref="int"/></param>
        /// <returns>The <see cref="Task"/></returns>
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
                        await _handler.HandleApplyAdminAsync(task, action);
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

        /// <summary>
        /// The GetTaskList 获取用户提交或审核的任务列表，方便前端展示
        /// </summary>
        /// <param name="userId">The userId<see cref="Guid"/></param>
        /// <returns>The <see cref="Task{List{SubmitReviewRequest}}"/></returns>
        public Task<List<TaskDtos>> GetTaskList(Guid userId)
        {
            var user = _db.Users.FirstOrDefaultAsync(u => u.UserID == userId && u.UserStatus == 1);
            if (user == null)
                throw new Exception("用户不存在");

            var tasks = _db.ReviewTasks
                 .Where(t => t.SubmitterID == userId || t.ReviewerID == userId)
                 .Select(t => new TaskDtos
                 {
                     TaskId = t.TaskID,
                     SubmitterName = t.Submitter.Username,
                     ActionName = t.ActionCode, // 可以通过映射关系获取更友好的名称
                     ChangeData = t.ChangeData, // 前端根据 ActionCode 解析显示
                     Reason = t.ApplyReason,
                     Status = t.Status == 0 ? "待审核" : (t.Status == 1 ? "审核通过" : "审核驳回"),
                     ReviewName = t.Reviewer.Username == null ? "待分配" : t.Reviewer.Username,
                     ReviewNotes = t.ReviewNotes ?? "无",
                     CreateTime = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                     ProcessTime = t.ProcessedAt != null ? t.ProcessedAt.Value.ToString("yyyy-MM-dd HH:mm") : "待处理"
                 })
                 .ToList();
            return Task.FromResult(tasks);
        }

        /// <summary>
        /// The GetAll 获取所有待审核的任务列表，供管理员审核使用
        /// </summary>
        /// <param name="userId">The userId<see cref="Guid"/></param>
        /// <returns>The <see cref="Task{List{TaskDtos}}"/></returns>
        public async Task<List<TaskDtos>> GetAll(Guid userId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserID == userId && u.UserStatus == 1);
            if (user == null)
                throw new Exception("用户不存在");
            if (user.RoleType != 0)
                throw new Exception("无权限访问");

            var tasks = _db.ReviewTasks
                 .Where(t => t.Status == 0)
                 .Select(t => new TaskDtos
                 {
                     TaskId = t.TaskID,
                     SubmitterName = t.Submitter.Username,
                     ActionName = t.ActionCode, // 可以通过映射关系获取更友好的名称
                     ChangeData = t.ChangeData, // 前端根据 ActionCode 解析显示
                     Reason = t.ApplyReason,
                     Status = t.Status == 0 ? "待审核" : (t.Status == 1 ? "审核通过" : "审核驳回"),
                     ReviewName = t.Reviewer.Username == null ? "待分配" : t.Reviewer.Username,
                     ReviewNotes = t.ReviewNotes ?? "无",
                     CreateTime = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                     ProcessTime = t.ProcessedAt != null ? t.ProcessedAt.Value.ToString("yyyy-MM-dd HH:mm") : "待处理"
                 })
                 .ToList();
            return tasks;
        }
    }

}
