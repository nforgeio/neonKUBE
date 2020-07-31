# Neon Release Process:

## Prepare

1. Merge all desired changes into the **MASTER** branch from the **JEFF** and/or other development branches.

## Release 

1. Select the new release-neonKUBE-VERSION branch and merge from **MASTER**.

2. Run all unit tests on Windows in **RELEASE** mode.

3. Close Visual Studio.  It seems to be locking some `obj` directories which prevents the **Snippits** project from building below.

4. Build the Dopcker images and release artifacts:
   ```
   neon-publish-images -all
   neon-builder -all
   ```

5. Verify that the new release installer works.

6. Push the `release-neonKUBE-VERSION` branch to GitHub with a comment like: **RELEASE: neonKUBE-v1.0.0**

7. Edit the release notes including adding the SHA512s for:
  ```
  %NF_BUILD%\neonKUBE-setup.sha512.txt
  %NF_BUILD%\osx\neon-1.0.0.sha512.txt
  %NF_BUILD%\neon.chm.sha512.txtl
  ```

11. Publish the release

## Post Release

1. Merge any changes from the RELEASE branch back into MASTER.

2. Merge **MASTER** into the **JEFF** and/or any other development branches, as required.

3. Create the next release branch and push it. This should be named like: **release-neonKUBE-v1.0.0**

4. Create the next draft release on GitHub:
  a. Copy `$/doc/neonKUBE-RELEASE-TEMPLATE.md` as the initial release text
  b. Set **Tag** to the version with a leading "v" (like **neonKUBE-v1.0.0**)
  c. Set **Target** to the `release-neonKUBE-VERSION` branch
  d. Set **Title** like: **neonKUBE-v1.0.0**
  e. Check **This is a pre-release** as required
  f. Add the release setup binary named like: **neonKUBE-setup-1.0.0.exe**
  g. Add the OS/X neon-cli binary from **osx** folder as: **neon-osx** 
  h. Add **neon.chm**
  i. Edit the release notes including adding the SHA512s for:
  ```
  %NF_BUILD%\neonKUBE-setup.sha512.txt
  %NF_BUILD%\osx\neon-1.0.0.sha512.txt
  %NF_BUILD%\neon.chm.sha512.txtl
  ```

5. Archive the source code:

  1. Close all Visual Studio windows.
  2. Run `neon-archive.cmd` in a command window.
  3. Archive `C:\neonKUBE.zip` to AWS S3 and the local backup disk.

## Release Version Conventions

* Use semantic versioning.
* The MAJOR, MINOR, and PATCH versions work as defined: [here](https://semver.org/)
* Patch versions start at 0.
* Non-production releases that are stable enough for limited public consumption will look like: **MAJOR.MINOR.PATCH-preview.#**
* Near production release candidates will look like: **MAJOR.MINOR.PATCH-rc.#**
