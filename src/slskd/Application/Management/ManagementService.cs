﻿// <copyright file="ManagementService.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

using Microsoft.Extensions.Options;

namespace slskd.Management
{
    using System;
    using System.Threading.Tasks;
    using Soulseek;

    /// <summary>
    ///     Application and Soulseek client management.
    /// </summary>
    public class ManagementService : IManagementService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ManagementService"/> class.
        /// </summary>
        /// <param name="optionsMonitor">The options monitor used to derive application options.</param>
        /// <param name="serviceStateMonitor">The state monitor for application service state.</param>
        /// <param name="soulseekClient">The Soulseek client.</param>
        /// <param name="sharedFileCache">The shared file cache.</param>
        public ManagementService(
            IOptionsMonitor<Options> optionsMonitor,
            IStateMonitor<ApplicationState> serviceStateMonitor,
            ISoulseekClient soulseekClient,
            ISharedFileCache sharedFileCache)
        {
            OptionsMonitor = optionsMonitor;
            ServiceStateMonitor = serviceStateMonitor;
            Client = soulseekClient;
            SharedFileCache = sharedFileCache;
        }

        /// <summary>
        ///     Gets the current state of the slskd service.
        /// </summary>
        public ApplicationState ApplicationState => ServiceStateMonitor.CurrentValue;

        /// <summary>
        ///     Gets the current state of the connection to the Soulseek server.
        /// </summary>
        public ServerState ServerState =>
            new ServerState()
            {
                Address = Client.Address,
                IPEndPoint = Client.IPEndPoint,
                State = Client.State,
                Username = Client.Username,
            };

        /// <summary>
        ///     Gets the current state of the connection to the Soulseek server.
        /// </summary>
        public SharedFileCacheState SharedFileCacheState => SharedFileCache.State.CurrentValue;

        private ISoulseekClient Client { get; }
        private Options Options => OptionsMonitor.CurrentValue;
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IStateMonitor<ApplicationState> ServiceStateMonitor { get; }
        private ISharedFileCache SharedFileCache { get; }

        /// <summary>
        ///     Connects the Soulseek client to the server using the configured username and password.
        /// </summary>
        /// <returns>The operation context.</returns>
        public Task ConnectServerAsync()
            => Client.ConnectAsync(Options.Soulseek.Username, Options.Soulseek.Password);

        /// <summary>
        ///     Disconnects the Soulseek client from the server.
        /// </summary>
        /// <param name="message">An optional message containing the reason for the disconnect.</param>
        /// <param name="exception">An optional Exception to associate with the disconnect.</param>
        public void DisconnectServer(string message = null, Exception exception = null)
            => Client.Disconnect(message, exception ?? new IntentionalDisconnectException(message));

        /// <summary>
        ///     Re-scans shared directories.
        /// </summary>
        /// <returns>The operation context.</returns>
        public Task RescanSharesAsync() => SharedFileCache.FillAsync();
    }
}