using Google.Protobuf.WellKnownTypes;
using Nanomite.Server.Base.Handler;
using Nanomite.Server.Base.Locator;
using Nanomite.Services.Network.Common.Models;
using Nanomite.Services.Network.Grpc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nanomite.MessageBroker.Helper
{
    public class TokenObserver
    {
        /// <summary>
        /// The token collection
        /// </summary>
        private static ConcurrentDictionary<string, NetworkUser> tokenCollection = new ConcurrentDictionary<string, NetworkUser>();

        /// <summary>
        /// The timer which is refreshing the tokens
        /// </summary>
        private static Timer observingTimer;

        /// <summary>
        /// Defines wether the timer is busy
        /// </summary>
        private static bool IsBusy = false;

        /// <summary>
        /// The unique id of the broker
        /// </summary>
        private static string SrcId = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamQueueWorker{T}"/> class.
        /// </summary>
        /// <param name="stream">The <see cref="IStream"/></param>
        static TokenObserver()
        {
            observingTimer = new Timer(Run, null, 0, 1000 * 60);
            SrcId = CloudLocator.GetConfig().SrcDeviceId;
        }

        /// <summary>
        /// Checks if the received token is valid
        /// </summary>
        public static async Task<bool> IsValid(string token)
        {
            if (tokenCollection.ContainsKey(token))
            {
                return true;
            }
            else
            {
                NetworkUser user = new NetworkUser();
                user.AuthenticationToken = token;
                return await TokenIsValid(user);
            }
        }

        /// <summary>
        /// Runs the timer which will check if the tokens are still valid
        /// </summary>
        public static async void Run(object state)
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;

                var tokens = tokenCollection.Keys.ToList();
                foreach (var token in tokens)
                {
                    NetworkUser user = tokenCollection[token];
                    user.AuthenticationToken = token;
                    if (!await TokenIsValid(user))
                    {
                        NetworkUser u = null;
                        while (!tokenCollection.TryRemove(token, out u))
                        {
                            await Task.Delay(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CommonBaseHandler.Log("TokenObserver", "", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static async Task<bool> TokenIsValid(NetworkUser user)
        {
            Command cmd = new Command() { Type = CommandType.Action, Topic = StaticCommandKeys.Connect };
            cmd.Data.Add(Any.Pack(user));

            var result = await CommonSubscriptionHandler.ForwardByTopicAndWaitForResponse(SrcId, cmd, cmd.Topic);
            if (result != null && result.Data.Count > 0)
            {
                var newUser = result.Data.FirstOrDefault().CastToModel<NetworkUser>();
                if (!string.IsNullOrEmpty(newUser.AuthenticationToken))
                {
                    NetworkUser old = null;

                    if (newUser.AuthenticationToken != user.AuthenticationToken)
                    {
                        while (!tokenCollection.TryRemove(user.AuthenticationToken, out old))
                        {
                            await Task.Delay(1);
                        }
                    }

                    if (!tokenCollection.ContainsKey(user.AuthenticationToken))
                    {
                        while (!tokenCollection.TryAdd(newUser.AuthenticationToken, newUser))
                        {
                            await Task.Delay(1);
                        }
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
