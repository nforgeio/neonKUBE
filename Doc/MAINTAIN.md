# Periodic Maintenance 

Once a week or so, we should perform some periodic maintenance described below.

## Docker Packages

The easiest way to to accomplish this is visit the Docker package repo at:

    [https://download.docker.com/linux/ubuntu/dists/bionic/pool/stable/amd64/](https://download.docker.com/linux/ubuntu/dists/bionic/pool/stable/amd64/)

1. Download new Docker CE versions.

2. Rename any new files to the standard format: **docker.ce-VERSION-DISTRO-CODENAME-REPOSITORY_amd64**, where:

  **VERSION** is the *nice* Docker version, like: **18.09.1-ce**  (ignore the part before the colon if present)
  **DISTRO** identifies the Linux distribution, like: **ubuntu**
  **CODENAME** identifies the distribution version, like: **bionic**
  **REPOSITORY** identifies the source repository, like: **stable**

  Example: `docker.ce-18.09.0-ce-ubuntu-bionic-stable-amd64.deb`

3. Upload the files to S3 and **MAKE THEM PUBLIC**: https://s3-us-west-2.amazonaws.com/neonforge/kube/FILENAME.deb
4. Update the headend services (or simulated services for now) to include a Docker version to URL mapping.

### Nuget API Key

You'll need an API key installed on your machine to publish the neonKUBE nuget packages.  You may also need to regenerate the key from time to time since they have a one year lifespan.

1. Login into **nuget.org** with your Microsoft account: [here](https://www.nuget.org/users/account/LogOn?returnUrl=%2F)
2. You'll generally want to click **Manage** and then **Regenerate** the **publish** key.
3. Click **Copy** to copy the new key into the clipboard.
4. Install the API via:
  ```
  nuget setapikey YOUR-KEY
  ```

## Check for new Kubernetes releases:

1. Check the official release notes: [here](https://github.com/kubernetes/kubernetes/releases)

2. Update the Headend service (currently stubbed)

   a. Start an Ubuntu VM

   b. Configure the Kubernetes package  repo:
```
apt-get update && apt-get install -y --allow-downgrades apt-transport-https curl
curl - s https://packages.cloud.google.com/apt/doc/apt-key.gpg | apt-key add -
echo "deb https://apt.kubernetes.io/ kubernetes-xenial main" > /etc/apt/sources.list.d/kubernetes.list
```
  c. Run these commands one at a time to discover the latest package releases:
```
apt-cache madison kubeadm
apt-cache madison kubectl
apt-cache madison kubelet
```
  d. Edit the `HeadendClient.cs` file:
    * updating the `latestSupportedVersion` constant.
    * adding or updating the package mappings.

## Check for base Docker image updates:

1. [haproxy](http://haproxy.org) - current: **1.9.2**
2. [.NET Core](https://github.com/dotnet/core/releases) - current: **2.1**
3. [golang](https://golang.org/doc/devel/release.html) - current: **1.9.4**

# Hyper-V and XenServer OS Images

**Ubuntu 18.04:** [releases](http://releases.ubuntu.com/18.04/) - current **18.04.1**

Follow the Hyper-V and XenServer template instructions in the `$/Doc` folder.

## Kubernetes Components

1. **Kubernetes:** [releases](https://github.com/kubernetes/kubernetes/releases) - current: **1.16.0**

2. **Kubernetes Dashboard:** [releases](https://github.com/kubernetes/dashboard/releases) - current: **1.10.1**

3. **Helm:** [releases](https://github.com/helm/helm/releases) - current: **2.12.3**

4. **Istio:** [releases](https://github.com/istio/istio/releases) - current: **1.3.1**

5. **Calico**: [releases](https://github.com/projectcalico/calico/releases) - current: **3.4.0**

  **NOTE:** You'll need to update the two URLs in `HeadendClient`.

# Other Components

1. **OpenSSL**: [releases](https://indy.fulgan.com/SSL/) - current: **1.0.2q** (March 2018)
  
  **NOTE:** Download and extract the files to `$/external/OpenSSL/*`

2. **PowerShell Core**: [releases](https://github.com/PowerShell/PowerShell/releases) = current: **6.1.1-win-x86**

  **NOTE:** Download the ZIP archive and replace the **$/External/Powershell-win-x86.zip** file.

3. **Ubuntu Node Disk Templates:** - current **18.04.1**

  Check for new Ubuntu 18.04 releases and build new templates

# Cadence

1. Check http://github.com/uber/cadence for new release branches.

2. Run this command to fetch the release from the official repository and add it to our for:
  ```
  neon-cadence BRANCH
  ```

3. Update **$/Images/cadence-test/publish.ps1** and build the image(s).
