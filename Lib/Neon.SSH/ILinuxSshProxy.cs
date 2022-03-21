//-----------------------------------------------------------------------------
// FILE:	    ILinuxSshProxy.cs
// CONTRIBUTOR: Jeff Lill
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

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

using ICSharpCode.SharpZipLib.Zip;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Renci.SshNet;
using Renci.SshNet.Common;

namespace Neon.SSH
{
    /// <summary>
    /// Defines core methods and properties implemented by <see cref="LinuxSshProxy"/>.
    /// </summary>
    public interface ILinuxSshProxy : IDisposable
    {
        /// <summary>
        /// <para>
        /// Returns the name of the remote operating system (e.g. "Ubuntu").
        /// </para>
        /// <note>
        /// This is only valid after a connection has been established.
        /// </note>
        /// </summary>
        string OsName { get; }

        /// <summary>
        /// <para>
        /// Returns the version of the remote operating system (e.g. "18.04.1").
        /// </para>
        /// <note>
        /// This is only valid after a connection has been established.
        /// </note>
        /// </summary>
        Version OsVersion { get; }

        /// <summary>
        /// <para>
        /// Returns the Linux kernel release version installed on the remote machine.
        /// </para>
        /// <note>
        /// <para>
        /// This currently assumes that the kernel versions returned by <b>uname -r</b>
        /// are formatted like:
        /// </para>
        /// <list type="bullet">
        ///     <item>5.4.0</item>
        ///     <item>5.4.0-66-generic</item>
        ///     <item>5.4.72-microsoft-standard-WSL2</item>
        /// </list>
        /// <para>
        /// This property extracts the version (up to the first dash) and
        /// returns that and <see cref="KernelRelease"/> includes the full
        /// release text.
        /// </para>
        /// </note>
        /// </summary>
        Version KernelVersion { get; }

        /// <summary>
        /// <para>
        /// Describes the Linux kernel release installed on the remote machine.
        /// </para>
        /// <note>
        /// <para>
        /// This currently assumes that the kernel versions returned by <b>uname -r</b>
        /// are formatted like:
        /// </para>
        /// <list type="bullet">
        ///     <item>5.4.0</item>
        ///     <item>5.4.0-66-generic</item>
        ///     <item>5.4.72-microsoft-standard-WSL2</item>
        /// </list>
        /// <para>
        /// This property returns the full release string.  Use <see cref="KernelVersion"/>
        /// if you just want the version.
        /// </para>
        /// </note>
        /// </summary>
        string KernelRelease { get; }

        /// <summary>
        /// Returns the display name for the remote machine.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The cluster private IP address to used for connecting to the remote machine.
        /// </summary>
        IPAddress Address { get; set; }

        /// <summary>
        /// The SSH port.  This defaults to <b>22</b>.
        /// </summary>
        int SshPort { get; set; }

        /// <summary>
        /// The connection attempt timeout.  This defaults to <b>5</b> seconds.
        /// </summary>
        TimeSpan ConnectTimeout { get; set; }

        /// <summary>
        /// The file operation timeout.  This defaults to <b>30</b> seconds.
        /// </summary>
        TimeSpan FileTimeout { get; set; }

        /// <summary>
        /// The number of times to retry a failed remote command.  
        /// This defaults to <b>5</b>.
        /// </summary>
        int RetryCount { get; set; }

        /// <summary>
        /// Specifies the default options to be bitwise ORed with any specific
        /// options passed to a run or sudo execution command when the <see cref="RunOptions.Defaults"/> 
        /// flag is specified.  This defaults to <see cref="RunOptions.None"/>.
        /// </summary>
        /// <remarks>
        /// Setting this is a good way to specify a global default for flags like <see cref="RunOptions.FaultOnError"/>.
        /// </remarks>
        RunOptions DefaultRunOptions { get; set; }

        /// <summary>
        /// The PATH to use on the remote machine when executing commands in the
        /// session or <c>null</c>/empty to run commands without a path.  This
        /// defaults to the standard Linux path.
        /// </summary>
        /// <remarks>
        /// <note>
        /// When you modify this, be sure to use a colon (<b>:</b>) to separate 
        /// multiple directories as required.
        /// </note>
        /// </remarks>
        string RemotePath { get; set; }

        /// <summary>
        /// Returns the username used to log into the remote node.
        /// </summary>
        string Username { get; }

