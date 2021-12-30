using System;
using NUnit.Framework;
using System.IO;
using YamlDotNet.RepresentationModel;
using DarkConfig;

[TestFixture]
public class YamlParseTests {
    static YamlNode ParseYamlNode(string str, string filename = null) {
        var input = new StringReader(str);
        var yaml = new YamlStream();
        yaml.Load(input, filename);

        return yaml.Documents[0].RootNode;
    }
    
    [TestFixture]
    public class YamlDocParser {
        [SetUp]
        public void DoSetUp() {
            Config.Platform = new UnityPlatform();
        }

        [TearDown]
        public void DoTearDown() {
            Config.Platform = null;
        }
        
        [Test]
        public void JsonSubset_TraversedByDocNode() {
            string testStr = "{\"test_key\":\"test_value\"}";
            var dn = (DocNode) new YamlDocNode(ParseYamlNode(testStr));

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
            var dn = (DocNode) new YamlDocNode(ParseYamlNode(testStr, "testfilename"));
            try {
                var x = dn["key"][3];
                Assert.Fail("Should not succeed at indexing dictionary " + x);
            } catch (DocNodeAccessException e) {
                // verify that there's a line number in the exception
                Assert.True(e.Message.IndexOf("Line: 4", StringComparison.Ordinal) > 0, e.Message);
                // verify that the dummy filename that we passed in shows up in the exception message
                Assert.True(e.Message.IndexOf("testfilename", StringComparison.Ordinal) > 0, e.Message);
            }
        }
    }
}