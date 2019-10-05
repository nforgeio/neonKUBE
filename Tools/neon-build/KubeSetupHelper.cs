//-----------------------------------------------------------------------------
// FILE:        KubeSetupHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

using Neon.Common;
using Neon.Windows;

using Renci.SshNet;
using Renci.SshNet.Common;

namespace NeonBuild
{
    /// <summary>
    /// Kubernetes setup related information and actions (for <b>ksetup</b>).
    /// </summary>
    public class KubeSetupHelper
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses a target platform string.
        /// </summary>
        /// <param name="platform">The platform string.</param>
        /// <returns>The <see cref="KubeClientPlatform"/> value.</returns>
        /// <exception cref="KubeSetupException">Thrown when required environment variables aren't set or are invalid.</exception>
        private static KubeClientPlatform ParsePlatform(string platform)
        {
            switch (platform.ToLowerInvariant())
            {
                case "windows":

                    return KubeClientPlatform.Windows;

                case "osx":

                    return KubeClientPlatform.Osx;

                default:

                    throw new KubeSetupException($"[{platform}] is not a supported Kubernetes client platform.");
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private Action<string>  outputAction;
        private Action<string>  errorAction;
        private Stack<bool>     logEnabledStack;
        private PowerShell      powershell;
        private string          cacheRoot;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="platform">The target client platform.</param>
        /// <param name="commandLine">The command line where version and other options are parsed.</param>
        /// <param name="outputAction">Optionally specifies an action to receive logged output.</param>
        /// <param name="errorAction">Optionally specifies an action to receive logged error output.</param>
        /// <exception cref="KubeSetupException">Thrown when required environment variables aren't set or are invalid.</exception>
        /// <remarks>
        /// You can pass callbacks to the <paramref name="outputAction"/> and/or <paramref name="errorAction"/>
        /// parameters to be receive logged output and errors.  Note that <paramref name="outputAction"/> will receive
        /// both STDERR and STDOUT text if <paramref name="errorAction"/> isn't specified.
        /// </remarks>
        public KubeSetupHelper(KubeClientPlatform platform, CommandLine commandLine, Action<string> outputAction = null, Action<string> errorAction = null)
        {
            Covenant.Requires<ArgumentException>(commandLine != null, nameof(commandLine));

            this.Platform     = platform;
            this.outputAction = outputAction;
            this.errorAction  = errorAction;

            if (NeonHelper.IsWindows)
            {
                this.powershell = new PowerShell(
                    outputAction: text => Log(text),
                    errorAction:  text => LogError(text));
            }

            // Ensure that the component cache folder exists.

            cacheRoot = Environment.GetEnvironmentVariable("NF_CACHE");

            if (string.IsNullOrWhiteSpace(cacheRoot))
            {
                throw new KubeSetupException("[NF_CACHE] environment variable is not set or is empty.");
            }

            Directory.CreateDirectory(CacheRoot);

            // Extract the component versions from the command line and set the corresponding
            // environment variables.

            Version tempVersion;

            KubeVersion = commandLine.GetOption("--kube-version", Program.DefaultKubernetesVersion);

            if (!Version.TryParse(KubeVersion, out tempVersion))
            {
                throw new KubeSetupException($"[--kube-version={KubeVersion}] option is invalid.");
            }

            // Initialize the environment variables.

            Environment.SetEnvironmentVariable("NF_KUBE_VERSION", KubeVersion);
            Environment.SetEnvironmentVariable("NF_KUBECTL_URL", $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersion}/bin/windows/amd64/kubectl.exe");

            // Configure logging.

            logEnabledStack = new Stack<bool>();
            logEnabledStack.Push(true);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="platform">The target client platform as a string.</param>
        /// <param name="commandLine">The command line where version and other options are parsed.</param>
        /// <param name="outputAction">Optional action to be called when text lines are written to STDOUT.</param>
        /// <param name="errorAction">Optional action to be called when text lines are written to STDERR.</param>
        /// <exception cref="KubeSetupException">Thrown when required environment variables aren't set or are invalid or if the platform is not valid.</exception>
        public KubeSetupHelper(string platform, CommandLine commandLine, Action<string> outputAction = null, Action<string> errorAction = null)
            : this(ParsePlatform(platform), commandLine, outputAction, errorAction)
        {
        }

        /// <summary>
        /// Returns the target client platform.
        /// </summary>
        public KubeClientPlatform Platform { get; private set; }

        /// <summary>
        /// Returns the cache folder path (from the <c>NF_CACHE</c> environment variable),
        /// ensuring that the folder exists.
        /// </summary>
        public string CacheRoot
        {
            get
            {
                Directory.CreateDirectory(cacheRoot);
                return cacheRoot;
            }
        }

        /// <summary>
        /// Returns the virtual machine cache folder, ensuring that the folder exists.
        /// </summary>
        public string CacheVM
        {
            get
            {
                var path = Path.Combine(CacheRoot, "vm");

                Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// Returns the OSX cache folder, ensuring that the folder exists.
        /// </summary>
        public string CacheOsx
        {
            get
            {
                var path = Path.Combine(CacheRoot, "osx");

                Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// Returns the Windows cache folder, ensuring that the folder exists.
        /// </summary>
        public string CacheWindows
        {
            get
            {
                var path = Path.Combine(CacheRoot, "windows");

                Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// Returns the Ubuntu cache folder, ensuring that the folder exists.
        /// </summary>
        public string CacheUbuntu
        {
            get
            {
                var path = Path.Combine(CacheRoot, "ubuntu");

                Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// Returns the path to the cached PowerShell setup folder.
        /// </summary>
        public string CachePowerShellFolder
        {
            get
            {
                var path = Path.Combine(CacheRoot, "windows", "powershell");

                Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// URL to the latest preconfigured Ubuntu 16.04 Hyper-V virtual machine VHDX file.
        /// </summary>
        public string Ubuntu1604VhdxUrl => "https://s3-us-west-2.amazonaws.com/neonforge/ksetup/ubuntu-16.04.base.vhdx";

        /// <summary>
        /// The name of the base Ubuntu 16.04 image downloaded to the cache (VHDX file).
        /// </summary>
        public string Ubuntu1604BaseName => "ubuntu-16.04.base.vhdx";

        /// <summary>
        /// The root username for the <see cref="Ubuntu1604VhdxUrl"/> virtual machine image.
        /// </summary>
        public string Ubuntu1604Username => "sysadmin";

        /// <summary>
        /// The root password for the <see cref="Ubuntu1604VhdxUrl"/> virtual machine image.
        /// </summary>
        public string Ubuntu1604Password => "sysadmin0000";

        /// <summary>
        /// Returns the target <b>kubectl</b> tool version (from the <c>NF_KUBE_VERSION</c> environment variable).
        /// </summary>
        public string KubeVersion { get; private set; }

        /// <summary>
        /// Returns the download URL for the KUBECTL executable.
        /// </summary>
        public string KubeCtlUrl => Environment.GetEnvironmentVariable("NF_KUBECTL_URL");

        /// <summary>
        /// Returns the path to the current user folder.
        /// </summary>
        public string UserFolder => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        /// <summary>
        /// Returns the path to the root KSETUP source folder.
        /// </summary>
        public string SourceRepoFolder => Environment.GetEnvironmentVariable("NF_ROOT");

        /// <summary>
        /// Returns the path to the build output folder (from the <c>NF_BUILD</c> environment variable).
        /// </summary>
        public string BuildFolder => Environment.GetEnvironmentVariable("NF_BUILD");

        /// <summary>
        /// Returns the path to the project's external folder.
        /// </summary>
        public string ExternalFolder => Path.Combine(SourceRepoFolder, "External");

        /// <summary>
        /// Returns the URI to be used to download the Windows VirtualBox installer.
        /// </summary>
        public string VirtualBoxWindowsUrl => "https://download.virtualbox.org/virtualbox/6.0.0/VirtualBox-6.0.0-127566-Win.exe";

        /// <summary>
        /// Returns path to the <b>WinInstaller</b> project folder.
        /// </summary>
        public string WinInstallerFolder => Path.Combine(SourceRepoFolder, "Desktop", "WinInstaller");

        /// <summary>
        /// Returns the path to the Inno Setup compiler.
        /// </summary>
        public string InnoSetupCompilerPath
        {
            get
            {
                // $hack(jefflill): Hardcoded

                var path = @"C:\Program Files (x86)\Inno Setup 5\Compil32.exe";

                if (!File.Exists(path))
                {
                    throw new IOException("Inno Setup 5 is not installed.");
                }

                return path;
            }
        }

        /// <summary>
        /// Returns the <see cref="PowerShell"/> client to be used for configuration.
        /// </summary>
        /// <exception cref="NotImplementedException">Thrown if PowerShell is not available on the current operating system.</exception>
        public PowerShell PowerShell
        {
            get
            {
                if (powershell == null)
                {
                    throw new NotImplementedException("PowerShell is not available on the current operating system.");
                }

                return powershell;
            }
        }

        /// <summary>
        /// Returns the subfolder name to use for the client platform.
        /// </summary>
        /// <returns>The folder name.</returns>
        private string PlatformFolder
        {
            get
            {
                switch (Platform)
                {
                    case KubeClientPlatform.Osx:

                        return "osx";

                    case KubeClientPlatform.Windows:

                        return "windows";

                    default:

                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Returns the fully qualified path to the downloaded <b>kubectl</b> command line
        /// executable for a client platform.
        /// </summary>
        /// <returns>The file path.</returns>
        public string CachedKubeCtlPath
        {
            get
            {
                var tool = Platform == KubeClientPlatform.Windows ? "kubectl.exe" : "kubectl";

                return Path.Combine(CacheRoot, PlatformFolder, "kubectl", KubeVersion, tool);
            }
        }

        /// <summary>
        /// Clears any cached setup related components.
        /// </summary>
        public void Clear()
        {
            if (Directory.Exists(CacheRoot))
            {
                NeonHelper.DeleteFolderContents(CacheRoot);
            }
        }

        /// <summary>
        /// Downloads the required setup components for the target client platform if
        /// these files aren't already cached.
        /// </summary>
        public void Download()
        {
            switch (Platform)
            {
                case KubeClientPlatform.Windows:

                    using (var handler = new HttpClientHandler() { AllowAutoRedirect = true })
                    {
                        using (var client = new HttpClient(handler))
                        {
                            // Download: kubectl

                            var kubeCtlPath = CachedKubeCtlPath;

                            if (!File.Exists(kubeCtlPath))
                            {
                                LogLine($"Download: {KubeCtlUrl}");

                                using (var download = client.GetStreamAsync(KubeCtlUrl).Result)
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(kubeCtlPath));

                                    using (var output = new FileStream(kubeCtlPath, FileMode.Create, FileAccess.ReadWrite))
                                    {
                                        download.CopyTo(output);
                                    }
                                }
                            }

#if TODO
                            // We're not actually using this right now and it takes a long time to
                            // download, so we'll comment this out until we actually need it
                            // (if ever).

                            // Download VirtualBox installer

                            var virtualBoxPath = Path.Combine(CacheWindows, "virtualbox-setup.exe");

                            if (!File.Exists(virtualBoxPath))
                            {
                                LogLine($"Download: {VirtualBoxWindowsUrl}");

                                using (var download = client.GetStreamAsync(VirtualBoxWindowsUrl).Result)
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(virtualBoxPath));

                                    using (var output = new FileStream(virtualBoxPath, FileMode.Create, FileAccess.ReadWrite))
                                    {
                                        download.CopyTo(output);
                                    }
                                }
                            }
#endif
                        }
                    }

                    // Extract the PowerShell 6x installation files from the [External] folder.

                    if (Directory.GetFiles(CachePowerShellFolder, "*.*", SearchOption.AllDirectories).Length == 0)
                    {
                        LogLine($"Extract: PowerShell Core files");
                        PowerShellExecute($"Expand-Archive -LiteralPath \"{Path.Combine(ExternalFolder, "PowerShell-win-x86.zip")}\" -DestinationPath \"{this.CachePowerShellFolder}\" -Force");
                    }

                    break;

                case KubeClientPlatform.Osx:
                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Adds the cached component folders for the current versions to the PATH environment variable
        /// so that we can execute the tools without a path prefix.
        /// </summary>
        public void SetToolPath()
        {
            var kubeCtlFolder = Path.GetDirectoryName(CachedKubeCtlPath);

            Environment.SetEnvironmentVariable("PATH", $"{kubeCtlFolder};{Environment.ExpandEnvironmentVariables("%PATH%")}");
        }

        /// <summary>
        /// Pushes the log output enabled state.
        /// </summary>
        /// <param name="enabled">The new state (ignored if output is already disabled).</param>
        public void PushLogEnable(bool enabled)
        {
            lock (logEnabledStack)
            {
                logEnabledStack.Push(enabled && logEnabledStack.Peek());
            }
        }

        /// <summary>
        /// Pops the log output enabled state.
        /// </summary>
        public void PopLogEnable()
        {
            lock (logEnabledStack)
            {
                if (logEnabledStack.Count == 1)
                {
                    throw new InvalidOperationException("Stack underflow");
                }

                logEnabledStack.Pop();
            }
        }

        /// <summary>
        /// Returns <c>true</c> if log output is enabled.
        /// </summary>
        private bool LogEnabled
        {
            get
            {
                lock (logEnabledStack)
                {
                    return logEnabledStack.Peek();
                }
            }
        }

        /// <summary>
        /// Writes a line of text to the standard output.
        /// </summary>
        /// <param name="text">The text.</param>
        public void LogLine(string text = null)
        {
            if (!LogEnabled)
            {
                return;
            }

            text  = text ?? string.Empty;
            text += NeonHelper.LineEnding;

            outputAction?.Invoke(text);
        }

        /// <summary>
        /// Writes text to the standard output.
        /// </summary>
        /// <param name="text">The text.</param>
        public void Log(string text)
        {
            if (!LogEnabled || string.IsNullOrEmpty(text))
            {
                return;
            }

            outputAction?.Invoke(text);
        }

        /// <summary>
        /// Writes a line of text to error output.
        /// </summary>
        /// <param name="text">The text.</param>
        public void LogErrorLine(string text = null)
        {
            text  = text ?? string.Empty;
            text += NeonHelper.LineEnding;

            if (errorAction != null)
            {
                errorAction(text);
            }
            else
            {
                errorAction?.Invoke(text);
            }
        }

        /// <summary>
        /// Writes text to the standard error output.
        /// </summary>
        /// <param name="text">The text.</param>
        public void LogError(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            errorAction?.Invoke(text);
        }

        /// <summary>
        /// Executes a program, wiring up the the standard output and error streams so they
        /// can be intercepted via the output and error actions.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="ignoreError">Optionally specifies that non-zero exit codes are to be ignored.</param>
        /// <returns>The execution result.</returns>
        /// <exception cref="ExecuteException">Thrown for non-zero process exit codes and <paramref name="ignoreError"/><c>=false</c>.</exception>
        public ExecuteResponse Execute(string path, string args, bool ignoreError = false)
        {
            args = args ?? string.Empty;

            var command = $"{path} {args}";

            LogLine($"Execute: {command}");

            var result = NeonHelper.ExecuteCapture(path, args,
                outputAction: text => Log(text),
                errorAction: text => LogError(text));

            if (result.ExitCode != 0)
            {
                LogErrorLine($"*** EXITCODE={result.ExitCode}");

                if (!ignoreError)
                {
                    throw new ExecuteException(result.ExitCode, $"EXITCODE={result.ExitCode}: {command}");
                }
            }

            return result;
        }

        /// <summary>
        /// Executes a PowerShell command that returns a simple string result.
        /// </summary>
        /// <param name="command">The command string.</param>
        /// <param name="noEnvironmentVars">
        /// Optionally disables that environment variable subsitution (defaults to <c>false</c>).
        /// </param>
        /// <param name="logOutput">Enables logging of standard output (errors are always logged).</param>
        /// <returns>The command response.</returns>
        /// <exception cref="PowerShellException">Thrown if the command failed.</exception>
        /// <exception cref="NotImplementedException">Thrown for non-Windows operating system where PowerShell isn't available.</exception>
        public string PowerShellExecute(string command, bool noEnvironmentVars = false, bool logOutput = false)
        {
            if (PowerShell == null)
            {
                throw new NotImplementedException("PowerShell is not available on the current operating system.");
            }

            PushLogEnable(logOutput);

            try
            {
                return PowerShell.Execute(command, noEnvironmentVars);
            }
            finally
            {
                PopLogEnable();
            }
        }

        /// <summary>
        /// Executes a PowerShell command that returns result JSON, subsituting any
        /// environment variable references of the form <b>${NAME}</b> and returning a list 
        /// of <c>dynamic</c> objects parsed from the table with the object property
        /// names set to the table column names and the values parsed as strings.
        /// </summary>
        /// <param name="command">The command string.</param>
        /// <param name="noEnvironmentVars">
        /// Optionally disables that environment variable subsitution (defaults to <c>false</c>).
        /// </param>
        /// <param name="logOutput">Enables logging of standard output (errors are always logged).</param>
        /// <returns>The list of <c>dynamic</c> objects parsed from the command response.</returns>
        /// <exception cref="PowerShellException">Thrown if the command failed.</exception>
        public List<dynamic> PowerShellExecuteJson(string command, bool noEnvironmentVars = false, bool logOutput = false)
        {
            if (PowerShell == null)
            {
                throw new NotImplementedException("PowerShell is not available on the current operating system.");
            }

            PushLogEnable(logOutput);

            try
            {
                return PowerShell.ExecuteJson(command, noEnvironmentVars);
            }
            finally
            {
                PopLogEnable();
            }
        }

        /// <summary>
        /// Downloads a file to the cache if it's not already present.
        /// </summary>
        /// <param name="uri">The source URI.</param>
        /// <param name="path">The relative target file path.</param>
        /// <param name="message">The optional message to log when doenloading the file.</param>
        /// <param name="force">Optionally specifies that the file should be redownloaded if it already exists.</param>
        /// <returns>The fully qualified path to the downloaded file.</returns>
        public string DownloadToCache(string uri, string path, string message = null, bool force = false)
        {
            path = Path.Combine(CacheRoot, path);

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            if (force || !File.Exists(path))
            {
                if (!string.IsNullOrEmpty(message))
                {
                    LogLine(message);
                }

                try
                {
                    using (var handler = new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip, AllowAutoRedirect = true })
                    {
                        using (var client = new HttpClient(handler))
                        {
                            using (var output = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
                            {
                                var download = client.GetStreamAsync(uri).Result;

                                download.CopyTo(output);
                            }
                        }
                    }
                }
                catch
                {
                    // Delete a partially downloaded file.

                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    throw;
                }
            }

            return path;
        }
    }
}
