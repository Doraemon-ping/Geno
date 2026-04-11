using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Entities;
using 家谱.Models.Enums;

namespace 家谱.Services
{
    public interface ISpacePostService
    {
        Task<List<SpacePostViewDto>> GetByTreeAsync(Guid treeId);

        Task<SpacePostViewDto> CreateAsync(SpacePostCreateDto dto, Guid userId);

        Task<SpacePostViewDto?> UpdateAsync(Guid postId, SpacePostUpdateDto dto, Guid userId);

        Task<bool> DeleteAsync(Guid postId, Guid userId);
    }

    public class SpacePostService : ISpacePostService
    {
        private readonly GenealogyDbContext _db;
        private readonly IMediaFileService _mediaFileService;

        public SpacePostService(GenealogyDbContext db, IMediaFileService mediaFileService)
        {
            _db = db;
            _mediaFileService = mediaFileService;
        }

        public async Task<List<SpacePostViewDto>> GetByTreeAsync(Guid treeId)
        {
            var posts = await _db.SpacePosts
                .AsNoTracking()
                .Where(item => item.TreeID == treeId)
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync();

            return await BuildViewAsync(posts);
        }

        public async Task<SpacePostViewDto> CreateAsync(SpacePostCreateDto dto, Guid userId)
        {
            var content = (dto.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("帖子内容不能为空");
            }

            var entity = new GenoSpacePost
            {
                PostID = Guid.NewGuid(),
                TreeID = dto.TreeId,
                UserID = userId,
                PostTitle = string.IsNullOrWhiteSpace(dto.PostTitle) ? null : dto.PostTitle.Trim(),
                Content = content,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDel = false
            };

            _db.SpacePosts.Add(entity);
            await _db.SaveChangesAsync();
            await _mediaFileService.SyncPostMediaAsync(entity.PostID, entity.TreeID, dto.MediaIds, userId);

            return (await BuildViewAsync(new List<GenoSpacePost> { entity })).Single();
        }

        public async Task<SpacePostViewDto?> UpdateAsync(Guid postId, SpacePostUpdateDto dto, Guid userId)
        {
            var content = (dto.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("帖子内容不能为空");
            }

            var post = await _db.SpacePosts.FirstOrDefaultAsync(item => item.PostID == postId);
            if (post == null)
            {
                return null;
            }

            post.PostTitle = string.IsNullOrWhiteSpace(dto.PostTitle) ? null : dto.PostTitle.Trim();
            post.Content = content;
            post.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await _mediaFileService.SyncPostMediaAsync(post.PostID, post.TreeID, dto.MediaIds, userId);

            return (await BuildViewAsync(new List<GenoSpacePost> { post })).Single();
        }

        public async Task<bool> DeleteAsync(Guid postId, Guid userId)
        {
            var post = await _db.SpacePosts.FirstOrDefaultAsync(item => item.PostID == postId);
            if (post == null)
            {
                return false;
            }

            post.IsDel = true;
            post.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _mediaFileService.SoftDeleteByOwnerAsync("post", post.PostID, userId);
            return true;
        }

        private async Task<List<SpacePostViewDto>> BuildViewAsync(IReadOnlyCollection<GenoSpacePost> posts)
        {
            if (posts.Count == 0)
            {
                return new List<SpacePostViewDto>();
            }

            var userIds = posts.Select(item => item.UserID).Distinct().ToList();
            var postIds = posts.Select(item => item.PostID).Distinct().ToList();
            var users = await BuildGuidPredicateQuery(_db.Users.AsNoTracking(), user => user.UserID, userIds)
                .ToDictionaryAsync(user => user.UserID, user => user.Username);
            var avatarUrls = await BuildGuidPredicateQuery(_db.Users.AsNoTracking(), user => user.UserID, userIds)
                .ToDictionaryAsync(user => user.UserID, user => user.AvatarUrl);

            var mediaFiles = await BuildGuidPredicateQuery(
                    _db.MediaFiles.AsNoTracking().Where(item => item.OwnerType == "post" && item.OwnerID.HasValue && item.Status == (byte)MediaFileStatus.Approved),
                    item => item.OwnerID!.Value,
                    postIds)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.CreatedAt)
                .ToListAsync();

            var mediaGroups = mediaFiles
                .GroupBy(item => item.OwnerID!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => new MediaFileDto
                    {
                        MediaId = item.MediaID,
                        TreeId = item.TreeID,
                        FileName = item.FileName,
                        FileExt = item.FileExt,
                        MimeType = item.MimeType,
                        FileSize = item.FileSize,
                        PublicUrl = item.PublicUrl,
                        Caption = item.Caption,
                        SortOrder = item.SortOrder,
                        Status = item.Status,
                        StatusName = ReviewActions.GetMediaStatusDisplayName(item.Status),
                        CreatedAt = item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    }).ToList());

            return posts.Select(item => new SpacePostViewDto
            {
                PostId = item.PostID,
                TreeId = item.TreeID,
                UserId = item.UserID,
                Username = users.TryGetValue(item.UserID, out var username) ? username : "家族成员",
                AvatarUrl = avatarUrls.TryGetValue(item.UserID, out var avatarUrl) ? avatarUrl : null,
                PostTitle = item.PostTitle,
                Content = item.Content,
                CreatedAt = item.CreatedAt,
                MediaFiles = mediaGroups.TryGetValue(item.PostID, out var media) ? media : new List<MediaFileDto>()
            }).ToList();
        }

        private static IQueryable<TEntity> BuildGuidPredicateQuery<TEntity>(
            IQueryable<TEntity> source,
            Expression<Func<TEntity, Guid>> selector,
            IReadOnlyCollection<Guid> ids)
        {
            if (ids.Count == 0)
            {
                return source.Where(_ => false);
            }

            var parameter = selector.Parameters[0];
            Expression? body = null;
            foreach (var id in ids)
            {
                var equals = Expression.Equal(selector.Body, Expression.Constant(id));
                body = body == null ? equals : Expression.OrElse(body, equals);
            }

            return source.Where(Expression.Lambda<Func<TEntity, bool>>(body!, parameter));
        }
    }
}
