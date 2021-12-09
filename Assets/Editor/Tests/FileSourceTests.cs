﻿using UnityEngine;
using DarkConfig;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;


[TestFixture]
class FileSourceFacts {
    string m_tmpDir;


    [SetUp]
    public void SetUp() {
        m_tmpDir = Path.Combine(Path.GetTempPath(), "FileSourceFacts");
        Directory.CreateDirectory(m_tmpDir);
    }

    [TearDown]
    public void TearDown() {
        Directory.Delete(m_tmpDir, true);
    }

    void CreateFile(string filename, string contents) {
        using(var sw = new StreamWriter(Path.Combine(m_tmpDir, filename), false, new System.Text.UTF8Encoding())) {
            sw.Write(contents);
        }
    }

    void DeleteFile(string filename) {
        File.Delete(Path.Combine(m_tmpDir, filename));
    }


    [Test]
    public void CanLoadIndex() {
        CreateFile("index.bytes", "");
        var fs = new FileSource(m_tmpDir, true);
        Assert.True(fs.CanLoadNow(), m_tmpDir);
    }

    [Test]
    public void PreloadCallsCallback() {
        CreateFile("derp.bytes", "key: value");
        CreateFile("index.bytes", "- derp");

        var fs = new FileSource(m_tmpDir, true);
        bool calledCallback = false;
        fs.Preload(() => { calledCallback = true; });
        Assert.True(calledCallback);
    }

    [Test]
    public void OpenOneFile() {
        CreateFile("derp.bytes", "key: value");
        CreateFile("index.bytes", "- derp");

        var fs = new FileSource(m_tmpDir, true);
        fs.Preload(() => {});
        var files = fs.GetFiles();
        Assert.AreEqual(2, files.Count);
        Assert.AreEqual("index", files[0].Name);
        Assert.AreEqual("derp", files[1].Name);
        Assert.AreEqual("value", files[1].Parsed["key"].StringValue);
    }

    [Test]
    public void ReceivePreloaded() {
        CreateFile("derp.bytes", "key: value");
        CreateFile("index.bytes", "- derp");

        var fs = new FileSource(m_tmpDir, true);
        fs.Preload(() => {});

        var fs2 = new FileSource(m_tmpDir, true);
        fs2.ReceivePreloaded(fs.GetFiles());
        var files = fs2.GetFiles();
        Assert.AreEqual(2, files.Count);
        Assert.AreEqual("index", files[0].Name);
        Assert.AreEqual("derp", files[1].Name);
    }


    [Test]
    public void Hotload_ExistingFile() {
        CreateFile("derp.bytes", "key: value");
        CreateFile("index.bytes", "- derp");

        var fs = new FileSource(m_tmpDir, true);
        fs.Preload(() => {});

        CreateFile("derp.bytes", "key: value2");
        var fi = fs.GetFiles()[1];
        var fi2 = fs.TryHotload(fi);
        Assert.IsNotNull(fi2);
        Assert.AreEqual("derp", fi2.Name);
        Assert.AreEqual("value2", fi2.Parsed["key"].StringValue);
    }

    [Test]
    public void Hotload_DeletedFile() {
        CreateFile("derp.bytes", "key: value");
        CreateFile("durr.bytes", "a: b");
        CreateFile("index.bytes", "- derp\n- durr");

        var fs = new FileSource(m_tmpDir, true);
        fs.Preload(() => {});

        CreateFile("index.bytes", "- derp");
        DeleteFile("durr.bytes");
        var fi = fs.GetFiles()[2];
        var fi2 = fs.TryHotload(fi);
        Assert.IsNull(fi2);
    }

    [Test]
    public void Hotload_CreatedFile() {
        CreateFile("derp.bytes", "key: value");
        CreateFile("index.bytes", "- derp");

        var fs = new FileSource(m_tmpDir, true);
        fs.Preload(() => {});

        CreateFile("durr.bytes", "a: b");
        CreateFile("index.bytes", "- derp\n- durr");

        var index = fs.GetFiles()[0];
        var fi = fs.TryHotload(index);
        Assert.AreEqual(2, fi.Parsed.Count);

        var files = fs.GetFiles();
        Assert.AreEqual(3, files.Count);
        Assert.AreEqual("durr", files[2].Name);
        Assert.AreEqual("b", files[2].Parsed["a"].StringValue);
    }

    [Test]
    public void Hotload_CreatedThreeFiles() {
        CreateFile("derp.bytes", "key: value");
        CreateFile("index.bytes", "- derp");

        var fs = new FileSource(m_tmpDir, true);
        fs.Preload(() => {});

        CreateFile("durr.bytes", "a: b");
        CreateFile("hurr.bytes", "x: y");
        CreateFile("err.bytes", "u: v");
        CreateFile("index.bytes", "- derp\n- durr\n- hurr\n- err");

        var index = fs.GetFiles()[0];
        var fi = fs.TryHotload(index);
        Assert.AreEqual(4, fi.Parsed.Count);

        var files = fs.GetFiles();
        Assert.AreEqual(5, files.Count);
        Assert.AreEqual("durr", files[2].Name);
        Assert.AreEqual("hurr", files[3].Name);
        Assert.AreEqual("err", files[4].Name);
        Assert.AreEqual("b", files[2].Parsed["a"].StringValue);
        Assert.AreEqual("y", files[3].Parsed["x"].StringValue);
        Assert.AreEqual("v", files[4].Parsed["u"].StringValue);
    }

    [Test]
    public void Hotload_CreatedFileTwice() {
        CreateFile("derp.bytes", "key: value");
        CreateFile("index.bytes", "- derp");

        var fs = new FileSource(m_tmpDir, true);
        fs.Preload(() => {});

        CreateFile("durr.bytes", "a: b");
        CreateFile("index.bytes", "- derp\n- durr");

        var index = fs.GetFiles()[0];
        fs.TryHotload(index);
        
        CreateFile("hurr.bytes", "x: y");
        CreateFile("index.bytes", "- derp\n- durr\n- hurr");

        fs.TryHotload(index);

        var files = fs.GetFiles();
        Assert.AreEqual(4, files.Count);
        Assert.AreEqual("durr", files[2].Name);
        Assert.AreEqual("b", files[2].Parsed["a"].StringValue);
        Assert.AreEqual("hurr", files[3].Name);
        Assert.AreEqual("y", files[3].Parsed["x"].StringValue);
    }
}
