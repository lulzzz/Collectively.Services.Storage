﻿using System.Threading.Tasks;
using Collectively.Messages.Events;
using Collectively.Common.Services;
using Collectively.Messages.Events.Remarks;
using Collectively.Services.Storage.Models.Remarks;
using Collectively.Services.Storage.Repositories;
using Collectively.Services.Storage.ServiceClients;
using Collectively.Common.Caching;
using Collectively.Services.Storage.Services;

namespace Collectively.Services.Storage.Handlers
{
    public class RemarkCreatedHandler : IEventHandler<RemarkCreated>
    {
        private readonly IHandler _handler;
        private readonly IRemarkRepository _remarkRepository;
        private readonly IRemarkServiceClient _remarkServiceClient;
        private readonly IRemarkCache _remarkCache;
        private readonly IUserCache _userCache;

        public RemarkCreatedHandler(IHandler handler, 
            IRemarkRepository remarkRepository,
            IRemarkServiceClient remarkServiceClient,
            IRemarkCache remarkCache,
            IUserCache userCache)
        {
            _handler = handler;
            _remarkRepository = remarkRepository;
            _remarkServiceClient = remarkServiceClient;
            _remarkCache = remarkCache;
            _userCache = userCache;
        }

        public async Task HandleAsync(RemarkCreated @event)
        {
            await _handler
                .Run(async () =>
                {
                    var remark = await _remarkServiceClient.GetAsync<Remark>(@event.RemarkId);
                    remark.Value.Status = null;
                    await _remarkRepository.AddAsync(remark.Value);
                    await _remarkCache.AddAsync(remark.Value, addGeo: true, addLatest: true);
                    await _userCache.AddRemarkAsync(remark.Value.Author.UserId, @event.RemarkId);
                })
                .OnError((ex, logger) =>
                {
                    logger.Error(ex, $"Error occured while handling {@event.GetType().Name} event");
                })
                .ExecuteAsync();
        }
    }
}