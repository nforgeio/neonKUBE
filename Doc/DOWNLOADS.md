# Hive File Downloads

neonHIVE image build, setup and update requires several files files to be available via the web to work correctly.  These files are currently persisted to AWS S3 but will eventually be relocated (hopefully) to GitHub Releases.

neonHIVE tools currently hardcode references to these files, but we'll eventually implement the neonHIVE Headend services which will provide an indirect lookup capability so we'll be able to relocate these files if necessary while providing additional information about version compatibility.

This document provides instructions for adding to an updating these files when new versions of dependant software is released (like new Docker releases).

## Docker

We originally installed Docker by trying to map a version like **18.09.0-ce** to one of Ubuntu packages published by Docker, but we encountered these problems:

* Docker sometimes publishes and republishes releases with strange names that don't follow a parsable convention.
* Docker sometimes removes releases after they've been published.
* Docker's repository doesn't seem entirely reliable (we may be throttled sometimes).

For these reasons, we're going to capture Docker packages as they are released, persist them to S3 and then unstall and updated from those. 

We currently support only Ubuntu/Xenial downloads but eventually we'll need to download other packages.

### Download Steps

**NOTE:** These instructions work for downloading **stable** or **edge** packages.

The easiest way to to accomplish this is to start an Ubuntu-16.04 (Xenial) virtual machine and follow these steps:

1. Start an Ubuntu-16.04 (Xenial) virtual machine and connect via SSH.

2. Run: `sudo bash`

3. Configure the Docker package **stable** repository via:

```
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | apt-key add -
add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable"
```
  or the **edge** repository via:
```
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | apt-key add -
add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) edge"
```

4. List the available packages:

```
apt-get update
apt-cache madison docker-ce
```
  You'll see output like this:

```
docker-ce | 5:18.09.0~3-0~ubuntu-xenial | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 18.06.1~ce~3-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 18.06.0~ce~3-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 18.03.1~ce-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 18.03.0~ce-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 17.12.1~ce-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 17.12.0~ce-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 17.09.1~ce-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 17.09.0~ce-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 17.06.2~ce-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 17.06.1~ce-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 17.06.0~ce-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 17.03.3~ce-0~ubuntu-xenial | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 17.03.2~ce-0~ubuntu-xenial | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 17.03.1~ce-0~ubuntu-xenial | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
docker-ce | 17.03.0~ce-0~ubuntu-xenial | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
```
  The package versions are in the second column the second column.

5. Download the desired package (specifying the desired version), like:

```
apt-get download docker-ce=5:18.09.0~3-0~ubuntu-xenial
```

6. Use WinSCP to download the files to Windows.

7. Rename the file to the standard format: **docker-VERSION-DISTRO-CODENAME-REPOSITORY_amd64**, where:

  **VERSION** is the *nice* Docker version, like: **18.09.1-ce**
  **DISTRO** identifies the Linux distribution, like: **ubuntu**
  **CODENAME** identifies the distribution version, like: **xenial**
  **REPOSITORY** identifies the source repository, like: **stable** or **edge**

  Example: `docker-18.09.0-ce-ubuntu-xenial-stable-amd64.deb`

8. Upload the file to S3 and make it public: https://s3-us-west-2.amazonaws.com/neonforge/neoncluster/FILENAME.deb

9. Update the headend services (or simulated services for now) to include a Docker version to URL mapping.


