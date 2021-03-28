using UnityEngine;
using NUnit.Framework;
using DarkConfig;
using System.Collections.Generic;
using System;

[TestFixture]
class DefaultFromDocFacts {
    const string c_filename = "DefaultFromDocFacts_TestFileName";

    T ReifyString<T>(string str) where T : new() {
        var doc = Config.LoadDocFromString(str, c_filename);
        T tc = default(T);
        ConfigReifier.Reify(ref tc, doc);
        return tc;
    }

    [Test]
    public void Color_FourFloats() {
        var c = ReifyString<Color>("[0.1, 0.5, 0.5, 0.9]");
        Assert.AreEqual(c, new Color(0.1f, 0.5f, 0.5f, 0.9f));
    }

    [Test]
    public void Color_ThreeFloats() {
        var c = ReifyString<Color>("[0.1, 0.5, 0.5]");
        Assert.AreEqual(c, new Color(0.1f, 0.5f, 0.5f, 1f));
    }

    [Test]
    public void Color_FourBytes() {
        var c = ReifyString<Color>("[47, 83, 200, 240]");
        Assert.AreEqual(0.1843137f, c.r, 0.0001);
        Assert.AreEqual(0.3254901f, c.g, 0.0001);
        Assert.AreEqual(0.7843137f, c.b, 0.0001);
        Assert.AreEqual(0.9411764f, c.a, 0.0001);
    }

    [Test]
    public void Color_ThreeBytes() {
        var c = ReifyString<Color>("[47, 83, 200]");
        Assert.AreEqual(0.1843137f, c.r, 0.0001);
        Assert.AreEqual(0.3254901f, c.g, 0.0001);
        Assert.AreEqual(0.7843137f, c.b, 0.0001);
        Assert.AreEqual(1f, c.a, 0.0001);
    }

    [Test]
    public void Color_Scalar_HexFourBytes() {
        var c = ReifyString<Color>("2b313efe");
        Assert.AreEqual(0.1686274f, c.r, 0.0001);
        Assert.AreEqual(0.1921568f, c.g, 0.0001);
        Assert.AreEqual(0.2431372f, c.b, 0.0001);
        Assert.AreEqual(0.9960784f, c.a, 0.0001);
    }

    [Test]
    public void Color_Scalar_HexThreeBytes() {
        var c = ReifyString<Color>("2b313e");
        Assert.AreEqual(0.1686274f, c.r, 0.0001);
        Assert.AreEqual(0.1921568f, c.g, 0.0001);
        Assert.AreEqual(0.2431372f, c.b, 0.0001);
        Assert.AreEqual(1f, c.a, 0.0001);
    }

    [Test]
    public void Color_Scalar_FourBytes() {
        var c = ReifyString<Color>("128, 128, 128, 128");
        Assert.AreEqual(0.50196f, c.r, 0.0001);
        Assert.AreEqual(0.50196f, c.g, 0.0001);
        Assert.AreEqual(0.50196f, c.b, 0.0001);
        Assert.AreEqual(0.50196f, c.a, 0.0001);
    }

    [Test]
    public void Color_Scalar_ThreeBytes() {
        var c = ReifyString<Color>("128, 128, 128");
        Assert.AreEqual(0.50196f, c.r, 0.0001);
        Assert.AreEqual(0.50196f, c.g, 0.0001);
        Assert.AreEqual(0.50196f, c.b, 0.0001);
        Assert.AreEqual(1f, c.a, 0.0001);
    }
}