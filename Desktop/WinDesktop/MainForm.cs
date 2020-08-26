//-----------------------------------------------------------------------------
// FILE:	    MainForm.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.Net.Http.Server;

using Neon;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube;
using Neon.Net;

namespace WinDesktop
{
    /// <summary>
    /// The main application form.  Note that this form will always be hidden.
    /// </summary>
    public partial class MainForm : Form
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the current (and only) main form instance so that other
        /// parts of the app can manipulate the UI.
        /// </summary>
        public static MainForm Current { get; private set; }

        //---------------------------------------------------------------------
        // Instance members

        private const double animationFrameRate = 2;
        private const string headendError       = "Unable to contact the neonKUBE headend service.";

        private Icon                appIcon;
        private Icon                disconnectedIcon;
        private Icon                connectedIcon;
        private Icon                errorIcon;
        private AnimatedIcon        connectingAnimation;
        private AnimatedIcon        workingAnimation;
        private AnimatedIcon        errorAnimation;
        private ContextMenuStrip    contextMenuStrip;
        private bool                operationInProgress;
        private RemoteOperation     remoteOperation;
        private Stack<NotifyState>  notifyStack;
        private List<ReverseProxy>  proxies;
        private List<PortForward>   portForwards;
        private KubeConfigContext   proxiedContext;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainForm()
        {
            MainForm.Current = this;

            InitializeComponent();

            Load  += MainForm_Load;
            Shown += (s, a) => Visible = false; // The main form should always be hidden

            // Ensure that temporary files are written to the users temporary folder because
            // there's a decent chance that this folder will be encrypted at rest.

            TempFile.Root   = KubeHelper.TempFolder;
            TempFolder.Root = KubeHelper.TempFolder;

            // Preload the notification icons and animations for better performance.

            appIcon             = new Icon(@"Images\app.ico");
            connectedIcon       = new Icon(@"Images\connected.ico");
            disconnectedIcon    = new Icon(@"Images\disconnected.ico");
            errorIcon           = new Icon(@"Images\error.ico");
            connectingAnimation = AnimatedIcon.Load("Images", "connecting", animationFrameRate);
            workingAnimation    = AnimatedIcon.Load("Images", "working", animationFrameRate);
            errorAnimation      = AnimatedIcon.Load("Images", "error", animationFrameRate);
            notifyStack         = new Stack<NotifyState>();

            // Initialize the cluster hosting provider components.

            HostingLoader.Initialize();

            // Initialize the client state.

            proxies = new List<ReverseProxy>();
            portForwards = new List<PortForward>();
            Headend = new HeadendClient();
            KubeHelper.LoadClientConfig();
        }

        /// <summary>
        /// Indicates whether the application is connected to a cluster.
        /// </summary>
        public bool IsConnected => KubeHelper.CurrentContext != null;

        /// <summary>
        /// Returns the neonKUBE head client to be used to query the headend services.
        /// </summary>
        public HeadendClient Headend { get; private set; }

        /// <summary>
        /// Handles form initialization.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void MainForm_Load(object sender, EventArgs args)
        {
            // Start the desktop API service that [neon-cli] will use
            // to communicate with the desktop application.  Note that
            // this will fail if another instance of the desktop is 
            // running.

            try
            {
                DesktopService.Start();
            }
            catch
            {
                MessageBox.Show($"Another neonDESKTOP instance is already running or another application is already listening on [127.0.0.1:{KubeHelper.ClientConfig.DesktopServicePort}].",
                    "neonDESKTOP", MessageBoxButtons.OK, MessageBoxIcon.Error);

                Environment.Exit(1);
            }

            // Set the text labels on the main form.  Nobody should ever see
            // this because the form should remain hidden but we'll put something
            // here just in case.

            productNameLabel.Text  = $"{Build.ProductName}  v{Build.NeonDesktopVersion}";
            copyrightLabel.Text    = Build.Copyright;
            licenseLinkLabel.Text  = Build.ProductLicense;

            // Initialize the notify icon and its context memu.

            SetBalloonText(Build.ProductName);

            notifyIcon.Icon             = disconnectedIcon;
            notifyIcon.ContextMenuStrip = contextMenuStrip = new ContextMenuStrip();
            notifyIcon.Visible          = true;
            contextMenuStrip.Opening    += ContextMenuOpening;

            // Setup a timer to periodically keep the UI in sync with any changes.

            statusTimer.Interval = (int)TimeSpan.FromSeconds(KubeHelper.ClientConfig.StatusPollSeconds).TotalMilliseconds;
            statusTimer.Tick    += async (s, a) => await UpdateUIStateAsync();
            statusTimer.Start();
        }

