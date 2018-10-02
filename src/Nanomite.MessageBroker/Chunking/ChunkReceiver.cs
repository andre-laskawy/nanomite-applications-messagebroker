///-----------------------------------------------------------------
///   File:         ChunkReceiver.cs
///   Author:   	Andre Laskawy           
///   Date:         02.10.2018 19:54:54
///-----------------------------------------------------------------

namespace Nanomite.MessageBroker.Chunking
{
    using Grpc.Core;
    using Nanomite.Server.Base.Handler;
    using Nanomite.Services.Network.Common;
    using Nanomite.Services.Network.Grpc;
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// Defines the <see cref="ChunkReceiver" />
    /// </summary>
    public class ChunkReceiver : IChunkReceiver<Command>
    {
        /// <inheritdoc />
        public Action<Command, string, string, Metadata> FileReceived { get; set; }

        /// <inheritdoc />
        internal BlockingCollection<U_File> fileList = new BlockingCollection<U_File>();

        /// <inheritdoc />
        private ConcurrentDictionary<string, BlockingCollection<U_FileChunk>> fileChunkDictionary = new ConcurrentDictionary<string, BlockingCollection<U_FileChunk>>();

        /// <inheritdoc />
        public void ChunkReceived(Command cmd, string streamId, string token, Metadata header)
        {
            CommonSubscriptionHandler.ForwardByTopic(cmd, cmd.Topic);
        }
    }
}
