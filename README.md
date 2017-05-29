# NeonForge
The NeonForge, LLC technology stack.
## Workstation Requirements
* Windows 10 (64-bit)
* Visual Studio Community Edition

Note that the build environment currently assumes that only one Windows user will be acting as a developer on any given workstation.  Developers cannot share a computer.
## Workstation Configuration
Follow steps below to configure a development or test workstation.
1. Make sure that Windows is **fully updated**.
2. Some versions of Skype listen for inbound connections on ports **80** and **443**.  This will interfere with services we'll want to test locally.  You need to disable this:

  * In Skype, select the **Tools/Options** menu.
  * Select the **Advanced/Connection** tab on the left.
  * **Uncheck**: Use **port 80 and 443** for additional incoming connections.
  
    ![Skype Connections](./README/SkypeConnections.png)
  * **Restart Skype**

3. Install **Visual Studio Community Edition** from [here](https://www.visualstudio.com/downloads/).  Do a full install to ensure that you have everything.  This an overkill, but it may help prevent build problems in the future.

  * Select **all workloads** on the first panel
  * Select **individual components**
  * Click to select **all components**
  * Click **Install** (and take a coffee break)

4. Create a **shortcut** for Visual Studio and configure it to run as **administrator**.  To build and run NeonForge applications and services, **Visual Studio must be running with elevated privileges**.
5. Install **Git for Windows** with defaults from [here](https://git-scm.com/download/win).
6. Install **Docker for Windows** from [here](https://www.docker.com/products/docker#/windows).

  * Use the **Stable** channel unless you have a specific need for bleeding edge features
  * **Right-click** the Docker icon in the system tray and select **Settings...*

    ![System Tray](./README/DockerSysTray.png)
  * Select the **Shared Drives** tab and **share** the drive with the project source code
  * You'll need to enter your workstation **credentials**

7. Test your Docker configuration.

  * Open a **DOS** command window.
  * Run this command: `docker pull alpine`

8. If the previous step failed with a **Network Timeout**, you'll need to update Docker's network settings:

  * **Right-click** the Docker again in the system tray and select **Settings...*
  * Click **Network** on the left, select Fixed DNS Server and then **Apply**

    ![Docker Network Settings](./README/DockerNetwork.png)

9. **Clone** the source repository on your workstation:

  * Create an individual Github account [here](https://github.com/join?source=header-home) if you don't already have one
  * Have one of the NeonForge repository administrators **grant you access** to the repository
  * Go to [GitHub](http://github.com) and log into your account
  * Go to the NeonForge [repository](https://github.com/jefflill/NeonForge).
  * Click the *green* **Clone or download** button and select **Open in Visual Studio**
  * A *Launch Application* dialog will appear.  Select **Microsoft Visual Studio Protocol Handler Selector** and click **Open Link**
  * Choose or enter the directory where the repository will be cloned.  This defaults to a user-specific folder.  I typically change this to a global folder to keep the file paths short.
  
    ![Video Studio Clone](./README/VisualStudioClone.png)
  * Click **Clone**

10. **Close** any running instances of **Visual Studio**
11. Configure the build **environment variables**:

  * Open **Windows Explorer**
  * Navigate to the directory holding the cloned repository
  * **Right-click** on **buildenv.cmd** and then **Run as administorator**
  * Close the DOS window when the script is finished

12. Confirm that the solution builds:

  * Run **Visual Studio** as **administrator**
  * Open **$/Stoke.sln** (where **$** is the repo directory path)
  * Select **Build/Rebuild** Solution

13. Many server components are deployed to Linux, so you’ll need terminal and file management programs.  We’re currently standardizing on **PuTTY** for the terminal and **WinSCP** for file transfer. install both programs to their default directories:

  * Install both **WinSCP** and **PuTTY** from [here](http://winscp.net/eng/download.php) (PuTTY is near the bottom of the page)
  * Run **WinSCP* and enable **hidden file display** [WinSCP Hidden Files](/README/WinSCPHiddenFile.png)
  * *Optional*: The default PuTTY color scheme sucks (dark blue on a black background doesn’t work for me).  You can update the default scheme to Zenburn Light by **right-clicking** on the `$\External\zenburn-ligh-putty.reg` in **Windows Explorer** and selecting **Merge**
  * WinSCP: Enable **hidden files**.  Start **WinSCP**, select **Tools/Preferences...", and then click **Panels** on the left and check **Show hidden files**:
  
    ![WinSCP Hidden Files](./README/WinSCPHiddenFiles.png)

14. *Optional*: Install **Fiddler4** from [here](http://www.telerik.com/download/fiddler)

15. *Optional*: Install **Notepad++** from [here](https://notepad-plus-plus.org/download)

16. *Optional*: Install Chrome **Markdown Viewer** extension from [here](https://github.com/simov/markdown-viewer)

## Cloud Environments

NeonClusters can currently be deployed to Microsoft Azure.  To test this, you'll need an Azure subscription and then gather the required authentication information.  The following sections describe how to accomplish this.

## Microsoft Azure

Follow the steps below to enable an Azure account for NeonCluster deployments using the **neon-cli**.  You’ll need to sign up for an Azure account [here](https://azure.microsoft.com/en-us/free/), if you don’t already have one.

Then you need to create credentials the **neon-cli** will use to authenticate with Azure.  The steps below are somewhat simplified from Microsoft’s [documentation](https://docs.microsoft.com/en-us/azure/azure-resource-manager/resource-group-create-service-principal-portal).  The instructions below assume that you have full administrative rights to the Azure subscription.

1. Install the **Azure CLI** for your operating system from [here](https://docs.microsoft.com/en-us/azure/xplat-cli-install).

2. Create a text file where you’ll save sensitive application credentials.

3. Open a command window and use the command below to log into an Azure subscription.  The command may direct you to open a link in a browser and enter a code.

  `azure login`

4. Run the command below to list your Azure subscriptions.  Save the **Subscription ID** where you’ll be provisioning your NeonCluster to the credentials file.

  `azure account list`

5. Create the **neon-cli** application in the Azure Active Directory, specifying the new **PASSWORD** the **neon-cli** will use to log into Azure (you can use neon create password to generate a secure password):

  `azure ad app create -n neon-cli \`<br/>
  &nbsp;&nbsp;&nbsp;&nbsp;`--home-page http://neoncluster.com \`<br/>
  &nbsp;&nbsp;&nbsp;&nbsp;`--identifier-uris http://neoncloud.com/neon-cli \`<br/>
  &nbsp;&nbsp;&nbsp;&nbsp;`-p PASSWORD`

6. Save the **Password** and **AppId** to the credentials file.

7. Use the command below to create the service principal, passing the **AppId** captured above:

  `azure ad sp create -a APP-ID`

8. Save the **ObjectId** returned as the **ServicePrincipalId** to the credentials file.

9. Grant the service principal owner rights to the subscription (advanced users may want to customize this to limit access to a specific resource group):

  `azure role assignment create --objectId SERVICE-PRINCIPAL-ID \`<br/>
  &nbsp;&nbsp;&nbsp;&nbsp;`-o Contributor \`<br/>
  &nbsp;&nbsp;&nbsp;&nbsp;`-c /subscriptions/SUBSCRIPTION-ID`

10. Run the following command and save the **TenantID** to your credentials file.  (This is the ID of your subscription’s Active Directory instance).

  `azure account show`
