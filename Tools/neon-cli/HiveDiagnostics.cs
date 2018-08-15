//-----------------------------------------------------------------------------
// FILE:	    HiveDiagnostics.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Hive;
using Neon.Net;

// $todo(jeff.lill): Verify that there are no unexpected nodes in the hive.

namespace NeonCli
{
    /// <summary>
    /// Methods to verify that hive nodes are configured and functioning properly.
    /// </summary>
    public static class HiveDiagnostics
    {
        /// <summary>
        /// Verifies that a hive manager node is healthy.
        /// </summary>
        /// <param name="node">The manager node.</param>
        /// <param name="hiveDefinition">The hive definition.</param>
        public static void CheckManager(SshProxy<NodeDefinition> node, HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(node != null);
            Covenant.Requires<ArgumentException>(node.Metadata.IsManager);
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            if (!node.IsFaulted)
            {
                CheckManagerNtp(node, hiveDefinition);
            }

            if (!node.IsFaulted)
            {
                CheckDocker(node, hiveDefinition);
            }

            if (!node.IsFaulted)
            {
                CheckConsul(node, hiveDefinition);
            }

            if (!node.IsFaulted)
            {
                CheckVault(node, hiveDefinition);
            }

            node.Status = "healthy";
        }

        /// <summary>
        /// Verifies that a hive worker or pet node is healthy.
        /// </summary>
        /// <param name="node">The server node.</param>
        /// <param name="hiveDefinition">The hive definition.</param>
        public static void CheckWorkersOrPet(SshProxy<NodeDefinition> node, HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(node != null);
            Covenant.Requires<ArgumentException>(node.Metadata.IsWorker || node.Metadata.IsPet);
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            if (!node.IsFaulted)
            {
                CheckWorkerNtp(node, hiveDefinition);
            }

            if (!node.IsFaulted)
            {
                CheckDocker(node, hiveDefinition);
            }

            if (!node.IsFaulted)
            {
                CheckConsul(node, hiveDefinition);
            }

            if (!node.IsFaulted)
            {
                CheckVault(node, hiveDefinition);
            }

            node.Status = "healthy";
        }

        /// <summary>
        /// Verifies the hive log service health.
        /// </summary>
        /// <param name="hive">The hive proxy.</param>
        public static void CheckLogServices(HiveProxy hive)
        {
            if (!hive.Definition.Log.Enabled)
            {
                return;
            }

            CheckLogEsDataService(hive);
            CheckLogCollectorService(hive);
            CheckLogKibanaService(hive);
        }

        /// <summary>
        /// Verifies the log collector service health.
        /// </summary>
        /// <param name="hive">The hive proxy.</param>
        private static void CheckLogCollectorService(HiveProxy hive)
        {
            // $todo(jeff.lill): Implement this.
        }

        /// <summary>
        /// Verifies the log Elasticsearch hive health.
        /// </summary>
        /// <param name="hive">The hive proxy.</param>
        private static void CheckLogEsDataService(HiveProxy hive)
        {
            // $todo(jeff.lill): Implement this.
        }

        /// <summary>
        /// Verifies the log Kibana service health.
        /// </summary>
        /// <param name="hive">The hive proxy.</param>
        private static void CheckLogKibanaService(HiveProxy hive)
        {
            // $todo(jeff.lill): Implement this.
        }

        /// <summary>
        /// Verifies that a manager node's NTP health.
        /// </summary>
        /// <param name="node">The manager node.</param>
        /// <param name="hiveDefinition">The hive definition.</param>
        private static void CheckManagerNtp(SshProxy<NodeDefinition> node, HiveDefinition hiveDefinition)
        {
            // We're going to use [ntpq -pw] to query the configured time sources.
            // We should get something back that looks like
            //
            //      remote           refid      st t when poll reach   delay   offset  jitter
            //      ==============================================================================
            //       LOCAL(0).LOCL.          10 l  45m   64    0    0.000    0.000   0.000
            //      * clock.xmission. .GPS.            1 u  134  256  377   48.939 - 0.549  18.357
            //      + 173.44.32.10    18.26.4.105      2 u  200  256  377   96.981 - 0.623   3.284
            //      + pacific.latt.ne 44.24.199.34     3 u  243  256  377   41.457 - 8.929   8.497
            //
            // For manager nodes, we're simply going to verify that we have at least one external 
            // time source answering.

            node.Status = "checking: NTP";

            var retryDelay = TimeSpan.FromSeconds(30);
            var fault      = (string)null;

            for (int tryCount = 0; tryCount < 6; tryCount++)
            {
                var response = node.SudoCommand("/usr/bin/ntpq -pw", RunOptions.LogOutput);

                if (response.ExitCode != 0)
                {
                    Thread.Sleep(retryDelay);
                    continue;
                }

                using (var reader = response.OpenOutputTextReader())
                {
                    string line;

                    // Column header and table bar lines.

                    line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        fault = "NTP: Invalid [ntpq -pw] response.";

                        Thread.Sleep(retryDelay);
                        continue;
                    }

                    line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line) || line[0] != '=')
                    {
                        fault = "NTP: Invalid [ntpq -pw] response.";

                        Thread.Sleep(retryDelay);
                        continue;
                    }

                    // Count the lines starting that don't include [*.LOCL.*], 
                    // the local clock.

                    var sourceCount = 0;

                    for (line = reader.ReadLine(); line != null; line = reader.ReadLine())
                    {
                        if (line.Length > 0 && !line.Contains(".LOCL."))
                        {
                            sourceCount++;
                        }
                    }

                    if (sourceCount == 0)
                    {
                        fault = "NTP: No external sources are answering.";

                        Thread.Sleep(retryDelay);
                        continue;
                    }

