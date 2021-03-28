using NUnit.Framework;
using DarkConfig;
using System.Collections.Generic;

[TestFixture]
class DocPathFacts {
    [SetUp]
    public void DoSetup() {       
    }

    [Test]
    public void Equal_SingleSegments() {
        Assert.True(new DocPath("a").Equals(new DocPath("a")));
    }

    [Test]
    public void NotEqual_SingleSegments() {
        Assert.False(new DocPath("b").Equals(new DocPath("a")));
    }

    [Test]
    public void Equal_SingleParent() {
        Assert.True(new DocPath("b", "a").Equals(new DocPath("b", "a")));
    }

    [Test]
    public void NotEqual_SingleParent() {
        Assert.False(new DocPath("b", "a").Equals(new DocPath("b", "aa")));
    }

    [Test]
    public void NotEqual_SingleParent2() {
        Assert.False(new DocPath("b", "a").Equals(new DocPath("c", "a")));
    }

    [Test]
    public void ToString_SingleSegment() {
        Assert.AreEqual("a", new DocPath("a").ToString());
    }

    [Test]
    public void ToString_MultiSegment() {
        Assert.AreEqual("a.b", new DocPath("b", "a").ToString());
    }
}
