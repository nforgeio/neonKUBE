//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Hive;
using Neon.Time;

namespace NeonVarnish
{
    /// <summary>
    /// Implements the <b>neon-proxy-cache</b> service listens for HiveMQ notifications from
    /// <b>neon-proxy-manager</b> that its configuration has changed.  This service uses a combination of polling Consul for
    /// changes and listening for HiveMQ notifications from <b>neon-proxy-manager</b>.  This is built into the
    /// <a href="https://hub.docker.com/r/nhive/neon-proxy-cache/">nhive/neon-proxy-cache</a> image.
    /// </summary>
    public static class Program
    {
        private static readonly string serviceName  = $"neon-proxy-cache:{GitVersion}";

        private static ProcessTerminator        terminator;
        private static INeonLogger              log;
        private static HiveProxy                hive;
        private static ConsulClient             consul;
        private static Task                     monitorTask;

        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static async Task Main(string[] args)
        {
            LogManager.Default.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.Default.GetLogger(typeof(Program));
            log.LogInfo(() => $"Starting [{serviceName}]");
            log.LogInfo(() => $"LOG_LEVEL={LogManager.Default.LogLevel.ToString().ToUpper()}");

            // Create process terminator that handles process termination signals.

            terminator = new ProcessTerminator(log);

            // Establish the hive connections.

            if (NeonHelper.IsDevWorkstation)
            {
                hive = HiveHelper.OpenHiveRemote();
            }
            else
            {
                hive = HiveHelper.OpenHive();
            }

            try
            {
                // Open the hive data services and then start the main service task.

                log.LogInfo(() => $"Connecting: Consul");

                using (consul = HiveHelper.OpenConsul())
                {
                    monitorTask = Task.Run(
                        async () =>
                        {
                            await RunAsync();
                        });

                    await monitorTask;
                    monitorTask = null;
                    terminator.ReadyToExit();
                }

            }
            catch (Exception e)
            {
                log.LogCritical(e);
                Program.Exit(1);
            }
            finally
            {
                HiveHelper.CloseHive();
                terminator.ReadyToExit();
            }

            Program.Exit(0);
        }

        /// <summary>
        /// Returns the program version as the Git branch and commit and an optional
        /// indication of whether the program was build from a dirty branch.
        /// </summary>
        public static string GitVersion
        {
            get
            {
                var version = $"{ThisAssembly.Git.Branch}-{ThisAssembly.Git.Commit}";

#pragma warning disable 162 // Unreachable code

                //if (ThisAssembly.Git.IsDirty)
                //{
                //    version += "-DIRTY";
                //}

#pragma warning restore 162 // Unreachable code

                return version;
            }
        }

        /// <summary>
        /// Exits the service with an exit code.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        public static void Exit(int exitCode)
        {
            log.LogInfo(() => $"Exiting: [{serviceName}]");
            terminator.ReadyToExit();
            Environment.Exit(exitCode);
        }

        /// <summary>
        /// Implements the service as a <see cref="Task"/>.
        /// </summary>
        /// <returns>The <see cref="Task"/>.</returns>
        private static async Task RunAsync()
        {
            // The implementation is pretty straightforward: We're going to 
            // listen for HiveMQ messages from [neon-proxy-manager] signalling 
            // that the Varnish configuration should be reloaded from Consul.
            // The service simply fetches the configuration from Consul when
            // it sees these messages and updates Varinsh as required.

            var cts  = new CancellationTokenSource();
            var ct   = cts.Token;
            var exit = false;

            // Gracefully exit when the application is being terminated (e.g. via a [SIGTERM]).

            terminator.AddHandler(
                () =>
                {
                    exit = true;

                    cts.Cancel();

                    if (monitorTask != null)
                    {
                        if (monitorTask.Wait(terminator.Timeout))
                        {
                            log.LogInfo(() => "Tasks stopped gracefully.");
                        }
                        else
                        {
                            log.LogWarn(() => $"Tasks did not stop within [{terminator.Timeout}].");
                        }
                    }
                });

            log.LogInfo(() => "Listening for HiveMQ notifications.");

            while (true)
            {
                if (terminator.CancellationToken.IsCancellationRequested)
                {
                    log.LogInfo(() => "Terminating.");
                    return;
                }

                try
                {
                    try
                    {
                        // $todo(jeff.lill): Implement this.

                    }
                    catch (Exception e)
                    {
                        if (!(e is OperationCanceledException))
                        {
                            log.LogError(e);
                        }
                    }

                    if (exit)
                    {
                        return;
                    }

                    await Task.Delay(1);
                }
                catch (OperationCanceledException)
                {
                    log.LogInfo(() => "Terminating.");
                    return;
                }
            }
        }
    }
}