        /// <summary>
        /// The current remote machine status.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is intended to be used by management tools to indicate the state
        /// of the remote machine for UX purposes.  This property will be set by some methods such
        /// as <see cref="WaitForBoot"/> but can also be set explicitly by tools when they
        /// have an operation in progress on the remote machine.
        /// </para>
        /// <note>
        /// This will return a variation of <b>*** FAULTED ***</b> if <see cref="IsFaulted"/>=<c>true</c>.
        /// </note>
        /// </remarks>
        string Status { get; set; }

        /// <summary>
        /// Used to indicate that the remote machine will be involved in a configuration step.  
        /// This property is a bit of a hack used when displaying the status of a neonKUBE cluster setup.
        /// </summary>
        bool IsInvolved { get; set; }

        /// <summary>
        /// Used to indicate that the remote machine is actively being being configured.  This property is 
        /// a bit of a hack used when displaying the status of a neonKUBE cluster setup.
        /// </summary>
        bool IsConfiguring { get; set; }

        /// <summary>
        /// Indicates that the remote machine has completed or has failed the current set of operations.  
        /// This property is a bit of a hack used when displaying the status of a neonKUBE cluster setup.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This will always return <c>false</c> if the remote machine when <see cref="IsFaulted"/>=<c>true</c>.
        /// </note>
        /// </remarks>
        bool IsReady { get; set; }

        /// <summary>
        /// Indicates that the remote machine is in a faulted state because one or more operations
        /// have failed.  This property is a bit of a hack used when displaying the status of a neonKUBE
        /// cluster setup.
        /// </summary>
        bool IsFaulted { get; set; }

        /// <summary>
        /// Returns the path to the user's home folder on the remote machine.
        /// </summary>
        string HomeFolderPath { get; }

        /// <summary>
        /// Returns the path to the user's download folder on the remote machine.
        /// </summary>
        string DownloadFolderPath { get; }

        /// <summary>
        /// Returns the path to the user's upload folder on the remote machine.
        /// </summary>
        string UploadFolderPath { get; }

        /// <summary>
        /// Closes any open connections to the Linux remote machine but leaves open the
        /// opportunity to reconnect later.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This is similar to <see cref="IDisposable.Dispose"/> but dispose does
        /// not allow reconnection.
        /// </note>
        /// <para>
        /// This command is useful situations where the client application may temporarily
        /// lose contact with the remote machine if for example, when it is rebooted or the network
        /// configuration changes.
        /// </para>
        /// </remarks>
        void Disconnect();

        /// <summary>
        /// Updates the proxy credentials.  Call this whenever you change the
        /// password or SSH certificate for the user account we're using for the
        /// current proxy connection.  This ensures that the proxy will be able
        /// to reconnect to the service when required.
        /// </summary>
        /// <param name="newCredentials">The new credentials.</param>
        void UpdateCredentials(SshCredentials newCredentials);

        /// <summary>
        /// <para>
        /// Prevents <b>sudo</b> from prompting for passwords and also ensures that
        /// the <b>/home/root</b> directory exists and has the appropriate permissions.
        /// </para>
        /// <note>
        /// The connected user must already be a member of the <b>root</b> group.
        /// </note>
        /// <note>
        /// You do not need to call <see cref="Connect(TimeSpan)"/> or <see cref="WaitForBoot(TimeSpan?)"/>
        /// before calling this method (in fact, calling those methods will probably fail).
        /// </note>
        /// </summary>
        /// <param name="password">The current user's password.</param>
        /// <remarks>
        /// <para>
        /// This method uses the existence of a file at <b>/etc/neon-sshproxy-init</b>
        /// file to ensure that it only executes once per machine.  This file will be
        /// created the first time this method is called on the machine.
        /// </para>
        /// </remarks>
        void DisableSudoPrompt(string password);

        /// <summary>
        /// Shutdown the remote machine.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Reboot the remote machine.
        /// </summary>
        /// <param name="wait">Optionally waits for the remote machine to reboot and then reconnects (defaults to <c>true</c>).</param>
        void Reboot(bool wait = true);

        /// <summary>
        /// Writes text to the operation log.
        /// </summary>
        /// <param name="text">The text.</param>
        void Log(string text);

        /// <summary>
        /// Writes a line of text to the operation log.
        /// </summary>
        /// <param name="text">The text.</param>
        void LogLine(string text);

        /// <summary>
        /// Flushes the log.
        /// </summary>
        void LogFlush();

        /// <summary>
        /// Writes exception information to the operation log.
        /// </summary>
        /// <param name="e">The exception.</param>
        void LogException(Exception e);

        /// <summary>
        /// Writes exception information to the operation log.
        /// </summary>
        /// <param name="message">The operation details.</param>
        /// <param name="e">The exception.</param>
        void LogException(string message, Exception e);

