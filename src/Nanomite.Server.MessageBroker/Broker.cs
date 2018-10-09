///-----------------------------------------------------------------
///   File:         Broker.cs
///   Author:   	Andre Laskawy           
///   Date:         02.10.2018 19:56:42
///-----------------------------------------------------------------

namespace Nanomite.Server.MessageBroker
{
    using Nanomite.Common.Common.Services.GrpcService;
    using Nanomite.Core.Network;
    using Nanomite.Core.Network.Common;
    using Nanomite.Core.Server;
    using Nanomite.Core.Server.Base;
    using Nanomite.Core.Server.Base.Broker;
    using Nanomite.Core.Server.Base.Handler;
    using Nanomite.Core.Server.Base.Locator;
    using Nanomite.MessageBroker.Chunking;
    using Nanomite.Server.MessageBroker.Helper;
    using Nanomite.Server.MessageBroker.Worker;
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="Broker" />
    /// </summary>
    public class Broker : BaseBroker
    {
        /// <summary>
        /// Mains the specified arguments.
        /// </summary>
        /// <param name="args">The arguments.</param>
        public static void Main(string[] args)
        {
            // start cloud
            Cloud.Run();
        }

        /// <inheritdoc />
        public override async Task Start(IConfig config)
        {
            // get endpoint for local grpc host
            IPEndPoint grpcEndPoint = new IPEndPoint(IPAddress.Any, config.PortGrpc);//GetCloudAddress(config);

            // get endpoint which will be repoted to the network
            IPHostEntry entry = Dns.GetHostEntry(Dns.GetHostName());
            var host = entry.AddressList.LastOrDefault(p => p.AddressFamily == AddressFamily.InterNetwork);
            var upnpEndpoint = new IPEndPoint(host, CloudLocator.GetConfig().PortGrpc);

            // Start the server grpc endpoints
            CommonBaseHandler.Log(this.ToString(), "GRPC IP ADRESS: " + upnpEndpoint, NLog.LogLevel.Info);
            StartGrpcServer(grpcEndPoint, config.SrcDeviceId);

            // accept client connections
            (this.ActionWorker as ActionWorker).ReadyForConnections = true;
            Console.WriteLine("Cloud started -> ready for connections.");
        }

        /// <inheritdoc />
        public override void Register()
        {
            CloudLocator.GetCloud = (() =>
            {
                return this;
            });

            CloudLocator.GetConfig = (() =>
            {
                return new ConfigHelper();
            });

            //config
            var config = CloudLocator.GetConfig() as ConfigHelper;

            // init the connection to the authentication server
            var tokenObserver = new TokenObserver(config.AuthServerAddress, config.SrcDeviceId);
            tokenObserver.Init(config.SrcDeviceId, config.BrokerPassword, config.Secret).Await(30);

            // Workers to handle messages (commands /fetch / messages)
            this.ActionWorker = new ActionWorker(config.SrcDeviceId, tokenObserver);
        }

        /// <inheritdoc />
        public override void AddMiddlewares(dynamic app, dynamic env)
        {
        }

        /// <inheritdoc />
        public override void AddServices(dynamic servicCollection)
        {
        }

        /// <summary>
        /// Starts the GRPC server.
        /// </summary>
        /// <param name="endpoint">The <see cref="IPEndPoint" /></param>
        /// <param name="srcDeviceId">The <see cref="string" /></param>
        private void StartGrpcServer(IPEndPoint endpoint, string srcDeviceId)
        {
            // GRPC server
            IServer<Command, FetchRequest, GrpcResponse> communicationServiceGrpc = GRPCServer.Create(endpoint, new ChunkSender(), new ChunkReceiver());
            StartServer(srcDeviceId, communicationServiceGrpc);
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        /// <param name="srcDeviceId">The source device identifier.</param>
        /// <param name="communicationService">The communication service.</param>
        private void StartServer(string srcDeviceId, IServer<Command, FetchRequest, GrpcResponse> communicationService)
        {
            // Grpc server
            CommunicationServer server = new CommunicationServer(communicationService, srcDeviceId);

            // Grpc server actions
            server.OnAction = async (cmd, streamId, token, header) => { return await ActionWorker.ProcessCommand(srcDeviceId, cmd, streamId, token, header); };
            server.OnStreaming = async (cmd, stream, token, header) => { return await ActionWorker.ProcessCommand(srcDeviceId, cmd, stream.Id, token, header); };
            server.OnStreamOpened = async (stream, token, header) => { return await ActionWorker.StreamConnected(stream, token, header); };
            server.OnClientDisconnected = async (id) => { await ActionWorker.StreamDisconnected(id); };
            server.OnLog += (sender, srcid, msg, level) =>
            {
                CommonBaseHandler.Log(sender.ToString(), msg, level);
            };
            server.Start();
        }
    }
}
