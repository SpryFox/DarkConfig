using NUnit.Framework;
using DarkConfig;
using System.Collections.Generic;
using System.Collections;

[TestFixture]
class ConsolePlatformFacts {
    ConsolePlatform cp;
    int counter = 0;
    int nestLevel = 0;

    [SetUp]
    public void DoSetup() {
        cp = new ConsolePlatform();
        counter = 0;
        nestLevel = 0;
    }

    IEnumerator TestCoro() {
        counter++;
        yield return null;
        counter++;
    }

    IEnumerator WaitCoro() {
        counter++;
        yield return cp.WaitForSeconds(1.5f);
        counter++;
    }

    IEnumerator NestCoro(uint nestAmount) {
        nestLevel++;
        if(nestAmount > 0) {
            yield return cp.StartCoroutine(NestCoro(nestAmount - 1));
        } else {
            yield return cp.StartCoroutine(TestCoro());
        }
        nestLevel--;
    }

    IEnumerator ExceptCoro() {
        counter++;
        yield return null;
        throw new System.ArgumentException("Arg");
    }

    [Test]
    public void StartCoroutine_Null_Skips() {
        cp.StartCoroutine(null);
    }

    [Test]
    public void StartCoroutine_Basic() {
        Assert.AreEqual(0, counter);
        cp.StartCoroutine(TestCoro());
        Assert.AreEqual(0, counter);
        cp.Update(0);
        Assert.AreEqual(1, counter);
        cp.Update(0.1f);
        Assert.AreEqual(2, counter);
    }

    [Test]
    public void StartCoroutine_Wait() {
        Assert.AreEqual(0, counter);
        cp.StartCoroutine(WaitCoro());
        Assert.AreEqual(0, counter);
        cp.Update(0);
        Assert.AreEqual(1, counter);
        cp.Update(0.1f);
        Assert.AreEqual(1, counter);
        cp.Update(1.6f);
        Assert.AreEqual(2, counter);
    }

    [Test]
    public void StartCoroutine_Nested1() {
        Assert.AreEqual(0, counter);
        cp.StartCoroutine(NestCoro(0));
        cp.Update(0);
        Assert.AreEqual(0, counter);
        Assert.AreEqual(1, nestLevel);
        cp.Update(0.1f);
        Assert.AreEqual(1, counter);
        Assert.AreEqual(1, nestLevel);
        cp.Update(0.2f);
        Assert.AreEqual(2, counter);
        Assert.AreEqual(1, nestLevel);
        cp.Update(0.1f);
        Assert.AreEqual(2, counter);
        Assert.AreEqual(0, nestLevel);
    }

    [Test]
    public void StartCoroutine_Nested2() {
        Assert.AreEqual(0, counter);
        cp.StartCoroutine(NestCoro(1));
        cp.Update(0);
        Assert.AreEqual(0, counter);
        Assert.AreEqual(1, nestLevel);
        cp.Update(0.1f);
        Assert.AreEqual(0, counter);
        Assert.AreEqual(2, nestLevel);
        cp.Update(0.2f);
        Assert.AreEqual(1, counter);
        Assert.AreEqual(2, nestLevel);
        cp.Update(0.3f);
        Assert.AreEqual(2, counter);
        Assert.AreEqual(2, nestLevel);
        cp.Update(0.4f);
        Assert.AreEqual(2, counter);
        Assert.AreEqual(1, nestLevel);
        cp.Update(0.5f);
        Assert.AreEqual(2, counter);
        Assert.AreEqual(0, nestLevel);
    }

    [Test]
    public void StartCoroutine_Exception() {
        Assert.AreEqual(0, counter);
        cp.StartCoroutine(ExceptCoro());
        cp.StartCoroutine(TestCoro());
        cp.Update(0);
        Assert.AreEqual(2, counter);
        cp.Update(0.1f);
        Assert.AreEqual(3, counter);
    }

    [Test]
    public void StartCoroutine_Parallel() {
        Assert.AreEqual(0, counter);
        cp.StartCoroutine(TestCoro());
        cp.StartCoroutine(TestCoro());
        cp.Update(0);
        Assert.AreEqual(2, counter);
        cp.Update(0.1f);
        Assert.AreEqual(4, counter);
    }

    [Test]
    public void StartCoroutine_NestedParallel() {
        Assert.AreEqual(0, counter);
        cp.StartCoroutine(NestCoro(0));
        cp.Update(0);
        cp.StartCoroutine(TestCoro());
        Assert.AreEqual(0, counter);
        Assert.AreEqual(1, nestLevel);
        cp.Update(0.1f);
        Assert.AreEqual(2, counter);
        Assert.AreEqual(1, nestLevel);
        cp.Update(0.2f);
        Assert.AreEqual(4, counter);
        Assert.AreEqual(1, nestLevel);
        cp.Update(0.1f);
        Assert.AreEqual(4, counter);
        Assert.AreEqual(0, nestLevel);
    }
}
