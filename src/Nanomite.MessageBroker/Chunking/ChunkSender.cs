///-----------------------------------------------------------------
///   File:         ChunkSender.cs
///   Author:   	Andre Laskawy           
///   Date:         02.10.2018 19:54:54
///-----------------------------------------------------------------

namespace Nanomite.MessageBroker.Chunking
{
    using Nanomite.Services.Network.Common;
    using Nanomite.Services.Network.Grpc;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="ChunkSender" />
    /// </summary>
    public class ChunkSender : IChunkSender<Command, FetchRequest, GrpcResponse>
    {
        /// <summary>
        /// The chunk size. A multiple of bytes.
        /// Default 1 MByte.
        /// </summary>
        public static int ChunkSize = 1024 * 1024;

        /// <summary>
        /// Sends the file.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="command">The command<see cref="Command"/></param>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        public async Task SendFile(IClient<Command, FetchRequest, GrpcResponse> client, Command command, int timeout = 60)
        {
            await Task.Delay(1);
            throw new NotImplementedException();
        }
    }
}
