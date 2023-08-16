using NUnit.Framework;
using System.Collections.Generic;
using DarkConfig.Internal;

[TestFixture]
class GlobMatchTests {
    static readonly List<string> AllFiles = new List<string> {
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
        var matchingFiles = new List<string>();
        RegexUtils.FilterMatchingGlob("Folder/*", AllFiles, matchingFiles);
        Assert.That(matchingFiles, Is.EqualTo(new List<string> {"Folder/1File", "Folder/2File", "Folder/3File", "Folder/Thumbs"}));
    }

    [Test]
    public void MatchStarPostfix() {
        var matchingFiles = new List<string>();
        RegexUtils.FilterMatchingGlob("Folder/*File", AllFiles, matchingFiles);
        Assert.That(matchingFiles, Is.EqualTo(new List<string> {"Folder/1File", "Folder/2File", "Folder/3File"}));
    }

    [Test]
    public void MatchQuestionMark() {
        var matchingFiles = new List<string>();
        RegexUtils.FilterMatchingGlob("Folder/?File", AllFiles, matchingFiles);
        Assert.That(matchingFiles, Is.EqualTo(new List<string> {"Folder/1File", "Folder/2File", "Folder/3File"}));
    }

    [Test]
    public void MatchStarOnePathOnly() {
        var matchingFiles = new List<string>();
        RegexUtils.FilterMatchingGlob("*", AllFiles, matchingFiles);
        Assert.That(matchingFiles, Is.EqualTo(new List<string> {"Uggabo", "Buggabo"}));
    }

    [Test]
    public void MatchDoubleStar() {
        var matchingFiles = new List<string>();
        RegexUtils.FilterMatchingGlob("Parent/**", AllFiles, matchingFiles);
        Assert.That(matchingFiles, Is.EqualTo(new List<string> {"Parent/Child/Grandchild/a", "Parent/Child/Grandchild/b", "Parent/Child/Grandchild/c"}));
    }

    [Test]
    public void MatchDoubleStarCapped() {
        var matchingFiles = new List<string>();
        RegexUtils.FilterMatchingGlob("Parent/**/b", AllFiles, matchingFiles);
        Assert.That(matchingFiles, Is.EqualTo(new List<string> {"Parent/Child/Grandchild/b"}));
    }
}
