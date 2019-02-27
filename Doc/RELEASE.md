# neonKUBE Release Process:

## Prepare

1. Merge all desired changes into the **MASTER** branch from the **JEFF** and/or other development branches.

2. Manually clean and rebuild the entire solution (**RELEASE** configuration**): 

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`neonkube-build`

4. Build and publish all of the Docker images: `powershell -file publish.ps1 -all`

5. Deploy a test cluster.

6. Run all unit tests against the test cluster and fix any bugs until all tests pass.

## Release 

1. Update `$/product-version.txt` (or `GitHub/product-version.txt` in the solution) with the 
   new package version as required.

2. Update the product version here too: `Neon.Global.Build.NuGetVersion`

3. Update `$/kube-version.txt` (or `GitHub/kube-version.txt` in the solution) with the 
   required Kubernetes version as required.

4. Open the **Properties** each of the library projects and update the **Release notes**.

5. Create a new local `release-VERSION` branch from `MASTER` (where `VERSION` is the same version as saved to `$/product-version.txt`).

6. Rebuild the RELEASE version via:

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`neonkube-build -release -installer`

7. Execute **as ADMIN**: `powershell -f %NF_ROOT%/Toolbin/nuget-neonforge-public.ps1` to publish the packages to **nuget.org**.

8. Build and publish all of the Docker images: `powershell -file publish.ps1 -all`

9. Upgrade an older cluster and verify by running cluster unit tests.

10. Deploy a fresh cluster and verify by running the cluster unit tests.

11. Fix any important issues and commit the changes.

12. Push the `release-VERSION` branch to GitHub.

13. GitHub Release: [link](https://help.github.com/articles/creating-releases/)

  a. Create the release if it doesn't already exist
  b. Set **Tag** to the version
  c. Set **Target** to the `release-VERSION` branch
  e: Check **This is a pre-release** as required
  f. Add the release setup binary named like: **neonKUBE-setup-0.1.0-alpha.exe**
  g. Edit the release notes including adding the SH512 for the setup from:

&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`"$NF_BUILD%\neonKUBE-setup.sha512.txt`

  g. Publish the release

## Post Release

1. Merge any changes from the RELEASE branch back into MASTER.

2. Bump the version numbers in `$/product-version.txt` and `$Lib/Neon.Global/Build.cs`.
k
3. Merge **MASTER** into the **JEFF** and/or any other development branches, as required.

4. Create a draft for the next GitHub release.

 # Release Version Conventions

* Use semantic versioning.
* The MAJOR, MINOR, and PATCH versions work as defined: [here](https://semver.org/)
* Patch versions start at 0.
* Non-production releases that are stable enough for limited public consumption will look like: **MAJOR.MINOR.PATCH-preview.#**
* Near production release candidates will look like: **MAJOR.MINOR.PATCH-rc.#**
