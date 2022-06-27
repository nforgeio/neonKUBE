//-----------------------------------------------------------------------------
// FILE:	    NatsHubLifetimeManager.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Tasks;

using NATS;
using NATS.Client;

namespace Neon.SignalR
{
    /// <summary>
    /// The NATS scaleout provider for multi-server support.
    /// </summary>
    /// <typeparam name="THub">The type of <see cref="Hub"/> to manage connections for.</typeparam>
    public class NatsHubLifetimeManager<THub> : HubLifetimeManager<THub>, IDisposable where THub : Hub
    {
        private readonly ConnectionFactory       natsConnectionFactory = new ConnectionFactory();
        private readonly HubConnectionStore      connections           = new HubConnectionStore();
        private readonly NatsSubscriptionManager groups;
        private readonly NatsSubscriptionManager users;
        private readonly ClientResultsManager    clientResultsManager = new();
        private readonly string serverName = GenerateServerName();
        private readonly ILogger logger;
        private readonly SemaphoreSlim connectionLock = new SemaphoreSlim(1);
        private readonly IConnection nats;
        private readonly NatsSubjects subjects;

        private int internalAckId;

        /// <summary>
        /// Constructs the <see cref="NatsHubLifetimeManager{THub}"/> with types from Dependency Injection.
        /// </summary>
        /// <param name="logger">The logger to write information about what the class is doing.</param>
        /// <param name="connection">The NATS <see cref="IConnection"/>.</param>
        public NatsHubLifetimeManager(IConnection connection,
                                      ILogger logger)
        {
            this.nats        = connection;
            this.logger      = logger;
            this.users       = new NatsSubscriptionManager(logger);
            this.groups      = new NatsSubscriptionManager(logger);
            subjects         = new NatsSubjects($"Neon.SignalR.{typeof(THub)}");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            nats?.Dispose();
        }

        /// <inheritdoc />
        public override async Task OnConnectedAsync(HubConnectionContext connection)
        {
            await SyncContext.Clear;
            
            nats.Flush();

            await SubscribeToAllAsync();
            await SubscribeToGroupManagementChannelAsync();

            var feature = new NatsFeature();
            connection.Features.Set<INatsFeature>(feature);

            var userTask = Task.CompletedTask;

            connections.Add(connection);

            var connectionTask = SubscribeToConnectionAsync(connection);

            if (!string.IsNullOrEmpty(connection.UserIdentifier))
            {
                userTask = SubscribeToUserAsync(connection);
            }

            await Task.WhenAll(connectionTask, userTask);
        }

        /// <inheritdoc />
        public override async Task OnDisconnectedAsync(HubConnectionContext connection)
        {
            await SyncContext.Clear;

            await nats.DrainAsync();

            connections.Remove(connection);

            // If the nats is null then the connection failed to be established and none of the other connection setup ran
            if (nats is null)
            {
                return;
            }

            var connectionChannel = subjects.Connection(connection.ConnectionId);
            var tasks = new List<Task>();

            tasks.Add(groups.RemoveSubscriptionAsync(connectionChannel, connection, this));

            var feature = connection.Features.Get<INatsFeature>();
            var groupNames = feature.Groups;

            if (groupNames != null)
            {
                // Copy the groups to an array here because they get removed from this collection
                // in RemoveFromGroupAsync
                foreach (var group in groupNames.ToArray())
                {
                    // Use RemoveGroupAsyncCore because the connection is local and we don't want to
                    // accidentally go to other servers with our remove request.
                    tasks.Add(RemoveGroupAsyncCore(connection, group));
                }
            }

            if (!string.IsNullOrEmpty(connection.UserIdentifier))
            {
                tasks.Add(RemoveUserAsync(connection));
            }

            await Task.WhenAll(tasks);
        }

        /// <inheritdoc />
        public override async Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(connectionId != null, nameof(connectionId));
            Covenant.Requires<ArgumentNullException>(groupName != null, nameof(groupName));

            var connection = connections[connectionId];
            if (connection != null)
            {
                // short circuit if connection is on this server
                await AddGroupAsyncCore(connection, groupName);
            }

            await SendGroupActionAndWaitForAckAsync(connectionId, groupName, GroupAction.Add);
        }

        /// <inheritdoc />
        public override async Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(connectionId != null, nameof(connectionId));
            Covenant.Requires<ArgumentNullException>(groupName != null, nameof(groupName));

            var connection = connections[connectionId];
            if (connection != null)
            {
                // short circuit if connection is on this server
                await RemoveGroupAsyncCore(connection, groupName);
            }

