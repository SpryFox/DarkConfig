CONTRIBUTING
==============

Thanks for thinking about contributing to DarkConfig!  There's a lot of ways that someone can contribute to an open source project, many of which can be done without programming experience.  For general information check out [this article](https://blog.newrelic.com/2014/05/05/open-source_gettingstarted/), or [this one](https://guides.github.com/activities/contributing-to-open-source/).

- Help organize issues.  Detect and consolidate duplicates.  Reply to issues that are just questions.
- Tackle existing issues.  Sometimes issues are stuck because they lack information or specificity.  Sometimes they need special setup to reproduce.  Sometimes they need decision making.
- Improve documentation.  Rewrite things that are confusing, fix typos.  Ask questions about things you're confused about!
- Report bugs clearly.  Include a simple reproduction if possible.  If not, be as deliberate and thorough in your description as you can, including code and context.  DarkConfig is something of a "be everything" project, unfortunately, so it will have lots of cases where folks are simply using it differently than anyone else ever did and exposing fresh bugs.
- Review pull requests.  They generally need reviewing to make sure they fit in with the rest of the project.  Not to mention testing that they work and achieve their goals!
- Improve compliance with the style.  We have a goal style but fail to meet it.  Finding those places and tidying them up will help keep the project clean for the future!
- Help broaden compatibility.  We currently support a narrow set of Unity versions, but it would be nice to support more, and also to support other C# platforms.
- Make Dark Skies better.  When developers are learning about a project, looking at a real-world example is more instructive than simple contrived demos.  The richer and more finished a game Dark Skies becomes, the more competent people will be at using DarkConfig based on its example.  This can also provide a motivating impetus to add or change features.

We'll be more likely to maintain DarkConfig if it's easy, so the more you can help us save time, the more likely we are to make forward progress!

# Code Contributions

We welcome code contributions!  Please check through this section to make sure that your contribution is as smooth as it can be.

- Keep in mind the scope of the project.  DarkConfig is meant to be flexible and powerful, but it probably shouldn't grow to be a sprawling Django-like framework.  It should remain focused on loading configuration files with minimum fuss.
- Please only open one pull request per feature/fix.  Omnibus pull requests can be hard to understand.
- Make sure to follow the code style.  Indent using 4 spaces, braces in OTBS style except one-line ifs lack newlines or braces, member variables prefixed with m_ and statics with s_, capitalize publics, no unnecessary properties.
- Make sure the new feature is covered by tests.  Let's shoot for 100% coverage, every contribution helps!
- Try to maintain backwards compatibility.  This just makes it smoother for a large group of people to keep contributing.  There are some things that might require breakage, in which case we should create a branch and a DarkConfig 2.0 release.
- Avoid major refactorings.  Those can make the contirbution hard to read and understand.
- Improve or maintain performance.  DarkConfig ain't never gonna be the fastest config loader out there, but we do have projects that need to load dozens of megabytes of configs in thousands of files and those should remain workable.
- When you contribute code to the project, you implicitly do so under the terms of the DarkConfig LICENSE.  Ensure you have the the rights to the code you're contributing.
- Always run all the tests before committing!  They're there to check for simple mistakes.  See the tests docmentation in the Docs directory for information on how to run them.

# Areas of Development

Some areas that currently need thought and/or work:

More tests!  This is *infrastructure* so we want it to be rock-solid.  I typically find it difficult to test the runtime of the system; pretty much every method on ConfigFileManager is undertested.  It's so annoying to set up integration tests in Unity!  The new ConsolePlatform maybe provides a way to make unit tests because we can run coroutines manually.  In general though, increasing test coverage, and simplifying test authoring, is always welcome.

Singletons.  Right now DarkConfig is designed as a singleton; you only have one Config.  I'm not completely convinced that this is the best thing.  Perhaps it should be refactored to be a constructable object, and if a game wants to treat it like a singleton, it's easy enough to just assign the object to a static variable.

Removing friction.  Here's a few rough edges:
- The necessity of creating your own editor script for index generation.  Maybe we should configure DarkConfig via a configuration file.  Or maybe we can get rid of the index file.  We maybe make the example auto-update the index file whenever files on disk change.
- Preloading.  It fixed a number of race condition bugs when introduced, so it's definitely good.  But maybe it could be cleaner and less obtrusive.
- PostDocs kinda suck in practice.  I'd love to wedge a tiny bit more magic into DarkConfig to make my day authoring configs a little easier.

Improving the DLL building.  I like DLLs because they make installation easy and reduce compilation times in the target project.  The way we currently build these DLLs sucks.  The Unity DLL building uses hardcoded paths to files elsewhere on my computer.  They're not tested.  What's the right thing to do?  I don't know.  Let's find out!  It'd also be nice to have a single command to build all the DLLs.

Compatibility improvements.  More Unity versions would be nice.  I'm also _pretty_ sure that DarkConfig works on iOS with the .Net 2.0 subset, but it's been a while since I've actually tested it.

Security.  It would be nice if without doing a ton of work we were able to tighten up the security story for DarkConfig, making it suitable to load untrusted files.  This will mostly involve the YAML parser.