        /// <summary>
        /// Handles license link clicks.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void licenseLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs args)
        {
            NeonHelper.OpenBrowser(Build.ProductLicenseUrl);
        }

        /// <summary>
        /// Ensures that an action is performed on the UI thread.
        /// </summary>
        /// <param name="action">The action.</param>
        private void InvokeOnUIThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                Invoke(action);
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Ensures that an function is invoked on the UI thread.
        /// </summary>
        /// <typeparam name="TResult">The function result type.</typeparam>
        /// <param name="function">The function.</param>
        private TResult InvokeOnUIThread<TResult>(Func<TResult> function)
        {
            Covenant.Requires<ArgumentNullException>(function != null, nameof(function));

            var result = default(TResult);

            if (InvokeRequired)
            {
                Invoke(new Action(
                    () =>
                    {
                        result = function();
                    }));
            }
            else
            {
                result = function();
            }

            return result;
        }

        /// <summary>
        /// <para>
        /// Sets the notify icon balloon text.
        /// </para>
        /// <note>
        /// Windows limits the balloon text to 64 characters.  This method will trim
        /// the text to fit if necessary.
        /// </note>
        /// </summary>
        /// <param name="balloonText">The balloon text.</param>
        private void SetBalloonText(string balloonText)
        {
            InvokeOnUIThread(
                () =>
                {
                    balloonText = balloonText ?? string.Empty;

                    if (balloonText.Length > 64)
                    {
                        balloonText = balloonText.Substring(0, 64 - 3) + "...";
                    }

                    notifyIcon.Text = balloonText;
                });
        }

        /// <summary>
        /// Starts a notify icon animation.
        /// </summary>
        /// <param name="animatedIcon">The icon animation.</param>
        /// <param name="balloonText">Optional text to be displayed in the balloon during the animation.</param>
        /// <param name="isTransient">
        /// Optionally indicates that the state is not associated with
        /// an operation indicating an error or other transient status.
        /// </param>
        /// <param name="isError">
        /// Optionally indicates that the application is in the error state.
        /// This implies <see cref="IsTransient"/><c>=true</c>.
        /// </param>
        /// <remarks>
        /// Calls to this method may be recursed and should be matched 
        /// with a call to <see cref="StopNotifyAnimation"/>.  The
        /// amimation will actually stop when the last matching
        /// <see cref="StartNotifyAnimation"/> call was matched with
        /// the last <see cref="StopNotifyAnimation"/>.
        /// </remarks>
        private void StartNotifyAnimation(AnimatedIcon animatedIcon, string balloonText = null, bool isTransient = false, bool isError = false)
        {
            Covenant.Requires<ArgumentNullException>(animatedIcon != null, nameof(animatedIcon));

            InvokeOnUIThread(
                () =>
                {
                    notifyStack.Push(new NotifyState(animatedIcon, balloonText, isTransient, isError));

                    if (!string.IsNullOrEmpty(balloonText))
                    {
                        SetBalloonText(balloonText);
                    }

                    if (animatedIcon != null)
                    {
                        animationTimer.Stop();
                        animationTimer.Interval = (int)TimeSpan.FromSeconds(1 / animatedIcon.FrameRate).TotalMilliseconds;
                        animationTimer.Tick    +=
                            (s, a) =>
                            {
                                notifyIcon.Icon = animatedIcon.GetNextFrame();
                            };

                        animationTimer.Start();
                    }
                });
        }

        /// <summary>
        /// Stops the notify icon animation.
        /// </summary>
        /// <param name="force">Optionally force the animation to stop regardless of the nesting level.</param>
        private void StopNotifyAnimation(bool force = false)
        {
            InvokeOnUIThread(
                () =>
                {
                    if (force)
                    {
                        if (notifyStack.Count > 0)
                        {
                            animationTimer.Stop();
                            notifyStack.Clear();
                            PostUpdateUIState();
                        }

                        return;
                    }

                    if (notifyStack.Count == 0)
                    {
                        throw new InvalidOperationException("StopNotifyAnimation: Stack underflow.");
                    }

                    notifyStack.Pop();
                    animationTimer.Stop();

                    if (notifyStack.Count == 0)
                    {
                        PostUpdateUIState();
                    }
                    else
                    {
                        // We need to restart the previous icon animation in the
                        // stack (if there is one).

                        var animatedIcon = (AnimatedIcon)null;

                        for (int i = notifyStack.Count - 1; i >= 0; i--)
                        {
                            var notifyState = notifyStack.ElementAt(i);

                            if (notifyState.AnimatedIcon != null)
                            {
                                animatedIcon = notifyState.AnimatedIcon;
                                break;
                            }
                        }

                        if (animatedIcon != null)
                        {
                            animationTimer.Interval = (int)TimeSpan.FromSeconds(1 / animatedIcon.FrameRate).TotalMilliseconds;
                            animationTimer.Tick    +=
                                (s, a) =>
                                {
                                    notifyIcon.Icon = animatedIcon.GetNextFrame();
                                };

                            animationTimer.Start();
                        }

                        // We also need to restore the previous balloon text.

                        if (notifyStack.Count > 0)
                        {
                            SetBalloonText(notifyStack.Peek().BalloonText);
                        }
                        else
                        {
                            SetBalloonText(string.Empty);
                        }
                    }
                });
        }

