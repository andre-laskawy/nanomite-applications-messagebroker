///-----------------------------------------------------------------
///   File:         ChunkReceiver.cs
///   Author:   	Andre Laskawy           
///   Date:         02.10.2018 19:54:54
///-----------------------------------------------------------------

namespace Nanomite.MessageBroker.Chunking
{
    using Grpc.Core;
    using Nanomite.Core.Network.Common;
    using Nanomite.Core.Network.Grpc;
    using Nanomite.Core.Server.Base.Handler;
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
