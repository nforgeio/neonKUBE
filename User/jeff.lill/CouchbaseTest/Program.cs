//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Cryptography;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Retry;

namespace CouchbaseTest
{
    public static class Program
    {
        private const string serviceName = "couchbase-test";

        private static ProcessTerminator    terminator;
        private static ILog                 log;

        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            LogManager.Default.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.Default.GetLogger(typeof(Program));
            log.LogInfo(() => $"Starting [{serviceName}]");

            terminator = new ProcessTerminator(log);

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

                Task.Run(() => RunAsync()).Wait();
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
            try
            {
                var settings =
                    new CouchbaseSettings()
                    {
                        Servers = new List<Uri>()
                        {
                            new Uri("couchbase://10.0.0.90"),
                            new Uri("couchbase://10.0.0.91"),
                            new Uri("couchbase://10.0.0.92")
                        },
                        Bucket = "stoke"
                    };

                var credentials =
                    new Credentials()
                    {
                        Username = Environment.GetEnvironmentVariable("TS_COUCHBASE_USERNAME"),
                        Password = Environment.GetEnvironmentVariable("TS_COUCHBASE_PASSWORD")
                    };

                using (var bucket = settings.OpenBucket(credentials))
                {
                    var retry = new ExponentialRetryPolicy(CouchbaseTransientDetector.IsTransient);

                    for (int i = 0; i < 500000; i++)
                    {
                        var key = bucket.GenKey();

                        await retry.InvokeAsync(async () => await bucket.InsertSafeAsync(key, new Document<Item>() { Id = key, Content = new Item() { Name = "Jeff", Age = 56 } }));

                        var exists = await bucket.ExistsAsync(key);

                        var result2 = await bucket.GetAsync<Document<Item>>(key);

                        result2.EnsureSuccess();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                terminator.ReadyToExit();
                return;
            }
            catch (Exception e)
            {
                log.LogError(e);
            }
        }
    }
}