        /// <summary>
        /// Displays the notify icon's balloon (AKA toast).
        /// </summary>
        /// <param name="text">The message text.</param>
        /// <param name="title">The ballon title text (defaults to the application name).</param>
        /// <param name="icon">The optional tool tip icon (defaults to <see cref="ToolTipIcon.Info"/>).</param>
        private void ShowToast(string text, string title = null, ToolTipIcon icon = ToolTipIcon.Info)
        {
            InvokeOnUIThread(
                () =>
                {
                    notifyIcon.ShowBalloonTip(0, title ?? this.Text, text, icon);
                });
        }

        /// <summary>
        /// Indicates that an operation is starting by optionally displaying a working
        /// animation and optionally displaying a status toast.
        /// </summary>
        /// <param name="animatedIcon">The optional notify icon animation.</param>
        /// <param name="toastText">The optional toast text.</param>
        private void StartOperation(AnimatedIcon animatedIcon = null, string toastText = null)
        {
            InvokeOnUIThread(
                () =>
                {
                    operationInProgress = true;

                    if (animatedIcon != null)
                    {
                        StartNotifyAnimation(animatedIcon);
                    }

                    if (!string.IsNullOrEmpty(toastText))
                    {
                        ShowToast(toastText);
                    }
                });
        }

        /// <summary>
        /// Indicates that the current operation has completed.
        /// </summary>
        private void StopOperation()
        {
            InvokeOnUIThread(
                () =>
                {
                    StopNotifyAnimation(force: true);

                    notifyStack.Clear();
                    operationInProgress = false;

                    PostUpdateUIState();
                });
        }

        /// <summary>
        /// Indicates that the current operation failed.
        /// </summary>
        /// <param name="toastErrorText">The optional toast error text.</param>
        private void StopFailedOperation(string toastErrorText = null)
        {
            InvokeOnUIThread(
                () =>
                {
                    StopNotifyAnimation(force: true);
                    PostUpdateUIState();

                    if (!string.IsNullOrEmpty(toastErrorText))
                    {
                        ShowToast(toastErrorText, icon: ToolTipIcon.Error);
                    }
                });
        }

        /// <summary>
        /// Places the application in the error state.
        /// </summary>
        /// <param name="balloonText">The message to be displayed in the notify icon balloon.</param>
        private void SetErrorState(string balloonText)
        {
            InvokeOnUIThread(
                () =>
                {
                    if (notifyStack.Count > 0 && notifyStack.Peek().IsTransient)
                    {
                        // $hack(jefflill):
                        //
                        // This is a bit of a hack.  If the top trasient item indicates 
                        // an error, we're going to simply replace the text.  If it's
                        // not an error, we're going to pop the old transient state
                        // and start an error animation.

                        if (notifyStack.Peek().IsError)
                        {
                            notifyStack.Peek().BalloonText = balloonText;
                        }
                        else
                        {
                            StopNotifyAnimation();
                            StartNotifyAnimation(errorAnimation, balloonText, isError: true);
                        }
                    }
                    else
                    {
                        StartNotifyAnimation(errorAnimation, balloonText, isError: true);
                    }

                    if (!string.IsNullOrEmpty(balloonText))
                    {
                        SetBalloonText(balloonText);
                    }
                });
        }

        /// <summary>
        /// Returns <c>true</c> if the application is currently in an error state.
        /// </summary>
        private bool InErrorState => InvokeOnUIThread(() => notifyStack.Count > 0 && notifyStack.Peek().IsError);

        /// <summary>
        /// Resets the application error state.
        /// </summary>
        private void ResetErrorState()
        {
            InvokeOnUIThread(
                () =>
                {
                    if (!InErrorState)
                    {
                        return;
                    }

                    StopNotifyAnimation();
                });
        }

