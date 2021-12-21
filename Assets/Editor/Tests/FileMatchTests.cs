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
        AssertSeq(DarkConfig.Internal.RegexUtils.FilterMatchingGlob("Folder/*", AllFiles),
            new string[] {"Folder/1File", "Folder/2File", "Folder/3File", "Folder/Thumbs"});
    }

    [Test]
    public void MatchStarPosfix() {
        AssertSeq(DarkConfig.Internal.RegexUtils.FilterMatchingGlob("Folder/*File", AllFiles),
            new string[] {"Folder/1File", "Folder/2File", "Folder/3File"});
    }

    [Test]
    public void MatchQuestionMark() {
        AssertSeq(DarkConfig.Internal.RegexUtils.FilterMatchingGlob("Folder/?File", AllFiles),
            new string[] {"Folder/1File", "Folder/2File", "Folder/3File"});
    }

    [Test]
    public void MatchStarOnePathOnly() {
        AssertSeq(DarkConfig.Internal.RegexUtils.FilterMatchingGlob("*", AllFiles),
            new string[] {"Uggabo", "Buggabo"});
    }

    [Test]
    public void MatchDoubleStar() {
        AssertSeq(DarkConfig.Internal.RegexUtils.FilterMatchingGlob("Parent/**", AllFiles),
            new string[] {"Parent/Child/Grandchild/a", "Parent/Child/Grandchild/b", "Parent/Child/Grandchild/c"});
    }

    [Test]
    public void MatchDoubleStarCapped() {
        AssertSeq(DarkConfig.Internal.RegexUtils.FilterMatchingGlob("Parent/**/b", AllFiles),
            new string[] {"Parent/Child/Grandchild/b"});
    }
}