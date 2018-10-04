//-----------------------------------------------------------------------------
// FILE:	    RabbitMQFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;

using Neon.Common;
using Neon.HiveMQ;
using Neon.Net;

namespace Neon.Xunit.RabbitMQ
{
    /// <summary>
    /// Used to run the Docker <b>nhive.rabbitmq-test</b> container on 
    /// the current machine as a test fixture while tests are being performed 
    /// and then deletes the container when the fixture is disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fixture assumes that RabbitMQ is not currently running on the
    /// local workstation or as a container that named <b>rmq-test</b>.
    /// You may see port conflict errors if either of these assumptions are
    /// not true.
    /// </para>
    /// <para>
    /// A somewhat safer but slower alternative, is to use the <see cref="DockerFixture"/>
    /// instead and add <see cref="RabbitMQFixture"/> as a subfixture.  The 
    /// advantage is that <see cref="DockerFixture"/> will ensure that all
    /// (potentially conflicting) containers are removed before the RabbitMQ
    /// fixture is started.
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public sealed class RabbitMQFixture : ContainerFixture
    {
        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public RabbitMQFixture()
        {
        }

        /// <summary>
        /// Starts a RabbitMQ container if it's not already running.  You'll generally want
        /// to call this in your test class constructor instead of <see cref="ITestFixture.Initialize(Action)"/>.
        /// </summary>
        /// <param name="image">Optionally specifies the RabbitMQ container image (defaults to <b>nhive/rabbitmq-test:latest</b>).</param>
        /// <param name="name">Optionally specifies the RabbitMQ container name (defaults to <c>rmq-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the RabbitMQ container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="username">Optional RabbitMQ username (defaults to <b>Administrator</b>).</param>
        /// <param name="password">Optional RabbitMQ password (defaults to <b>password</b>).</param>
        /// <param name="precompile">
        /// Optionally configure RabbitMQ precompiling.  This may improve RabbitMQ performance by
        /// 20-50% at the cost of an additional 30-45 seconds of startup time.  This can be
        /// enabled for performance oriented unit tests.  This defaults to <c>false</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if the fixture wasn't previously initialized and
        /// this method call initialized it or <c>false</c> if the fixture
        /// was already initialized.
        /// </returns>
        public bool Start(
            string          image      = "nhive/rabbitmq-test:latest",
            string          name       = "rmq-test",
            List<string>    env        = null,
            string          username   = "Administrator",
            string          password   = "password",
            bool            precompile = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));