        /// <summary>
        /// Stops any running reverse proxies.
        /// </summary>
        private void StopProxies()
        {
            InvokeOnUIThread(
                () =>
                {
                    lock (Program.SyncLock)
                    {
                        foreach (var proxy in proxies)
                        {
                            proxy.Dispose();
                        }

                        proxies.Clear();
                    }
                });
        }

        /// <summary>
        /// Stops any running reverse proxies.
        /// </summary>
        private void StopPortForwards()
        {
            InvokeOnUIThread(
                () =>
                {
                    lock (Program.SyncLock)
                    {
                        foreach (var proxy in portForwards)
                        {
                            proxy.Dispose();
                        }

                        portForwards.Clear();
                    }
                });
        }

        /// <summary>
        /// Updates the running proxies to match the current cluster 
        /// (if there is one).  This may only be called on the UI thread.
        /// </summary>
        private async Task UpdateProxiesAsync()
        {
            if (InvokeRequired)
            {
                throw new InvalidOperationException($"[{nameof(UpdateProxiesAsync)}()] may only be called on the UI thread.");
            }

            if (KubeHelper.CurrentContext == null)
            {
                StopProxies();
                StopPortForwards();
            }
            else
            {
                var cluster = Program.GetCluster();

                // We're going to use the current Kubenetes context name and cluster ID
                // to determine whether we're still connected to the same cluster.

                if (proxiedContext != null)
                {
                    if (proxiedContext.Name == KubeHelper.CurrentContext.Name &&
                        proxiedContext.Extension.ClusterId == KubeHelper.CurrentContext.Extension.ClusterId)
                    {
                        // We're still proxying the same cluster so no changes 
                        // are required.

                        return;
                    }
                }

                StopProxies();
                StopPortForwards();

                if (KubeHelper.CurrentContext == null)
                {
                    // Wr're not logged into a cluster so don't start any proxies.

                    return;
                }

                try
                {
                    // Start the connecting animation if we're not already in the error state.

                    if (!InErrorState)
                    {
                        StartNotifyAnimation(connectingAnimation, $"{KubeHelper.CurrentContextName}: Connecting...", isTransient: true);
                    }

                    //-------------------------------------------------------------
                    // The Kubernetes dashboard reverse proxy.

                    // Setup a callback that transparently adds an [Authentication] header
                    // to all requests with the correct bearer token.  We'll need to
                    // obtain the token secret via two steps:
                    //
                    //      1. Identify the dashboard token secret by listing all secrets
                    //         in the [kube-system] namespace looking for one named like
                    //         [root-user-token-*].
                    //
                    //      2. Reading that secret and extracting the value.

                    var response       = (ExecuteResponse)null;
                    var secretName     = string.Empty;
                    var dashboardToken = string.Empty;

                    await Task.Run(
                        async () =>
                        {
                            response = KubeHelper.Kubectl("--namespace", "kube-system", "get", "secrets", "-o=name");
                            await Task.CompletedTask;
                        });

                    if (response.ExitCode != 0)
                    {
                        try
                        {
                            response.EnsureSuccess();
                        }
                        catch (Exception e)
                        {
                            Program.LogError(e);
                            SetErrorState($"{KubeHelper.CurrentContextName}: Kubernetes API failure");
                            return;
                        }
                    }

                    // Step 1: Determine the secret name.

                    using (var reader = new StringReader(response.OutputText))
                    {
                        const string secretPrefix = "secret/";

                        secretName = reader.Lines().FirstOrDefault(line => line.StartsWith($"{secretPrefix}root-user-token-"));

                        Covenant.Assert(!string.IsNullOrEmpty(secretName));

                        secretName = secretName.Substring(secretPrefix.Length);
                    }

                    // Step 2: Describe the secret and extract the token value.  This
                    //         is a bit of a hack because I'm making assumptions about
                    //         the output format.

                    await Task.Run(
                        async () =>
                        {
                            response = KubeHelper.Kubectl("--namespace", "kube-system", "describe", "secret", secretName);
                            await Task.CompletedTask;
                        });

                    if (response.ExitCode != 0)
                    {
                        try
                        {
                            response.EnsureSuccess();
                        }
                        catch (Exception e)
                        {
                            Program.LogError(e);
                            SetErrorState($"{KubeHelper.CurrentContextName}: Kubernetes API failure");
                            return;
                        }
                    }

                    using (var reader = new StringReader(response.OutputText))
                    {
                        var tokenLine = reader.Lines().FirstOrDefault(line => line.StartsWith("token:"));

                        Covenant.Assert(!string.IsNullOrEmpty(tokenLine));

                        dashboardToken = tokenLine.Split(' ', 2).Skip(1).First().Trim();
                    }

                    Action<RequestContext> kubernetesDashboardRequestHandler =
                        context =>
                        {
                            context.Request.Headers.Add("Authorization", $"Bearer {dashboardToken}");
                        };

                    // Start the proxy.

                    var userContext   = KubeHelper.Config.GetUser(KubeHelper.CurrentContext.Properties.User);
                    var certPem       = Encoding.UTF8.GetString(Convert.FromBase64String(userContext.Properties.ClientCertificateData));
                    var keyPem        = Encoding.UTF8.GetString(Convert.FromBase64String(userContext.Properties.ClientKeyData));
                    var dashboardCert = TlsCertificate.Parse(KubeHelper.CurrentContext.Extension.KubernetesDashboardCertificate).ToX509(publicOnly: true);

                    var kubeDashboardProxy =
                        new ReverseProxy(
                            localPort: KubeHelper.ClientConfig.KubeDashboardProxyPort,
                            remotePort: KubeHostPorts.KubeDashboard,
                            remoteHost: cluster.GetReachableMaster().Address.ToString(),
                            validCertificate: dashboardCert,
                            requestHandler: kubernetesDashboardRequestHandler);

                    proxies.Add(kubeDashboardProxy);

                    var kibanaDashboardProxy =
                        new PortForward(
                            serviceName: "kibana-kibana",
                            localPort: KubeConst.KibanaDashboardProxyPort,
                            remotePort: KubeConst.KibanaDashboardProxyPort,
                            @namespace: "monitoring");

                    portForwards.Add(kibanaDashboardProxy);

                    var prometheusDashboardProxy =
                        new PortForward(
                            serviceName: "prometheus-operated",
                            localPort: KubeConst.PrometheusDashboardProxyPort,
                            remotePort: KubeConst.PrometheusDashboardProxyPort,
                            @namespace: "monitoring");

                    portForwards.Add(prometheusDashboardProxy);

                    var kialiDashboardProxy =
                        new PortForward(
                            serviceName: "kiali",
                            localPort: KubeConst.KialiDashboardProxyPort,
                            remotePort: KubeConst.KialiDashboardProxyPort,
                            @namespace: "monitoring");

                    portForwards.Add(kialiDashboardProxy);

                    var grafanaDashboardProxy =
                        new PortForward(
                            serviceName: "grafana",
                            localPort: KubeConst.GrafanaDashboardProxyPort,
                            remotePort: 80,
                            @namespace: "monitoring");


                    portForwards.Add(grafanaDashboardProxy);

                    //-------------------------------------------------------------
                    // Remember which cluster context we're proxying.

                    proxiedContext = KubeHelper.CurrentContext;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    // Stop any non-error transient animation on the top of the notify stack.

                    if (notifyStack.Count > 0 && notifyStack.Peek().IsTransient && !notifyStack.Peek().IsError)
                    {
                        StopNotifyAnimation();
                    }
                }
            }
        }

        
        //---------------------------------------------------------------------
        // These methods are called by [DesktopService] (and perhaps from some
        // other places):

