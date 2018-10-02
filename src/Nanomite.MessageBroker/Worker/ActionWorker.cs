///-----------------------------------------------------------------
///   File:         ActionWorker.cs
///   Author:   	Andre Laskawy           
///   Date:         02.10.2018 19:45:39
///-----------------------------------------------------------------

namespace Nanomite.MessageBroker.Worker
{
    using Grpc.Core;
    using Nanomite.MessageBroker.Helper;
    using Nanomite.Server.Base.Handler;
    using Nanomite.Server.Base.Worker;
    using Nanomite.Services.Network.Common;
    using Nanomite.Services.Network.Grpc;
    using NLog;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="ActionWorker" />
    /// </summary>
    public class ActionWorker : CommonActionWorker
    {
        /// <summary>
        /// Gets or sets a value indicating whether the cloud is ready for a client connection.
        /// </summary>
        public bool ReadyForConnections { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionWorker"/> class.
        /// </summary>
        public ActionWorker() : base()
        {
            this.ReadyForConnections = false;
        }

        /// <inheritdoc />
        public override async Task<GrpcResponse> StreamConnected(IStream<Command> stream, string token, Metadata header)
        {
            if (!await TokenObserver.IsValid(token))
            {
                return Unauthorized();
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
                if (!await TokenObserver.IsValid(token))
                {
                    return Unauthorized();
                }

                Log("Begin command: " + cmd.Topic, LogLevel.Debug);
                return HandleCommand(cmd, streamId, cloudId);
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
        private GrpcResponse HandleCommand(Command cmd, string streamId, string cloudId)
        {
            GrpcResponse result = Ok();
            switch (cmd.Topic)
            {
                case "Subscribe":
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
                case "UnSubcribe":
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
