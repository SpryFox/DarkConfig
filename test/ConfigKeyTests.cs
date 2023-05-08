using DarkConfig;
using NUnit.Framework;
using System;

[TestFixture]
class ConfigKeyTests {

    class TestType {
        [ConfigKey("level")]
        public int CurrentLevel = 1;

        [ConfigKey("StartingXP")]
        public int XP { get; set; } = 5;
    }
    
    [Test]
    public void ConfigKeyAttributeChangesParsing() {
        const string yaml = "{level: 42, StartingXP: 99}";
        var doc = Configs.ParseString(yaml, "ConfigKeyAttributeChangesParsing");
        var instance = new TestType();
        Configs.Reify(ref instance, doc);
        
        Assert.Multiple(() => {
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.CurrentLevel, Is.EqualTo(42));
            Assert.That(instance.XP, Is.EqualTo(99));
        });
    }

    class NullKeyClass {
        [ConfigKey(null)]
        public int fail;
    }
    
    [Test]
    public void SettingANullKeyNameThrows() {
        const string yaml = "{fail: 42}";
        var doc = Configs.ParseString(yaml, "SettingANullKeyNameThrows");
        var instance = new NullKeyClass();
        
        Assert.Throws<ParseException>(() => {
            Configs.Reify(ref instance, doc);
        });
    }

    class EmptyKeyClass {
        [ConfigKey("")]
        public int fail;
    }
    
    [Test]
    public void SettingAnEmptyKeyNameThrows() {
        const string yaml = "{fail: 42}";
        var doc = Configs.ParseString(yaml, "SettingAnEmptyKeyNameThrows");
        var instance = new EmptyKeyClass();
        
        Assert.Throws<ParseException>(() => {
            Configs.Reify(ref instance, doc);
        });
    }
    
    class WhitespaceKeyClass {
        [ConfigKey("   ")]
        public int fail;
    }
    
    [Test]
    public void SettingAnAllWhitespaceKeyNameThrows() {
        const string yaml = "{fail: 42}";
        var doc = Configs.ParseString(yaml, "SettingAnAllWhitespaceKeyNameThrows");
        var instance = new WhitespaceKeyClass();
        
        Assert.Throws<ParseException>(() => {
            Configs.Reify(ref instance, doc);
        });
    }
    
    class PaddedKeyClass {
        [ConfigKey(" after  ")]
        public int before;
    }

    [Test]
    public void KeyNamesAreTrimmed() {
        const string yaml = "{after: 42}";
        var doc = Configs.ParseString(yaml, "SettingAnAllWhitespaceKeyNameThrows");
        var instance = new PaddedKeyClass();
        Configs.Reify(ref instance, doc);
        
        Assert.That(instance.before, Is.EqualTo(42));
    }
}