        /// <summary>
        /// Schedules a call to <see cref="UpdateUIStateAsync"/> on the UI thread
        /// by posting a message to the UI message loop.  Note that the actual
        /// operation will be performed some time in the near future.
        /// </summary>
        public void PostUpdateUIState()
        {
            SynchronizationContext.Current?.Post(
                async state => await UpdateUIStateAsync(),
                        state: null);
        }

        /// <summary>
        /// <para>
        /// Synchronizes the UI state with the current cluster configuration.
        /// </para>
        /// <note>
        /// This method is somewhat special in that it must be executed on the UI
        /// thread and if it's called on another thread, then the method will post
        /// a message to itself to invoke itself shortly on the UI thread.
        /// </note>
        /// </summary>
        public async Task UpdateUIStateAsync()
        {
            if (InvokeRequired)
            {
                PostUpdateUIState();
                return;
            }

            KubeHelper.LoadConfig();
            await UpdateProxiesAsync();

            if (InErrorState)
            {
                return;
            }

            if (!operationInProgress)
            {
                notifyIcon.Icon = IsConnected ? connectedIcon : disconnectedIcon;

                if (notifyStack.Count > 0 && !string.IsNullOrEmpty(notifyStack.Peek().BalloonText))
                {
                    SetBalloonText(notifyStack.Peek().BalloonText);
                }
                else if (IsConnected)
                {
                    SetBalloonText($"{Text}: {KubeHelper.CurrentContextName}");
                }
                else
                {
                    SetBalloonText($"{Text}: disconnected");
                }
            }
            else if (remoteOperation != null && NeonHelper.GetProcessById(remoteOperation.ProcessId) == null)
            {
                // The original [neon-cli] process is no longer running;
                // it must have terminated before signalling the end
                // of the operation.  We're going to clear the operation
                // status.
                //
                // This is an important fail-safe.

                StopOperation();
                return;
            }
        }