        /// <summary>
        /// Puts the node proxy into the faulted state.
        /// </summary>
        /// <param name="message">The optional message to be logged.</param>
        void Fault(string message = null);

        /// <summary>
        /// Establishes a connection to the remote machine, disconnecting first if the proxy is already connected.
        /// </summary>
        /// <param name="timeout">Maximum amount of time to wait for a connection (defaults to <see cref="ConnectTimeout"/>).</param>
        /// <exception cref="SshProxyException">
        /// Thrown if the host hasn't been prepared yet and the SSH connection credentials are not username/password
        /// or if there's problem with low-level host configuration.
        /// </exception>
        /// <remarks>
        /// <note>
        /// The first time a connection is established is called on a particular host, password credentials 
        /// must be used so that low-level <b>sudo</b> configuration can be performed.  Subsequent connections
        /// can use TLS certificates.
        /// </note>
        /// </remarks>
        void Connect(TimeSpan timeout = default);

        /// <summary>
        /// Waits for the remote machine to boot by continuously attempting to establish a SSH session.
        /// </summary>
        /// <param name="timeout">The operation timeout (defaults to <b>10 minutes</b>).</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="SshProxyException">
        /// Thrown if the host hasn't been prepared yet and the SSH connection credentials are not username/password
        /// or if there's problem with low-level host configuration.
        /// </exception>
        /// <remarks>
        /// <note>
        /// The first time a connection is established is called on a particular host, password credentials 
        /// must be used so that low-level <b>sudo</b> configuration can be performed.  Subsequent connections
        /// can use TLS certificates.
        /// </note>
        /// <para>
        /// The method will attempt to connect to the remote machine every 10 seconds up to the specified
        /// timeout.  If it is unable to connect during this time, the exception thrown by the
        /// SSH client will be rethrown.
        /// </para>
        /// </remarks>
        void WaitForBoot(TimeSpan? timeout = null);

        /// <summary>
        /// <para>
        /// Creates and returns a clone of a low-level <see cref="SshClient"/> to 
        /// the remote endpoint.
        /// </para>
        /// <note>
        /// The caller is responsible for disposing the returned instance.
        /// </note>
        /// </summary>
        /// <returns>The cloned client.</returns>
        /// <exception cref="SshConnectionException">Thrown if a connection could not be established.</exception>
        SshClient CloneSshClient();

        /// <summary>
        /// <para>
        /// Creates and returns a clone of a low-level <see cref="ScpClient"/> to 
        /// the remote endpoint.
        /// </para>
        /// <note>
        /// The caller is responsible for disposing the returned instance.
        /// </note>
        /// </summary>
        /// <returns>The cloned client.</returns>
        /// <exception cref="SshConnectionException">Thrown if a connection could not be established.</exception>
        ScpClient CloneScpClient();

        /// <summary>
        /// Removes a file on the server if it exists.
        /// </summary>
        /// <param name="target">The path to the target file.</param>
        void RemoveFile(string target);

        /// <summary>
        /// Downloads a file from the Linux server and writes it out a stream.
        /// </summary>
        /// <param name="source">The source path of the file on the Linux server.</param>
        /// <param name="output">The output stream.</param>
        void Download(string source, Stream output);

        /// <summary>
        /// Downloads a file as bytes from the Linux server .
        /// </summary>
        /// <param name="source">The source path of the file on the Linux server.</param>
        /// <returns>The file contents as UTF8 text.</returns>
        byte[] DownloadBytes(string source);

        /// <summary>
        /// Downloads a file as text from the Linux server.
        /// </summary>
        /// <param name="source">The source path of the file on the Linux server.</param>
        /// <returns>The file contents as UTF8 text.</returns>
        string DownloadText(string source);

        /// <summary>
        /// Determines whether a directory exists on the remote server.
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <returns><c>true</c> if the directory exists.</returns>
        bool DirectoryExists(string path);

        /// <summary>
        /// Determines whether a file exists on the remote server.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns><c>true</c> if the file exists.</returns>
        bool FileExists(string path);

