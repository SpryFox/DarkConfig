using DarkConfig;
using NUnit.Framework;

[TestFixture]
class PostDocTests {
    class PostDocClass {
        public static PostDocClass PostDoc(PostDocClass existing) {
            existing.baseKey += 1;
            return existing;
        }

        public int baseKey;
    }

    class PostDocClass2 {
        public int baseKey = 0;
    }

    class PostDocClass3 {
        public static PostDocClass3 PostDoc(PostDocClass3 existing) {
            return new PostDocClass3 {
                baseKey = 99
            };
        }

        public int baseKey = 0;
    }

    const string c_filename = "PostDocTests_TestFilename";

    static T ReifyString<T>(string str) where T : new() {
        var doc = Config.LoadDocFromString(str, c_filename);
        T tc = default(T);
        ConfigReifier.Reify(ref tc, doc);
        return tc;
    }

    [Test]
    public void PostDoc_IsCalled() {
        var tc = ReifyString<PostDocClass>("baseKey: 10");
        Assert.AreEqual(tc.baseKey, 11);
    }

    [Test]
    public void PostDoc_DoesntExist() {
        var doc = Config.LoadDocFromString("baseKey: 10", c_filename);
        PostDocClass2 tc = null;
        ConfigReifier.Reify(ref tc, doc);
        Assert.NotNull(tc);
        Assert.AreEqual(tc.baseKey, 10);
    }

    [Test]
    public void PostDoc_CanReplaceWithReturnValue() {
        var doc = Config.LoadDocFromString("baseKey: 10", c_filename);
        PostDocClass3 tc = null;
        ConfigReifier.Reify(ref tc, doc);
        Assert.NotNull(tc);
        Assert.AreEqual(tc.baseKey, 99);
    }
}