        /// <summary>
        /// Signals the start of a long-running operation.
        /// </summary>
        /// <param name="operation">The <b>neon-cli</b> operation information.</param>
        public void OnRemoteStartOperation(RemoteOperation operation)
        {
            InvokeOnUIThread(
                () =>
                {
                    if (operationInProgress)
                    {
                        // Another operation is already in progress.  If the current
                        // operation was initiated by the same [neon-cli] process then
                        // we'll just substitute the new operation info otherwise
                        // we'll start a new operation.
                        //
                        // If the current operation was initiated by the Desktop app
                        // then we'll ignore the new operation.

                        if (remoteOperation != null && remoteOperation.ProcessId == operation.ProcessId)
                        {
                            remoteOperation = operation;
                            PostUpdateUIState();

                        }
                        else
                        {
                            remoteOperation = operation;
                            //StartOperation(workingAnimation);     // $todo(jefflill): Notification status is a bit of a mess.
                        }

                        SetBalloonText(operation.Summary);
                    }
                    else
                    {
                        // Remove any transient notification.

                        if (notifyStack.Count > 0 && notifyStack.Peek().IsTransient)
                        {
                            //StopNotifyAnimation();
                        }

                        remoteOperation = operation;
                        //StartOperation(workingAnimation);
                        PostUpdateUIState();
                    }
                });
        }

        /// <summary>
        /// Signals the end of a long-running operation.
        /// </summary>
        /// <param name="operation">The <b>neon-cli</b> operation information.</param>
        public void OnEndOperation(RemoteOperation operation)
        {
            InvokeOnUIThread(
                () =>
                {
                    if (operationInProgress)
                    {
                        remoteOperation = null;
                        StopOperation();

                        if (!string.IsNullOrEmpty(operation.CompletedToast))
                        {
                            ShowToast(operation.CompletedToast);
                        }

                        if (operation.Failed)
                        {
                            StartNotifyAnimation(errorAnimation, operation.CompletedToast, isError: true);
                        }
                    }
                });
        }

        /// <summary>
        /// Signals that the workstation has logged into a cluster.
        /// </summary>
        public void OnLogin()
        {
            InvokeOnUIThread(
                () =>
                {
                    PostUpdateUIState();
                });
        }

        /// <summary>
        /// Signals that the workstation has logged out of a cluster.
        /// </summary>
        public void OnLogout()
        {
            InvokeOnUIThread(
                () =>
                {
                    PostUpdateUIState();
                });
        }

        //---------------------------------------------------------------------
        // Menu commands