        /// <summary>
        /// Uploads a binary stream to the Linux server and then writes it to the file system.
        /// </summary>
        /// <param name="target">The target path on the Linux server.</param>
        /// <param name="input">The input stream.</param>
        /// <param name="permissions">Optionally specifies the file permissions (must be <c>chmod</c> compatible).</param>
        /// <param name="owner">Optionally specifies the file owner (must be <c>chown</c> compatible).</param>
        /// <param name="userPermissions">
        /// Optionally indicates that the operation should be performed with user-level permissions
        /// rather than <b>sudo</b>, which is the default.
        /// </param>
        /// <remarks>
        /// <note>
        /// <para>
        /// <b>Implementation Note:</b> The SSH.NET library we're using does not allow for
        /// files to be uploaded directly to arbitrary file system locations, even if the
        /// logged-in user has admin permissions.  The problem is that SSH.NET does not
        /// provide a way to use <b>sudo</b> to claim these higher permissions.
        /// </para>
        /// <para>
        /// The workaround is to create an upload folder in the user's home directory
        /// called <b>~/upload</b> and upload the file there first and then use SSH
        /// to move the file to its target location under sudo.
        /// </para>
        /// </note>
        /// </remarks>
        void Upload(string target, Stream input, string permissions = null, string owner = null, bool userPermissions = false);

        /// <summary>
        /// Uploads a byte array to a Linux server file.
        /// </summary>
        /// <param name="target">The target path of the file on the Linux server.</param>
        /// <param name="bytes">The bytes to be uploaded.</param>
        void UploadBytes(string target, byte[] bytes);

        /// <summary>
        /// Uploads a text stream to the Linux server and then writes it to the file system,
        /// converting any CR-LF line endings to the Unix-style LF.
        /// </summary>
        /// <param name="target">The target path on the Linux server.</param>
        /// <param name="textStream">The input stream.</param>
        /// <param name="tabStop">
        /// Optionally expands TABs into spaces when greater than zero or converts 
        /// a series of leading spaces into tabs if less than zero.
        /// </param>
        /// <param name="inputEncoding">Optionally specifies the input text encoding (defaults to UTF-8).</param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
        /// <param name="permissions">Optionally specifies the file permissions (must be <c>chmod</c> compatible).</param>
        /// <param name="owner">Optionally specifies the file owner (must be <c>chown</c> compatible).</param>
        /// <remarks>
        /// <note>
        /// Any Unicode Byte Order Marker (BOM) at start of the input stream will be removed.
        /// </note>
        /// <note>
        /// <para>
        /// <b>Implementation Note:</b> The SSH.NET library we're using does not allow for
        /// files to be uploaded directly to arbitrary file system locations, even if the
        /// logged-in user has admin permissions.  The problem is that SSH.NET does not
        /// provide a way to use <b>sudo</b> to claim these higher permissions.
        /// </para>
        /// <para>
        /// The workaround is to create an upload folder in the user's home directory
        /// called <b>~/upload</b> and upload the file there first and then use SSH
        /// to move the file to its target location under sudo.
        /// </para>
        /// </note>
        /// </remarks>
        void UploadText(string target, Stream textStream, int tabStop = 0, Encoding inputEncoding = null, Encoding outputEncoding = null, string permissions = null, string owner = null);

        /// <summary>
        /// Uploads a text string to the Linux server and then writes it to the file system,
        /// converting any CR-LF line endings to the Unix-style LF.
        /// </summary>
        /// <param name="target">The target path on the Linux server.</param>
        /// <param name="text">The input text.</param>
        /// <param name="tabStop">
        /// Optionally expands TABs into spaces when greater than zero or converts 
        /// a series of leading spaces into tabs if less than zero.
        /// </param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
        /// <param name="permissions">Optionally specifies the file permissions (must be <c>chmod</c> compatible).</param>
        /// <param name="owner">Optionally specifies the file owner (must be <c>chown</c> compatible).</param>
        /// <remarks>
        /// <note>
        /// <para>
        /// <b>Implementation Note:</b> The SSH.NET library we're using does not allow for
        /// files to be uploaded directly to arbitrary file system locations, even if the
        /// logged-in user has admin permissions.  The problem is that SSH.NET does not
        /// provide a way to use <b>sudo</b> to claim these higher permissions.
        /// </para>
        /// <para>
        /// The workaround is to create an upload folder in the user's home directory
        /// called <b>~/upload</b> and upload the file there first and then use SSH
        /// to move the file to its target location under sudo.
        /// </para>
        /// </note>
        /// </remarks>
        void UploadText(string target, string text, int tabStop = 0, Encoding outputEncoding = null, string permissions = null, string owner = null);

