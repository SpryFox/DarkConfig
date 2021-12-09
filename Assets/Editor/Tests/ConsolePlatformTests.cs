using NUnit.Framework;
using DarkConfig;
using System.Collections.Generic;
using System.Collections;

[TestFixture]
class ConsolePlatformTests {
    ConsolePlatform cp;
    int counter = 0;
    int nestLevel = 0;

    IEnumerator coroPasser;

    [SetUp]
    public void DoSetup() {
        cp = new ConsolePlatform();
        counter = 0;
        nestLevel = 0;
        coroPasser = null;
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
        if (nestAmount > 0) {
            var tmp = NestCoro(nestAmount - 1);
            coroPasser = tmp;
            yield return cp.StartCoroutine(tmp);
        } else {
            var tmp = TestCoro();
            coroPasser = tmp;
            yield return cp.StartCoroutine(tmp);
        }

        nestLevel--;
    }

    IEnumerator ExceptCoro() {
        counter++;
        yield return null;
        throw new System.ArgumentException("Arg");
    }

    IEnumerator SelfStopper() {
        counter++;
        cp.StopCoroutine(coroPasser);
        yield return null;
        counter++;
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
        cp.Update(0.3f);
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

    [Test]
    public void StopCoroutine_BeforeUpdate() {
        Assert.AreEqual(0, counter);
        var x = TestCoro();
        cp.StartCoroutine(x);
        Assert.AreEqual(0, counter);
        cp.StopCoroutine(x);
        cp.Update(0);
        Assert.AreEqual(0, counter);
        cp.Update(0.1f);
        Assert.AreEqual(0, counter);
    }

    [Test]
    public void StopCoroutine_AfterUpdate() {
        Assert.AreEqual(0, counter);
        var x = TestCoro();
        cp.StartCoroutine(x);
        Assert.AreEqual(0, counter);
        cp.Update(0);
        cp.StopCoroutine(x);
        Assert.AreEqual(1, counter);
        cp.Update(0.1f);
        Assert.AreEqual(1, counter);
    }

    [Test]
    public void StopCoroutine_Null() {
        Assert.AreEqual(0, counter);
        var x = TestCoro();
        cp.StartCoroutine(x);
        Assert.AreEqual(0, counter);
        cp.Update(0);
        cp.StopCoroutine(null);
        Assert.AreEqual(1, counter);
        cp.Update(0.1f);
        Assert.AreEqual(2, counter);
    }

    [Test]
    public void StopCoroutine_AfterDone() {
        Assert.AreEqual(0, counter);
        var x = TestCoro();
        cp.StartCoroutine(x);
        cp.Update(0);
        cp.Update(0.1f);
        Assert.AreEqual(2, counter);
        cp.StopCoroutine(x);
        cp.Update(0.2f);
        Assert.AreEqual(2, counter);
    }

    [Test]
    public void StopCoroutine_Nested_ChildrenDie() {
        Assert.AreEqual(0, counter);
        var x = NestCoro(0);
        cp.StartCoroutine(x);
        cp.Update(0);
        Assert.AreEqual(0, counter);
        Assert.AreEqual(1, nestLevel);
        cp.Update(0.1f);
        Assert.AreEqual(1, counter);
        Assert.AreEqual(1, nestLevel);

        cp.StopCoroutine(x);

        cp.Update(0.2f);
        Assert.AreEqual(1, counter);
        Assert.AreEqual(1, nestLevel);
        cp.Update(0.3f);
        Assert.AreEqual(1, counter);
        Assert.AreEqual(1, nestLevel);
    }


    [Test]
    public void StopCoroutine_Nested_ParentReplaces() {
        Assert.AreEqual(0, counter);
        cp.StartCoroutine(NestCoro(0));
        cp.Update(0);
        Assert.AreEqual(0, counter);
        Assert.AreEqual(1, nestLevel);

        cp.StopCoroutine(coroPasser);
        cp.Update(0.2f);

        Assert.AreEqual(0, counter);
        Assert.AreEqual(1, nestLevel);

        cp.Update(0.3f);
        Assert.AreEqual(0, counter);
        Assert.AreEqual(0, nestLevel);
    }

    [Test]
    public void StopCoroutine_Nested_SelfStops() {
        coroPasser = SelfStopper();
        cp.StartCoroutine(coroPasser);
        Assert.AreEqual(0, counter);
        cp.Update(0);
        Assert.AreEqual(1, counter);
        cp.Update(0.1f);
        Assert.AreEqual(1, counter);
    }
}