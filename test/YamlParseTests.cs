using System;
using NUnit.Framework;
using System.IO;
using YamlDotNet.RepresentationModel;
using DarkConfig;

[TestFixture]
public class YamlParseTests {
    static YamlNode ParseYamlNode(string str) {
        var yaml = new YamlStream();
        yaml.Load(new StringReader(str));
        return yaml.Documents[0].RootNode;
    }
    
    [TestFixture]
    public class YamlDocParser {
        [SetUp]
        public void DoSetUp() {
            Config.Platform = new ConsolePlatform();
        }

        [TearDown]
        public void DoTearDown() {
            Config.Platform = null;
        }
        
        [Test]
        public void JsonSubset_TraversedByDocNode() {
            string testStr = "{\"test_key\":\"test_value\"}";
            var dn = (DocNode) new YamlDocNode(ParseYamlNode(testStr), "testfilename");

            Assert.AreEqual(dn.Count, 1);
            Assert.AreEqual(dn["test_key"].StringValue, "test_value");
            Assert.IsTrue(dn.ContainsKey("test_key"));
            foreach (var entry in dn.Pairs) {
                Assert.AreEqual(entry.Key, "test_key");
                Assert.AreEqual(entry.Value.StringValue, "test_value");
            }
        }

        [Test]
        public void DocNodeDictionaryAccess_ThrowsWhenIndexed() {
            string testStr = @"---
# interrupting comment
key:
    inner_key: value
";
            var dn = (DocNode) new YamlDocNode(ParseYamlNode(testStr), "testfilename");

            var exception = Assert.Throws<DocNodeAccessException>(() => {
                var x = dn["key"][3];
            });
            
            Assert.IsNotNull(exception);
            
            // verify that there's a line number in the exception
            Assert.GreaterOrEqual(exception.Message.IndexOf("Line: 4", StringComparison.Ordinal), 0);

            // verify that the dummy filename that we passed in shows up in the exception message
            Assert.GreaterOrEqual(exception.Message.IndexOf("testfilename", StringComparison.Ordinal), 0);
        }
    }
}