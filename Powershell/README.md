This folder includes commonly useful Powershell scripts.

**WARNING:**

These files are intended to be shared/included across multiple GitHub repos 
and should never include repo-specific code.

After modifying any file, you should take care to push any changes to the
other repos where the file is also present.

**includes.ps1:** This includes all of the other files

**utility.ps1:** Handy utilities

**git.ps1:** Git related operations

**github.ps1:** GitHub related operations

**github.actions.ps1:** GitHub Actions related operations

**one-password.ps1:** 1Password related operations

Note that you can include **includes.ps1** to import all of these files to your script or you can mix-and-match individual files.

**WARNING:**

You need to manually pull the NEONCLOUD repo on any jobrunners after making changes to any of these files.
