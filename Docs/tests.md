# Tests

DarkConfig has a number of unit tests and integration tests.  These are divided into general DarkConfig tests and Unity-specific tests.

There is also a demo game, Dark Skies, which can be used to test behavior.  To run the game, open and run the "Loader" scene in Unity.  Changes to the Whip.bytes card (or any config) should hotload and be visible in the player plane's appearance or behavior.

## General Unit Tests

The unit tests are located in the `test/` directory and are part of the `test/DarkConfig.Tests.csproj` project.  They use the nunit framework and you can run them through Rider or Visual Studio.  

## Unity specific tests

From within Unity, run them using `Window > Tests Runner`.