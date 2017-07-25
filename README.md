# Honorbuddy Quest Behaviors

This repo contains the sources for the default quest behaviors
shipped with [Honorbuddy](http://www.honorbuddy.com/).

## Using the quest behaviors

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

## Developing the quest behaviors

Since Honorbuddy compiles the quest behaviors by itself there is no need to set up
a proper build environment. However, this is still beneficial if you are going to
be making changes to the quest behaviors to make sure your changes still compile.

The repo includes at VS2017 solution which can be opened. To make the project compile
you must add references to Honorbuddy's `.exe` and `.dll` files. The project is already
set up to reference the correct assemblies in the `Dependencies` directory, so this
directory just needs to be created.

The easiest way to do that is with a symbolic link to your Honorbuddy installation. If
the path `C:\Path\to\Honorbuddy\Honorbuddy.exe` is valid, this is easily done by opening
a command prompt in the root of quest behaviors (in the same folder as the `.sln` file)
and running the following command (if using PowerShell, you should prefix the following command
with `cmd /c`):
```
mklink /J Dependencies "C:\Path\to\Honorbuddy"
```
The quest behaviors should now build successfully in VS2017.

## Contributing

See the [Contributing document](CONTRIBUTING.md) for guidelines for making contributions.

## Discuss

You can discuss Honorbuddy in our Discord channel which can be found [here](https://discordapp.com/invite/0q6seK1er9pqFZkZ).