                    // Everything looks good.

                    break;
                }
            }

            if (fault != null)
            {
                node.Fault(fault);
            }
        }

        /// <summary>
        /// Verifies that a worker node's NTP health.
        /// </summary>
        /// <param name="node">The manager node.</param>
        /// <param name="hiveDefinition">The hive definition.</param>
        private static void CheckWorkerNtp(SshProxy<NodeDefinition> node, HiveDefinition hiveDefinition)
        {
            // We're going to use [ntpq -pw] to query the configured time sources.
            // We should get something back that looks like
            //
            //           remote           refid      st t when poll reach   delay   offset  jitter
            //           ==============================================================================
            //            LOCAL(0).LOCL.          10 l  45m   64    0    0.000    0.000   0.000
            //           * 10.0.1.5        198.60.22.240    2 u  111  128  377    0.062    3.409   0.608
            //           + 10.0.1.7        198.60.22.240    2 u  111  128  377    0.062    3.409   0.608
            //           + 10.0.1.7        198.60.22.240    2 u  111  128  377    0.062    3.409   0.608
            //
            // For worker nodes, we need to verify that each of the managers are answering
            // by confirming that their IP addresses are present.

            node.Status = "checking: NTP";

            var retryDelay = TimeSpan.FromSeconds(30);
            var fault      = (string)null;
            var firstTry   = true;

        tryAgain:

            for (var tries = 0; tries < 6; tries++)
            {
                var output = node.SudoCommand("/usr/bin/ntpq -pw", RunOptions.LogOutput).OutputText;

                foreach (var manager in hiveDefinition.SortedManagers)
                {
                    // We're going to check the for presence of the manager's IP address
                    // or its name, the latter because [ntpq] appears to attempt a reverse
                    // IP address lookup which will resolve into one of the DNS names defined
                    // in the local [/etc/hosts] file.

                    if (!output.Contains(manager.PrivateAddress.ToString()) && !output.Contains(manager.Name.ToLower()))
                    {
                        fault = $"NTP: Manager [{manager.Name}/{manager.PrivateAddress}] is not answering.";

                        Thread.Sleep(retryDelay);
                        continue;
                    }

                    // Everything looks OK.

                    break;
                }
            }

            if (fault != null)
            {
                if (firstTry)
                {
                    // $hack(jeff.lill):
                    //
                    // I've seen the NTP check fail on a non-manager node, complaining
                    // that the connection attempt was rejected.  I manually restarted
                    // the node and then it worked.  I'm not sure if the rejected connection
                    // was being made to the local NTP service or from the local service
                    // to NTP running on the manager.
                    //
                    // I'm going to assume that it was to the local NTP service and I'm
                    // going to try mitigating this by restarting the local NTP service
                    // and then re-running the tests.  I'm only going to do this once.

                    node.SudoCommand("systemctl restart ntp", node.DefaultRunOptions & ~RunOptions.FaultOnError);

                    firstTry = false;
                    goto tryAgain;
                }

                node.Fault(fault);
            }
        }

        /// <summary>
        /// Verifies Docker health.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="hiveDefinition">The hive definition.</param>
        private static void CheckDocker(SshProxy<NodeDefinition> node, HiveDefinition hiveDefinition)
        {
            node.Status = "checking: docker";

            // This is a super simple ping to verify that Docker appears to be running.

            var response = node.SudoCommand("docker info");

            if (response.ExitCode != 0)
            {
                node.Fault($"Docker: {response.AllText}");
            }
        }

        /// <summary>
        /// Verifies Consul health.
        /// </summary>
        /// <param name="node">The manager node.</param>
        /// <param name="hiveDefinition">The hive definition.</param>
        private static void CheckConsul(SshProxy<NodeDefinition> node, HiveDefinition hiveDefinition)
        {
            node.Status = "checking: consul";

            // Verify that the daemon is running.

            switch (Program.ServiceManager)
            {
                case ServiceManager.Systemd:

                    {
                        var output = node.SudoCommand("systemctl status consul", RunOptions.LogOutput).OutputText;

                        if (!output.Contains("Active: active (running)"))
                        {
                            node.Fault($"Consul deamon is not running.");
                            return;
                        }
                    }
                    break;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Verifies Vault health for a node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="hiveDefinition">The hive definition.</param>
        private static void CheckVault(SshProxy<NodeDefinition> node, HiveDefinition hiveDefinition)
        {
            // $todo(jeff.lill): Implement this.

            return;

            node.Status = "checking: vault";

            // This is a minimal health test that just verifies that Vault
            // is listening for requests.  We're going to ping the local
            // Vault instance at [/v1/sys/health].
            //
            // Note that this should return a 500 status code with some
            // JSON content.  The reason for this is because we have not
            // yet initialized and unsealed the vault.

            var targetUrl = $"https://{node.Metadata.PrivateAddress}:{hiveDefinition.Vault.Port}/v1/sys/health?standbycode=200";

            using (var client = new HttpClient())
            {
                try
                {
                    var response = client.GetAsync(targetUrl).Result;

                    if (response.StatusCode != HttpStatusCode.OK && 
                        response.StatusCode != HttpStatusCode.InternalServerError)
                    {
                        node.Fault($"Vault: Unexpected HTTP response status [{(int) response.StatusCode}={response.StatusCode}]");
                        return;
                    }

                    if (!response.Content.Headers.ContentType.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                    {
                        node.Fault($"Vault: Unexpected content type [{response.Content.Headers.ContentType.MediaType}]");
                        return;
                    }
                }
                catch (Exception e)
                {
                    node.Fault($"Vault: {NeonHelper.ExceptionError(e)}");
                }
            }
        }
    }
}