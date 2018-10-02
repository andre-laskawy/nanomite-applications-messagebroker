///-----------------------------------------------------------------
///   File:         FetchWorker.cs
///   Author:   	Andre Laskawy           
///   Date:         02.10.2018 19:46:56
///-----------------------------------------------------------------

namespace Nanomite.MessageBroker.Worker
{
    using Grpc.Core;
    using Nanomite.Common.Models.Base;
    using Nanomite.MessageBroker.Helper;
    using Nanomite.Server;
    using Nanomite.Server.Base.Worker;
    using Nanomite.Services.Network.Grpc;
    using NLog;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="FetchWorker" />
    /// </summary>
    public class FetchWorker : CommonFetchWorker
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FetchWorker"/> class.
        /// </summary>
        public FetchWorker() : base()
        {
        }

        /// <summary>
        /// Processes the fetch request
        /// </summary>
        /// <param name="req">The req<see cref="FetchRequest"/></param>
        /// <param name="streamId">The streamId<see cref="string"/></param>
        /// <param name="token">The token<see cref="string"/></param>
        /// <param name="header">The header<see cref="Metadata"/></param>
        /// <param name="checkForAuthentication">if set to <c>true</c> [check for authentication].</param>
        /// <returns>The <see cref="Task{GrpcResponse}"/></returns>
        public override async Task<GrpcResponse> ProcessFetch(FetchRequest req, string streamId, string token, Metadata header, bool checkForAuthentication = true)
        {
            try
            {
                if (!await TokenObserver.IsValid(token))
                {
                    return Unauthorized();
                }

                if (string.IsNullOrEmpty(req.TypeDescription))
                {
                    throw new Exception("invalid modeltype for fetch request");
                }

                Log("Processing fetch request for type: " + req.TypeDescription, LogLevel.Trace);

                // come on, this is great ;)
                Type type = CommonRepositoryHandler.GetAllRepositories().FirstOrDefault(p => p.ProtoTypeUrl == req.TypeDescription).ModelType;
                bool includeAll = req.InlcudeRelatedEntities;
                string query = req.Query;

                var result = CommonRepositoryHandler.GetListByQuery(type, query, includeAll).Cast<IBaseModel>().Select(p => p.MapToProto());
                return Ok(result.ToList());
            }
            catch (Exception ex)
            {
                Log("Fetch error", ex);
                return BadRequest(ex);
            }
        }
    }
}
