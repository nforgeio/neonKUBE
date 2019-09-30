# neonKUBE Developer Setup

This page describes how to get started with neonKUBE development.

## Workstation Requirements

* Windows 10 Professional (64-bit) with at least 16GB RAM
* Virtualization capable workstation
* Visual Studio 2019 Edition (or better)
* Visual Studio Code

Note that the build environment currently assumes that only one Windows user will be acting as a developer on any given workstation.  Developers cannot share a machine.

## Workstation Configuration

Follow the steps below to configure a development or test workstation:

1. Make sure that Windows is **fully updated**.

2. We highly recommend that you configure Windows to display hidden files:

    * Press the **Windows key** and run **File Explorer**
    * Click the **View** tab at the top.
    * Click the **Options** icon on the right and select **Change folder and search options**.
    * Click the **View** tab in the popup dialog.
    * Select the **Show hidden files, folders, and drives** radio button.
    * Uncheck the **Hide extensions for known types** check box.

3. Some versions of Skype listen for inbound connections on ports **80** and **443**.  This will interfere with services we'll want to test locally.  You need to disable this:

    * In Skype, select the **Tools/Options** menu.
    * Select the **Advanced/Connection** tab on the left.
    * **Uncheck**: Use **port 80 and 443** for additional incoming connections.

      ![Skype Connections](Images/DEVELOPER/SkypeConnections.png?raw=true)
    * **Restart Skype**

4. Ensure that Hyper-V is installed and enabled:

    a. Run the following command in a **cmd** window to verify that your workstation is capable of virtualization and that it's enabled. You're looking for output like the image below:
      ```
      systeminfo
      ```
      ![Virtualization Info](Images/DEVELOPER/virtualization.png?raw=true)

    or a message saying that: **A hypervisor has been detected.**

    b. Press the Windows key and enter: **windows features** and press ENTER.

    c. Ensure that the check boxes highlighted in red below are checked:

    ![Hyper-V Features](Images/DEVELOPER/hyper-v.png?raw=true) 

    d. Reboot your machine as required.