        /// <summary>
        /// Uploads text from a <see cref="StringBuilder"/> to the Linux server and then writes it to the file system,
        /// converting any CR-LF line endings to the Unix-style LF.
        /// </summary>
        /// <param name="target">The target path on the Linux server.</param>
        /// <param name="text">The input text.</param>
        /// <param name="tabStop">
        /// Optionally expands TABs into spaces when greater than zero or converts 
        /// a series of leading spaces into tabs if less than zero.
        /// </param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
        /// <param name="permissions">Optionally specifies the file permissions (must be <c>chmod</c> compatible).</param>
        /// <param name="owner">Optionally specifies the file owner (must be <c>chown</c> compatible).</param>
        /// <remarks>
        /// <note>
        /// <para>
        /// <b>Implementation Note:</b> The SSH.NET library we're using does not allow for
        /// files to be uploaded directly to arbitrary file system locations, even if the
        /// logged-in user has admin permissions.  The problem is that SSH.NET does not
        /// provide a way to use <b>sudo</b> to claim these higher permissions.
        /// </para>
        /// <para>
        /// The workaround is to create an upload folder in the user's home directory
        /// called <b>~/upload</b> and upload the file there first and then use SSH
        /// to move the file to its target location under sudo.
        /// </para>
        /// </note>
        /// </remarks>
        void UploadText(string target, StringBuilder text, int tabStop = 0, Encoding outputEncoding = null, string permissions = null, string owner = null);

        /// <summary>
        /// Downloads a file from the remote node to the local file computer, creating
        /// parent folders as necessary.
        /// </summary>
        /// <param name="source">The source path on the Linux server.</param>
        /// <param name="target">The target path on the local computer.</param>
        void Download(string source, string target);

        /// <summary>
        /// Runs a shell command on the Linux server.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="args">The optional command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method uses <see cref="DefaultRunOptions"/> when executing the command.
        /// </para>
        /// <para>
        /// You can override this behavior by passing an <see cref="RunOptions"/> to
        /// the <see cref="RunCommand(string, RunOptions, object[])"/> override.
        /// </para>
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// </remarks>
        CommandResponse RunCommand(string command, params object[] args);

        /// <summary>
        /// Runs a shell command on the Linux server with <see cref="RunOptions"/>.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="runOptions">The execution options.</param>
        /// <param name="args">The optional command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <exception cref="RemoteCommandException">
        /// Thrown if the command returned a non-zero exit code and 
        /// <see cref="RunOptions.FaultOnError"/> was passed.
        /// </exception>
        /// <remarks>
        /// <note>
        /// <paramref name="command"/> may not include single quotes or redirect
        /// angle brackets such as <b>&lt;</b> or <b>>&gt;</b>.  For more complex
        /// command, try uploading and executing a <see cref="CommandBundle"/> instead.
        /// </note>
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// <para>
        /// The <paramref name="runOptions"/> flags control how this command functions.
        /// If <see cref="RunOptions.FaultOnError"/> is set, then commands that return
        /// a non-zero exit code will put the server into the faulted state by setting
        /// <see cref="IsFaulted"/>=<c>true</c> and throwing a <see cref="RemoteCommandException"/>.
        /// This means that <see cref="IsReady"/> will  always return <c>false</c> 
        /// afterwards and subsequent calls to <see cref="RunCommand(string, object[])"/>
        /// and <see cref="SudoCommand(string, object[])"/> will be ignored unless 
        /// <see cref="RunOptions.RunWhenFaulted"/> is passed with the future command. 
        /// <see cref="RunOptions.LogOnErrorOnly"/> indicates that command output should
        /// be logged only for non-zero exit codes.
        /// </para>
        /// </remarks>
        CommandResponse RunCommand(string command, RunOptions runOptions, params object[] args);

        /// <summary>
        /// Runs a <see cref="CommandBundle"/> with user permissions on the remote machine.
        /// </summary>
        /// <param name="bundle">The bundle.</param>
        /// <param name="runOptions">The execution options (defaults to <see cref="RunOptions.Defaults"/>).</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <note>
        /// <paramref name="bundle"/> may not include single quotes or redirect
        /// angle brackets such as <b>&lt;</b> or <b>>&gt;</b>.  For more complex
        /// command, try uploading and executing a <see cref="CommandBundle"/> instead.
        /// </note>
        /// <para>
        /// This method is intended for situations where one or more files need to be uploaded to a cluster node 
        /// and then be used when a command is executed.
        /// </para>
        /// <para>
        /// To use this class, construct an instance passing the command and arguments to be executed.  The command be 
        /// an absolute reference to an executable in folders such as <b>/bin</b> or <b>/usr/local/bin</b>, an executable
        /// somewhere on the current PATH, or relative to the files unpacked from the bundle.  The current working directory
        /// will be set to the folder where the bundle was unpacked, so you can reference local executables like
        /// <b>./MyExecutable</b>.
        /// </para>
        /// <para>
        /// Once a bundle is constructed, you will add <see cref="CommandFile"/> instances specifying the
        /// file data you want to include.  These include the relative path to the file to be uploaded as well
        /// as its text or binary data.  You may also indicate whether each file is to be marked as executable.
        /// </para>
        /// <note>
        /// <paramref name="runOptions"/> is set to <see cref="RunOptions.Defaults"/> by default.  This means
        /// that the flags specified by <see cref="DefaultRunOptions"/> will be be used.  This is a 
        /// good way to specify a global default for flags like <see cref="RunOptions.FaultOnError"/>.
        /// </note>
        /// <note>
        /// This command requires that the <b>unzip</b> package be installed on the host.
        /// </note>
        /// </remarks>
        CommandResponse RunCommand(CommandBundle bundle, RunOptions runOptions = RunOptions.Defaults);