            await SendGroupActionAndWaitForAckAsync(connectionId, groupName, GroupAction.Remove);
        }

        /// <inheritdoc />
        public override async Task SendAllAsync(string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            await PublishAsync(subjects.All, Invokation.Write(methodName: methodName, args: args));
        }

        /// <inheritdoc />
        public override async Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));
            Covenant.Requires<ArgumentNullException>(excludedConnectionIds != null, nameof(excludedConnectionIds));

            await PublishAsync(subjects.All, Invokation.Write(methodName: methodName, args: args, excludedConnectionIds: excludedConnectionIds));
        }

        /// <inheritdoc />
        public override async Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(connectionId != null, nameof(connectionId));
            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            await PublishAsync(subjects.Connection(connectionId), Invokation.Write(methodName: methodName, args: args));
        }

        /// <inheritdoc />
        public override async Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(connectionIds != null, nameof(connectionIds));
            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            var tasks   = new List<Task>();
            var message = Invokation.Write(methodName: methodName, args: args);

            foreach (var connectionId in connectionIds)
            {
                tasks.Add(PublishAsync(subjects.Connection(connectionId), message));
            }

            await Task.WhenAll(tasks);
        }

        /// <inheritdoc />
        public override async Task SendGroupAsync(string groupName, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(groupName != null, nameof(groupName));
            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            await PublishAsync(subjects.Group(groupName), Invokation.Write(methodName: methodName, args: args));
        }

        /// <inheritdoc />
        public override async Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(groupName != null, nameof(groupName));
            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));
            Covenant.Requires<ArgumentNullException>(excludedConnectionIds != null, nameof(excludedConnectionIds));

            await PublishAsync(subjects.Group(groupName), Invokation.Write(methodName: methodName, args: args, excludedConnectionIds: excludedConnectionIds));
        }

        /// <inheritdoc />
        public override async Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(groupNames != null, nameof(groupNames));
            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            var tasks = new List<Task>();
            var message = Invokation.Write(methodName: methodName, args: args);

            foreach (var groupName in groupNames)
            {
                tasks.Add(PublishAsync(subjects.Group(groupName), message));
            }

            await Task.WhenAll(tasks);
        }

        /// <inheritdoc />
        public override async Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(userId != null, nameof(userId));
            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            await PublishAsync(subjects.User(userId), Invokation.Write(methodName: methodName, args: args));
        }

        /// <inheritdoc />
        public override async Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(userIds != null, nameof(userIds));
            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            var tasks   = new List<Task>();
            var message = Invokation.Write(methodName: methodName, args: args);

            foreach (var userId in userIds)
            {
                tasks.Add(PublishAsync(subjects.User(userId), message));
            }

            await Task.WhenAll(tasks);
        }

        private static string GenerateServerName()
        {
            // Use the machine name for convenient diagnostics, but add a guid to make it unique.
            // Example: MyServerName_02db60e5fab243b890a847fa5c4dcb29
            return $"{Environment.MachineName}_{Guid.NewGuid():N}";
        }

        private async Task PublishAsync(string subject, byte[] payload)
        {
            await SyncContext.Clear;

            nats.Publish(subject, payload);
        }

        private async Task RemoveUserAsync(HubConnectionContext connection)
        {
            await SyncContext.Clear;

            var userChannel = subjects.User(connection.UserIdentifier!);

            await users.RemoveSubscriptionAsync(userChannel, connection, this);
        }

        private async Task SubscribeToConnectionAsync(HubConnectionContext connection)
        {
            await SyncContext.Clear;
            
            var connectionChannel = subjects.Connection(connection.ConnectionId);

            EventHandler<MsgHandlerEventArgs> handler = async (sender, args) =>
            {
                await SyncContext.Clear;
                
                try
                {
                    var invocation = Invokation.Read(args.Message.Data);
                    var message    = new InvocationMessage(invocation.MethodName, invocation.Args);

                    await connection.WriteAsync(message).AsTask();
                }
                catch (Exception ex)
                {
                    logger.LogError("SubscribeToConnectionAsync", ex);
                    throw;
                }
            };

            IAsyncSubscription sAsync = nats.SubscribeAsync(connectionChannel);
            sAsync.MessageHandler += handler;
            sAsync.Start();
        }

        private async Task SubscribeToUserAsync(HubConnectionContext connection)
        {
            await SyncContext.Clear;

            var userChannel = subjects.User(connection.UserIdentifier!);

            await users.AddSubscriptionAsync(userChannel, connection, async (channelName, subscriptions) =>
            {
                await SyncContext.Clear;

                EventHandler<MsgHandlerEventArgs> handler = async (sender, args) =>
                {
                    await SyncContext.Clear;

                    try
                    {
                        var invocation = Invokation.Read(args.Message.Data);
                        var tasks      = new List<Task>(subscriptions.Count);
                        var message    = new InvocationMessage(invocation.MethodName, invocation.Args);

                        foreach (var userConnection in subscriptions)
                        {
                            tasks.Add(userConnection.WriteAsync(message).AsTask());
                        }

                        await Task.WhenAll(tasks);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("SubscribeToUser", ex);
                    }
                };

                IAsyncSubscription sAsync = nats.SubscribeAsync(channelName);
                sAsync.MessageHandler += handler;
                return sAsync;
            });
        }

        private async Task<IAsyncSubscription> SubscribeToGroupAsync(string groupChannel, HubConnectionStore groupConnections)
        {
            await SyncContext.Clear;

            EventHandler<MsgHandlerEventArgs> handler = async (sender, args) =>
            {
                try
                {
                    var invocation = Invokation.Read(args.Message.Data);
                    var tasks      = new List<Task>(groupConnections.Count);
                    var message    = new InvocationMessage(invocation.MethodName, invocation.Args);

                    foreach (var groupConnection in groupConnections)
                    {
                        if (invocation.ExcludedConnectionIds?.Contains(groupConnection.ConnectionId) == true)
                        {
                            continue;
                        }

                        tasks.Add(groupConnection.WriteAsync(message).AsTask());
                    }

                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    logger.LogError("SubscribeToGroupAsync", ex);
                }
            };

            IAsyncSubscription sAsync = nats.SubscribeAsync(groupChannel);
            sAsync.MessageHandler += handler;
            return sAsync;
        }

        private async Task AddGroupAsyncCore(HubConnectionContext connection, string groupName)
        {
            await SyncContext.Clear;

            var feature = connection.Features.Get<INatsFeature>()!;
            var groupNames = feature.Groups;

            lock (groupNames)
            {
                // Connection already in group
                if (!groupNames.Add(groupName))
                {
                    return;
                }
            }

            var groupChannel = subjects.Group(groupName);

            await groups.AddSubscriptionAsync(groupChannel, connection, SubscribeToGroupAsync);
        }

        /// <summary>
        /// This takes <see cref="HubConnectionContext"/> because we want to remove the connection from the
        /// _connections list in OnDisconnectedAsync and still be able to remove groups with this method.
        /// </summary>
        private async Task RemoveGroupAsyncCore(HubConnectionContext connection, string groupName)
        {
            await SyncContext.Clear;

            var groupChannel = subjects.Group(groupName);

            await groups.RemoveSubscriptionAsync(groupChannel, connection, this);

            var feature = connection.Features.Get<INatsFeature>();
            var groupNames = feature.Groups;
            if (groupNames != null)
            {
                lock (groupNames)
                {
                    groupNames.Remove(groupName);
                }
            }
        }

        private async Task SendGroupActionAndWaitForAckAsync(string connectionId, string groupName, GroupAction action)
        {
            await SyncContext.Clear;

            try
            {
                var id = Interlocked.Increment(ref internalAckId);

                // Send Add/Remove Group to other servers and wait for an ack or timeout
                var message = GroupCommand.Write(id, serverName, action, groupName, connectionId);

                await nats.RequestAsync(subjects.GroupManagement, message, timeout: 10000);
            }
            catch (Exception e)
            {
                logger.LogError("SendGroupActionAndWaitForAck", e);
            }
        }
        private async Task SubscribeToAllAsync()
        {
            await SyncContext.Clear;

            EventHandler<MsgHandlerEventArgs> handler = async (sender, args) =>
            {
                try
                {
                    var invocation = Invokation.Read(args.Message.Data);
                    var tasks      = new List<Task>(connections.Count);
                    var message    = new InvocationMessage(invocation.MethodName, invocation.Args);

                    foreach (var connection in connections)
                    {
                        if (invocation.ExcludedConnectionIds == null || !invocation.ExcludedConnectionIds.Contains(connection.ConnectionId))
                        {
                            
                            tasks.Add(connection.WriteAsync(message).AsTask());
                        }
                    }

                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    logger.LogError("SubscribeToAllAsync", ex);
                }
            };

            IAsyncSubscription sAsync = nats.SubscribeAsync(subjects.All);
            sAsync.MessageHandler += handler;
            sAsync.Start();
        }

        private async Task SubscribeToGroupManagementChannelAsync()
        {
            await SyncContext.Clear;

            EventHandler<MsgHandlerEventArgs> handler = async (sender, args) =>
            {
                await SyncContext.Clear;

                try
                {
                    var groupMessage = GroupCommand.Read(args.Message.Data);

                    var connection = connections[groupMessage.ConnectionId];
                    if (connection == null)
                    {
                        // user not on this server
                        return;
                    }

                    if (groupMessage.Action == GroupAction.Remove)
                    {
                        await RemoveGroupAsyncCore(connection, groupMessage.GroupName);
                    }

                    if (groupMessage.Action == GroupAction.Add)
                    {
                        await AddGroupAsyncCore(connection, groupMessage.GroupName);
                    }

                    // Send an ack to the server that sent the original command.
                    nats.Publish(args.Message.Reply, Encoding.UTF8.GetBytes($"{groupMessage.Id}"));
                }
                catch (Exception ex)
                {
                    logger.LogError("SubscribeToGroupManagementChannelAsync", ex);
                }
            };

            IAsyncSubscription sAsync = nats.SubscribeAsync(subjects.GroupManagement);
            sAsync.MessageHandler += handler;
            sAsync.Start();
        }

        private interface INatsFeature
        {
            HashSet<string> Groups { get; }
        }

        private sealed class NatsFeature : INatsFeature
        {
            public HashSet<string> Groups { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