5. Install the latest **32-bit** production release of PowerShell Core from [here](https://github.com/PowerShell/PowerShell/releases) (`PowerShell-#.#.#-win.x86.msi`)

6. Enable PowerShell script execution via (in a CMD window as administrator):
    ```
    powershell Set-ExecutionPolicy -ExecutionPolicy Unrestricted -Scope CurrentUser
    ```

  `powershell Set-ExecutionPolicy -ExecutionPolicy Unrestricted -Scope CurrentUser`

7. Install **Visual Studio 2019 Community 16.3+** from [here](https://visualstudio.microsoft.com/thank-you-downloading-visual-studio/?sku=Community&rel=16)

  * Select **all workloads** on the first panel
  * Click **Individual components**, type *Git* in the search box and select **Git for Windows** and **GitHub extension for Visual Studio**
  * Click **Install** (and take a coffee break)
  * Install **.NET Core SDK 3.0.100 (Windows .NET Core Installer x64)** from [here](https://dotnet.microsoft.com/download/dotnet-core/3.0)
  * Apply any pending **Visual Studio updates**
  * **Close** Visual Studio and install any updates
  
8. Create a **shortcut** for Visual Studio and configure it to run as **administrator**.  To build and run neonKUBE applications and services, **Visual Studio must be running with elevated privileges**.

9. Install **.NET Framework 4.8 Developer Pack** from [here](https://dotnet.microsoft.com/download/thank-you/net48-developer-pack)

10. Install **Visual Studio Code** from [here](https://code.visualstudio.com/download)

11. Configure the GOLANG development environment:

    * Install **go1.13.windows-amd64.msi** or later for Windows from: [here](https://golang.org/dl/)

12. Install **Docker for Windows (Stable)** from [here](http://hub.docker.com)

    * You'll need to create a DockerHub account if you don't already have one.
    * **Right-click** the Docker icon in the system tray and select **Settings...**

      ![System Tray](Images/DEVELOPER/DockerSysTray.png?raw=true)

    * Select the **Shared Drives** tab click the check box to **share** the drive where you'll clone the project source code (typically drive C:)
    * Click **Apply** (You may need to enter your workstation **credentials**).
    * Select the **Advanced** tab and enable **Manual DNS Configuration** and set the DNS to **8.8.8.8**.
    * Click **Apply**
    * Select the **Resource** tab on the left set **CPUs=4** and **Memory=4GB**.
    * Click **Apply**

13. **Clone** the [https://github.com/nforgeio/neonKUBE](https://github.com/nforgeio/neonKUBE) repository to your workstation:

    * Create an individual GitHub account [here](https://github.com/join?source=header-home) if you don't already have one
    * Have one of the neonKUBE repository administrators **grant you access** to the repository
    * Go to [GitHub](http://github.com) and log into your account
    * Go to the neonKUBE [repository](https://github.com/nforgeio/neonKUBE).
    * Click the *green* **Clone or download** button and select **Open in Visual Studio**
    * A *Launch Application* dialog will appear.  Select **Microsoft Visual Studio Protocol Handler Selector** and click **Open Link**
    * Choose or enter the directory where the repository will be cloned.  This defaults to a user-specific folder.  I typically change this to a global folder to keep the file paths short.
    
      ![Video Studio Clone](Images/DEVELOPER/VisualStudioClone.png?raw=true)
    * Click **Clone**

14. **Close** any running instances of **Visual Studio**

15. Install the latest release of **neonKUBE** from [here](https://github.com/nforgeio/neonKUBE/releases)

16. Install **7-Zip (32-bit)** (using the Windows *.msi* installer) from [here](http://www.7-zip.org/download.html)

17. Install **MinGW (Minimalist GNU for Windows):**

    * Install MinGW from [here](https://sourceforge.net/projects/mingw-w64/) and follow these [instructions](https://code.visualstudio.com/docs/cpp/config-mingw)
    * You only need to follow instruction through the "Prerequisites" section.
    * Make sure that when you install, you install for your specific architecture (i.e. x86_64).  

18. Many server components are deployed to Linux, so you’ll need terminal and file management programs.  We’re currently standardizing on **PuTTY** for the terminal and **WinSCP** for file transfer. install both programs to their default directories:

    * Install **WinSCP** from [here](http://winscp.net/eng/download.php) (I typically use the "Explorer" interface)
    * Install **PuTTY** from [here](https://www.chiark.greenend.org.uk/~sgtatham/putty/latest.html)
    * *Optional*: The default PuTTY color scheme sucks (dark blue on a black background doesn’t work for me).  You can update the default scheme to Zenburn Light by **right-clicking** on the `$\External\zenburn-ligh-putty.reg` in **Windows Explorer** and selecting **Merge**
    * WinSCP: Enable **hidden files**.  Start **WinSCP**, select **Tools/Preferences...**, and then click **Panels** on the left and check **Show hidden files**:
    
      ![WinSCP Hidden Files](Images/DEVELOPER/WinSCPHiddenFiles.png?raw=true)

19. Configure the build **environment variables**:

    * Open **File Explorer**
    * Navigate to the directory holding the cloned repository
    * **Right-click** on **buildenv.cmd** and then **Run as adminstrator**
    * Close the CMD window when the script is finished
  
20. Install the latest build of neonKUBE from [here](https://github.com/nforgeio/neonKUBE/releases)

21. Restart Visual Studio (to pick up the environment changes).

22. Confirm that the solution builds:

    * Run **Visual Studio** as **administrator**
    * Open **$/neonKUBE.sln** (where **$** is the repo root directory)
    * Select **Build/Rebuild** Solution

23. Install **InnoSetup** from the **$/External** directory using default settings.  This is required to build the Windows neonKUBE installer.

24. *Optional*: Install **OpenVPN**

    * Download the Windows Installer from [here](https://openvpn.net/index.php/open-source/downloads.html)
    * Run this command as administrator in a CMD window to install a second TAP interface:
      ```
      "%PROGRAMFILES%\Tap-Windows\bin\addtap.bat"
      ```
    * Obtain your WowRacks VPN credentials from another developer who has ADMIN access.

25. *Optional*: Install **Notepad++** from [here](https://notepad-plus-plus.org/download)

26. *Optional*: Install **Postman** REST API tool from [here](https://www.getpostman.com/postman)

27. *Optional*: Install **Cmdr/Mini** command shell:

  * **IMPORTANT: Don't install the Full version** to avoid installing Linux command line tools that might conflict with the Cygwin tools installed earlier.
  * Download the ZIP archive from: [here](http://cmder.net/)
  * Unzip it into a new folder and then ensure that this folder is in your **PATH**.
  * Create a desktop shortcut if you wish and configure it to run as administrator.
  * Run Cmdr and configure settings as desired.
  * Consider removing the alias definitions in `$\config\user-aliases.cmd` file so that commands like `ls` will work properly.  I deleted all lines beneath the first `@echo off`.

28. *Optional*: Install the latest version of **XCP-ng Center** from [here](https://github.com/xcp-ng/xenadmin/releases) if you'll need to manage Virtual Machines hosted on XCP-ng.

29. *Optional*: Developers who will be publishing **nuget packages** will need to:

    * Download the latest recommended **nuget.exe** from [here](https://www.nuget.org/downloads) and put this somewhere in your `PATH`
    * Obtain a nuget API key from a senior developer and install the key on their workstation via:
      ```
      nuget SetApiKey APIKEY
      ```

30. *Optional*: Create the **EDITOR** environment variable and point it to `C:\Program Files\Notepad++\notepad++.exe` or your favorite text editor executable.

## Git Branches

neonKUBE conventions for GitHub branches:

* **master:**

  Includes the most recent relatively stable commits.  Developers will merge any changes here after confirming that the changes appear to work.  The **master** branch should always build and pass unit tests and will generally act as the candidate for test, staging, and production releases.

* **product-version:**

  These are used to track released software.  Release branches should generally not be modified after the release has been made.  When minor changes are required, a new release branch (incrementing the PATCH version) should be created from the current release branch and the new release should be built and published.

* **developer:**

  Developers will generally have one or more branches prefixed by their first name (lowercase), like: **jeff**, **jeff-experimental**,...
  
* **feature:**

  When developers need to colloborate on a feature over an extended period of time, we'll create feature branches named like **feature-coolstuff**.  Most development work will happen in a developer or feature branch.

## Coding Conventions

We'll be generally following the [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions).

## Code Comments

In general, all public types, methods, and properties should have reasonable code comments describing the basic functionality.  Please use the C# `<see>`, `<paramref>`, `<typeparam>`, `<para>`, `<b>`, `<i>`, `<c>` markup elements so that the generated web pages will look nice.  This is especially true for REST APIs so that Swagger can generate nice documentation for developers looking at the API.

## Unit Tests

Each important class library and application should have its own **xunit** based unit test project.  This should be named like **Test.PROJECT** where **PROJECT** is the name of the component being test.  For example we'd create a test project named **Test.Loopie.Common** for the **Loopie.Common** library.

The C# namespace for each test project should be the same as the project name (e.g. **Test.Neon.Common**) and each test class name should be prefixed by **Test_** to avoid namespace conflicts with the classes you need to test against.

Test methods should ne organized into categories using the xunit **[Trait(...)]** attribute.
