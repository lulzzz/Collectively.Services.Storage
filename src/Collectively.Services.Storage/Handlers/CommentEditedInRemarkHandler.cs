using System;
using System.Linq;
using System.Threading.Tasks;
using Collectively.Common.Caching;
using Collectively.Common.Services;
using Collectively.Messages.Events;
using Collectively.Messages.Events.Remarks;
using Collectively.Services.Storage.Models.Remarks;
using Collectively.Services.Storage.Repositories;
using Collectively.Services.Storage.Services;

namespace Collectively.Services.Storage.Handlers
{
    public class CommentEditedInRemarkHandler : IEventHandler<CommentEditedInRemark>
    {
        private readonly IHandler _handler;
        private readonly IRemarkRepository _repository;
        private readonly IRemarkCache _cache;

        public CommentEditedInRemarkHandler(IHandler handler, 
            IRemarkRepository repository,
            IRemarkCache cache)
        {
            _handler = handler;
            _repository = repository;
            _cache = cache;
        }

        public async Task HandleAsync(CommentEditedInRemark @event)
        {
            await _handler
                .Run(async () =>
                {
                    var remark = await _repository.GetByIdAsync(@event.RemarkId);
                    if (remark.HasNoValue)
                    {
                        return;
                    }

                    var comment = remark.Value.Comments.SingleOrDefault(x => x.Id == @event.CommentId);
                    if(comment == null)
                    {
                        return;
                    }
                    comment.Text = @event.Text;
                    comment.History.Add(new CommentHistory
                    {
                        Text = comment.Text,
                        CreatedAt = @event.CreatedAt
                    });
                    remark.Value.UpdatedAt = DateTime.UtcNow;
                    await _repository.UpdateAsync(remark.Value);
                    await _cache.AddAsync(remark.Value);
                })
                .OnError((ex, logger) =>
                {
                    logger.Error(ex, $"Error occured while handling {@event.GetType().Name} event");
                })
                .ExecuteAsync();
        }
    }
}