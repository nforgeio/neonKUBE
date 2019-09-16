# neonKUBE Release Process:

## Prepare

1. Merge all desired changes into the **MASTER** branch from the **JEFF** and/or other development branches.

2. Manually clean and rebuild the entire solution (**RELEASE** configuration**): 

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`neonkube-build`

3. Build and publish all of the Docker images: `powershell -file %NF_ROOT%/Images/publish.ps1 -all`

## Release 

1. Select the release branch.

2. Update `$/product-version.txt` (or `GitHub/product-version.txt` in the solution) with the 
   new package version as required.

3. Update the product version here too: `$/Lib/Neon.Common/Build.cs`

4. Run all unit tests.

5. Update `$/kube-version.txt` (or `GitHub/kube-version.txt` in the solution) with the 
   required Kubernetes version as required.

6. Rebuild the RELEASE version via:

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`neonkube-build -release -installer`

7. Verify that the new release installer works.

8. Execute **as ADMIN**: `powershell -f %NF_ROOT%/Toolbin/nuget-neonforge-public.ps1` to publish the packages to **nuget.org**.

9. Build and publish all of the Docker images: `powershell -file %NF_ROOT%/Images/publish.ps1 -all`

10. Push the `release-VERSION` branch to GitHub with a comment like: **RELEASE: 0.5.8-alpha**

11. GitHub Release: [link](https://help.github.com/articles/creating-releases/)

  a. Create the release if it doesn't already exist
  b. Set **Tag** to the version
  c. Set **Target** to the `release-VERSION` branch
  e: Check **This is a pre-release** as required
  f. Add the release setup binary named like: **neonKUBE-setup-0.1.0-alpha.exe**
  g. Edit the release notes including adding the SHA512 for the setup from:

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`"$NF_BUILD%\neonKUBE-setup.sha512.txt`

  g. Publish the release

## Post Release

1. Merge any changes from the RELEASE branch back into MASTER.

2. Merge **MASTER** into the **JEFF** and/or any other development branches, as required.

3. Create the next release branch and push it.

4. Create a draft for the next GitHub release.

    * Be sure to set the branch to the new release branch.

5. Archive the source code:

  1. Close all Visual Studio windows.
  2. Run `neon-archive.cmd` in a command window.
  3. Archive `C:\neonKUBE.zip` to AWS S3 and the local disk.

 # Release Version Conventions

* Use semantic versioning.
* The MAJOR, MINOR, and PATCH versions work as defined: [here](https://semver.org/)
* Patch versions start at 0.
* Non-production releases that are stable enough for limited public consumption will look like: **MAJOR.MINOR.PATCH-preview.#**
* Near production release candidates will look like: **MAJOR.MINOR.PATCH-rc.#**
