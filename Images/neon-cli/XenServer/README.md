# XenServer Assets

NOTE: I've decided not to include the Citrix XenServer XE CLI in **neon-cli** and use SSH to access the tool on the XenServer hosts instead.  I'm going to leave these instructions here though as documentation if I change my mind in the future.

To enable XenServer cluster provisioning, the **neon-cli** tool requires that the XenServer **xe** client tool be installed within the Docker image.  This installation is handled by the `Dockerfile` by copying one of the Debian packages from this folder into the container and then installing it.

Citrix provides the **xe** client package as part of its distribution but unforunately, XenServer is based on Centos Linux so this is a RPM package not an Ubuntu compatible DEB package.  It is possible though to convert an RPM into a DEB package and the instructions below describe how to accomplish this:

1. Create a temporary folder on your workstation.
2. Download the XenServer ISO from: [here](https://xenserver.org/open-source-virtualization-download.html)
3. Mount the ISO.
4. Open the /client_install folder and look for an RPM file that looks like: **xapi-xe-1.30.0-1.x86_64.rpm**
5. Copy the RPM file to your temporary folder.

6. Open a command window and then start an Ubuntu Docker container:
  
  &nbsp;&nbsp;&nbsp;&nbsp;`docker run -it --rm --name xe.deb neoncluster/ubuntu-16.04`

7. Open another command window and copy the RPM file into the container (something like):

  &nbsp;&nbsp;&nbsp;&nbsp;`docker cp c:\temp\xapi-xe-1.30.0-1.x86_64.rpm xe.deb:/xapi-xe.rpm`

8. Go back to the first command window and convert the RPM into a DEB via:

  &nbsp;&nbsp;&nbsp;&nbsp;`apt-get update`
  &nbsp;&nbsp;&nbsp;&nbsp;`apt-get install -yq alien dpkg-dev debhelper build-essential`
  &nbsp;&nbsp;&nbsp;&nbsp;`alien xapi-xe.rpm`

9. Go back to the second command window and copy the converted DEB file to your workstation (note that the package name might be different):

   &nbsp;&nbsp;&nbsp;&nbsp;`docker cp xe.deb:\xapi-xe_1.30.0-2_amd64.deb c:\temp`

10. Copy the DEB package to this folder and update `Dockerfile` to install the new DEB package.