        /// <summary>
        /// Runs a shell command on the Linux server under <b>sudo</b>.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="args">The optional command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <note>
        /// <paramref name="command"/> may not include single quotes or redirect
        /// angle brackets such as <b>&lt;</b> or <b>>&gt;</b>.  For more complex
        /// command, try uploading and executing a <see cref="CommandBundle"/> instead.
        /// </note>
        /// <para>
        /// This method uses the <see cref="DefaultRunOptions"/> when executing the command.
        /// </para>
        /// <para>
        /// You can override this behavior by passing an <see cref="RunOptions"/> to
        /// the <see cref="RunCommand(string, RunOptions, object[])"/> override.
        /// </para>
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// </remarks>
        CommandResponse SudoCommand(string command, params object[] args);

        /// <summary>
        /// Runs a shell command on the Linux server under <b>sudo</b> with <see cref="RunOptions"/>.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="runOptions">The execution options.</param>
        /// <param name="args">The optional command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <note>
        /// <paramref name="command"/> may not include single quotes or redirect
        /// angle brackets such as <b>&lt;</b> or <b>>&gt;</b>.  For more complex
        /// command, try uploading and executing a <see cref="CommandBundle"/> instead.
        /// </note>
        /// <para>
        /// The <paramref name="runOptions"/> flags control how this command functions.
        /// If <see cref="RunOptions.FaultOnError"/> is set, then commands that return
        /// a non-zero exit code will put the server into the faulted state by setting
        /// <see cref="IsFaulted"/>=<c>true</c>.  This means that <see cref="IsReady"/> will 
        /// always return <c>false</c> afterwards and subsequent command executions will be 
        /// ignored unless  <see cref="RunOptions.RunWhenFaulted"/> is specified for the 
        /// future command.
        /// </para>
        /// <para>
        /// <see cref="RunOptions.LogOnErrorOnly"/> indicates that command output should
        /// be logged only for non-zero exit codes.
        /// </para>
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// </remarks>
        CommandResponse SudoCommand(string command, RunOptions runOptions, params object[] args);

        /// <summary>
        /// Runs a shell command on the Linux server under <b>sudo</b> as a specific user.
        /// </summary>
        /// <param name="user">The username.</param>
        /// <param name="command">The command.</param>
        /// <param name="args">The optional command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <note>
        /// <paramref name="command"/> may not include single quotes or redirect
        /// angle brackets such as <b>&lt;</b> or <b>>&gt;</b>.  For more complex
        /// command, try uploading and executing a <see cref="CommandBundle"/> instead.
        /// </note>
        /// <para>
        /// This method uses the <see cref="DefaultRunOptions"/> when executing the command.
        /// </para>
        /// <para>
        /// You can override this behavior by passing an <see cref="RunOptions"/> to
        /// the <see cref="RunCommand(string, RunOptions, object[])"/> override.
        /// </para>
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// </remarks>
        CommandResponse SudoCommandAsUser(string user, string command, params object[] args);

