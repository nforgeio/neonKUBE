Notes from Jack:

* We need to set this in the Cadence client:

  ```
  Environment.SetEnvironmentVariable("GODEBUG", "cgocheck=1");
  ```

* Examples of passing pointers, byte[], int[], strings, etc. to a shared-c golang .dll that might be useful:

  https://github.com/johncburns1/Golang-.NET-SharedC
  
* This is a summary of all of the go execution modes.  I have only read through half of it, but there is some useful information that might pertain to the debugging issue:

  https://docs.google.com/document/d/1nr-TQHw_er6GOQRsF6T43GGhFDelrAP0NqSS_00RgZQ/edit#
  
* I made another project in the cadence-proxy called playground.  It has everything set up so that all you have to do is run the exe or debug the main with a cadence-server running:

  $(NF_ROOT)/Go/src/github.com/cadence-proxy/cmd/playground
  
* The workflow code and main file are in `./simple`

* All of the cadence-client configuration is in `playground/common` the default domain is **test-domain**
 * I added its build to the Makefile as well the build command is make simple in the cadence-proxy project root: `$(NF_ROOT/Go/src/github.com/cadence-proxy)`
  * I would recommend doing this in VS code because it allows you to infer a $GO_PATH
  * And build go projects from within the neonKUBE directory
  
* To do this you are going to need some settings configured in VS code:

  * Workspace settings:
  ```
  {
    "terminal.integrated.shell.windows": "C:\\WINDOWS\\System32\\cmd.exe",
    "terminal.integrated.shellArgs.windows": [
        "/k", "C:\\Program Files (x86)\\cmder_mini\\vendor\\init.bat", "/noautorun"
    ],
    "terminal.integrated.env.windows": {
        "GOPATH": "C:\\Users\\johnc\\source\\repos\\nforgeio\\neonKUBE\\Go"
    },
    "go.inferGopath": true,
    "go.gocodeAutoBuild": true,
    "go.autocompleteUnimportedPackages": true,
    "go.lintTool": "gometalinter",
    "go.useCodeSnippetsOnFunctionSuggest": true,
    "go.vetOnSave": "package",
    "go.lintOnSave": "package",
    "breadcrumbs.enabled": true,
    "window.zoomLevel": 0,
    "editor.renderWhitespace": "all",
    "editor.renderControlCharacters": true,
    "workbench.editor.highlightModifiedTabs": true
  }
  ```
  
I have cmder as my integrated terminal
And you can set your $GO_PATH for good measure
in terminal.integrated.windows:
You are also going to need to install the VScode Go extension.
It should prompt you to install it when you open a go file
Also these are the launch configurations that I am using in launch.json
{
    // Use IntelliSense to learn about possible attributes.
    // Hover to view descriptions of existing attributes.
    // For more information, visit: https://go.microsoft.com/fwlink/?linkid=830387
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Launch",
            "type": "go",
            "request": "launch",
            "mode": "auto",
            "program": "${fileDirname}",
            "env": {},
            "args": []
        }
    ]
}
specifies debugging settings in VS code
So all you have to do to write sample workflows is to edit simple_workflow.go (you can add activities and whatnot as well).  If you want to change the behavior about how those workflows are executed, use the workflowClient initialized in main.gol
main.go
  
I added a file for child workflows as well, so the files are parent_workflow.go (which is the main workflow file), child_workflow.go (where you can define child workflows, and main.go (where you actually execute the workflows).
