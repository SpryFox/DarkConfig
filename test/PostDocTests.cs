using DarkConfig;
using NUnit.Framework;

[TestFixture]
class PostDocTests {
    const string FILENAME = "PostDocTests_TestFilename";

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

    static T ReifyString<T>(string str) where T : new() {
        var doc = Configs.ParseString(str, FILENAME);
        var result = default(T);
        Configs.Reify(ref result, doc);
        return result;
    }

    [Test]
    public void PostDoc_IsCalled() {
        var instance = ReifyString<PostDocClass>("baseKey: 10");
        Assert.That(instance.baseKey, Is.EqualTo(11));
    }

    [Test]
    public void PostDoc_DoesntExist() {
        var doc = Configs.ParseString("baseKey: 10", FILENAME);
        PostDocClass2 instance = null;
        Configs.Reify(ref instance, doc);
        Assert.Multiple(() => {
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.baseKey, Is.EqualTo(10));
        });
    }

    [Test]
    public void PostDoc_CanReplaceWithReturnValue() {
        var doc = Configs.ParseString("baseKey: 10", FILENAME);
        PostDocClass3 instance = null;
        Configs.Reify(ref instance, doc);
        Assert.Multiple(() => {
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.baseKey, Is.EqualTo(99));
        });
    }

    [Test]
    public void PostDoc_ManuallyRegistered() {
        var doc = Configs.ParseString("baseKey: 5", FILENAME);
        PostDocClass2 instance = null;
        Configs.RegisterPostDoc<PostDocClass2>(instance => {
            return new PostDocClass2 {baseKey = 10};
        });
        Configs.Reify(ref instance, doc);
        Assert.Multiple(() => {
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.baseKey, Is.EqualTo(10));
        });
    }
}
