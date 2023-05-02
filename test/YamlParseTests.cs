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
        [Test]
        public void JsonSubset_TraversedByDocNode() {
            string testStr = "{\"test_key\":\"test_value\"}";
            var dn = (DocNode) new YamlDocNode(ParseYamlNode(testStr), "testfilename");
            Assert.Multiple(() => {
                Assert.That(dn, Has.Count.EqualTo(1));
                Assert.That(dn["test_key"].StringValue, Is.EqualTo("test_value"));
                Assert.That(dn.ContainsKey("test_key"), Is.True);
            });
            foreach (var entry in dn.Pairs) {
                Assert.Multiple(() => {
                    Assert.That(entry.Key, Is.EqualTo("test_key"));
                    Assert.That(entry.Value.StringValue, Is.EqualTo("test_value"));
                });
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

            Assert.Multiple(() => {
                Assert.That(exception, Is.Not.Null);

                // verify that there's a line number in the exception
                Assert.That(exception.Message.IndexOf("Line: 4", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));

                // verify that the dummy filename that we passed in shows up in the exception message
                Assert.That(exception.Message.IndexOf("testfilename", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            });
        }
    }
}
