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

#if !NETCOREAPP3_1

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

namespace Neon.Web.SignalR
{
    /// <summary>
    /// The NATS scaleout provider for multi-server support.
    /// </summary>
    /// <typeparam name="THub">The type of <see cref="Hub"/> to manage connections for.</typeparam>
    public class NatsHubLifetimeManager<THub> : HubLifetimeManager<THub>, IDisposable where THub : Hub
    {
        private readonly ConnectionFactory       natsConnectionFactory = new ConnectionFactory();
        private readonly HubConnectionStore      hubConnections        = new HubConnectionStore();
        private readonly ClientResultsManager    clientResultsManager  = new();
        private readonly SemaphoreSlim           connectionLock        = new SemaphoreSlim(1);
        private readonly NatsSubscriptionManager connections;
        private readonly NatsSubscriptionManager groups;
        private readonly NatsSubscriptionManager users;
        private readonly INeonLogger             logger;
        private readonly IConnection             nats;
        private readonly NatsSubjects            subjects;
        private readonly string                  serverName;

        private int internalAckId;

        /// <summary>
        /// Constructs the <see cref="NatsHubLifetimeManager{THub}"/> with types from Dependency Injection.
        /// </summary>
        /// <param name="connection">The NATS <see cref="IConnection"/>.</param>
        public NatsHubLifetimeManager(IConnection connection)
            : this(connection, logger: null)
        {

        }

        /// <summary>
        /// Constructs the <see cref="NatsHubLifetimeManager{THub}"/> with types from Dependency Injection.
        /// </summary>
        /// <param name="logger">The logger to write information about what the class is doing.</param>
        /// <param name="connection">The NATS <see cref="IConnection"/>.</param>
        public NatsHubLifetimeManager(IConnection connection,
                                      ILogger logger = null)
        {
            this.serverName  = GenerateServerName();
            this.nats        = connection;
            this.logger      = (INeonLogger)logger;
            this.users       = new NatsSubscriptionManager(this.logger);
            this.groups      = new NatsSubscriptionManager(this.logger);
            this.connections = new NatsSubscriptionManager(this.logger);
            this.subjects    = new NatsSubjects($"Neon.SignalR.{typeof(THub).FullName}");


            _ = SubscribeToAllAsync();
            _ = SubscribeToGroupManagementSubjectAsync();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            nats?.Dispose();
        }

        private async Task EnsureNatsServerConnection()
        {
            await SyncContext.Clear;

            if (nats.IsClosed() && !nats.IsReconnecting())
            {
                throw new NATSConnectionException("The connection to NATS is closed");
            }

            await connectionLock.WaitAsync();
            try
            {
                await NeonHelper.WaitForAsync(async () =>
                {
                    await SyncContext.Clear;

                    return !nats.IsReconnecting();
                },
                timeout: TimeSpan.FromSeconds(60),
                pollInterval: TimeSpan.FromMilliseconds(250));

                nats.Flush();

                logger?.LogDebug("Connected to NATS.");
            }
            finally
            {
                connectionLock.Release();
            }
        }

        /// <inheritdoc />
        public override async Task OnConnectedAsync(HubConnectionContext connection)
        {
            await SyncContext.Clear;

            await EnsureNatsServerConnection();

            var feature = new NatsFeature();
            connection.Features.Set<INatsFeature>(feature);

            hubConnections.Add(connection);

            var tasks = new List<Task>();

            tasks.Add(SubscribeToConnectionAsync(connection));

            if (!string.IsNullOrEmpty(connection.UserIdentifier))
            {
                tasks.Add(SubscribeToUserAsync(connection));
            }

            await Task.WhenAll(tasks);
        }

        /// <inheritdoc />
        public override async Task OnDisconnectedAsync(HubConnectionContext connection)
        {
            await SyncContext.Clear;

            hubConnections.Remove(connection);

            // If the nats is null then the connection failed to be established and none of the other connection setup ran
            if (nats is null)
            {
                return;
            }

            var connectionSubject = subjects.Connection(connection.ConnectionId);

            var tasks = new List<Task>();
            tasks.Add(RemoveConnectionSubscriptionAsync(connection));
            tasks.Add(groups.RemoveSubscriptionAsync(connectionSubject, connection, this));

            var feature    = connection.Features.Get<INatsFeature>();
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

            var connection = hubConnections[connectionId];
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

            var connection = hubConnections[connectionId];
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

            await PublishAsync(subjects.All, Invocation.Write(methodName: methodName, args: args));
        }

