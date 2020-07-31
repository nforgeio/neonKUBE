This is a full neonKUBE release intended for internal testing.  We're activily working towards getting ready for a preview release.

## Highlights:

**TODO:** Write something here!

### Docker Images

neonKUBE production Docker images can be found: [here](https://hub.docker.com/orgs/nkubeio/repositories)

Neon related production images are hosted on DockerHub: [here](https://hub.docker.com/orgs/nkubeio/repositories)

**Changes:** No significant changes for this release.

### <img src="https://doc.neonkube.com/media/alpha.png"/> neonKUBE Desktop

We're working on yet another Kubernetes distribution but this is still very much a work in progress.  Although we include the install binaries for this, we recommend that you folks avoid these until we're further along.

<table>
  <tr>
    <td width="85px" align="center"><img src="https://doc.neonkube.com/media/alpha.png"/></td>
    <td><b>neon-osx:</b> is the OS/X build of the <b>neon-cli</b> command line tool.  We're currently using this internally in a day-job project for generating data models using <b>ModelGen</b>.  This will eventually be included in a proper OS/X installer.</td>
  </tr>
  <tr>
    <td><img src="https://doc.neonkube.com/media/alpha.png"/></td>
    <td><b>neonKUBE-setup-#.#.#.exe:</b> installs the neonKUBE Desktop as well as the <b>neon-cli</b> command line tool.</td>
  </tr>
</table>

For Windows, you simply need to download and run **neonKUBE-setup-#.#.#.exe** to install or upgrade **neonKUBE Desktop** and the **neon-cli** command line tool. 

We don't have an OS/X version of the desktop yet, but you can manually install **neon-cli** via:
1. Download the **neon-osx** file below.  This will appear in Safari downloads as **neon-osx.dms**.
2. Manually copy **neon-osx.dms** below to your `/usr/local/bin` directory (we don't have a **.dmg** file yet).
3. Open a terminal window and run these commands:
    ```
    sudo bash
    cd /usr/local/bin
    rm neon
    mv neon-osx.dms neon
    chmod 777 neon
    spctl --master-disable
    ```

**Changes:** No significant changes for this release.

Neon components are released using versions compatible with [Semantic Versioning 2.0](https://semver.org/).  All packages and binaries are unit tested together before being published and you should upgrade all Neon nuget packages together so that all have the same version number.  Note that some packages may have pre-release identifier, indicating that component is still a work in progress or that a package is only for use by other Neon components.

<table>
  <tr>
    <td width="85px" align="center"><img src="https://doc.neonkube.com/media/release.png"/></td>
    <td>Indicates that the release is expected suitable for production use. Released binary versions follow the semantic version 2.0 specification and don't include a pre-release identifier.</td>
  </tr>
  <tr>
    <td><img src="https://doc.neonkube.com/media/preview.png"/></td>
    <td>Indicates that the released binary still has some work in progress but is relatively stable and also that we expect that we we'll try to avoid making significant breaking changes to the API surface area. This may be suitable for production but you should take care.</td>
  </tr>
  <tr>
    <td><img src="https://doc.neonkube.com/media/alpha.png"/></td>
    <td>Indicates that the released binary is not ready for general production use. There are likely to be serious bugs and implementation gaps and it is also very likely that the API may still see very significant changes. We do early alpha releases to give interested parties a chance to review what we're doing and also so that we and close partners can give these a spin in test and sometimes production.</td>
  </tr>
  <tr>
    <td><img src="https://doc.neonkube.com/media/internal.png"/></td>
    <td>Indicates that the released binary is not intended for general consumption. These are typically referenced by other neonKUBE libraries and tools. </td>
  </tr>
</table>

### Binary SHA512 signatures:

**neonKUBE-setup-1.0.0.exe:**
`FILL THIS IN`

**neon.chm:**
`FILL THIS IN`

**neon-osx:**
`FILL THIS IN`