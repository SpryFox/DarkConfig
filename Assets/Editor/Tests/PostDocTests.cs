using DarkConfig;
using DarkConfig.Internal;
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

        public int baseKey;
    }

    const string FILENAME = "PostDocTests_TestFilename";

    static T ReifyString<T>(string str) where T : new() {
        var doc = Config.LoadDocFromString(str, FILENAME);
        var result = default(T);
        ConfigReifier.Reify(ref result, doc);
        return result;
    }

    [Test]
    public void PostDoc_IsCalled() {
        var instance = ReifyString<PostDocClass>("baseKey: 10");
        Assert.AreEqual(instance.baseKey, 11);
    }

    [Test]
    public void PostDoc_DoesntExist() {
        var doc = Config.LoadDocFromString("baseKey: 10", FILENAME);
        PostDocClass2 instance = null;
        ConfigReifier.Reify(ref instance, doc);
        Assert.NotNull(instance);
        Assert.AreEqual(instance.baseKey, 10);
    }

    [Test]
    public void PostDoc_CanReplaceWithReturnValue() {
        var doc = Config.LoadDocFromString("baseKey: 10", FILENAME);
        PostDocClass3 instance = null;
        ConfigReifier.Reify(ref instance, doc);
        Assert.NotNull(instance);
        Assert.AreEqual(instance.baseKey, 99);
    }
}