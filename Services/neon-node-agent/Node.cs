//-----------------------------------------------------------------------------
// FILE:	    Node.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Kube.Resources;
using Neon.Retry;
using Neon.Tasks;

using k8s.Models;

using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;

using Prometheus;
using Tomlyn;

namespace NeonNodeAgent
{
    /// <summary>
    /// Abstracts access to the host node.
    /// </summary>
    public static class Node
    {
        /// <summary>
        /// The Linux path where the host node's file system is mounted into the container.
        /// </summary>
        public const string HostMount = "/mnt/host";

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Node()
        {
            try
            {
                Name = File.ReadAllLines(LinuxPath.Combine(HostMount, "etc/hostname")).First().Trim();
            }
            catch
            {
                Name = "UNKNOWN";
            }

            AgentId = Guid.NewGuid().ToString("d");
        }

        /// <summary>
        /// Returns the cluster node's host name.
        /// </summary>
        public static string Name { get; private set; }

        /// <summary>
        /// Returns a globally unique ID for the executing node agent.
        /// </summary>
        public static string AgentId { get; private set; }

        /// <summary>
        /// <para>
        /// Synchronously executes a process to run an executable file and then waits for the process to terminate
        /// while capturing any output written to the standard output and error streams.
        /// </para>
        /// <note>
        /// This method is nearly the same as <see cref="NeonHelper.ExecuteCaptureAsync(string, object[], string, Dictionary{string, string}, TimeSpan?, Process, TextReader, Encoding, Action{Process})"/>
        /// with the only difference being that the <b>workingDirectory</b> parameter has been removed here.
        /// </note>
        /// </summary>
        /// <param name="path">
        /// <para>
        /// Name or path to the executable file.
        /// </para>
        /// <note>
        /// The <c>PATH</c> environment variable will be searched for the executable file 
        /// when no path is specified.
        /// </note>
        /// </param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Optional maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// Optional existing <see cref="Process"/> instance used to launch the process.
        /// </param>
        /// <param name="workingDirectory">
        /// Optionally specifies the working directory for executing the program.
        /// </param>
        /// <param name="environmentVariables">
        /// Optionally specifies the environment variables to be passed into the process.
        /// The new process inherits the current processes variables when this is <c>null</c>.
        /// </param>
        /// <param name="outputAction">Optional action that will be called when the process outputs some text.</param>
        /// <param name="errorAction">Optional action that will be called when the process outputs some error text.</param>
        /// <param name="input">
        /// Optionally specifies a <see cref="TextReader"/> with text to be sent 
        /// to the process as standard input.
        /// </param>
        /// <param name="outputEncoding">
        /// Optionally specifies the expected standard output/error encoding.  This defaults to 
        /// <c>null</c> which sets the default system codepage.
        /// </param>
        /// <param name="processCallback">
        /// Optionally passed to obtain the details of the procvess created to execute the command.
        /// When a non-null value is passed, the callback will be called with the <see cref="Process"/> 
        /// instances just after it is launched.
        /// </param>
        /// <returns>
        /// The <see cref="ExecuteResponse"/> including the process exit code and capture 
        /// standard output and error streams.
        /// </returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is exceeded and execution has not completed in time 
        /// then a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// <para>
        /// You can optionally specify the <paramref name="outputAction"/> and/or <paramref name="errorAction"/>
        /// callbacks to receive the process output as it is received.  <paramref name="outputAction"/> will 
        /// be called with both the STDOUT and STDERR streams if <paramref name="errorAction"/> is <c>null</c>
        /// otherwise it will called only with STDOUT text.
        /// </para>
        /// </remarks>
        public static ExecuteResponse ExecuteCapture(
            string                      path, 
            object[]                    args, 
            TimeSpan?                   timeout              = null,
            Process                     process              = null,
            Dictionary<string, string>  environmentVariables = null,
            Action<string>              outputAction         = null,
            Action<string>              errorAction          = null,
            TextReader                  input                = null,
            Encoding                    outputEncoding       = null,
            Action<Process>             processCallback      = null)
        {
            return NeonHelper.ExecuteCapture(
                path:                 path,
                args:                 args,
                timeout:              timeout,
                process:              process, 
                workingDirectory:     Environment.CurrentDirectory,
                environmentVariables: environmentVariables,
                outputAction:         outputAction,
                errorAction:          errorAction,
                input:                input,
                outputEncoding:       outputEncoding,
                processCallback:      processCallback);
        }

        /// <summary>
        /// <para>
        /// Asynchronously executes a process to run an executable file and then waits for the process to terminate
        /// while capturing any output written to the standard output and error streams.
        /// </para>
        /// <note>
        /// This method is nearly the same as <see cref="NeonHelper.ExecuteCaptureAsync(string, object[], string, Dictionary{string, string}, TimeSpan?, Process, TextReader, Encoding, Action{Process})"/>
        /// with the only difference being that the <b>workingDirectory</b> parameter has been removed here.
        /// </note>
        /// </summary>
        /// <param name="path">
        /// <para>
        /// Name or path to the executable file.
        /// </para>
        /// <note>
        /// The <c>PATH</c> environment variable will be searched for the executable file 
        /// when no path is specified.
        /// </note>
        /// </param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// Optional existing <see cref="Process"/> instance used to launch the process.
        /// </param>
        /// <param name="workingDirectory">
        /// Optionally specifies the working directory for executing the program.  This defaults
        /// to <b>/tmp</b>.
        /// </param>
        /// <param name="environmentVariables">
        /// Optionally specifies the environment variables to be passed into the process.
        /// The new process inherits the current processes variables when this is <c>null</c>.
        /// </param>
        /// <param name="input">
        /// Optionally specifies a <see cref="TextReader"/> with text to be sent 
        /// to the process as input.
        /// </param>
        /// <param name="outputEncoding">
        /// Optionally specifies the expected standard output/error encoding.  This defaults to 
        /// <c>null</c> which sets the default system codepage.
        /// </param>
        /// <param name="processCallback">
        /// Optionally passed to obtain the details of the procvess created to execute the command.
        /// When a non-null value is passed, the callback will be called with the <see cref="Process"/> 
        /// instances just after it is launched.
        /// </param>
        /// <returns>
        /// The <see cref="ExecuteResponse"/> including the process exit code and capture 
        /// standard output and error streams.
        /// </returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is exceeded and execution has not completed in time 
        /// then a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static async Task<ExecuteResponse> ExecuteCaptureAsync(
            string                      path, 
            object[]                    args,
            Dictionary<string, string>  environmentVariables = null,
            TimeSpan?                   timeout              = null, 
            Process                     process              = null,
            TextReader                  input                = null,
            Encoding                    outputEncoding       = null,
            Action<Process>             processCallback      = null)
        {
            await SyncContext.Clear;

            return await NeonHelper.ExecuteCaptureAsync(
                path:                 path,
                args:                 args,
                workingDirectory:     Environment.CurrentDirectory,
                environmentVariables: environmentVariables,
                timeout:              timeout,
                process:              process,
                input:                input,
                outputEncoding:       outputEncoding,
                processCallback:      processCallback);
        }
    }
}
