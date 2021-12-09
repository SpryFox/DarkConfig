using System.Collections.Generic;
using NUnit.Framework;
using System.IO;
using YamlDotNet.RepresentationModel;
using UnityEngine;
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
    public class RawYamlParser {
        [Test]
        public void JsonSubset_ParsesOK() {
            string testStr = "{\"test_key\":\"test_value\"}";
            var mapping = (YamlMappingNode) ParseYamlNode(testStr);

            Assert.AreEqual(mapping.Children.Count, 1);

            foreach (var entry in mapping.Children) {
                Assert.AreEqual(((YamlScalarNode) entry.Key).Value, "test_key");
                Assert.AreEqual(((YamlScalarNode) entry.Value).Value, "test_value");
            }
        }

        [Test]
        public void BasicYamlDoc_ParsesOK() {
            string testStr = @"---
            yaml_key: yaml_value
        ";
            var mapping = (YamlMappingNode) ParseYamlNode(testStr);

            foreach (var entry in mapping.Children) {
                Assert.AreEqual(((YamlScalarNode) entry.Key).Value, "yaml_key");
                Assert.AreEqual(((YamlScalarNode) entry.Value).Value, "yaml_value");
            }
        }
    }

    [TestFixture]
    public class YamlDocParser {
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
                Assert.True(e.Message.IndexOf("Line: 4") > 0, e.Message);
                // verify that the dummy filename that we passed in shows up in the exception message
                Assert.True(e.Message.IndexOf("testfilename") > 0, e.Message);
            }
        }
    }
}