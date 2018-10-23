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

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Hive;
using Neon.Time;

namespace NeonSecretRetriever
{
    /// <summary>
    /// This service provides a way to obtain Docker secrets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The idea is to start this application as a Docker service, mounting a secret.
    /// Then the name of the secret and a Consul key path is specified on the command 
    /// line.  The service simply reads the secret file, writes it to the Consul key
    /// and then goes to sleep.
    /// </para>
    /// <para>
    /// Invoke this like:
    /// </para>
    /// <example>
    /// neon-secret-retriever MYSECRET MYCONSULKEY
    /// </example>
    /// </remarks>
    public class Program
    {
        private static readonly string serviceName = $"neon-secret-retriever:{GitVersion}";

        private static ProcessTerminator        terminator;
        private static INeonLogger              log;
        private static HiveProxy                hive;

        /// <summary>
        /// Main program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        public static void Main(string[] args)
        {
            LogManager.Default.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.Default.GetLogger(typeof(Program));
            log.LogInfo(() => $"Starting [{serviceName}]");
            log.LogInfo(() => $"LOG_LEVEL={LogManager.Default.LogLevel.ToString().ToUpper()}");

            // Create process terminator to handle termination signals.

            terminator = new ProcessTerminator(log);

            terminator.AddHandler(() => terminator.ReadyToExit());

            // Establish the hive connections.

            if (NeonHelper.IsDevWorkstation)
            {
                var vaultCredentialsSecret = "neon-proxy-manager-credentials";

                Environment.SetEnvironmentVariable("VAULT_CREDENTIALS", vaultCredentialsSecret);

                hive = HiveHelper.OpenHiveRemote(new DebugSecrets().VaultAppRole(vaultCredentialsSecret, "neon-proxy-manager"));
            }
            else
            {
                hive = HiveHelper.OpenHive();
            }

            // Parse the command line.

            var commandLine = new CommandLine(args);

            if (commandLine.Arguments.Count() != 2)
            {
                log.LogError($"*** ERROR: Invalid command line arguments: {commandLine}");
                log.LogError($"***        Expected: MYSECRET MYCONSULKEY");
                SleepForever();
            }

            var secretName = commandLine.Arguments[0];
            var consulKey  = commandLine.Arguments[1];

            try
            {
                // Read the secret file.

                var secretPath = ($"/run/secrets/{secretName}");

                log.LogInfo($"Reading secret [{secretName}].");

                if (!File.Exists(secretPath))
                {
                    log.LogError($"The secret file [{secretPath}] does not exist.");
                }
                else
                {
                    var secret = File.ReadAllBytes(secretPath);

                    log.LogInfo($"Writing secret to Consul [{consulKey}].");
                    HiveHelper.Consul.KV.PutBytes(consulKey, secret).Wait();
                }
            }
            catch (Exception e)
            {
                log.LogError(e);
            }

            SleepForever();
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
        /// Sleep forever.
        /// </summary>
        private static void SleepForever()
        {
            log.LogInfo("Sleeping forever...");

            while (true)
            {
                Thread.Sleep(TimeSpan.FromMinutes(5));
            }
        }

        /// <summary>
        /// <para>
        /// Exits the service with an exit code.  This method defaults to using
        /// the <see cref="ProcessTerminator"/> if there is one to gracefully exit 
        /// the program.  The program will be exited immediately by passing 
        /// <paramref name="immediate"/><c>=true</c> or when there is no process
        /// terminator.
        /// </para>
        /// <note>
        /// You should always ensure that you exit the current operation
        /// context after calling this method.  This will ensure that the
        /// <see cref="ProcessTerminator"/> will have a chance to determine
        /// that the process was able to be stopped cleanly.
        /// </note>
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        /// <param name="immediate">Forces an immediate ungraceful exit.</param>
        public static void Exit(int exitCode, bool immediate = false)
        {
            log.LogInfo(() => $"Exiting: [{serviceName}]");

            if (terminator == null || immediate)
            {
                Environment.Exit(exitCode);
            }
            else
            {
                // Signal the terminator to stop on another thread
                // so this method can return and the caller will be
                // able to return from its operation code.

                var threadStart = new ThreadStart(() => terminator.Exit(exitCode));
                var thread      = new Thread(threadStart);

                thread.Start();
            }
        }
    }
}