        /// <summary>
        /// Runs a shell command on the Linux server under <b>sudo</b> as a specific user
        /// and with <see cref="RunOptions"/>.
        /// </summary>
        /// <param name="user">The username.</param>
        /// <param name="command">The command.</param>
        /// <param name="runOptions">The execution options.</param>
        /// <param name="args">The optional command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <note>
        /// <paramref name="command"/> may not include single quotes or redirect
        /// angle brackets such as <b>&lt;</b> or <b>>&gt;</b>.  For more complex
        /// command, try uploading and executing a <see cref="CommandBundle"/> instead.
        /// </note>
        /// <para>
        /// The <paramref name="runOptions"/> flags control how this command functions.
        /// If <see cref="RunOptions.FaultOnError"/> is set, then commands that return
        /// a non-zero exit code will put the server into the faulted state by setting
        /// <see cref="IsFaulted"/>=<c>true</c>.  This means that <see cref="IsReady"/> will 
        /// always return <c>false</c> afterwards and subsequent command executions will be 
        /// ignored unless  <see cref="RunOptions.RunWhenFaulted"/> is specified for the 
        /// future command.
        /// </para>
        /// <para>
        /// <see cref="RunOptions.LogOnErrorOnly"/> indicates that command output should
        /// be logged only for non-zero exit codes.
        /// </para>
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// </remarks>
        CommandResponse SudoCommandAsUser(string user, string command, RunOptions runOptions, params object[] args);

        /// <summary>
        /// Runs a <see cref="CommandBundle"/> under <b>sudo</b> on the remote machine.
        /// </summary>
        /// <param name="bundle">The bundle.</param>
        /// <param name="runOptions">The execution options (defaults to <see cref="RunOptions.Defaults"/>).</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method is intended for situations where one or more files need to be uploaded to a cluster node 
        /// and then be used when a command is executed.
        /// </para>
        /// <para>
        /// To use this class, construct an instance passing the command and arguments to be executed.  The command be 
        /// an absolute reference to an executable in folders such as <b>/bin</b> or <b>/usr/local/bin</b>, an executable
        /// somewhere on the current PATH, or relative to the files unpacked from the bundle.  The current working directory
        /// will be set to the folder where the bundle was unpacked, so you can reference local executables like
        /// <b>./MyExecutable</b>.
        /// </para>
        /// <para>
        /// Once a bundle is constructed, you will add <see cref="CommandFile"/> instances specifying the
        /// file data you want to include.  These include the relative path to the file to be uploaded as well
        /// as its text or binary data.  You may also indicate whether each file is to be marked as executable.
        /// </para>
        /// <note>
        /// <paramref name="runOptions"/> is set to <see cref="RunOptions.Defaults"/> by default.  This means
        /// that the flags specified by <see cref="DefaultRunOptions"/> will be be used.  This is a 
        /// good way to specify a global default for flags like <see cref="RunOptions.FaultOnError"/>.
        /// </note>
        /// <note>
        /// This command requires that the <b>unzip</b> package be installed on the host.
        /// </note>
        /// </remarks>
        CommandResponse SudoCommand(CommandBundle bundle, RunOptions runOptions = RunOptions.Defaults);

        /// <summary>
        /// Verifies a TLS/SSL certificate.
        /// </summary>
        /// <param name="name">The certificate name (included in errors).</param>
        /// <param name="certificate">The certificate being tested or <c>null</c>.</param>
        /// <param name="hostname">The hostname to be secured by the certificate.</param>
        /// <returns>The command response.</returns>
        /// <remarks>
        /// You may pass <paramref name="certificate"/> as <c>null</c> to indicate that no 
        /// checking is to be performed as a convienence.
        /// </remarks>
        CommandResponse VerifyCertificate(string name, TlsCertificate certificate, string hostname);

        /// <summary>
        /// Creates an interactive shell.
        /// </summary>
        /// <returns>A <see cref="ShellStream"/>.</returns>
        ShellStream CreateShell();

        /// <summary>
        /// Creates an interactive shell for running with <b>sudo</b> permissions. 
        /// </summary>
        /// <returns>A <see cref="ShellStream"/>.</returns>
        ShellStream CreateSudoShell();

        /// <summary>
        /// Returns the name of the network interface assigned to a specific IP address.
        /// </summary>
        /// <param name="address">The target IP address.</param>
        /// <returns>The network interface name.</returns>
        /// <exception cref="SshProxyException">Thrown if the interface was not found.</exception>
        /// <remarks>
        /// <para>
        /// In the olden days, network devices were assigned names like <b>eth0</b>,
        /// <b>eth1</b>,... during boot somewhat randomly and there was no guarantee
        /// that the same assignments would be made on subsequent server restarts.
        /// </para>
        /// <para>
        /// Modern Linux systems generate predictable network interfaces names during
        /// boot by enumerating the physical devices installed and generating device
        /// names based on the topology of the system (e.g. slots, channels,...).
        /// This is discussed <a href="https://www.freedesktop.org/wiki/Software/systemd/PredictableNetworkInterfaceNames/">here</a>.
        /// </para>
        /// <note>
        /// Cloud environments as well as environments where nodes hosted on hypervisors 
        /// like Hyper-V or XenServer will still assign interface names like <b>eth0</b>...
        /// This method will still work for these environments.
        /// </note>
        /// </remarks>
        string GetNetworkInterface(IPAddress address);

