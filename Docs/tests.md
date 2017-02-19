TESTS
========

We have unit tests and integration tests.  Both should be run to assure success.

Another "test" is to run the demo game, Dark Skies.  It should run correctly in the Unity editor from both the main scene and the loader scene.  Loading should be quick despite the volume of configs to load.  Changes to the Whip.bytes card should hotload and be visible in the player plane's appearance or behavior.


## Unit Tests

The unit tests are in the Assets/Editor/Tests directory.  From within Unity, run them using the Window -> Editor Tests Runner.

They are just NUnit tests so should be runnable in the normal ways.

## Integration Tests

Running these involves opening the TestScenes/IntegrationTests.unity in Unity, and running the Integration Test Runner from Unity Test Tools.  They're pretty flaky since they modify files on disk and rely on Unity's asset reloading.  Best results are achieved by running them one by one instead of all at once.