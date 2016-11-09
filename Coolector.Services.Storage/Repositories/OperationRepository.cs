﻿using System;
using System.Threading.Tasks;
using Coolector.Common.Types;
using Coolector.Dto.Operations;
using Coolector.Services.Storage.Repositories.Queries;
using MongoDB.Driver;

namespace Coolector.Services.Storage.Repositories
{
    public class OperationRepository : IOperationRepository
    {
        private readonly IMongoDatabase _database;

        public OperationRepository(IMongoDatabase database)
        {
            _database = database;
        }

        public async Task<Maybe<OperationDto>> GetAsync(Guid requestId)
            => await _database.Operations().GetByRequestIdAsync(requestId);

        public async Task AddAsync(OperationDto operation) => await _database.Operations().InsertOneAsync(operation);

        public async Task UpdateAsync(OperationDto operation)
            => await _database.Operations().ReplaceOneAsync(x => x.Id == operation.Id, operation);
    }
}