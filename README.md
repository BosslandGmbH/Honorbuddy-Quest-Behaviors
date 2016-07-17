# Honorbuddy Quest Behaviors
This repo contains the sources for the default quest behaviors
shipped with [Honorbuddy](http://www.honorbuddy.com/).

# Using the quest behaviors
When a new version of Honorbuddy is released it automatically contains
a snapshot of the master branch of this repository at the time of the release.
This means that a new release of HB always will have the newest available version
of quest behaviors.

If you still want to update manually, simply download a .zip from GitHub by pressing
the `Clone or download` -> `Download ZIP` buttons. Then delete the `Quest Behaviors`
folder inside Honorbuddy and extract the `Quest Behaviors` folder from the .zip into
the Honorbuddy directory. Assuming `C:\Path\to\Honorbuddy\Honorbuddy.exe` is a valid
path, the .zip should be extracted so that `C:\Path\to\Honorbuddy\Quest Behaviors\InteractWith.cs`
is valid.

# Developing the quest behaviors
Since Honorbuddy compiles the quest behaviors by itself there is no need to set up
a proper build environment. However, this is still beneficial if you are going to
be making changes to the quest behaviors to make sure your changes still compile.

The repo includes at VS2015 solution which can be opened. To make the project compile
you must add references to Honorbuddy's `.exe` and `.dll` files. The project is already
set up to reference the correct assemblies in the `Dependencies` directory, so this
directory just needs to be created.

The easiest way to do that is with a symbolic link to your Honorbuddy installation. If
the path `C:\Path\to\Honorbuddy\Honorbuddy.exe` is valid, this is easily done by opening
a command prompt in the root of quest behaviors (in the same folder as the `.sln` file)
and running the following command:
```
mklink /J Dependencies "C:\Path\to\Honorbuddy"
```
The quest behaviors should now build successfully in VS2015.

# Contributing
## Issues
Feel free to open an issue if you think you have found a bug, or if you have a request
for an enhancement. Please include as much information as possible when opening these.
This includes:
* Log files
* Screenshots. Make sure to blur out names and other uniquely identifying information.
* Locations
* Steps to reproduce
See [here](https://help.github.com/articles/file-attachments-on-issues-and-pull-requests/) for how to attach files to issues.

## Pull requests
Pull requests are always welcome! Please make sure to follow our guidelines when submitting one:
* Coding style: We use the .NET foundation coding style. You can read it [here](https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/coding-style.md).  
Note that we will not accept pull that do not follow this coding style appropriately.
* Use coroutines instead of behavior trees. This rule applies if you are introducing new logic code.

The repo includes a `.editorconfig` file. If you have an IDE that supports this file
it will automatically apply our indentation settings (spaces) when you open the files.
Otherwise you can [download a plugin](http://editorconfig.org/) for your favorite IDE.
This is optional but is very useful if you are switching between projects with different
indentation settings.

## Discuss
You can discuss Honorbuddy in our Discord channel which can be found [here](https://discordapp.com/invite/0q6seK1er9pqFZkZ).