        /// <inheritdoc />
        public override async Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));
            Covenant.Requires<ArgumentNullException>(excludedConnectionIds != null, nameof(excludedConnectionIds));

            await PublishAsync(subjects.All, Invocation.Write(methodName: methodName, args: args, excludedConnectionIds: excludedConnectionIds));
        }

        /// <inheritdoc />
        public override async Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(connectionId != null, nameof(connectionId));
            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            await PublishAsync(subjects.Connection(connectionId), Invocation.Write(methodName: methodName, args: args));
        }

        /// <inheritdoc />
        public override async Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(connectionIds != null, nameof(connectionIds));
            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            var tasks   = new List<Task>();
            var message = Invocation.Write(methodName: methodName, args: args);

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

            await PublishAsync(subjects.Group(groupName), Invocation.Write(methodName: methodName, args: args));
        }

        /// <inheritdoc />
        public override async Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(groupName != null, nameof(groupName));
            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));
            Covenant.Requires<ArgumentNullException>(excludedConnectionIds != null, nameof(excludedConnectionIds));

            await PublishAsync(subjects.Group(groupName), Invocation.Write(methodName: methodName, args: args, excludedConnectionIds: excludedConnectionIds));
        }

        /// <inheritdoc />
        public override async Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(groupNames != null, nameof(groupNames));
            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            var tasks = new List<Task>();
            var message = Invocation.Write(methodName: methodName, args: args);

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

            await PublishAsync(subjects.User(userId), Invocation.Write(methodName: methodName, args: args));
        }

        /// <inheritdoc />
        public override async Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(userIds != null, nameof(userIds));
            Covenant.Requires<ArgumentNullException>(methodName != null, nameof(methodName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            var tasks   = new List<Task>();
            var message = Invocation.Write(methodName: methodName, args: args);

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
            
            await EnsureNatsServerConnection();

            logger?.LogDebug($"Publishing message to NATS subject. [Subject={subject}].");

            nats.Publish(subject, payload);
        }

        private async Task RemoveUserAsync(HubConnectionContext connection)
        {
            await SyncContext.Clear;

            var userSubject = subjects.User(connection.UserIdentifier!);

            await users.RemoveSubscriptionAsync(userSubject, connection, this);
        }

        private async Task SubscribeToConnectionAsync(HubConnectionContext connection)
        {
            await SyncContext.Clear;
            
            var connectionSubject = subjects.Connection(connection.ConnectionId);

            await connections.AddSubscriptionAsync(connectionSubject, connection, async (subjectName, subscriptions) =>
            {
                await SyncContext.Clear;

                EventHandler<MsgHandlerEventArgs> handler = async (sender, args) =>
                {
                    await SyncContext.Clear;

                    logger?.LogDebug($"Received message from NATS subject. [Subject={connectionSubject}].");

                    try
                    {
                        var invocation = Invocation.Read(args.Message.Data);
                        var message = new InvocationMessage(invocation.MethodName, invocation.Args);

                        await connection.WriteAsync(message).AsTask();
                    }
                    catch (Exception e)
                    {
                        logger?.LogError(e);
                        logger?.LogDebug($"Failed writing message. [Subject={connectionSubject}] [Connection{connection.ConnectionId}]");
                    }
                };

                IAsyncSubscription sAsync = nats.SubscribeAsync(connectionSubject);
                sAsync.MessageHandler += handler;
                sAsync.Start();
                return sAsync;
            });
        }

        private async Task RemoveConnectionSubscriptionAsync(HubConnectionContext connection)
        {
            await SyncContext.Clear;

            var connectionSubject = subjects.Connection(connection.ConnectionId);

            await connections.RemoveSubscriptionAsync(connectionSubject, connection, this);
        }

        private async Task SubscribeToUserAsync(HubConnectionContext connection)
        {
            await SyncContext.Clear;

            var userSubject = subjects.User(connection.UserIdentifier!);

            await users.AddSubscriptionAsync(userSubject, connection, async (subjectName, subscriptions) =>
            {
                await SyncContext.Clear;

                EventHandler<MsgHandlerEventArgs> handler = async (sender, args) =>
                {
                    await SyncContext.Clear;

                    logger?.LogDebug($"Received message from NATS subject. [Subject={userSubject}].");

                    try
                    {
                        var invocation = Invocation.Read(args.Message.Data);
                        var tasks      = new List<Task>(subscriptions.Count);
                        var message    = new InvocationMessage(invocation.MethodName, invocation.Args);

                        foreach (var userConnection in subscriptions)
                        {
                            tasks.Add(userConnection.WriteAsync(message).AsTask());
                        }

                        await Task.WhenAll(tasks);
                    }
                    catch (Exception e)
                    {
                        logger?.LogError(e);
                        logger?.LogDebug($"Failed writing message. [Subject={userSubject}].");
                    }
                };

                IAsyncSubscription sAsync = nats.SubscribeAsync(subjectName);
                sAsync.MessageHandler += handler;
                return sAsync;
            });
        }

        private async Task<IAsyncSubscription> SubscribeToGroupAsync(string groupSubject, HubConnectionStore groupConnections)
        {
            await SyncContext.Clear;

            EventHandler<MsgHandlerEventArgs> handler = async (sender, args) =>
            {
                await SyncContext.Clear;

                logger?.LogDebug($"Received message from NATS subject. [Subject={groupSubject}].");

                try
                {
                    var invocation = Invocation.Read(args.Message.Data);
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
                catch (Exception e)
                {
                    logger?.LogError(e);
                    logger?.LogDebug($"Failed writing message. [Subject={groupSubject}].");
                }
            };

            IAsyncSubscription sAsync = nats.SubscribeAsync(groupSubject);
            sAsync.MessageHandler += handler;
            return sAsync;
        }

        private async Task AddGroupAsyncCore(HubConnectionContext connection, string groupName)
        {
            await SyncContext.Clear;

            var feature    = connection.Features.Get<INatsFeature>()!;
            var groupNames = feature.Groups;

            lock (groupNames)
            {
                // Connection already in group
                if (!groupNames.Add(groupName))
                {
                    return;
                }
            }

            var groupSubject = subjects.Group(groupName);

            await groups.AddSubscriptionAsync(groupSubject, connection, SubscribeToGroupAsync);
        }

        /// <summary>
        /// This takes <see cref="HubConnectionContext"/> because we want to remove the connection from the
        /// _connections list in OnDisconnectedAsync and still be able to remove groups with this method.
        /// </summary>
        private async Task RemoveGroupAsyncCore(HubConnectionContext connection, string groupName)
        {
            await SyncContext.Clear;

            var groupSubject = subjects.Group(groupName);

            await groups.RemoveSubscriptionAsync(groupSubject, connection, this);

            var feature    = connection.Features.Get<INatsFeature>();
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

            logger?.LogDebug($"Publishing message to NATS subject. [Subject={subjects.GroupManagement}].");

            try
            {
                var id = Interlocked.Increment(ref internalAckId);

                // Send Add/Remove Group to other servers and wait for an ack or timeout
                var message = GroupCommand.Write(id, serverName, action, groupName, connectionId);

                await nats.RequestAsync(subjects.GroupManagement, message, timeout: 10000);
            }
            catch (Exception e)
            {
                logger?.LogError(e);
                logger?.LogDebug($"Ack timed out. [Connection={connectionId}] [Group={groupName}]");
            }
        }

        private async Task SubscribeToAllAsync()
        {
            await SyncContext.Clear;

            await EnsureNatsServerConnection();

            logger?.LogDebug($"Subscribing to subject: [Subject={subjects.All}].");

            EventHandler<MsgHandlerEventArgs> handler = async (sender, args) =>
            {
                await SyncContext.Clear;

                logger?.LogDebug($"Received message from NATS subject. [Subject={subjects.All}].");

                try
                {
                    var invocation = Invocation.Read(args.Message.Data);
                    var tasks      = new List<Task>(hubConnections.Count);
                    var message    = new InvocationMessage(invocation.MethodName, invocation.Args);

                    foreach (var connection in hubConnections)
                    {
                        if (invocation.ExcludedConnectionIds == null || !invocation.ExcludedConnectionIds.Contains(connection.ConnectionId))
                        {
                            
                            tasks.Add(connection.WriteAsync(message).AsTask());
                        }
                    }

                    await Task.WhenAll(tasks);
                }
                catch (Exception e)
                {
                    logger?.LogError(e);
                    logger?.LogDebug($"Failed writing message. [Subject={subjects.All}].");
                }
            };

            IAsyncSubscription sAsync = nats.SubscribeAsync(subjects.All);
            sAsync.MessageHandler += handler;
            sAsync.Start();
        }

        private async Task SubscribeToGroupManagementSubjectAsync()
        {
            await SyncContext.Clear;

            await EnsureNatsServerConnection();

            logger?.LogDebug($"Subscribing to subject. [Subject={subjects.GroupManagement}].");

            EventHandler<MsgHandlerEventArgs> handler = async (sender, args) =>
            {
                await SyncContext.Clear;

                logger?.LogDebug($"Received message from NATS subject. [Subject={subjects.GroupManagement}].");

                try
                {
                    var groupMessage = GroupCommand.Read(args.Message.Data);

                    var connection = hubConnections[groupMessage.ConnectionId];
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

                    logger?.LogDebug($"Publishing message to NATS subject. [Subject={subjects.GroupManagement}].");

                    // Send an ack to the server that sent the original command.
                    nats.Publish(args.Message.Reply, Encoding.UTF8.GetBytes($"{groupMessage.Id}"));
                }
                catch (Exception e)
                {
                    logger?.LogError(e);
                    logger?.LogDebug($"Error processing message for internal server message. [Subject={subjects.GroupManagement}]");
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

#endif