///-----------------------------------------------------------------
///   File:         ActionWorker.cs
///   Author:   	Andre Laskawy           
///   Date:         02.10.2018 19:45:39
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
    using Nanomite.Core.Network.Grpc;
    using Nanomite.Core.Network.Common;
    using Nanomite.Core.Server.Base.Handler;
    using Nanomite.Core.Network.Common.Models;
    using Nanomite.Core.Network;
    using Google.Protobuf.WellKnownTypes;
    using Nanomite.Common;

    /// <summary>
    /// Defines the <see cref="ActionWorker" />
    /// </summary>
    public class ActionWorker : CommonActionWorker
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
        /// The data access stream identifier
        /// </summary>
        private string dataAccessStreamId;

        /// <summary>
        /// Gets or sets a value indicating whether the cloud is ready for a client connection.
        /// </summary>
        public bool ReadyForConnections { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionWorker" /> class.
        /// </summary>
        /// <param name="brokerId">The broker identifier.</param>
        public ActionWorker(string brokerId, string authServiceId, string dataAccessStreamId) : base()
        {
            this.ReadyForConnections = false;
            this.brokerId = brokerId;
            this.authServiceId = authServiceId;
            this.dataAccessStreamId = dataAccessStreamId;
        }

        /// <inheritdoc />
        public override async Task<GrpcResponse> StreamConnected(IStream<Command> stream, string token, Metadata header)
        {
            if (stream.Id != this.dataAccessStreamId
                && stream.Id != this.authServiceId)
            {
                if (await TokenObserver.IsValid(token) == null)
                {
                    return Unauthorized();
                }
            }

            CommonSubscriptionHandler.RegisterStream(stream, stream.Id);
            CommonBaseHandler.Log(this.ToString(), string.Format("Client stream {0} is connected.", stream.Id), LogLevel.Info);
            return Ok();
        }

        /// <inheritdoc />
        public override async Task<GrpcResponse> StreamDisconnected(string streamID)
        {
            await CommonSubscriptionHandler.UnregisterStream(streamID);
            CommonBaseHandler.Log(this.ToString(), string.Format("Client stream {0} disconnted.", streamID), LogLevel.Info);
            return Ok();
        }

        /// <inheritdoc />
        public override async Task<GrpcResponse> ProcessCommand(string cloudId, Command cmd, string streamId, string token, Metadata header, bool checkForAuthentication = true)
        {
            while (!this.ReadyForConnections)
            {
                await Task.Delay(1);
            }

            try
            {
                Log("Begin command: " + cmd.Topic, LogLevel.Debug);

                // auth or data access service or connect
                if (streamId == this.dataAccessStreamId
                    || streamId == this.authServiceId
                    || cmd.Topic == StaticCommandKeys.Connect)
                {
                    return await HandleCommand(null, cmd, streamId, cloudId);
                }
                else
                {
                    NetworkUser user = await TokenObserver.IsValid(token);
                    if (user == null)
                    {
                        return Unauthorized();
                    }

                    return await HandleCommand(user, cmd, streamId, cloudId);
                }
            }
            catch (Exception ex)
            {
                Log("Command Error " + cmd.Topic, ex);
                return BadRequest(ex);
            }
            finally
            {
                Log("End Command: " + cmd.Topic, LogLevel.Debug);
            }
        }

        /// <summary>
        /// Handles the command.
        /// </summary>
        /// <param name="cmd">The cmd<see cref="Command"/></param>
        /// <param name="streamId">The streamId<see cref="string"/></param>
        /// <param name="cloudId">The cloud identifier.</param>
        /// <returns>a task</returns>
        private async Task<GrpcResponse> HandleCommand(NetworkUser user, Command cmd, string streamId, string cloudId)
        {
            GrpcResponse result = Ok();
            switch (cmd.Topic)
            {
                case StaticCommandKeys.Connect:
                    CommonSubscriptionHandler.ForwardByTopic(cmd, cmd.Topic);

                    var response = await AsyncCommandProcessor.ProcessCommand(cmd, 10);
                    var proto = response.Data.FirstOrDefault();
                    if(proto.TypeUrl == Any.Pack(new S_Exception()).TypeUrl)
                    {
                        var ex = proto.CastToModel<S_Exception>();
                        return BadRequest(ex.Message);
                    }

                    return Ok(proto);

                case StaticCommandKeys.Subscribe:
                    try
                    {
                        S_SubscriptionMessage msg = cmd.Data.FirstOrDefault()?.CastToModel<S_SubscriptionMessage>();
                        if (msg == null)
                        {
                            throw new Exception("Invalid data for the subscription message.");
                        }

                        Log("Client subscribed to Topic: " + msg.Topic, LogLevel.Debug);
                        CommonSubscriptionHandler.SubscribeToTopic(streamId, msg.Topic.ToString());
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(ex);
                    }

                    break;
                case StaticCommandKeys.Unsubscribe:
                    try
                    {
                        S_SubscriptionMessage msg = cmd.Data.FirstOrDefault()?.CastToModel<S_SubscriptionMessage>();
                        if (msg == null)
                        {
                            throw new Exception("Invalid data for the subscription message.");
                        }

                        Log("Client unsubscribed from Topic: " + msg.Topic, LogLevel.Debug);
                        CommonSubscriptionHandler.UnsubscribeFromTopic(streamId, msg.Topic.ToString());
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(ex);
                    }

                    break;

                default:

                    string topic = cmd.Topic;
                    if (!string.IsNullOrEmpty(cmd.TargetId))
                    {
                        topic = topic + "/" + cmd.TargetId;
                    }

                    CommonSubscriptionHandler.ForwardByTopic(cmd, topic);
                    break;
            }

            return result;
        }
    }
}