        /// <summary>
        /// Returns the current time (UTC) on the remote machine.
        /// </summary>
        /// <returns>The machine's current <see cref="DateTime"/> (UTC).</returns>
        DateTime GetTimeUtc();

        /// <summary>
        /// Lists information about the disks on the remote machine.
        /// </summary>
        /// <param name="fixedDiskOnly">
        /// Optionally specifies that non-fixed disks should be returned as well 
        /// (defaults to <c>true</c>).
        /// </param>
        /// <returns>
        /// A <see cref="Dictionary{TKey, TValue}"/> relating the case sensitive 
        /// disk name to a <see cref="LinuxDiskInfo"/> including information
        /// on the disk partitions.
        /// </returns>
        Dictionary<string, LinuxDiskInfo> ListDisks(bool fixedDiskOnly = true);

        /// <summary>
        /// Returns the names of any unpartitioned disks (excluding floppy disks).
        /// </summary>
        /// <returns>The names of the unpartitioned disks.</returns>
        List<string> ListUnpartitionedDisks();

        /// <summary>
        /// Returns the names of any partitioned disks (excluding floppy disks).
        /// </summary>
        /// <returns>The names of the unpartitioned disks.</returns>
        List<string> ListPartitionedDisks();

        /// <summary>
        /// Uses <c>kubectl apply -f</c> to apply a YAML file.
        /// </summary>
        /// <param name="yaml">The YAML file contents.</param>
        /// <param name="runOptions">Optional <see cref="RunOptions"/>.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        CommandResponse KubectlApply(string yaml, RunOptions runOptions = RunOptions.Defaults);

        /// <summary>
        /// Uses <c>kubectl apply -f</c> to apply a YAML file.
        /// </summary>
        /// <param name="sbYaml">The YAML file contents.</param>
        /// <param name="runOptions">Optional <see cref="RunOptions"/>.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        CommandResponse KubeCtlApply(StringBuilder sbYaml, RunOptions runOptions = RunOptions.Defaults);

        /// <summary>
        /// <para>
        /// Returns an indication of whether the <b>neon-init</b> service has been executed
        /// on the remote machine.  This service is deployed to neonKUBE cluster nodes to
        /// act as a poor-man's <b>cloud-init</b> used to configure the network and credentials 
        /// by mounting a virual ISO drive with a configuration script for non-cloud environments.
        /// </para>
        /// <note>
        /// The <b>neon-init</b> service disables itself after running for the first time.
        /// You'll need to call <see cref="SetNeonInitStatus(bool, bool)"/> passing <c>false</c>
        /// the re-enable this service when required.
        /// </note>
        /// </summary>
        /// <returns><c>true</c> if <b>neon-init</b> has been executed.</returns>
        bool GetNeonInitStatus();

        /// <summary>
        /// <para>
        /// Manually sets the <b>neon-init</b> service execution status. 
        /// </para>
        /// <para>
        /// The <b>neon-init</b> service is deployed to neonKUBE cluster nodes to act
        /// as a poor-man's <b>cloud-init</b> to configure the network and credentials 
        /// by mounting a virual ISO drive with a configuration script for non-cloud 
        /// environments.
        /// </para>
        /// <para>
        /// Calling this with <c>true</c> will prevent the <b>neon-init</b> service from
        /// looking for a mounted ISO on next boot and executing the special script  there.
        /// Calling this with <c>false</c> will re-enable the <b>neon-init</b> service
        /// when the machine is rebooted.
        /// </para>
        /// <note>
        /// The <b>neon-init</b> service disables itself after running for the first time.
        /// You'll need to call <see cref="SetNeonInitStatus(bool, bool)"/> passing <c>false</c>
        /// the re-enable this service when required.
        /// </note>
        /// </summary>
        /// <param name="initialized">
        /// Pass <c>true</c> to indicate that the <b>neon-init</b> service has been executed, 
        /// <c>false</c> to clear the status.
        /// </param>
        /// <param name="keepNetworkSettings">
        /// Optionally retains the static network settings when <paramref name="initialized"/> is
        /// passed as <c>false</c>, otherwise the original (probably DHCP) settings will be restored.
        /// </param>
        void SetNeonInitStatus(bool initialized, bool keepNetworkSettings = false);
    }
}