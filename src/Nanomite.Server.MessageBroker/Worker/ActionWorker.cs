///-----------------------------------------------------------------
///   File:         ActionWorker.cs
///   Author:   	Andre Laskawy           
///   Date:         02.10.2018 19:45:39
///-----------------------------------------------------------------

namespace Nanomite.Server.MessageBroker.Worker
{
    using Grpc.Core;
    using Nanomite.Server.MessageBroker.Helper;
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
    using System.Collections.Concurrent;

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
        /// The token observer
        /// </summary>
        private TokenObserver tokenObserver;

        /// <summary>
        /// A collection of all meta data for all connected services
        /// </summary>
        private ConcurrentDictionary<string, ServiceMetaData> serviceMetaData = new ConcurrentDictionary<string, ServiceMetaData>();

        /// <summary>
        /// Gets or sets a value indicating whether the cloud is ready for a client connection.
        /// </summary>
        public bool ReadyForConnections { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionWorker" /> class.
        /// </summary>
        /// <param name="brokerId">The broker identifier.</param>
        /// <param name="secret">The secret.</param>
        /// <param name="tokenObserver">The token observer.</param>
        public ActionWorker(string brokerId, TokenObserver tokenObserver) : base()
        {
            this.ReadyForConnections = false;
            this.brokerId = brokerId;
            this.tokenObserver = tokenObserver;
        }

        /// <inheritdoc />
        public override async Task<GrpcResponse> StreamConnected(IStream<Command> stream, string token, Metadata header)
        {
            if (await this.tokenObserver.IsValid(token) == null)
            {
                return Unauthorized();
            }

            CommonSubscriptionHandler.RegisterStream(stream, stream.Id);
            CommonBaseHandler.Log(this.ToString(), string.Format("Client stream {0} is connected.", stream.Id), LogLevel.Info);

            // Send service metadata informations to client after successful connect
            foreach (var metaData in serviceMetaData.Values)
            {
                Command cmd = new Command { Type = CommandType.Action, Topic = StaticCommandKeys.ServiceMetaData };
                cmd.Data.Add(Any.Pack(metaData));
                stream.AddToQueue(cmd);
            }
            
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

                // connect 
                if(cmd.Topic == StaticCommandKeys.Connect)
                {
                    return await HandleCommand(null, cmd, streamId, cloudId);
                }

                // other commands
                NetworkUser user = await this.tokenObserver.IsValid(token);
                if (user == null)
                {
                    return Unauthorized();
                }

                return await HandleCommand(user, cmd, streamId, cloudId);
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
                    user = cmd.Data.FirstOrDefault()?.CastToModel<NetworkUser>();
                    if (user == null)
                    {
                        throw new Exception("Invalid connection data.");
                    }

                    return await this.tokenObserver.Authenticate(user.LoginName, user.PasswordHash);

                case StaticCommandKeys.ServiceMetaData:
                    ServiceMetaData metaData = cmd.Data.FirstOrDefault()?.CastToModel<ServiceMetaData>();
                    if (metaData == null)
                    {
                        throw new Exception("Invalid data for service meta data information.");
                    }

                    if (serviceMetaData.ContainsKey(metaData.ServiceAddress))
                    {
                        ServiceMetaData s;
                        while (!serviceMetaData.TryRemove(metaData.ServiceAddress, out s))
                        {
                            await Task.Delay(1);
                        }
                    }

                    while (!serviceMetaData.TryAdd(metaData.ServiceAddress, metaData))
                    {
                        await Task.Delay(1);
                    }

                    Log("Added service metadata for service " + streamId, LogLevel.Debug);
                    break;
                case StaticCommandKeys.Subscribe:
                    try
                    {
                        SubscriptionMessage msg = cmd.Data.FirstOrDefault()?.CastToModel<SubscriptionMessage>();
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
                        SubscriptionMessage msg = cmd.Data.FirstOrDefault()?.CastToModel<SubscriptionMessage>();
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
