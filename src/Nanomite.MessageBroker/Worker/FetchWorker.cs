///-----------------------------------------------------------------
///   File:         FetchWorker.cs
///   Author:   	Andre Laskawy           
///   Date:         02.10.2018 19:46:56
///-----------------------------------------------------------------

namespace Nanomite.MessageBroker.Worker
{
    using Grpc.Core;
    using Nanomite.MessageBroker.Helper;
    using NLog;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Nanomite.Core.Server.Base.Worker;
    using Nanomite.Core.Server.Base.Handler;
    using Google.Protobuf.WellKnownTypes;
    using Nanomite.Common;
    using Nanomite.Core.Network.Common;

    /// <summary>
    /// Defines the <see cref="FetchWorker" />
    /// </summary>
    public class FetchWorker : CommonFetchWorker
    {
        /// <summary>
        /// The broker identifier
        /// </summary>
        private string brokerId;

        /// <summary>
        /// The authentication service identifier
        /// </summary>
        private string authServiceId;

        /// <summary>
        /// Initializes a new instance of the <see cref="FetchWorker" /> class.
        /// </summary>
        /// <param name="brokerId">The broker identifier.</param>
        public FetchWorker(string brokerId, string authServiceId) : base()
        {
            this.brokerId = brokerId;
            this.authServiceId = authServiceId;
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
                if (streamId != this.authServiceId
                    && await TokenObserver.IsValid(token) == null)
                {
                    return Unauthorized();
                }

                if (string.IsNullOrEmpty(req.TypeDescription))
                {
                    throw new Exception("invalid modeltype for fetch request");
                }

                Log("Processing fetch request for type: " + req.TypeDescription, LogLevel.Trace);

                Command cmd = new Command() { Type = CommandType.Action, Topic = "GetData" };
                cmd.Data.Add(Any.Pack(req));
                cmd.SenderId = this.brokerId;

                var result = await CommonSubscriptionHandler.ForwardByTopicAndWaitForResponse(this.brokerId, cmd, cmd.Topic);
                if(result == null)
                {
                    return BadRequest("Unknown fetch exception");
                }

                GrpcResponse response = new GrpcResponse() { Result = ResultCode.Ok };
                response.Data.Add(result.Data);

                if(result.Data.Count > 0
                    && result.Data.FirstOrDefault().TypeUrl == Any.Pack(new S_Exception()).TypeUrl)
                {
                    response.Result = ResultCode.Error;
                }

                return response;
            }
            catch (Exception ex)
            {
                Log("Fetch error", ex);
                return BadRequest(ex);
            }
        }
    }
}