        /// <summary>
        /// Poulates the context menu when it is clicked, based on the current
        /// application state.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void ContextMenuOpening(object sender, EventArgs args)
        {
            contextMenuStrip.Items.Clear();

            // Append submenus for each of the cluster contexts that have
            // neonKUBE extensions.  We're not going to try to manage 
            // non-neonKUBE clusters.
            //
            // Put a check mark next to the logged in cluster (if there
            // is one) and also enable [Logout] if we're logged in.

            var contexts = KubeHelper.Config.Contexts
                .Where(c => c.Extension != null)
                .OrderBy(c => c.Name)
                .ToArray();

            var currentContextName = (string)KubeHelper.CurrentContextName;
            var loggedIn           = !string.IsNullOrEmpty(currentContextName);;

            if (contexts.Length > 0)
            {
                var contextMenu = new ToolStripMenuItem(loggedIn ? currentContextName : "Login to") { Checked = loggedIn, Enabled = !operationInProgress };

                contextMenu.Checked = loggedIn;

                if (loggedIn)
                {
                    contextMenu.DropDownItems.Add(new ToolStripMenuItem(currentContextName) { Checked = true, Enabled = !operationInProgress });
                }

                var addedContextsSeparator = false;

                foreach (var context in contexts.Where(c => c.Name != currentContextName))
                {
                    if (!addedContextsSeparator)
                    {
                        contextMenu.DropDownItems.Add("-");
                        addedContextsSeparator = true;
                    }

                    contextMenu.DropDownItems.Add(new ToolStripMenuItem(context.Name, null, OnClusterContext) { Enabled = !operationInProgress });
                }

                contextMenu.DropDownItems.Add("-");
                contextMenu.DropDownItems.Add(new ToolStripMenuItem("Logout", null, OnLogoutCommand) { Enabled = loggedIn && !operationInProgress });

                contextMenuStrip.Items.Add(contextMenu);
            }

            // Append cluster-specific menus.

            if (loggedIn)
            {
                contextMenuStrip.Items.Add("-");

                var dashboardsMenu = new ToolStripMenuItem("Dashboard") { Enabled = loggedIn && !operationInProgress };

                dashboardsMenu.DropDownItems.Add(new ToolStripMenuItem("Kubernetes", null, OnKubernetesDashboardCommand) { Enabled = loggedIn && !operationInProgress });
                dashboardsMenu.DropDownItems.Add(new ToolStripMenuItem("Kibana", null, OnKibanaDashboardCommand) { Enabled = loggedIn && !operationInProgress });
                dashboardsMenu.DropDownItems.Add(new ToolStripMenuItem("Prometheus", null, OnPrometheusDashboardCommand) { Enabled = loggedIn && !operationInProgress });
                dashboardsMenu.DropDownItems.Add(new ToolStripMenuItem("Kiali", null, OnKialiDashboardCommand) { Enabled = loggedIn && !operationInProgress });
                dashboardsMenu.DropDownItems.Add(new ToolStripMenuItem("Grafana", null, OnGrafanaDashboardCommand) { Enabled = loggedIn && !operationInProgress });

                contextMenuStrip.Items.Add(dashboardsMenu);
            }

            // Append the static commands.

            contextMenuStrip.Items.Add("-");
            contextMenuStrip.Items.Add(new ToolStripMenuItem("GitHub", null, OnGitHubCommand));
            contextMenuStrip.Items.Add(new ToolStripMenuItem("Help", null, OnHelpCommand));
            contextMenuStrip.Items.Add(new ToolStripMenuItem("About", null, OnAboutCommand));
            contextMenuStrip.Items.Add("-");
            contextMenuStrip.Items.Add(new ToolStripMenuItem("Settings", null, OnSettingsCommand));
            contextMenuStrip.Items.Add(new ToolStripMenuItem("Command", null, OnCmdCommand));
            contextMenuStrip.Items.Add(new ToolStripMenuItem("Check for Updates", null, OnCheckForUpdatesCommand) { Enabled = !operationInProgress });
            contextMenuStrip.Items.Add("-");
            contextMenuStrip.Items.Add(new ToolStripMenuItem("Exit", null, OnExitCommand));
        }

        /// <summary>
        /// Handles the <b>Github</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private async void OnGitHubCommand(object sender, EventArgs args)
        {
            StartOperation(workingAnimation);

            try
            {
                var clientInfo = await Headend.GetClientInfoAsync();

                NeonHelper.OpenBrowser(clientInfo.GitHubUrl);
            }
            catch
            {
                StopFailedOperation(headendError);
                return;
            }
            finally
            {
                StopOperation();
            }
        }

        /// <summary>
        /// Handles the <b>Help</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private async void OnHelpCommand(object sender, EventArgs args)
        {
            StartOperation(workingAnimation);

            try
            {
                var clientInfo = await Headend.GetClientInfoAsync();

                NeonHelper.OpenBrowser(clientInfo.HelpUrl);
            }
            catch
            {
                StopFailedOperation(headendError);
                return;
            }
            finally
            {
                StopOperation();
            }
        }

        /// <summary>
        /// Handles the <b>About</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnAboutCommand(object sender, EventArgs args)
        {
            var aboutBox = new AboutBox();

            aboutBox.ShowDialog();
        }

