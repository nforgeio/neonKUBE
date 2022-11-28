# neonKUBE Developer Setup

This page describes how to get started with neonKUBE development.

## Workstation Requirements

* Windows 10 Professional (64-bit) with at least 16GB RAM
* Virtualization capable workstation
* Visual Studio 2019 Edition (or better)
* Visual Studio Code

Note that the build environment currently assumes that only one Windows user will be acting as a developer on any given workstation.  Developers cannot share a machine and neonKUBE only builds on Windows at this time.

## Workstation Configuration

Follow the steps below to configure a development or test workstation:

1. Follow the maintainer setup instructions for the **neonSDK** repo at: [neonSDK](https://github.com/nforgeio/neonsdk/blob/master/Doc/DEVELOPER.md)

2. **Clone** the https://github.com/nforgeio/neonKUBE.git repository to your workstation:

    * **IMPORTANT:** All NEONFORGE related repositories must be cloned within the same parent directory and their folder names must be the same as the repo names.

3. Configure the build **environment variables**:

    * Open **File Explorer**
    * Navigate to the directory holding the cloned repository
    * **Right-click** on **buildenv.cmd** and then **Run as adminstrator**
    * Press ENTER to close the CMD window when the script is finished

4. Install Node.js from: [here](https://nodejs.org/dist/v16.17.0/node-v16.17.0-x64.msi)

   NOTE: Install with default options.
  
5. Confirm that the solution builds:

    * Restart **Visual Studio** as **administrator** (to pick up the new environment variables)
    * Open **$/neonKUBE.sln** (where **$** is the repo root directory)
    * You may be asked to login to GitHub.  Enter your GitHub username and GITHUB_PAT as the password and check the save password button
    * Click the **Install** link at the top of the solution explorer panel when there's a warning about a missing SDK.
    * Select **Build/Rebuild** Solution

6: *Optional:* Maintainers authorized to perform releases will need to follow the README.md instructions in the neonCLOUD repo to configure credentials for the GitHub Releases and the Container Registry.
