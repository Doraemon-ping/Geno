namespace 家谱.Services
{
    using Microsoft.EntityFrameworkCore;
    using System;
    using Volo.Abp;
    using 家谱.DB;
    using 家谱.Middleware;
    using 家谱.Models.DTOs;
    using 家谱.Models.Entities;

    public interface IAuditService
    {
        #region 申请端 (用户操作)

        /// <summary>
        /// 发起一项审核申请 (身份认领、权限提升、信息修改)
        /// </summary>
        /// <param name="userId">当前登录用户ID</param>
        /// <param name="request">申请数据 DTO</param>
        /// <exception cref="BusinessException">当存在重复申请或参数非法时抛出</exception>
        Task SubmitApplyAsync(Guid userId, AuditApplyRequest request);

        /// <summary>
        /// 获取当前用户的所有申请记录 (包含历史和待审)
        /// </summary>
        /// <param name="userId">当前登录用户ID</param>
        /// <returns>统一的审核任务响应列表</returns>
        Task<List<AuditTaskResponse>> GetMyAuditHistoryAsync(Guid userId);

        /// <summary>
        /// 用户撤回自己的待审核申请
        /// </summary>
        /// <param name="userId">当前用户ID（用于校验所有权）</param>
        /// <param name="serialNo">业务流水号</param>
        Task CancelMyApplyAsync(Guid userId, string serialNo);

        #endregion

        #region 管理端 (管理员操作)

        /// <summary>
        /// 获取所有待处理的审核任务 (Status = 0)
        /// </summary>
        /// <returns>待办任务列表</returns>
        Task<List<AuditTaskResponse>> GetAdminPendingListAsync();

        /// <summary>
        /// 获取已处理的历史审核任务 (Status = 1 或 2)
        /// </summary>
        /// <param name="page">页码</param>
        /// <param name="pageSize">每页条数</param>
        Task<List<AuditTaskResponse>> GetAdminHistoryListAsync(int page = 1, int pageSize = 20);

        /// <summary>
        /// 执行审批操作（准许或驳回）
        /// </summary>
        /// <param name="adminId">当前处理的管理员ID</param>
        /// <param name="request">处理指令 DTO</param>
        /// <exception cref="BusinessException">当流水号无效或任务已被处理时抛出</exception>
        Task HandleTaskAsync(Guid adminId, HandleAuditRequest request);

        #endregion


    }

    public class AuditService : IAuditService
    {
        private readonly GenealogyDbContext _db;

        public AuditService(GenealogyDbContext db) => _db = db;

        public async Task CancelMyApplyAsync(Guid userId, string serialNo)
        {
            Guid guid = Guid.Parse(serialNo);
            var user = _db.Users.Find(userId);
            if (user == null) throw new BusinessException("用户不存在");
            var request = _db.AuditRequests.FirstOrDefault(r => r.Id == guid);
            if (request == null) throw new BusinessException("申请不存在");
            if (request.ApplicantId != userId) throw new BusinessException("只能撤回自己的申请");
            if (request.Status != 0) throw new BusinessException("只能撤回待审核的申请");
            request.Status = 3; // 3 = 已撤回
            request.ProcessedAt = DateTime.UtcNow;
            request.AuditRemark = "用户撤回申请";
            request.ProcessId = userId;
            await _db.SaveChangesAsync();
        }



        public async Task<List<AuditTaskResponse>> GetAdminHistoryListAsync(Guid userId, int page = 1, int pageSize = 20)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) throw new BusinessException("用户不存在");
            if (user.RoleType > 2) throw new BusinessException("无权限访问此资源");
            // 1. 在 IQueryable 阶段只做数据库能理解的事：筛选、排序、分页、关联查询
            var rawData = await _db.AuditRequests
                .Include(r => r.Applicant)      // 预加载申请人
                .Include(r => r.TargetMember)
                .Include(r => r.Process)
                .Where(r => r.Status != 0)      // 只查询已处理的
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(); // 执行到这里，数据已经从 SQL 变回了内存中的 List<AuditRequest>

            // 2. 在内存中进行复杂的 DTO 转换，此时可以使用所有 C# 语法
            return rawData.Select(r => new AuditTaskResponse
            {
                SerialNo = r.Id.ToString(),
                ApplicantName = r.Applicant?.Username ?? "未知用户",
                CategoryName = r.Category switch
                {
                    1 => "身份认领",
                    2 => "权限申请",
                    _ => "未知类别"
                },
                TargetDescription = r.Category switch
                {
                    1 => r.TargetMember != null
                         ? $"认领：{r.TargetMember.LastName}{r.TargetMember.FirstName} (第{r.TargetMember.GenerationNum}代)"
                         : "认领目标已丢失",
                    2 => $"申请提升至权限层级：{r.PayloadJson}", // 如果 PayloadJson 是 JSON，建议之后反序列化
                    _ => "其他业务"
                },
                Reason = r.Reason,
                Status = r.Status,
                StatusText = r.Status switch
                {
                    0 => "待审核",
                    1 => "已准许",
                    2 => "已驳回",
                    3 => "已撤回",
                    _ => "未知状态"
                },
                AuditRemark = r.AuditRemark,
                // 在内存中可以自由使用 ToString 格式化日期
                CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                ProcessedAt = r.ProcessedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"
            }).ToList();
        }

        public async Task<List<AuditTaskResponse>> GetAdminPendingListAsync(Guid userId, int page = 1, int pageSize = 20)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) throw new BusinessException("用户不存在");
            if (user.RoleType > 2) throw new BusinessException("无权限访问此资源");
            // 1. 在 IQueryable 阶段只做数据库能理解的事：筛选、排序、分页、关联查询
            var rawData = await _db.AuditRequests
                .Include(r => r.Applicant)      // 预加载申请人
                .Include(r => r.TargetMember)
                .Include(r => r.Process)
                .Where(r => r.Status == 0)      // 只查询待审核的
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(); // 执行到这里，数据已经从 SQL 变回了内存中的 List<AuditRequest>

            // 2. 在内存中进行复杂的 DTO 转换，此时可以使用所有 C# 语法
            return rawData.Select(r => new AuditTaskResponse
            {
                SerialNo = r.Id.ToString(),
                ApplicantName = r.Applicant?.Username ?? "未知用户",
                CategoryName = r.Category switch
                {
                    1 => "身份认领",
                    2 => "权限申请",
                    _ => "未知类别"
                },
                TargetDescription = r.Category switch
                {
                    1 => r.TargetMember != null
                         ? $"认领：{r.TargetMember.LastName}{r.TargetMember.FirstName} (第{r.TargetMember.GenerationNum}代)"
                         : "认领目标已丢失",
                    2 => $"申请提升至权限层级：{r.PayloadJson}", // 如果 PayloadJson 是 JSON，建议之后反序列化
                    _ => "其他业务"
                },
                Reason = r.Reason,
                Status = r.Status,
                StatusText = r.Status switch
                {
                    0 => "待审核",
                    1 => "已准许",
                    2 => "已驳回",
                    3 => "已撤回",
                    _ => "未知状态"
                },
                AuditRemark = r.AuditRemark,
                // 在内存中可以自由使用 ToString 格式化日期
                CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                ProcessedAt = r.ProcessedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"
            }).ToList();

        }

        public async Task<List<AuditTaskResponse>> GetMyAuditHistoryAsync(Guid userId, int page = 1, int pageSize = 20)
        {
            // 1. 查询数据库中当前用户的所有申请记录，包含关联的申请人和目标成员信息
            var user = await _db.Users.FindAsync(userId);
            if (user == null) throw new BusinessException("用户不存在");
            // 1. 在 IQueryable 阶段只做数据库能理解的事：筛选、排序、分页、关联查询
            var rawData = await _db.AuditRequests
                .Include(r => r.Applicant)      // 预加载申请人
                .Include(r => r.TargetMember)
                .Include(r => r.Process)
                .Where(r => r.ApplicantId == userId)     // 只查询当前用户的申请记录
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(); // 执行到这里，数据已经从 SQL 变回了内存中的 List<AuditRequest>

            // 2. 在内存中进行复杂的 DTO 转换，此时可以使用所有 C# 语法
            return rawData.Select(r => new AuditTaskResponse
            {
                SerialNo = r.Id.ToString(),
                ApplicantName = r.Applicant?.Username ?? "未知用户",
                CategoryName = r.Category switch
                {
                    1 => "身份认领",
                    2 => "权限申请",
                    _ => "未知类别"
                },
                TargetDescription = r.Category switch
                {
                    1 => r.TargetMember != null
                         ? $"认领：{r.TargetMember.LastName}{r.TargetMember.FirstName} (第{r.TargetMember.GenerationNum}代)"
                         : "认领目标已丢失",
                    2 => $"申请提升至权限层级：{r.PayloadJson}", // 如果 PayloadJson 是 JSON，建议之后反序列化
                    _ => "其他业务"
                },
                Reason = r.Reason,
                Status = r.Status,
                StatusText = r.Status switch
                {
                    0 => "待审核",
                    1 => "已准许",
                    2 => "已驳回",
                    3 => "已撤回",
                    _ => "未知状态"
                },
                AuditRemark = r.AuditRemark,
                // 在内存中可以自由使用 ToString 格式化日期
                CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                ProcessedAt = r.ProcessedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"
            }).ToList();



        }

        public Task HandleTaskAsync(Guid adminId, HandleAuditRequest request)
        {
            var user = _db.Users.Find(adminId);
            if (user == null) throw new BusinessException("用户不存在");
            if (user.RoleType > 2) throw new BusinessException("无权限访问此资源");

            Guid apply = Guid.Parse(request.SerialNo);
            var auditRequest = _db.AuditRequests.FirstOrDefault(r => r.Id == apply);
            if (auditRequest == null) throw new BusinessException("申请不存在");
            if (auditRequest.Status != 0) throw new BusinessException("该申请已被处理");



        }





        //发起申请的逻辑比较简单，
        public Task SubmitApplyAsync(Guid userId, AuditApplyRequest request)
        {

        }

        private async Task<bool> ProcessApprovalAsync(AuditRequest auditRequest, int action)
        {
            if (action == 1)// 准许
            {
                switch (auditRequest.Category)
                {
                    case 1: // 身份认领
                        // 1. 将 TargetMemberId 对应的成员与申请人绑定（示例逻辑，实际可能更复杂）
                        var json = auditRequest.PayloadJson; // 假设 PayloadJson 中存了新的姓名等信息
                                                             //从json中解析出新的姓名等信息（示例，实际应更严谨地处理 JSON 结构）
                        var newName = json; // 简化示例，实际应解析 JSON 获取具体字段

                        break;
                    case 2: // 权限申请
                        // 1. 根据 PayloadJson 中的权限层级信息提升用户权限（示例逻辑）
                        var user = await _db.Users.FindAsync(auditRequest.ApplicantId);
                        if (user == null) return false;
                        user.RoleType = int.Parse(auditRequest.PayloadJson); // 简化示例，实际应更严谨地处理权限层级
                        break;
                    default:
                        return false; // 未知类别
                }
            }
            else if (action == 2) // 驳回
            {
                return false; // 驳回操作不需要修改业务数据，只更新审核记录状态即可
            }
            else
            {
                return false; // 无效操作
            }

        }
    }


}
