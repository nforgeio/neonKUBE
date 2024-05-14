The [ssh-keygen.exe] file will be included in NeonDESKTOP installer for Windows and will be deployed
to the app's root directory.  Windows now includes [ssh-keygen.exe] in recent Windows builds by default,
but that version of the tool does not allow SSH keys to be generated without passphrases when called by
scripts or other programs.

So I obtained this [ssh-keygen.exe] from Git at:

	C:\Program Files\Git\usr\bin\ssh-keygen.exe

This version can generate keys without a passphrase.  We may need to update this from time to time.
Note that I also copied just enough of the DLLs such that [ssh-keygen] can actually generate keys.
