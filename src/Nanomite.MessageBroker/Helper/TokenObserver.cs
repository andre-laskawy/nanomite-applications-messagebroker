///-----------------------------------------------------------------
///   File:         TokenObserver.cs
///   Author:   	Andre Laskawy           
///   Date:         03.10.2018 15:36:55
///-----------------------------------------------------------------

namespace Nanomite.MessageBroker.Helper
{
    using Google.Protobuf.WellKnownTypes;
    using Nanomite.Core.Network.Common;
    using Nanomite.Core.Network.Common.Models;
    using Nanomite.Core.Server.Base.Handler;
    using Nanomite.Core.Server.Base.Locator;
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="TokenObserver" />
    /// </summary>
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
        /// Initializes static members of the <see cref="TokenObserver"/> class.
        /// </summary>
        static TokenObserver()
        {
            observingTimer = new Timer(Run, null, 0, 1000 * 60);
            var config = CloudLocator.GetConfig() as ConfigHelper;
            SrcId = config.SrcDeviceId;
        }

        /// <summary>
        /// Checks if the received token is valid
        /// </summary>
        /// <param name="token">The token<see cref="string"/></param>
        /// <returns>The <see cref="Task{bool}"/></returns>
        public static async Task<NetworkUser> IsValid(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            if (tokenCollection.ContainsKey(token))
            {
                return tokenCollection[token];
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
        /// <param name="state">The state<see cref="object"/></param>
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
                    if (await TokenIsValid(user) == null)
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

        /// <summary>
        /// Tokens the is valid.
        /// </summary>
        /// <param name="user">The user<see cref="NetworkUser" /></param>
        /// <returns>
        /// The <see cref="Task{bool}" /></returns>
        private static async Task<NetworkUser> TokenIsValid(NetworkUser user)
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

                    return newUser;
                }
            }

            return null;
        }
    }
}