            return base.Initialize(
                () =>
                {
                    StartInAction(image, name, env, username, password, precompile);
                });
        }

        /// <summary>
        /// Actually starts RabbitMQ within the initialization <see cref="Action"/>.  You'll
        /// generally want to use <see cref="Start(string, string, List{string}, string, string, bool)"/>
        /// but this method is used internally or for special situations.
        /// </summary>
        /// <param name="image">Optionally specifies the RabbitMQ container image (defaults to <b>nhive/rabbitmq-test:latest</b>).</param>
        /// <param name="name">Optionally specifies the RabbitMQ container name (defaults to <c>rmq-test</c>).</param>
        /// <param name="env">Optional environment variables to be passed to the RabbitMQ container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="username">Optional RabbitMQ username (defaults to <b>Administrator</b>).</param>
        /// <param name="password">Optional RabbitMQ password (defaults to <b>password</b>).</param>
        /// <param name="precompile">
        /// Optionally configure RabbitMQ precompiling.  This may improve RabbitMQ performance by
        /// 20-50% at the cost of an additional 30-45 seconds of startup time.  This can be
        /// enabled for performance oriented unit tests.  This defaults to <c>false</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if the fixture wasn't previously initialized and
        /// this method call initialized it or <c>false</c> if the fixture
        /// was already initialized.
        /// </returns>
        public void StartInAction(
            string          image      = "nhive/rabbitmq-test:latest",
            string          name       = "rmq-test",
            List<string>    env        = null,
            string          username   = "Administrator",
            string          password   = "password",
            bool            precompile = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));

            base.CheckWithinAction();

            if (IsInitialized)
            {
                return;
            }

            if (precompile)
            {
                if (env == null)
                {
                    env = new List<string>();
                }

                env.Add("RABBITMQ_HIPE_COMPILE=1");
            }

            RunContainer(
                name, 
                image,
                new string[] {
                    "--detach",
                    "--mount", "type=volume,target=/var/lib/rabbitmq",
                    "--publish", $"{NetworkPorts.AMQP}:{NetworkPorts.AMQP}",
                    "--publish", $"{NetworkPorts.RabbitMQAdmin}:{NetworkPorts.RabbitMQAdmin}",
                    "--env", "DEBUG=false"
                }, 
                env: env);

            var hosts = new string[] { "127.0.0.1" };

            Settings = new HiveMQSettings()
            {
                AdminHosts = hosts.ToList(),
                AmqpHosts  = hosts.ToList(),
                Username   = username,
                Password   = password,
                NeonLog    = false
            };

            Covenant.Assert(Settings.IsValid);

            // Wait for container to warm up by ensuring that we can connect admin and AMQP clients.

            NeonHelper.WaitFor(
                () =>
                {
                    try
                    {
                        using (Settings.ConnectRabbitMQ(Username, Password, dispatchConsumersAsync: false))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout: TimeSpan.FromSeconds(precompile ? 120 : 30),   // We need to wait longer then precompiling (it takes an additional 45-60 seconds to compile).
                pollTime: TimeSpan.FromSeconds(0.5));

            NeonHelper.WaitFor(
                () =>
                {
                    try
                    {
                        using (var manager = Settings.ConnectManager(Username, Password))
                        {
                            // Ensure that the manager can actually process requests.

                            return !manager.GetVHostsAsync().Result.IsEmpty();
                        }
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout: TimeSpan.FromSeconds(30),
                pollTime: TimeSpan.FromSeconds(0.5));
        }

        /// <summary>
        /// Returns the RabbitMQ username.
        /// </summary>
        public string Username => Settings?.Username;

        /// <summary>
        /// Returns the RabbitMQ password.
        /// </summary>
        public string Password => Settings?.Password;

        /// <summary>
        /// Returns the <see cref="HiveMQSettings"/> that can be used to connect clients to access
        /// the RabbitMQ container.
        /// </summary>
        public HiveMQSettings Settings { get; private set; }

        /// <summary>
        /// Clears the RabbitMQ state by removing all virtual hosts and accounts
        /// besides the administrator account and then recreating the root <b>"/"</b>
        /// virtual host.
        /// </summary>
        public void Clear()
        {
            // NOTE: I'm assuming that tests are not going mess with the
            //       permissions for the admin account.

            using (var manager = Settings.ConnectManager())
            {
                // Remove all users except for the admin.

                foreach (var user in manager.GetUsersAsync().Result
                    .Where(u => u.Name != Username))
                {
                    manager.DeleteUserAsync(user).Wait();
                }

                // Remove all virtual hosts other than the root.

                foreach (var vhost in manager.GetVHostsAsync().Result
                    .Where(vh => vh.Name != "/"))
                {
                    manager.DeleteVirtualHostAsync(vhost).Wait();
                }

                // Remove all policies, queues, exchanges and bindings.

                var rootVHost = manager.GetVhostAsync("/").Result;

                foreach (var policy in manager.GetPoliciesAsync().Result)
                {
                    manager.DeletePolicyAsync(policy.Name, rootVHost).Wait();
                }

                foreach (var queue in manager.GetQueuesAsync().Result)
                {
                    manager.DeleteQueueAsync(queue).Wait();
                }

                // Remove all exchanges besides the built-in ones with an empty
                // name or names that start with "amq.".

                foreach (var exchange in manager.GetExchangesAsync().Result
                    .Where(e => e.Name != string.Empty && !e.Name.StartsWith("amq.")))
                {
                    manager.DeleteExchangeAsync(exchange).Wait();
                }

                foreach (var binding in manager.GetBindingsAsync().Result)
                {
                    manager.DeleteBindingAsync(binding).Wait();
                }
            }
        }

        /// <summary>
        /// This method completely resets the fixture by removing the RabbitMQ 
        /// container from Docker.  Use <see cref="Clear"/> if you just want to 
        /// clear the messaging system state.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
        }
    }
}
