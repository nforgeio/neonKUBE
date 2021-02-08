# Binary Assets

The files (besides this README) in this folder will be included in the nuget package and will be published to the consuming project's **bin** folder.  We've picked a folder name that should hupefully be unique across all files that might land in the parent project's bin folder.

This will work aout-of-the-box for Windows but file permissions won't be set automatically for other environments like Linux and OS/X.  As a convention, our libraries will attempt to configure these permissions when necessary, but that may fail depending on the permissions the current user is running under, in which case the user will need to manually configure permissions via a Dockerfile or other means.

## Files and sources

For this library, we're including the XenServer **xe-cli** binaries for Linux, OS/X, and Windows.  This library uses this to manage remote XenServer/XCP-ng hypervisor host servers without having to SSH into the machine directly.

These files were obtained from:

**Linux:**

$todo(jefflill):

We need to figure this out.  It looks like [RPM packages are available](https://docs.citrix.com/en-us/citrix-hypervisor/command-line-interface.html) but sure about a Debian package (perhaps [this](https://packages.ubuntu.com/focal/xen-tools).

**OS/X:**

$todo(jefflill):

I haven't found a **xe-cli** for the Macintosh yet.  We may need to recode **Neon.XenServer** to use the [XenServer SDK](https://citrix.github.io/xenserver-sdk/#csharp) which has C# bindings.  This would be cleaner and will also address the permissions issues for mLinux and OS/X.

**Windows:**

* `CommandLib.dll`
* `xe.exe`

We obtained these files by installing **XCP-ng Center** from https://github.com/xcp-ng/xenadmin/releases and then copying them from: `C:\Program Files (x86)\XCP-ng Center`