        /// <summary>
        /// Handles the <b>Settings</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnSettingsCommand(object sender, EventArgs args)
        {
            MessageBox.Show("$todo(jefflill): Not implemented yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Implements the <b>Cmd Window</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        /// <remarks>
        /// <para>
        /// This command opens a CMD window with the PATH environment variable specifying the
        /// path to the neonDESKTOP installation folder with <b>kubectl.exe</b> before other
        /// folders that may include other versions of <b>kubectl.exe</b>, like Docker.
        /// </para>
        /// <para>
        /// Note that this will not update the PATH before launching the CMD window if
        /// the neonDESKTOP installation folder cannot be found or if it doesn't include
        /// <b>kubectl.exe</b>.  In this case, we'll just use the current PATH and live
        /// with that.  This won't happen for real users who have installed neonDESKTOP,
        /// just us neonKUBE developers messing with stuff.
        /// </para>
        /// </remarks>
        private void OnCmdCommand(object sender, EventArgs args)
        {
            var neonProgramFolder = Environment.GetEnvironmentVariable("NEONKUBE_PROGRAM_FOLDER");
            var orgPATH           = Environment.GetEnvironmentVariable("PATH");

            try
            {
                if (!string.IsNullOrEmpty(neonProgramFolder) && File.Exists(Path.Combine(neonProgramFolder, "kubectl.exe")))
                {
                    // We found the neonKUBE installation folder so munge the path to make
                    // it first in line.

                    var pathFolders = orgPATH.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    var sbPATH      = new StringBuilder();

                    sbPATH.AppendWithSeparator(neonProgramFolder, ";");

                    foreach (var folder in pathFolders.Where(f => f != neonProgramFolder))
                    {
                        sbPATH.AppendWithSeparator(folder, ";");
                    }

                    Environment.SetEnvironmentVariable("PATH", sbPATH.ToString());

                    // Open the CMD window.

                    var startInfo = new ProcessStartInfo("cmd.exe", "")
                    {
                        CreateNoWindow   = false,
                        WindowStyle      = ProcessWindowStyle.Normal,
                        WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    };

                    Process.Start(startInfo);
                }
            }
            finally
            {
                // Restore the orignal PATH.

                Environment.SetEnvironmentVariable("PATH", orgPATH);
            }
        }

        /// <summary>
        /// Handles the <b>Settings</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private async void OnCheckForUpdatesCommand(object sender, EventArgs args)
        {
            StartOperation(workingAnimation);

            try
            {
                var clientInfo = await Headend.GetClientInfoAsync();

                if (clientInfo.UpdateVersion == null)
                {
                    MessageBox.Show("The latest version of neonKUBE is installed.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("$todo(jefflill): Not implemented yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch
            {
                StopFailedOperation("Update check failed");
                return;
            }
            finally
            {
                StopOperation();
            }
        }

        /// <summary>
        /// Handles cluster context commands.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnClusterContext(object sender, EventArgs args)
        {
            // The cluster context name is the text of the sending menu item.

            var menuItem    = (ToolStripMenuItem)sender;
            var contextName = menuItem.Text;

            StartOperation(connectingAnimation);

            try
            {
                KubeHelper.SetCurrentContext(contextName);
                ShowToast($"Logged into: {contextName}");
            }
            catch
            {
                StopFailedOperation($"Cannot log into: {contextName}");
            }
            finally
            {
                StopOperation();
            }
        }

        /// <summary>
        /// Handles the <b>Logout</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnLogoutCommand(object sender, EventArgs args)
        {
            if (KubeHelper.CurrentContext != null)
            {
                ShowToast($"Logging out of: {KubeHelper.CurrentContext.Name}");
                KubeHelper.SetCurrentContext((string)null);
                PostUpdateUIState();
            }
        }

        /// <summary>
        /// Handles the <b>Kubernetes Dashboard</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnKubernetesDashboardCommand(object sender, EventArgs args)
        {
            NeonHelper.OpenBrowser($"http://localhost:{KubeHelper.ClientConfig.KubeDashboardProxyPort}/");
        }

        /// <summary>
        /// Handles the <b>Kibana Dashboard</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnKibanaDashboardCommand(object sender, EventArgs args)
        {
            NeonHelper.OpenBrowser($"http://localhost:{KubeHelper.ClientConfig.KibanaDashboardProxyPort}/");
        }

        /// <summary>
        /// Handles the <b>Prometheus Dashboard</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnPrometheusDashboardCommand(object sender, EventArgs args)
        {
            NeonHelper.OpenBrowser($"http://localhost:{KubeHelper.ClientConfig.PrometheusDashboardProxyPort}/");
        }

        /// <summary>
        /// Handles the <b>Kiali Dashboard</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnKialiDashboardCommand(object sender, EventArgs args)
        {
            NeonHelper.OpenBrowser($"http://localhost:{KubeHelper.ClientConfig.KialiDashboardProxyPort}/");
        }

        /// <summary>
        /// Handles the <b>Grafana Dashboard</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnGrafanaDashboardCommand(object sender, EventArgs args)
        {
            NeonHelper.OpenBrowser($"http://localhost:{KubeHelper.ClientConfig.GrafanaDashboardProxyPort}/");
        }

        /// <summary>
        /// Handles the <b>Exit</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnExitCommand(object sender, EventArgs args)
        {
            StopNotifyAnimation(force: true);
            notifyIcon.Visible = false;
            StopPortForwards();
            DesktopService.Stop();

            Environment.Exit(0);
        }
    }
}
