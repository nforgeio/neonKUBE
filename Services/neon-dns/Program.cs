//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Cluster;
using Neon.Diagnostics;
using Neon.Web;

namespace NeonDns
{
    /// <summary>
    /// Implements a simple API service over the main Stoke Couchbase database.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Returns the service name.
        /// </summary>
        private const string serviceName = "neon-dns";

        private static ProcessTerminator    terminator;
        private static INeonLogger          log;

        /// <summary>
        /// Main program entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            LogManager.Default.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.Default.GetLogger(typeof(Program));
            log.LogInfo(() => $"Starting [{serviceName}:{Program.GitVersion}]");

            terminator = new ProcessTerminator(log);

            WebHelper.Initialize();

            try
            {
                // Establish the cluster connections.

                if (NeonHelper.IsDevWorkstation)
                {
                    NeonClusterHelper.OpenRemoteCluster();
                }
                else
                {
                    NeonClusterHelper.OpenCluster();
                }

                // Initialize and start the web service.

                log.LogInfo(() => $"[{serviceName}] is listening on [port={NeonHostPorts.DynamicDNS}].");

                var host = new WebHostBuilder()
                    .UseKestrel()
                    .UseContentRoot(Directory.GetCurrentDirectory())
                    .ConfigureLogging(
                        (hostingConbtext, logging) =>
                        {
                            logging.AddDebug();
                        })
                    .UseStartup<Startup>()
                    .UseUrls($"http://*:{NeonHostPorts.DynamicDNS}")
                    .Build();

                host.Run();
            }
            catch (Exception e)
            {
                log.LogCritical(e);
                Program.Exit(1);
            }
            finally
            {
                NeonClusterHelper.CloseCluster();
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

                if (ThisAssembly.Git.IsDirty)
                {
                    version += "-DIRTY";
                }

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
    }
}
