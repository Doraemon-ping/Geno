using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using 家谱.DB;
using 家谱.Models.DTOs;
using 家谱.Models.Entities;

namespace 家谱.Services
{
    public interface ICommentService
    {
        Task<List<CommentViewDto>> GetByOwnerAsync(string ownerType, Guid ownerId);

        Task<CommentViewDto> CreateAsync(CommentCreateDto dto, Guid userId);

        Task<bool> DeleteAsync(Guid commentId, Guid userId);
    }

    public class CommentService : ICommentService
    {
        private readonly GenealogyDbContext _db;

        public CommentService(GenealogyDbContext db)
        {
            _db = db;
        }

        public async Task<List<CommentViewDto>> GetByOwnerAsync(string ownerType, Guid ownerId)
        {
            var normalizedOwnerType = NormalizeOwnerType(ownerType);
            var comments = await _db.GenoComments
                .AsNoTracking()
                .Where(item => item.OwnerType == normalizedOwnerType && item.OwnerID == ownerId)
                .OrderBy(item => item.CreatedAt)
                .ToListAsync();

            return await BuildViewAsync(comments);
        }

        public async Task<CommentViewDto> CreateAsync(CommentCreateDto dto, Guid userId)
        {
            var content = (dto.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("评论内容不能为空");
            }

            var entity = new GenoComment
            {
                CommentID = Guid.NewGuid(),
                TreeID = dto.TreeId == Guid.Empty ? null : dto.TreeId,
                OwnerType = NormalizeOwnerType(dto.OwnerType),
                OwnerID = dto.OwnerId,
                ParentCommentID = dto.ParentCommentId == Guid.Empty ? null : dto.ParentCommentId,
                UserID = userId,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDel = false
            };

            _db.GenoComments.Add(entity);
            await _db.SaveChangesAsync();

            return (await BuildViewAsync(new List<GenoComment> { entity })).Single();
        }

        public async Task<bool> DeleteAsync(Guid commentId, Guid userId)
        {
            var comment = await _db.GenoComments.FirstOrDefaultAsync(item => item.CommentID == commentId);
            if (comment == null)
            {
                return false;
            }

            comment.IsDel = true;
            comment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        private async Task<List<CommentViewDto>> BuildViewAsync(IReadOnlyCollection<GenoComment> comments)
        {
            if (comments.Count == 0)
            {
                return new List<CommentViewDto>();
            }

            var userIds = comments.Select(item => item.UserID).Distinct().ToList();
            var users = await BuildGuidPredicateQuery(_db.Users.AsNoTracking(), user => user.UserID, userIds)
                .ToDictionaryAsync(user => user.UserID, user => user.Username);
            var avatarUrls = await BuildGuidPredicateQuery(_db.Users.AsNoTracking(), user => user.UserID, userIds)
                .ToDictionaryAsync(user => user.UserID, user => user.AvatarUrl);

            var views = comments.Select(item => new CommentViewDto
            {
                CommentId = item.CommentID,
                TreeId = item.TreeID,
                OwnerType = item.OwnerType,
                OwnerId = item.OwnerID,
                ParentCommentId = item.ParentCommentID,
                UserId = item.UserID,
                Username = users.TryGetValue(item.UserID, out var username) ? username : "家族成员",
                AvatarUrl = avatarUrls.TryGetValue(item.UserID, out var avatarUrl) ? avatarUrl : null,
                Content = item.Content,
                CreatedAt = item.CreatedAt
            }).ToList();

            var map = views.ToDictionary(item => item.CommentId);
            var roots = new List<CommentViewDto>();
            foreach (var view in views)
            {
                if (view.ParentCommentId.HasValue && map.TryGetValue(view.ParentCommentId.Value, out var parent))
                {
                    parent.Replies.Add(view);
                }
                else
                {
                    roots.Add(view);
                }
            }

            return roots;
        }

        private static string NormalizeOwnerType(string ownerType)
        {
            return string.IsNullOrWhiteSpace(ownerType) ? "event" : ownerType.Trim().ToLowerInvariant();
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
