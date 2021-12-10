using DarkConfig;
using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
class FileMatchTests {
    static List<string> AllFiles = new List<string>(new string[] {
        "Folder/1File",
        "Folder/2File",
        "Folder/3File",
        "Folder/Thumbs",
        "Uggabo",
        "Buggabo",
        "Parent/Child/Grandchild/a",
        "Parent/Child/Grandchild/b",
        "Parent/Child/Grandchild/c",
    });

    void AssertSeq(List<string> a, string[] b) {
        Assert.AreEqual(new List<string>(b), a);
    }

    [Test]
    public void MatchStar() {
        AssertSeq(Config.FileManager.GetFilesByGlobImpl("Folder/*", AllFiles),
            new string[] {"Folder/1File", "Folder/2File", "Folder/3File", "Folder/Thumbs"});
    }

    [Test]
    public void MatchStarPosfix() {
        AssertSeq(Config.FileManager.GetFilesByGlobImpl("Folder/*File", AllFiles),
            new string[] {"Folder/1File", "Folder/2File", "Folder/3File"});
    }

    [Test]
    public void MatchQuestionMark() {
        AssertSeq(Config.FileManager.GetFilesByGlobImpl("Folder/?File", AllFiles),
            new string[] {"Folder/1File", "Folder/2File", "Folder/3File"});
    }

    [Test]
    public void MatchStarOnePathOnly() {
        AssertSeq(Config.FileManager.GetFilesByGlobImpl("*", AllFiles),
            new string[] {"Uggabo", "Buggabo"});
    }

    [Test]
    public void MatchDoubleStar() {
        AssertSeq(Config.FileManager.GetFilesByGlobImpl("Parent/**", AllFiles),
            new string[] {"Parent/Child/Grandchild/a", "Parent/Child/Grandchild/b", "Parent/Child/Grandchild/c"});
    }

    [Test]
    public void MatchDoubleStarCapped() {
        AssertSeq(Config.FileManager.GetFilesByGlobImpl("Parent/**/b", AllFiles),
            new string[] {"Parent/Child/Grandchild/b"});
    }
}