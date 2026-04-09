namespace 家谱.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ApiExplorer;
    using Microsoft.EntityFrameworkCore;
    using 家谱.DB;
    using 家谱.Models.DTOs.Common;
    using 家谱.Services;

    /// <summary>
    /// Defines the <see cref="TaskController" />
    /// 获取审核任务以及处理审核任务的接口，供前端调用
    /// </summary>
    [Authorize]// 核心：要求用户必须登录才能访问这个控制器的任何方法
    [ApiController]
    [Route("api/[controller]")]
    public class TaskController : ControllerBase
    {
        /// <summary>
        /// Defines the _reviewService
        /// </summary>
        private readonly IReviewService _reviewService;


        /// <summary>
        /// Initializes a new instance of the <see cref="TaskController"/> class.
        /// </summary>
        /// <param name="reviewService">The reviewService<see cref="IReviewService"/></param>
        /// <param name="dbContext">The dbContext<see cref="GenealogyDbContext"/></param>
        public TaskController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }


        [HttpGet("my-tasks")]
        public async Task<ActionResult> GetTasks()
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            var tasks = await _reviewService.GetTaskList(userId);
            return Ok(ApiResponse.OK(tasks));
        }

        [HttpPost("get-all")]
        public async Task<ActionResult> GetAllTasks()
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            var tasks = await _reviewService.GetAll(userId);
            return Ok(ApiResponse.OK(tasks));
        }
    }
}
