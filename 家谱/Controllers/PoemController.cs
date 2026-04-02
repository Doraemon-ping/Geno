namespace 家谱.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Claims;
    using 家谱.Middleware;
    using 家谱.Models.DTOs;
    using 家谱.Services;

    [Authorize] // 必须登录才能访问此控制器下的所有接口
    [ApiController]
    [Route("api/[controller]")]
    public class PoemController : ControllerBase
    {
        private readonly IGenoPoemService _poemService;

        public PoemController(IGenoPoemService poemService)
        {
            _poemService = poemService;
        }

        [HttpGet("tree/{treeId}")]
        public async Task<IActionResult> GetList(Guid treeId)
        {
            if (User == null || !User.Identity.IsAuthenticated || byte.Parse(User.FindFirst(ClaimTypes.Role)!.Value) > 2)
                throw new UnauthorizedAccessException("无权限访问此资源");
            var list = await _poemService.GetByTreeIdAsync(treeId);
            return Ok(new ApiResponse { Code = 200, Message = "查询成功", Data = list });
        }

        [HttpPost]
        // 如果你有角色管理，可以写成 [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Add([FromBody] PoemDto dto)
        {
            if (User == null || !User.Identity.IsAuthenticated || byte.Parse(User.FindFirst(ClaimTypes.Role)!.Value) > 2)
                throw new UnauthorizedAccessException("无权限访问此资源");
            await _poemService.CreateAsync(dto);
            return Ok(new ApiResponse { Code = 200, Message = "字辈添加成功" });
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] PoemDto dto)
        {
            await _poemService.UpdateAsync(dto);
            return Ok(new ApiResponse { Code = 200, Message = "字辈修改成功" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _poemService.DeleteAsync(id);
            return Ok(new ApiResponse { Code = 200, Message = "字辈删除成功" });
        }
    }
}
