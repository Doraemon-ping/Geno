namespace 家谱.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Claims;
    using 家谱.Middleware;
    using 家谱.Models.DTOs;
    using 家谱.Models.DTOs.Common;
    using 家谱.Models.Entities;
    using 家谱.Services;

    [Authorize] // 必须登录才能访问此控制器下的所有接口
    [ApiController]
    [Route("api/[controller]")]
    public class PoemController : ControllerBase
    {
        private readonly IGenoPoemService _poemService;

        private readonly IGenoTreeService _genoTreeService;

        public PoemController(IGenoPoemService poemService, IGenoTreeService genoTreeService)
        {
            _poemService = poemService;
            _genoTreeService = genoTreeService;
        }

        [HttpGet("tree/{treeId}")]
        public async Task<IActionResult> GetList(Guid treeId)
        {
            var tree = await _genoTreeService.GetByIdAsync(treeId);
            if (tree == null) return NotFound(new ErrorResponse { Code = 404, Message = "家谱树不存在" });
            // 权限检查：如果是私有的且不是本人，或者用户角色不够
            if (tree.OwnerID != Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value)
                && !tree.IsPublic
                && byte.Parse(User.FindFirst(ClaimTypes.Role)!.Value) > 2)
                throw new UnauthorizedAccessException("无权限访问此资源");
            var list = await _poemService.GetByTreeIdAsync(treeId);
            return Ok(new ApiResponse { Code = 200, Message = "获取成功", Data = list });
        }

        [HttpPost("Add")]
        // 如果你有角色管理，可以写成 [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Add([FromBody] PoemDto dto)
        {
            var tree = await _genoTreeService.GetByIdAsync(dto.TreeId);
            if (tree == null) return NotFound(new ErrorResponse { Code = 404, Message = "家谱树不存在" });
            // 权限检查：必须是树的拥有者或者管理员才能修改
            if (tree.OwnerID != Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value)
                && byte.Parse(User.FindFirst(ClaimTypes.Role)!.Value) > 2)
                throw new UnauthorizedAccessException("无权限访问此资源");
            await _poemService.CreateAsync(dto);
            return Ok(ApiResponse.OK());
        }

        [HttpPut("Update")]
        public async Task<IActionResult> Update([FromBody] PoemDto dto, Guid poemId)

        {
            var tree = await _genoTreeService.GetByIdAsync(dto.TreeId);

            if (tree == null) return NotFound(new ErrorResponse { Code = 404, Message = "家谱树不存在" });
            // 权限检查：必须是树的拥有者或者管理员才能修改
            if (tree.OwnerID != Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value)
                && byte.Parse(User.FindFirst(ClaimTypes.Role)!.Value) > 2)
                throw new UnauthorizedAccessException("无权限访问此资源");
            //不允许修改树ID，必须在同一棵树内修改
            var poem = await _poemService.GetByIdAsync(poemId);
            if (poem == null) return NotFound(new ErrorResponse { Code = 404, Message = "字辈不存在" });
            if (poem.TreeID != dto.TreeId)
                return BadRequest(new ErrorResponse { Code = 400, Message = "不允许修改树ID，必须在同一棵树内修改" });
            // 其他字段的修改权限已经在服务层检查了，这里就不重复检查了
            await _poemService.UpdateAsync(dto, poemId);
            return Ok(ApiResponse.OK());
        }

        [HttpDelete("Del/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var poem = await _poemService.GetByIdAsync(id);
            if (poem == null) return NotFound(new ErrorResponse { Code = 404, Message = "字辈不存在" });
            var tree = await _genoTreeService.GetByIdAsync(poem.TreeID);
            if (tree == null) return NotFound(new ErrorResponse { Code = 404, Message = "家谱树不存在" });
            // 权限检查：必须是树的拥有者或者管理员才能修改
            if (tree.OwnerID != Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value)
                && byte.Parse(User.FindFirst(ClaimTypes.Role)!.Value) > 2)
                throw new UnauthorizedAccessException("无权限访问此资源");
            await _poemService.DeleteAsync(id);
            return Ok(new ApiResponse { Code = 200, Message = "字辈删除成功" });
        }
    }
}
