namespace 家谱.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Claims;
    using 家谱.Models.DTOs;
    using 家谱.Models.Entities;
    using 家谱.Services;

    [Authorize] // 必须登录
    [ApiController]
    [Route("api/[controller]")]
    public class GenoTreeController : ControllerBase
    {
        private readonly IGenoTreeService _treeService;

        public GenoTreeController(IGenoTreeService treeService)
        {
            _treeService = treeService;
        }

        // 辅助方法：从 Token 中安全获取用户 ID

        private Guid GetCurrentUserId()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Guid.Empty;
            return Guid.Parse(userIdStr);
        }

        [HttpPost("Add")]
        public async Task<IActionResult> Create([FromBody] GenoTreeDtos dto)
        {
            var userId = GetCurrentUserId();
            if (dto == null)
                throw new ArgumentNullException(nameof(dto), "请求体不能为空");
            dto.Owner = userId; // 强制绑定当前用户为树的拥有者
            var success = await _treeService.CreateAsync(dto);

            if (!success) return BadRequest(new { message = "创建失败" });
            return Ok(new { message = "家族树创建成功" });
        }

        [HttpGet("my-trees")]
        public async Task<IActionResult> GetMyTrees()
        {
            var userId = GetCurrentUserId();
            var trees = await _treeService.GetByOwner(userId);
            return Ok(trees);
        }

        [AllowAnonymous] // 允许不登录查看公开列表
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAllPublic()
        {
            if (User == null || !User.Identity.IsAuthenticated || byte.Parse(User.FindFirst(ClaimTypes.Role)!.Value) > 2)
                throw new UnauthorizedAccessException("无权限访问此资源");
            //只允许管理员访问公开列表，普通用户无法访问
            // 你现有的 GetAll 是获取所有，通常建议在这里过滤 IsPublic == true
            var trees = await _treeService.GetAll();
            return Ok(trees);
        }

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var tree = await _treeService.GetByIdAsync(id);
            if (tree == null) return NotFound(new { message = "未找到家族树" });

            // 权限检查：如果是私有的且不是本人
            if (!tree.IsPublic && tree.OwnerID != GetCurrentUserId())
            {
                return Forbid(); // 或返回 404 隐藏存在性
            }

            return Ok(tree);
        }

        [HttpPut("Update/{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] GenoTreeDtos dto)
        {
            // 业务安全性：先检查这棵树是不是你的
            var existingTree = await _treeService.GetByIdAsync(id);
            if (existingTree == null) return NotFound();
            if (existingTree.OwnerID != GetCurrentUserId()) return Forbid();

            var success = await _treeService.UpdateAsync(dto, id);
            return success ? Ok(new { message = "更新成功" }) : BadRequest();
        }

        [HttpDelete("Del/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            // 业务安全性：先检查归属权
            var existingTree = await _treeService.GetByIdAsync(id);
            if (existingTree == null) return NotFound();
            if (existingTree.OwnerID != GetCurrentUserId()) return Forbid();

            var success = await _treeService.DeleteAsync(id);
            return success ? Ok(new { message = "删除成功" }) : BadRequest();
        }
    }
}
