using NUnit.Framework;
using System.Collections.Generic;
using DarkConfig.Internal;

[TestFixture]
class FileMatchTests {
    static readonly List<string> AllFiles = new List<string>{
        "Folder/1File",
        "Folder/2File",
        "Folder/3File",
        "Folder/Thumbs",
        "Uggabo",
        "Buggabo",
        "Parent/Child/Grandchild/a",
        "Parent/Child/Grandchild/b",
        "Parent/Child/Grandchild/c",
    };

    [Test]
    public void MatchStar() {
        Assert.AreEqual(RegexUtils.FilterMatchingGlob("Folder/*", AllFiles),
            new List<string>{"Folder/1File", "Folder/2File", "Folder/3File", "Folder/Thumbs"});
    }

    [Test]
    public void MatchStarPosfix() {
        Assert.AreEqual(RegexUtils.FilterMatchingGlob("Folder/*File", AllFiles),
            new List<string>{"Folder/1File", "Folder/2File", "Folder/3File"});
    }

    [Test]
    public void MatchQuestionMark() {
        Assert.AreEqual(RegexUtils.FilterMatchingGlob("Folder/?File", AllFiles),
            new List<string>{"Folder/1File", "Folder/2File", "Folder/3File"});
    }

    [Test]
    public void MatchStarOnePathOnly() {
        Assert.AreEqual(RegexUtils.FilterMatchingGlob("*", AllFiles),
            new List<string>{"Uggabo", "Buggabo"});
    }

    [Test]
    public void MatchDoubleStar() {
        Assert.AreEqual(RegexUtils.FilterMatchingGlob("Parent/**", AllFiles),
            new List<string>{"Parent/Child/Grandchild/a", "Parent/Child/Grandchild/b", "Parent/Child/Grandchild/c"});
    }

    [Test]
    public void MatchDoubleStarCapped() {
        Assert.AreEqual(RegexUtils.FilterMatchingGlob("Parent/**/b", AllFiles),
            new List<string>{"Parent/Child/Grandchild/b"});
    }
}