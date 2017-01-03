﻿using System.IO;
using System.Threading.Tasks;
using Coolector.Common.Queries;
using Coolector.Common.Security;
using Coolector.Common.Types;

namespace Coolector.Services.Storage.Services
{
    public interface IServiceClient
    {
        void SetSettings(ServiceSettings serviceSettings);
        Task<Maybe<T>> GetAsync<T>(string url, string endpoint) 
            where T : class;
        Task<Maybe<Stream>> GetStreamAsync(string url, string endpoint);
        Task<Maybe<PagedResult<T>>> GetCollectionAsync<T>(string url, string endpoint) 
            where T : class;
        Task<Maybe<PagedResult<TResult>>> GetFilteredCollectionAsync<TQuery,TResult>(TQuery query, 
            string url, string endpoint)
            where TResult : class where TQuery : class, IPagedQuery;
    }
}