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
            LogManager.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.GetLogger(serviceName);
            log.Info(() => $"Starting [{serviceName}]");

            terminator = new ProcessTerminator(log);

            try
            {
                // Establish the cluster connections.

                if (NeonHelper.IsDevWorkstation)
                {
                    NeonClusterHelper.ConnectRemoteCluster();
                }
                else
                {
                    NeonClusterHelper.ConnectCluster();
                }

                Task.Run(() => RunAsync()).Wait();
            }
            catch (Exception e)
            {
                log.Fatal(e);
                Program.Exit(1);
            }
            finally
            {
                NeonClusterHelper.DisconnectCluster();
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
            log.Info(() => $"Exiting: [{serviceName}]");
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
                            new Uri("http://10.0.0.90:8091/pools/"),
                            new Uri("http://10.0.0.91:8091/pools/"),
                            new Uri("http://10.0.0.92:8091/pools/")
                        },
                        Bucket = "stoke"
                    };

                var credentials =
                    new Credentials()
                    {
                        Username = Environment.GetEnvironmentVariable("TS_COUCHBASE_USERNAME"),
                        Password = Environment.GetEnvironmentVariable("TS_COUCHBASE_PASSWORD")
                    };

                var config = new Couchbase.Configuration.Client.ClientConfiguration
                {
                    Servers = new List<Uri>
                    {
                        new Uri("http://10.0.0.90:8091/pools/"),
                        new Uri("http://10.0.0.91:8091/pools/"),
                        new Uri("http://10.0.0.92:8091/pools/")
                    },
                    UseSsl = false,
                    BucketConfigs = new Dictionary<string, Couchbase.Configuration.Client.BucketConfiguration>
                    {
                        {
                            "stoke",
                            new Couchbase.Configuration.Client.BucketConfiguration
                            {
                                BucketName = "stoke",
                                UseSsl = false,
                                Username = "stoke",
                                Password = "stoke.pro",
                                PoolConfiguration = new Couchbase.Configuration.Client.PoolConfiguration
                                {
                                    MaxSize = 10,
                                    MinSize = 5
                                }
                            }}
                    }
                };

                var cluster = new Cluster(config);

                using (var bucket = cluster.OpenBucket("stoke"))
                {
                    var key = bucket.GenKey();
                    var result1 = await bucket.InsertAsync(key, new Document<Item>() { Id = key, Content = new Item() { Name = "Jeff", Age = 56 } });

                    //result1.EnsureSuccess();

                    var exists = await bucket.ExistsAsync(key);

                    var result2 = await bucket.GetAsync<Document<Item>>(key);

                    result2.EnsureSuccess();
                }
            }
            catch (OperationCanceledException)
            {
                terminator.ReadyToExit();
                return;
            }
            catch (Exception e)
            {
                log.Error(e);
            }
        }
    }
}
