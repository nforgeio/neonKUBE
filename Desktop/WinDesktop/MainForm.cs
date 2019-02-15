//-----------------------------------------------------------------------------
// FILE:	    MainForm.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Threading.Tasks;
using System.Windows.Forms;

using Neon;
using Neon.Common;
using Neon.Cryptography;
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
        private const string headendError = "Unable to contact the neonKUBE headend service.";

        private Icon                appIcon;
        private Icon                disconnectedIcon;
        private Icon                connectedIcon;
        private AnimatedIcon        connectingAnimation;
        private AnimatedIcon        workingAnimation;
        private int                 animationNesting;
        private ContextMenu         contextMenu;
        private bool                operationInProgress;
        private RemoteOperation     remoteOperation;
        private List<ReverseProxy>  proxies;
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

            // Preload the notification icons and animations for better performance.

            appIcon             = new Icon(@"Images\app.ico");
            connectedIcon       = new Icon(@"Images\connected.ico");
            disconnectedIcon    = new Icon(@"Images\disconnected.ico");
            connectingAnimation = AnimatedIcon.Load("Images", "connecting", animationFrameRate);
            workingAnimation    = AnimatedIcon.Load("Images", "working", animationFrameRate);

            // Initialize the cluster hosting provider components.

            HostingLoader.Initialize();

            // Initialize the client state.

            proxies = new List<ReverseProxy>();
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
            // Set the text labels on the main form.  Nobody should ever see
            // this because the form should remain hidden but we'll put something
            // here just in case.

            productNameLabel.Text  = $"{Build.ProductName}  v{Build.ProductVersion}";
            copyrightLabel.Text    = Build.Copyright;
            licenseLinkLabel.Text  = Build.ProductLicense;

            // Initialize the notify icon and its context memu.

            notifyIcon.Text        = Build.ProductName;
            notifyIcon.Icon        = disconnectedIcon;
            notifyIcon.ContextMenu = contextMenu = new ContextMenu();
            notifyIcon.Visible     = true;
            contextMenu.Popup     += Menu_Popup;

            // Set the initial notify icon state and setup a timer
            // to periodically keep the UI in sync with any changes.

            UpdateUIState();

            statusTimer.Interval = (int)TimeSpan.FromSeconds(KubeHelper.ClientConfig.StatusPollSeconds).TotalMilliseconds;
            statusTimer.Tick    += (s, a) => UpdateUIState();
            statusTimer.Start();

            // Start the desktop API service that [neon-cli] will use
            // to communicate with the desktop application.

            DesktopService.Start();
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
        /// Intercept the window close event and minimize it instead.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs args)
        {
            // The main form should always be hidden but we'll 
            // implement this just in case.

            args.Cancel = true;
            this.Visible = false;
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
        /// Starts a notify icon animation.
        /// </summary>
        /// <param name="animatedIcon">The icon animation.</param>
        /// <remarks>
        /// Calls to this method may be recursed and should be matched 
        /// with a call to <see cref="StopWorkingAnimnation"/>.  The
        /// amimation will actually stop when the last matching
        /// <see cref="StartWorkingAnimation"/> call was matched with
        /// the last <see cref="StopWorkingAnimnation"/>.
        /// </remarks>
        private void StartNotifyAnimation(AnimatedIcon animatedIcon)
        {
            Covenant.Requires<ArgumentNullException>(animatedIcon != null);

            if (animationNesting == 0)
            {
                animationTimer.Interval = (int)TimeSpan.FromSeconds(1 / animatedIcon.FrameRate).TotalMilliseconds;
                animationTimer.Tick +=
                    (s, a) =>
                    {
                        notifyIcon.Icon = animatedIcon.GetNextFrame();
                    };

                animationTimer.Start();
            }

            animationNesting++;
        }

        /// <summary>
        /// Stops the notify icon animation.
        /// </summary>
        /// <param name="force">Optionally force the animation to stop regardless of the nesting level.</param>
        private void StopNotifyAnimation(bool force = false)
        {
            if (force)
            {
                if (animationNesting > 0)
                {
                    animationTimer.Stop();
                    UpdateUIState();
                    animationNesting = 0;
                }

                return;
            }

            if (animationNesting == 0)
            {
                throw new InvalidOperationException("StopNotifyAnimation: Stack underflow.");
            }

            if (--animationNesting == 0)
            {
                animationTimer.Stop();
                UpdateUIState();
            }
        }

        /// <summary>
        /// Displays the notify icon's balloon (AKA toast).
        /// </summary>
        /// <param name="text">The message text.</param>
        /// <param name="title">The ballon title text (defaults to the application name).</param>
        /// <param name="icon">The optional tool tip icon (defaults to <see cref="ToolTipIcon.Info"/>).</param>
        private void ShowToast(string text, string title = null, ToolTipIcon icon = ToolTipIcon.Info)
        {
            notifyIcon.ShowBalloonTip(0, title ?? this.Text, text, icon);
        }

        /// <summary>
        /// Indicates that an operation is starting by optionally displaying a working
        /// animation and optionally displaying a status toast.
        /// </summary>
        /// <param name="animatedIcon">The optional notify icon animation.</param>
        /// <param name="toastText">The optional toast text.</param>
        private void StartOperation(AnimatedIcon animatedIcon = null, string toastText = null)
        {
            if (operationInProgress)
            {
                throw new InvalidOperationException("Another operation is already in progress.");
            }

            operationInProgress = true;

            if (animatedIcon != null)
            {
                StartNotifyAnimation(animatedIcon);
            }

            if (!string.IsNullOrEmpty(toastText))
            {
                ShowToast(toastText);
            }
        }

        /// <summary>
        /// Indicates that the current operation has been stopped.
        /// </summary>
        private void StopOperation()
        {
            if (!operationInProgress)
            {
                throw new InvalidOperationException("No operation is in progress.");
            }

            if (animationNesting > 0)
            {
                StopNotifyAnimation(force: true);
            }

            operationInProgress = false;

            UpdateUIState();
        }

        /// <summary>
        /// Indicates that the current operation failed.
        /// </summary>
        /// <param name="toastErrorText">The optional toast error text.</param>
        private void StopFailedOperation(string toastErrorText = null)
        {
            if (animationNesting > 0)
            {
                StopNotifyAnimation(force: true);
            }

            UpdateUIState();

            if (!string.IsNullOrEmpty(toastErrorText))
            {
                ShowToast(toastErrorText, icon: ToolTipIcon.Error);
            }
        }

        /// <summary>
        /// Stops any running reverse proxies.
        /// </summary>
        private void StopProxies()
        {
            foreach (var proxy in proxies)
            {
                proxy.Dispose();
            }

            proxies.Clear();
        }

        /// <summary>
        /// Updates the running proxies to match the current cluster 
        /// (if there is one).
        /// </summary>
        private void UpdateProxies()
        {
            if (KubeHelper.CurrentContext == null)
            {
                StopProxies();
            }
            else
            {
                var cluster = Program.GetCluster();

                // We're going to use the current Kubenetes context name and cluster ID
                // to determine whether we're still connected to the same cluster.

                if (proxiedContext != null)
                {
                    if (proxiedContext.Name == KubeHelper.CurrentContext.Name &&
                        proxiedContext.Extensions.ClusterId == KubeHelper.CurrentContext.Extensions.ClusterId)
                    {
                        // We're still proxying the same cluster so no changes 
                        // are required.

                        return;
                    }
                }

                StopProxies();

                // The Kubernetes dashboard reverse proxy.  Note that we're going
                // to proxy this via [kubectl proxy] because we couldn't get a
                // direct [ReverseProxy] connection to the Kubernetes API server 
                // to work.

                var cert = KubeHelper.ClusterCertificate;

                var userContext = KubeHelper.Config.GetUser(KubeHelper.CurrentContext.Properties.User);
                var certPem     = Encoding.UTF8.GetString(Convert.FromBase64String(userContext.Properties.ClientCertificateData));
                var keyPem      = Encoding.UTF8.GetString(Convert.FromBase64String(userContext.Properties.ClientKeyData));
                var tlsCert     = TlsCertificate.FromPem(certPem, keyPem);
                var clientCert  = tlsCert.ToX509();

                var kubeDashboardProxy =
                    new ReverseProxy(
                        localPort:   KubeHelper.ClientConfig.KubeDashboardProxyPort,
                        remotePort:  KubeHostPorts.KubeDashboard,
                        remoteHost:  cluster.GetReachableMaster().PrivateAddress.ToString(),
                        certificate: clientCert);

                proxies.Add(kubeDashboardProxy);

                // Remember which cluster context we're proxying.

                proxiedContext = KubeHelper.CurrentContext;
            }
        }

        //---------------------------------------------------------------------
        // These methods are called by [DesktopService] (and perhaps from some
        // other places):

        /// <summary>
        /// Synchronizes the UI state with the current cluster configuration.
        /// </summary>
        public void UpdateUIState()
        {
            InvokeOnUIThread(
                () =>
                {
                    KubeHelper.LoadConfig();

                    UpdateProxies();

                    if (!operationInProgress)
                    {
                        notifyIcon.Icon = IsConnected ? connectedIcon : disconnectedIcon;

                        if (IsConnected)
                        {
                            notifyIcon.Text = $"{Text}: {KubeHelper.CurrentContextName}";
                        }
                        else
                        {
                            notifyIcon.Text = $"{Text}: disconnected";
                        }

                        return;
                    }

                    if (remoteOperation != null)
                    {
                        if (Process.GetProcessById(remoteOperation.ProcessId) == null)
                        {
                            // The original [neon-cli] process is no longer running;
                            // it must have terminated before signalling the end
                            // of the operation.  We're going to terminate the
                            // operation status.
                            //
                            // This is an important fail-safe.

                            StopOperation();
                            return;
                        }

                        notifyIcon.Text = $"{Text}: {remoteOperation.Summary}";
                    }
                    else
                    {
                        notifyIcon.Icon = IsConnected ? connectedIcon : disconnectedIcon;

                        if (IsConnected)
                        {
                            notifyIcon.Text = $"{Text}: {KubeHelper.CurrentContextName}";
                        }
                        else
                        {
                            notifyIcon.Text = $"{Text}: disconnected";
                        }
                    }
                });
        }

        /// <summary>
        /// Signals the start of a long-running operation.
        /// </summary>
        /// <param name="operation">The <b>neon-cli</b> operation information.</param>
        public void OnStartOperation(RemoteOperation operation)
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
                        // If the operation was initiated by the Desktop app then
                        // we'll ignore the new operation.

                        if (remoteOperation != null && remoteOperation.ProcessId == operation.ProcessId)
                        {
                            remoteOperation = operation;
                            UpdateUIState();
                        }
                        else
                        {
                            remoteOperation = operation;
                            StartOperation(workingAnimation);
                        }

                        return;
                    }
                    else
                    {
                        remoteOperation = operation;
                        StartOperation(workingAnimation);
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
                        // Stop the operation only if the the current operation
                        // was initiated by the same [neon-cli] process that
                        // started the operation.

                        if (remoteOperation != null && remoteOperation.ProcessId == operation.ProcessId)
                        {
                            remoteOperation = null;
                            StopOperation();

                            if (!string.IsNullOrEmpty(operation.CompletedToast))
                            {
                                ShowToast(operation.CompletedToast);
                            }
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
                    UpdateUIState();
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
                    UpdateUIState();
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
        private void Menu_Popup(object sender, EventArgs args)
        {
            contextMenu.MenuItems.Clear();

            // Append submenus for each of the cluster contexts that have
            // neonKUBE extensions.  We're not going to try to manage 
            // non-neonKUBE clusters.
            //
            // Put a check mark next to the logged in cluster (if there
            // is one) and also enable [Logout] if we're logged in.

            var contexts = KubeHelper.Config.Contexts
                .Where(c => c.Extensions != null)
                .OrderBy(c => c.Name)
                .ToArray();

            var currentContextName = (string)KubeHelper.CurrentContextName;
            var loggedIn           = !string.IsNullOrEmpty(currentContextName);;

            if (contexts.Length > 0)
            {
                var contextsMenu = new MenuItem(loggedIn ? currentContextName : "Login to") { Checked = loggedIn, Enabled = !operationInProgress };

                contextsMenu.RadioCheck = loggedIn;

                if (loggedIn)
                {
                    contextsMenu.MenuItems.Add(new MenuItem(currentContextName) { Checked = true, Enabled = !operationInProgress });
                }

                var addedContextsSeparator = false;

                foreach (var context in contexts.Where(c => c.Name != currentContextName))
                {
                    if (!addedContextsSeparator)
                    {
                        contextsMenu.MenuItems.Add("-");
                        addedContextsSeparator = true;
                    }

                    contextsMenu.MenuItems.Add(new MenuItem(context.Name, OnClusterContext) { Enabled = !operationInProgress });
                }

                contextsMenu.MenuItems.Add("-");
                contextsMenu.MenuItems.Add(new MenuItem("Logout", OnLogoutCommand) { Enabled = loggedIn && !operationInProgress });

                contextMenu.MenuItems.Add(contextsMenu);
            }

            // Append cluster-specific menus.

            if (loggedIn)
            {
                contextMenu.MenuItems.Add("-");

                var dashboardsMenu = new MenuItem("Dashboard") { Enabled = loggedIn && !operationInProgress };

                dashboardsMenu.MenuItems.Add(new MenuItem("Kubernetes", OnKubernetesDashboardCommand) { Enabled = loggedIn && !operationInProgress });

                var addedDashboardSeparator = false;

                if (KubeHelper.CurrentContext.Extensions.ClusterDefinition.Ceph.Enabled)
                {
                    if (!addedDashboardSeparator)
                    {
                        dashboardsMenu.MenuItems.Add(new MenuItem("-"));
                        addedDashboardSeparator = true;
                    }

                    dashboardsMenu.MenuItems.Add(new MenuItem("Ceph", OnCephDashboardCommand) { Enabled = loggedIn && !operationInProgress });
                }

                if (KubeHelper.CurrentContext.Extensions.ClusterDefinition.EFK.Enabled)
                {
                    if (!addedDashboardSeparator)
                    {
                        dashboardsMenu.MenuItems.Add(new MenuItem("-"));
                        addedDashboardSeparator = true;
                    }

                    dashboardsMenu.MenuItems.Add(new MenuItem("Kibana", OnKibanaDashboardCommand) { Enabled = loggedIn && !operationInProgress });
                }

                if (KubeHelper.CurrentContext.Extensions.ClusterDefinition.Prometheus.Enabled)
                {
                    if (!addedDashboardSeparator)
                    {
                        dashboardsMenu.MenuItems.Add(new MenuItem("-"));
                        addedDashboardSeparator = true;
                    }

                    dashboardsMenu.MenuItems.Add(new MenuItem("Prometheus", OnPrometheusDashboardCommand) { Enabled = loggedIn && !operationInProgress });
                }

                contextMenu.MenuItems.Add(dashboardsMenu);
            }

            // Append the static commands.

            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add(new MenuItem("GitHub", OnGitHubCommand));
            contextMenu.MenuItems.Add(new MenuItem("Help", OnHelpCommand));
            contextMenu.MenuItems.Add(new MenuItem("About", OnAboutCommand));
            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add(new MenuItem("Settings", OnSettingsCommand));
            contextMenu.MenuItems.Add(new MenuItem("Check for Updates", OnCheckForUpdatesCommand) { Enabled = !operationInProgress });
            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add(new MenuItem("Exit", OnExitCommand));
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
            MessageBox.Show("$todo(jeff.lill): Not implemented yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    MessageBox.Show("$todo(jeff.lill): Not implemented yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            var menuItem    = (MenuItem)sender;
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
                UpdateUIState();
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
        /// Handles the <b>Ceph Dashboard</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnCephDashboardCommand(object sender, EventArgs args)
        {
            MessageBox.Show("$todo(jeff.lill): Not implemented yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Handles the <b>Kibana Dashboard</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnKibanaDashboardCommand(object sender, EventArgs args)
        {
            MessageBox.Show("$todo(jeff.lill): Not implemented yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Handles the <b>Prometheus Dashboard</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnPrometheusDashboardCommand(object sender, EventArgs args)
        {
            MessageBox.Show("$todo(jeff.lill): Not implemented yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            DesktopService.Stop();

            Environment.Exit(0);
        }
    }
}
