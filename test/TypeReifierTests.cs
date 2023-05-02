using System;
using NUnit.Framework;
using DarkConfig;
using DarkConfig.Internal;
using System.Collections.Generic;

class TestTypes {
    protected enum TestEnum {
        Primi,
        Secondi
    }

    protected class TestClass {
        // scalars
        public bool boolKeyDefaultFalse = false;
        public bool boolKeyDefaultTrue = true;
        public string stringKey = "wrong";
        public int intKey = -1;
        public float floatKey = -1f;
        public double doubleKey = -1.0;
        public byte byteKey = 0;
        public TestEnum enumKey = TestEnum.Primi;
        public int? nullableIntKey = null;
        public ChildStruct? nullableChildStructKey = null;

        // collections of primitives
        public List<int> listIntKey = null;
        public int[] arrayIntKey = null;
        public float[,] array2dFloatKey = null;
        public float[,,] array3dFloatKey = null;

        // static variables should work as well
        public static string staticStringKey = null;
        public static int[] staticIntArrKey = null;
    }

    protected struct ChildStruct {
        public int childIntKey;
        public float childFloatKey;

        public static int staticIntKey = 0;
    }

    protected class ParentClass {
#pragma warning disable 649
        public TestClass nestedObject;
        public List<TestClass> nestedList;
        public Dictionary<string, TestClass> nestedDict;
        public Dictionary<string, ChildStruct> nestedStructDict;
        public ChildStruct nestedStruct;
#pragma warning restore 649
    }

    protected static class PureStatic {
        public static List<string> staticStringList = null;
    }

    protected class SingleFieldClass {
        public int SingleField = 0;
    }

    protected class SingleListClass {
        public List<string> SingleList = null;
    }

    protected class AttributesClass {
        [ConfigMandatory] public int Mandatory = -1;

        [ConfigAllowMissing] public string AllowedMissing = "initial";

        [ConfigIgnore] public bool Ignored = false;

        public string MissingOrNotDependingOnDefault = "initial2";
    }

    [ConfigMandatory]
    protected class MandatoryClass {
        public int intField = -1;

        [ConfigAllowMissing] public string stringField = "initial";

        [ConfigIgnore] public string ignoreField = "initialignore";
    }

    [ConfigAllowMissing]
    protected class AllowMissingClass {
        [ConfigMandatory] public string stringField = "init";

        public List<string> listField = null;
    }

    protected class PropertiesClass {
        [ConfigIgnore]
        public int backing3Int = 3;

        // Normal auto-property
        public int int1Value { get; set; } = 1;

        // Get-only auto-property
        public int int2Value { get; } = 2;

        // Set-only property
        public int int3Value { set => backing3Int = value; }

        // Computed property
        public int int4Value => 4;

        // Static property
        public static string staticStringValue { get; set; } = "static str";

        [ConfigIgnore]
        public string ignoredValue { get; set; }

        [ConfigAllowMissing]
        public string allowMissing { get; set; } = "missing";

        [ConfigMandatory]
        public string mandatoryValue { get; set; } = null;
    }

    protected ReificationOptions defaults;
    protected TypeReifier reifier;

    [SetUp]
    public void DoSetup() {
        reifier = new TypeReifier();
        defaults = Configs.Settings.DefaultReifierOptions;
        Configs.Settings.DefaultReifierOptions = ReificationOptions.AllowMissingExtraFields;
    }

    [TearDown]
    public void TearDown() {
        reifier = null;
        Configs.Settings.DefaultReifierOptions = defaults;
    }

    protected T ReifyString<T>(string str) where T : new() {
        var doc = Configs.ParseString(str, "TestFilename");
        T tc = default(T);
        Configs.Reify(ref tc, doc);
        return tc;
    }

    protected T UpdateFromString<T>(ref T obj, string str) {
        var doc = Configs.ParseString(str, "TestFilename");
        Configs.Reify(ref obj, doc);
        return obj;
    }
}

[TestFixture]
[TestOf(typeof(TypeReifier))]
class TypeReifierTests : TestTypes {
    [Test]
    public void SetsString() {
        var tc = ReifyString<TestClass>(@"---
            stringKey: right
            ");
        Assert.That(tc.stringKey, Is.EqualTo("right"));
    }

    [Test]
    public void SetsBoolToTrue() {
        var tc = ReifyString<TestClass>(@"---
            boolKeyDefaultFalse: true
            ");
        Assert.That(tc.boolKeyDefaultFalse, Is.True);
    }

    [Test]
    public void SetsBoolToTrueWithCapital() {
        var tc = ReifyString<TestClass>(@"---
            boolKeyDefaultFalse: True
            ");
        Assert.That(tc.boolKeyDefaultFalse, Is.True);
    }

    [Test]
    public void SetsBoolToFalse() {
        var tc = ReifyString<TestClass>(@"---
            boolKeyDefaultTrue: false
            ");
        Assert.That(tc.boolKeyDefaultTrue, Is.False);
    }

    [Test]
    public void SetsBoolToFalseWithCapital() {
        var tc = ReifyString<TestClass>(@"---
            boolKeyDefaultTrue: False
            ");
        Assert.That(tc.boolKeyDefaultTrue, Is.False);
    }

    [Test]
    public void SetsInt() {
        var tc = ReifyString<TestClass>(@"---
            intKey: 100
            ");
        Assert.That(tc.intKey, Is.EqualTo(100));
    }

    [Test]
    public void SetsFloat() {
        var tc = ReifyString<TestClass>(@"---
            floatKey: 1.56
            ");
        Assert.That(tc.floatKey, Is.EqualTo(1.56f));
    }

    [Test]
    [SetCulture("pt-BR")]
    [SetUICulture("pt-BR")]
    public void SetsFloatWithCultureInvariant() {
        var tc = ReifyString<TestClass>(@"---
            floatKey: 1.56
            ");
        Assert.That(tc.floatKey, Is.EqualTo(1.56f));
    }

    [Test]
    public void SetsDouble() {
        var tc = ReifyString<TestClass>(@"---
            doubleKey: 1.10101
            ");
        Assert.That(tc.doubleKey, Is.EqualTo(1.10101));
    }

    [Test]
    public void SetsByte() {
        var tc = ReifyString<TestClass>(@"---
            byteKey: 55
            ");
        Assert.That(tc.byteKey, Is.EqualTo((byte) 55));
    }

    [Test]
    public void SetsEnum() {
        var tc = ReifyString<TestClass>(@"---
            enumKey: Secondi
            ");
        Assert.That(tc.enumKey, Is.EqualTo(TestEnum.Secondi));
    }

    [Test]
    public void SetsNullable() {
        var tc = ReifyString<TestClass>(@"---
            nullableIntKey: 194
            ");
        Assert.Multiple(() => {
            Assert.That(tc.nullableIntKey.HasValue, Is.True);
            Assert.That(tc.nullableIntKey.Value, Is.EqualTo(194));
        });
    }

    [Test]
    public void SetsNullableToNull() {
        var tc = ReifyString<TestClass>(@"---
            nullableIntKey: null
            ");
        Assert.Multiple(() => {
            Assert.That(tc.nullableIntKey, Is.Null);
            Assert.That(tc.nullableIntKey.HasValue, Is.False);
        });
    }

    [Test]
    public void SetsNullableStruct() {
        var tc = ReifyString<TestClass>(@"---
            nullableChildStructKey: { childIntKey: 4202 }
            ");
        Assert.Multiple(() => {
            Assert.That(tc.nullableChildStructKey.HasValue, Is.True);
            Assert.That(tc.nullableChildStructKey.Value.childIntKey, Is.EqualTo(4202));
        });
    }

    [Test]
    public void SetsNullableStructToNull() {
        var tc = ReifyString<TestClass>(@"---
            nullableChildStructKey: null
            ");
        Assert.Multiple(() => {
            Assert.That(tc.nullableChildStructKey, Is.Null);
            Assert.That(tc.nullableChildStructKey.HasValue, Is.False);
        });
    }

    [Test]
    public void SetsListOfInts() {
        var instance = ReifyString<TestClass>(@"---
            listIntKey: [0, 1, 2, 3, 4]
            ");
        Assert.That(instance.listIntKey, Is.EqualTo(new[] {0, 1, 2, 3, 4}));
    }

    [Test]
    public void SetsArrayOfInts() {
        var instance = ReifyString<TestClass>(@"---
            arrayIntKey: [0, 1, 2, 3, 4]
            ");
        Assert.That(instance.arrayIntKey, Is.EqualTo(new[] {0, 1, 2, 3, 4}));
    }

    [Test]
    public void ArrayUpdatesInPlace() {
        var array = new[] {
            new TestClass {intKey = 62, floatKey = 4.56f},
            new TestClass {intKey = 1234, floatKey = 8.98f}
        };
        var saved = array[0];
        UpdateFromString(ref array, @"---
            - intKey: 4404
            - floatKey: 5505
            ");
        Assert.Multiple(() => {
            Assert.That(array, Has.Length.EqualTo(2));
            Assert.That(ReferenceEquals(array[0], saved), Is.True);
            Assert.That(array[0].intKey, Is.EqualTo(4404));
            Assert.That(array[0].floatKey, Is.EqualTo(4.56f));
            Assert.That(array[1].intKey, Is.EqualTo(1234));
            Assert.That(array[1].floatKey, Is.EqualTo(5505f));
        });
    }

    [Test]
    public void ArrayUpdatesInPlaceWhenAddingItems() {
        var array = new[] {
            new TestClass {intKey = 62, floatKey = 4.56f},
            new TestClass {intKey = 1234, floatKey = 8.98f}
        };
        var saved = array[0];
        UpdateFromString(ref array, @"---
            - intKey: 4404
            - floatKey: 5505
            - intKey: 1011
            ");
        Assert.Multiple(() => {
            Assert.That(array, Has.Length.EqualTo(3));
            Assert.That(ReferenceEquals(array[0], saved), Is.True);
            Assert.That(array[0].intKey, Is.EqualTo(4404));
            Assert.That(array[0].floatKey, Is.EqualTo(4.56f));
            Assert.That(array[1].intKey, Is.EqualTo(1234));
            Assert.That(array[1].floatKey, Is.EqualTo(5505f));
            Assert.That(array[2].intKey, Is.EqualTo(1011));
            Assert.That(array[2].floatKey, Is.EqualTo(-1f));
        });
    }

    [Test]
    public void ArrayUpdatesInPlaceWhenRemovingItems() {
        var array = new[] {
            new TestClass {intKey = 62, floatKey = 4.56f},
            new TestClass {intKey = 1234, floatKey = 8.98f},
            new TestClass {intKey = 392, floatKey = 44.55f}
        };
        var saved = array[0];
        UpdateFromString(ref array, @"---
            - intKey: 4404
            ");
        Assert.Multiple(() => {
            Assert.That(array, Has.Length.EqualTo(1));
            Assert.That(ReferenceEquals(array[0], saved), Is.True);
            Assert.That(array[0].intKey, Is.EqualTo(4404));
            Assert.That(array[0].floatKey, Is.EqualTo(4.56f));
        });
    }

    [Test]
    public void SetsArray2DOfFloats() {
        var instance = ReifyString<TestClass>(@"---
            array2dFloatKey:
                - [9, 8, 7]
                - [1, 2, 3]
            ");
        Assert.Multiple(() => {
            Assert.That(instance.array2dFloatKey[0, 0], Is.EqualTo(9f));
            Assert.That(instance.array2dFloatKey[0, 1], Is.EqualTo(8f));
            Assert.That(instance.array2dFloatKey[0, 2], Is.EqualTo(7f));
            Assert.That(instance.array2dFloatKey[1, 0], Is.EqualTo(1f));
            Assert.That(instance.array2dFloatKey[1, 1], Is.EqualTo(2f));
            Assert.That(instance.array2dFloatKey[1, 2], Is.EqualTo(3f));
        });
    }

    [Test]
    public void SetsArray3DOfFloats() {
        var instance = ReifyString<TestClass>(@"---
            array3dFloatKey: [
                [
                    [1, 2, 3],
                    [4, 5, 6]
                ],
                [
                    [7, 8, 9],
                    [10, 11, 12]
                ]
            ]");
        Assert.Multiple(() => {
            Assert.That(instance.array3dFloatKey[0, 0, 0], Is.EqualTo(1f));
            Assert.That(instance.array3dFloatKey[0, 0, 1], Is.EqualTo(2f));
            Assert.That(instance.array3dFloatKey[0, 0, 2], Is.EqualTo(3f));
            Assert.That(instance.array3dFloatKey[0, 1, 0], Is.EqualTo(4f));
            Assert.That(instance.array3dFloatKey[0, 1, 1], Is.EqualTo(5f));
            Assert.That(instance.array3dFloatKey[0, 1, 2], Is.EqualTo(6f));
            Assert.That(instance.array3dFloatKey[1, 0, 0], Is.EqualTo(7f));
            Assert.That(instance.array3dFloatKey[1, 0, 1], Is.EqualTo(8f));
            Assert.That(instance.array3dFloatKey[1, 0, 2], Is.EqualTo(9f));
            Assert.That(instance.array3dFloatKey[1, 1, 0], Is.EqualTo(10f));
            Assert.That(instance.array3dFloatKey[1, 1, 1], Is.EqualTo(11f));
            Assert.That(instance.array3dFloatKey[1, 1, 2], Is.EqualTo(12f));
        });
    }

    [Test]
    public void Array2DUpdatesInPlace() {
        var array = new TestClass[1, 2];
        array[0, 0] = new TestClass {intKey = 62, floatKey = 4.56f};
        array[0, 1] = new TestClass {intKey = 1234, floatKey = 8.98f};

        var saved = array[0, 0];

        UpdateFromString(ref array, @"---
            - - intKey: 4404
              - floatKey: 5505
            ");
        Assert.Multiple(() => {
            Assert.That(array.GetLength(0), Is.EqualTo(1));
            Assert.That(array.GetLength(1), Is.EqualTo(2));

            Assert.That(ReferenceEquals(array[0, 0], saved), Is.True);
            Assert.That(array[0, 0].intKey, Is.EqualTo(4404));
            Assert.That(array[0, 0].floatKey, Is.EqualTo(4.56f));
            Assert.That(array[0, 1].intKey, Is.EqualTo(1234));
            Assert.That(array[0, 1].floatKey, Is.EqualTo(5505f));
        });
    }

    [Test]
    public void Array2DUpdatesInPlaceWhenAddingItems() {
        var array = new TestClass[1, 2];
        array[0, 0] = new TestClass {intKey = 62, floatKey = 4.56f};
        array[0, 1] = new TestClass {intKey = 1234, floatKey = 8.98f};

        var saved = array[0, 0];
        UpdateFromString(ref array, @"---
            - - intKey: 4404
              - floatKey: 5505
              - intKey: 1011
            ");
        Assert.Multiple(() => {
            Assert.That(array.GetLength(0), Is.EqualTo(1));
            Assert.That(array.GetLength(1), Is.EqualTo(3));
            Assert.That(ReferenceEquals(array[0, 0], saved), Is.True);
            Assert.That(array[0, 0].intKey, Is.EqualTo(4404));
            Assert.That(array[0, 0].floatKey, Is.EqualTo(4.56f));
            Assert.That(array[0, 1].intKey, Is.EqualTo(1234));
            Assert.That(array[0, 1].floatKey, Is.EqualTo(5505f));
            Assert.That(array[0, 2].intKey, Is.EqualTo(1011));
            Assert.That(array[0, 2].floatKey, Is.EqualTo(-1f));
        });
    }

    [Test]
    public void Array2DUpdatesInPlaceWhenRemovingItems() {
        var array = new TestClass[1, 3];
        array[0, 0] = new TestClass {intKey = 62, floatKey = 4.56f};
        array[0, 1] = new TestClass {intKey = 1234, floatKey = 8.98f};
        array[0, 2] = new TestClass {intKey = 392, floatKey = 44.55f};

        var saved = array[0, 0];
        UpdateFromString(ref array, @"---
            - - intKey: 4404
            ");
        Assert.Multiple(() => {
            Assert.That(array.GetLength(0), Is.EqualTo(1));
            Assert.That(array.GetLength(1), Is.EqualTo(1));
            Assert.That(ReferenceEquals(array[0, 0], saved), Is.True);
            Assert.That(array[0, 0].intKey, Is.EqualTo(4404));
            Assert.That(array[0, 0].floatKey, Is.EqualTo(4.56f));
        });
    }

    [Test]
    public void SetsStruct() {
        var pc = ReifyString<ParentClass>(@"---
            nestedStruct:
                childIntKey: 1201                
            ");
        Assert.That(pc.nestedStruct.childIntKey, Is.EqualTo(1201));
    }

    [Test]
    public void CreatesNestedObject() {
        var pc = ReifyString<ParentClass>(@"---
            nestedObject:
                intKey: 41
            ");
        Assert.That(pc.nestedObject.intKey, Is.EqualTo(41));
    }

    [Test]
    public void CreatesNestedListObjects() {
        var pc = ReifyString<ParentClass>(@"---
            nestedList:
                - intKey: 35
                - intKey: 23
                - intKey: 65
            ");
        Assert.Multiple(() => {
            Assert.That(pc.nestedList.Count, Is.EqualTo(3));
            Assert.That(pc.nestedList[0].intKey, Is.EqualTo(35));
            Assert.That(pc.nestedList[1].intKey, Is.EqualTo(23));
            Assert.That(pc.nestedList[2].intKey, Is.EqualTo(65));
        });
    }

    [Test]
    public void NestedObjectUpdateDoesntCreateNewObject() {
        var o = new ParentClass {
            nestedObject = new TestClass()
        };
        var saved = o.nestedObject;
        UpdateFromString(ref o, @"---
            nestedObject:
                intKey: 100
            ");
        Assert.Multiple(() => {
            Assert.That(ReferenceEquals(o.nestedObject, saved), Is.True);
            Assert.That(o.nestedObject.intKey, Is.EqualTo(100));
        });
    }

    [Test]
    public void NestedObjectUpdateDoesntClobberStruct() {
        var o = new ParentClass();
        o.nestedStruct.childIntKey = 10;
        o.nestedStruct.childFloatKey = 10;
        UpdateFromString(ref o, @"---
            nestedStruct:
                childIntKey: 99
            ");
        Assert.Multiple(() => {
            Assert.That(o.nestedStruct.childIntKey, Is.EqualTo(99));
            Assert.That(o.nestedStruct.childFloatKey, Is.EqualTo(10f));
        });
    }

    [Test]
    public void NestedListUpdatesInPlace() {
        var o = new ParentClass {
            nestedList = new List<TestClass> {
                new TestClass {intKey = 78, floatKey = 1.2f}
            }
        };
        var saved = o.nestedList[0];
        UpdateFromString(ref o, @"---
            nestedList:
                - intKey: 4404
            ");
        Assert.Multiple(() => {
            Assert.That(o.nestedList, Has.Count.EqualTo(1));
            Assert.That(ReferenceEquals(o.nestedList[0], saved), Is.True);
            Assert.That(o.nestedList[0].intKey, Is.EqualTo(4404));
            Assert.That(o.nestedList[0].floatKey, Is.EqualTo(1.2f));
        });
    }

    [Test]
    public void NestedListUpdatesInPlaceWhenAddingItems() {
        var o = new ParentClass {
            nestedList = new List<TestClass> {
                new TestClass {intKey = 62, floatKey = 4.56f},
                new TestClass {intKey = 1234, floatKey = 8.98f}
            }
        };
        var saved = o.nestedList[0];
        UpdateFromString(ref o, @"---
            nestedList:
                - intKey: 4404
                - floatKey: 5505
                - intKey: 1011
            ");
        Assert.Multiple(() => {
            Assert.That(o.nestedList, Has.Count.EqualTo(3));
            Assert.That(ReferenceEquals(o.nestedList[0], saved), Is.True);
            Assert.That(o.nestedList[0].intKey, Is.EqualTo(4404));
            Assert.That(o.nestedList[0].floatKey, Is.EqualTo(4.56f));
            Assert.That(o.nestedList[1].intKey, Is.EqualTo(1234));
            Assert.That(o.nestedList[1].floatKey, Is.EqualTo(5505f));
            Assert.That(o.nestedList[2].intKey, Is.EqualTo(1011));
            Assert.That(o.nestedList[2].floatKey, Is.EqualTo(-1f));
        });
    }

    [Test]
    public void NestedListUpdatesInPlaceWhenRemovingItems() {
        var o = new ParentClass {
            nestedList = new List<TestClass> {
                new TestClass {intKey = 62, floatKey = 4.56f},
                new TestClass {intKey = 1234, floatKey = 8.98f},
                new TestClass {intKey = 392, floatKey = 44.55f}
            }
        };
        var saved = o.nestedList[0];
        UpdateFromString(ref o, @"---
            nestedList:
                - intKey: 4404
            ");
        Assert.Multiple(() => {
            Assert.That(o.nestedList, Has.Count.EqualTo(1));
            Assert.That(ReferenceEquals(o.nestedList[0], saved), Is.True);
            Assert.That(o.nestedList[0].intKey, Is.EqualTo(4404));
            Assert.That(o.nestedList[0].floatKey, Is.EqualTo(4.56f));
        });
    }

    [Test]
    public void NestedListUpdatesInPlaceWhenTheSameCount() {
        var o = new ParentClass {
            nestedList = new List<TestClass> {
                new TestClass {intKey = 62, floatKey = 4.56f},
                new TestClass {intKey = 1234, floatKey = 8.98f},
                new TestClass {intKey = 11, floatKey = 55.24f}
            }
        };
        UpdateFromString(ref o, @"---
            nestedList:
                - intKey: 503
                - floatKey: 66
                - intKey: 67
            ");
        Assert.Multiple(() => {
            Assert.That(o.nestedList, Has.Count.EqualTo(3));
            Assert.That(o.nestedList[0].intKey, Is.EqualTo(503));
            Assert.That(o.nestedList[0].floatKey, Is.EqualTo(4.56f));
            Assert.That(o.nestedList[1].intKey, Is.EqualTo(1234));
            Assert.That(o.nestedList[1].floatKey, Is.EqualTo(66f));
            Assert.That(o.nestedList[2].intKey, Is.EqualTo(67));
            Assert.That(o.nestedList[2].floatKey, Is.EqualTo(55.24f));
        });
    }

    [Test]
    public void DictEnumKeys() {
        var o = new Dictionary<TestEnum, int>();
        UpdateFromString(ref o, @"---
            Primi: 1024
            Secondi: 999
        ");
        Assert.Multiple(() => {
            Assert.That(o, Has.Count.EqualTo(2));
            Assert.That(o[TestEnum.Primi], Is.EqualTo(1024));
            Assert.That(o[TestEnum.Secondi], Is.EqualTo(999));
        });
    }

    [Test]
    public void DictIntKeys() {
        var o = new Dictionary<int, int>();
        UpdateFromString(ref o, @"---
            101: 1024
            504: 999
        ");
        Assert.Multiple(() => {
            Assert.That(o, Has.Count.EqualTo(2));
            Assert.That(o[101], Is.EqualTo(1024));
            Assert.That(o[504], Is.EqualTo(999));
        });
    }

    [Test]
    public void DictEnumKeysAddsNewWithoutDeletingExisting() {
        var o = new Dictionary<TestEnum, TestClass>();
        var saved = new TestClass {intKey = 101};
        o[TestEnum.Primi] = saved;
        UpdateFromString(ref o, @"---
            Primi: { intKey: 99 }
            Secondi: { intKey: 12 }
        ");
        Assert.Multiple(() => {
            Assert.That(o, Has.Count.EqualTo(2));
            Assert.That(ReferenceEquals(saved, o[TestEnum.Primi]), Is.True);
            Assert.That(o[TestEnum.Primi].intKey, Is.EqualTo(99));
            Assert.That(o[TestEnum.Secondi].intKey, Is.EqualTo(12));
        });
    }

    [Test]
    public void DictEnumKeysRemovesMissingWithoutDeletingExisting() {
        var o = new Dictionary<TestEnum, TestClass>();
        var saved = new TestClass {intKey = 101};
        o[TestEnum.Primi] = saved;
        o[TestEnum.Secondi] = new TestClass {intKey = 1200};

        UpdateFromString(ref o, @"---
            Primi: { intKey: 99 }
        ");
        Assert.Multiple(() => {
            Assert.That(o, Has.Count.EqualTo(1));
            Assert.That(o.ContainsKey(TestEnum.Secondi), Is.False);
            Assert.That(ReferenceEquals(saved, o[TestEnum.Primi]), Is.True);
            Assert.That(o[TestEnum.Primi].intKey, Is.EqualTo(99));
        });
    }

    [Test]
    public void NestedDictUpdatesInPlace() {
        var o = new ParentClass {
            nestedDict = new Dictionary<string, TestClass> {
                ["dictKey"] = new TestClass {intKey = 22, floatKey = 4.56f}
            }
        };
        var saved = o.nestedDict["dictKey"];
        UpdateFromString(ref o, @"---
            nestedDict:
                dictKey:
                    intKey: 56
            ");
        Assert.Multiple(() => {
            Assert.That(o.nestedDict, Has.Count.EqualTo(1));
            Assert.That(ReferenceEquals(o.nestedDict["dictKey"], saved), Is.True);
            Assert.That(o.nestedDict["dictKey"].intKey, Is.EqualTo(56));
        });
    }

    [Test]
    public void NestedDictUpdatesInPlaceStructs() {
        var o = new ParentClass {
            nestedStructDict = new Dictionary<string, ChildStruct> {
                ["dictKey"] = new ChildStruct {childIntKey = 11, childFloatKey = 6.54f}
            }
        };
        UpdateFromString(ref o, @"---
            nestedStructDict:
                dictKey:
                    childIntKey: 110
            ");
        Assert.Multiple(() => {
            Assert.That(o.nestedStructDict, Has.Count.EqualTo(1));
            Assert.That(o.nestedStructDict["dictKey"].childIntKey, Is.EqualTo(110));
            Assert.That(o.nestedStructDict["dictKey"].childFloatKey, Is.EqualTo(6.54f));
        });
    }

    [Test]
    public void NestedDictAddsNewPairs() {
        var o = new ParentClass {
            nestedDict = new Dictionary<string, TestClass> {
                ["dictKey"] = new TestClass {intKey = 67, floatKey = 1.06f}
            }
        };
        UpdateFromString(ref o, @"---
            nestedDict:
                dictKey:
                    intKey: 42
                newKey:
                    intKey: 43
                    floatKey: 12
            ");
        Assert.Multiple(() => {
            Assert.That(o.nestedDict, Has.Count.EqualTo(2));
            Assert.That(o.nestedDict["dictKey"].intKey, Is.EqualTo(42));
            Assert.That(o.nestedDict["dictKey"].floatKey, Is.EqualTo(1.06f));
            Assert.That(o.nestedDict["newKey"].intKey, Is.EqualTo(43));
            Assert.That(o.nestedDict["newKey"].floatKey, Is.EqualTo(12f));
        });
    }

    [Test]
    public void NestedDictRemovesMissingPairs() {
        var o = new ParentClass {
            nestedDict = new Dictionary<string, TestClass> {
                ["dictKey"] = new TestClass {intKey = 200, floatKey = 10099.2f}
            }
        };
        UpdateFromString(ref o, @"---
            nestedDict:
                newKey:
                    intKey: 999
            ");
        Assert.Multiple(() => {
            Assert.That(o.nestedDict, Has.Count.EqualTo(1));
            Assert.That(o.nestedDict.ContainsKey("dictKey"), Is.False);
            Assert.That(o.nestedDict["newKey"].intKey, Is.EqualTo(999));
        });
    }

    [Test]
    public void SetFieldsOnObjectForAPlainObject() {
        var tc = new TestClass();
        var doc = Configs.ParseString(
            @"---
            intKey: 99088
            "
            , "TestFilename");
        reifier.SetFieldsOnObject(ref tc, doc);
        Assert.That(tc.intKey, Is.EqualTo(99088));
    }

    [Test]
    public void SetFieldsOnObjectForACastedObject() {
        object tc = (object) new TestClass();
        var doc = Configs.ParseString(
            @"---
            intKey: 99077
            "
            , "TestFilename");
        reifier.SetFieldsOnObject(ref tc, doc);
        Assert.That(((TestClass) tc).intKey, Is.EqualTo(99077));
    }

    [Test]
    public void SetFieldsOnStructForATemplatedStructCall() {
        var s = new ChildStruct {
            childIntKey = 1,
            childFloatKey = 1
        };
        var doc = Configs.ParseString(
            @"---
            childIntKey: 12345
            "
            , "TestFilename");
        reifier.SetFieldsOnStruct(ref s, doc);
        Assert.Multiple(() => {
            Assert.That(s.childIntKey, Is.EqualTo(12345));
            Assert.That(s.childFloatKey, Is.EqualTo(1));
        });
    }

    [Test]
    public void SetFieldsOnObjectForABoxedStructArgument() {
        var s = new ChildStruct {
            childIntKey = 1,
            childFloatKey = 1
        };
        var doc = Configs.ParseString(
            @"---
            childIntKey: 34567
            "
            , "TestFilename");
        object os = s;
        reifier.SetFieldsOnObject(ref os, doc);
        Assert.Multiple(() => {
            Assert.That(((ChildStruct) os).childIntKey, Is.EqualTo(34567));
            Assert.That(((ChildStruct) os).childFloatKey, Is.EqualTo(1));
        });
    }

    [Test]
    public void EmptyEnumThrowsParseException() {
        Assert.Throws<ParseException>(() => { ReifyString<TestClass>("enumKey: \"\""); });
    }

    [Test]
    public void BadIntThrowsParseException() {
        Assert.Throws<ParseException>(() => { ReifyString<TestClass>("intKey: incorrect"); });
    }

    [Test]
    public void BadBoolThrowsParseException() {
        Assert.Throws<ParseException>(() => { ReifyString<TestClass>("boolKeyDefaultFalse: incorrect"); });
    }

    [Test]
    public void EmptyDocReturnsDocNode() {
        var doc = Configs.ParseString("", "TestFilename");
        Assert.Multiple(() => {
            Assert.That(doc, Is.Not.Null);
            Assert.That(doc, Is.InstanceOf<DocNode>());
        });
    }

    [Test]
    public void EmptyDocStreamReturnsDocNode() {
        var doc = Configs.LoadDocFromStream(new System.IO.MemoryStream(), "EmptyDoc");
        Assert.Multiple(() => {
            Assert.That(doc, Is.Not.Null);
            Assert.That(doc, Is.InstanceOf<DocNode>());
        });
    }
}

[TestFixture]
class ReifiesStatic : TestTypes {
    [Test]
    public void Class() {
        var doc = Configs.ParseString(
            @"---
            staticStringKey: arbitrage
            staticIntArrKey: [4, 4, 0, 0]
            intKey: 10   # test non-static fields
            "
            , "TestFilename");
        Configs.ReifyStatic<TestClass>(doc);
        Assert.Multiple(() => {
            Assert.That(TestClass.staticStringKey, Is.EqualTo("arbitrage"));
            Assert.That(TestClass.staticIntArrKey, Is.EqualTo(new[] {4, 4, 0, 0}));
        });
    }

    [Test]
    public void IgnoresNonStaticFields() {
        var doc = Configs.ParseString(
            @"---
            intKey: 10   # try to bogusly set a non-static field
            "
            , "TestFilename");

        // passes if there are no exceptions
        Configs.ReifyStatic<TestClass>(doc);
    }

    [Test]
    public void Struct() {
        var doc = Configs.ParseString(
            @"---
            staticIntKey: 3049
            "
            , "TestFilename");
        Configs.ReifyStatic<ChildStruct>(doc);
        Assert.That(ChildStruct.staticIntKey, Is.EqualTo(3049));
    }

    [Test]
    public void StaticClass() {
        var doc = Configs.ParseString(
            @"---
            staticStringList: [herp, derp]
            "
            , "TestFilename");
        Configs.ReifyStatic(typeof(PureStatic), doc);
        Assert.Multiple(() => {
            Assert.That(PureStatic.staticStringList[0], Is.EqualTo("herp"));
            Assert.That(PureStatic.staticStringList[1], Is.EqualTo("derp"));
        });
    }
}

[TestFixture]
class ReifiesSingle : TestTypes {
    [Test]
    public void CreateSingleFieldClass() {
        var doc = Configs.ParseString(
            @"---
            8342
            "
            , "TestFilename");
        var inst = Activator.CreateInstance<SingleFieldClass>();
        reifier.SetFieldsOnObject(ref inst, doc);
        Assert.That(inst.SingleField, Is.EqualTo(8342));
    }

    [Test]
    public void CreateSingleListClass() {
        var doc = Configs.ParseString(
            @"---
            [a, b, c, d]
            "
            , "TestFilename");
        var inst = Activator.CreateInstance<SingleListClass>();
        reifier.SetFieldsOnObject(ref inst, doc);
        Assert.Multiple(() => {
            Assert.That(inst.SingleList[0], Is.EqualTo("a"));
            Assert.That(inst.SingleList[1], Is.EqualTo("b"));
            Assert.That(inst.SingleList[2], Is.EqualTo("c"));
            Assert.That(inst.SingleList[3], Is.EqualTo("d"));
        });
    }
}

[TestFixture]
class ReifiesExtraFields : TestTypes {
    [Test]
    public void NoException() {
        var doc = Configs.ParseString(@"---
            floatKey: 1.56
        ", "TestFilename");
        var inst = Activator.CreateInstance<TestClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.AllowMissingFields);
        Assert.That(inst.floatKey, Is.EqualTo(1.56f));
    }

    [Test]
    public void Raises() {
        var doc = Configs.ParseString(@"---
            floatKey: 1.56
            extraKey1: derp
            extraKey2: herp
        ", "TestFilename");
        var exception = Assert.Throws<ExtraFieldsException>(() => {
            var inst = Activator.CreateInstance<TestClass>();
            reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.CaseSensitive);
        });
        // check that it found all the extra keys
        Assert.Multiple(() => {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.Message.IndexOf("extraKey1", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(exception.Message.IndexOf("extraKey2", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
        });
    }
}

[TestFixture]
class ReifiesMissingFields : TestTypes {
    [Test]
    public void NoException() {
        var doc = Configs.ParseString(@"---
            childIntKey: 42
            childFloatKey: 1.25
            staticIntKey: 332
        ", "TestFilename");
        var inst = Activator.CreateInstance<ChildStruct>();
        reifier.SetFieldsOnStruct(ref inst, doc, ReificationOptions.None);
        Assert.Multiple(() => {
            Assert.That(inst.childIntKey, Is.EqualTo(42));
            Assert.That(inst.childFloatKey, Is.EqualTo(1.25));
            Assert.That(ChildStruct.staticIntKey, Is.EqualTo(332));
        });
    }

    [Test]
    public void Raises() {
        var doc = Configs.ParseString(@"---
            childIntKey: 32
        ", "TestFilename");
        var exception = Assert.Throws<MissingFieldsException>(() => {
            var inst = Activator.CreateInstance<ChildStruct>();
            reifier.SetFieldsOnStruct(ref inst, doc, ReificationOptions.CaseSensitive);
        });
        // check that it found all the missing keys
        Assert.Multiple(() => {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.Message.IndexOf("childFloatKey", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(exception.Message.IndexOf("staticIntKey", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
        });
    }
}

[TestFixture]
class ReifiesCaseInsensitive : TestTypes {
    [Test]
    public void RefiesCaseInsensitive() {
        var doc = Configs.ParseString(@"---
            childintkey: 32
            CHILDFLOATKEY: 11
            StaticiNtKey: 5
        ", "TestFilename");
        var inst = Activator.CreateInstance<ChildStruct>();
        reifier.SetFieldsOnStruct(ref inst, doc, ReificationOptions.None);
        Assert.Multiple(() => {
            Assert.That(inst.childIntKey, Is.EqualTo(32));
            Assert.That(inst.childFloatKey, Is.EqualTo(11));
            Assert.That(ChildStruct.staticIntKey, Is.EqualTo(5));
        });
    }

    [Test]
    public void RefiesCaseInsensitiveWithMissing() {
        var doc = Configs.ParseString(@"---
            childintkey: 32
        ", "TestFilename");
        var inst = Activator.CreateInstance<ChildStruct>();
        reifier.SetFieldsOnStruct(ref inst, doc, ReificationOptions.AllowMissingFields);
        Assert.That(inst.childIntKey, Is.EqualTo(32));
    }
}

[TestFixture]
class ReifierAttributes : TestTypes {
    [Test]
    public void MandatoryAllowsSetting() {
        var doc = Configs.ParseString(@"---
            Mandatory: 10
            AllowedMissing: derp
        ", "TestFilename");
        var inst = Activator.CreateInstance<AttributesClass>();
        reifier.SetFieldsOnObject(ref inst, doc);
        Assert.Multiple(() => {
            Assert.That(inst.Mandatory, Is.EqualTo(10));
            Assert.That(inst.AllowedMissing, Is.EqualTo("derp"));
            Assert.That(inst.Ignored, Is.False);
        });
    }

    [Test]
    public void MandatoryExceptsIfNotSet() {
        var doc = Configs.ParseString(@"---
            AllowedMissing: derp
        ", "TestFilename");
        Assert.Throws<MissingFieldsException>(() => {
            var inst = Activator.CreateInstance<AttributesClass>();
            reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.AllowMissingFields);
        });
    }

    [Test]
    public void AllowedMissingDoesNotThrowExceptionIfMissing() {
        var doc = Configs.ParseString(@"---
            Mandatory: 15
            MissingOrNotDependingOnDefault: true
        ", "TestFilename");
        var inst = Activator.CreateInstance<AttributesClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.None);
        Assert.Multiple(() => {
            Assert.That(inst.Mandatory, Is.EqualTo(15));
            Assert.That(inst.AllowedMissing, Is.EqualTo("initial"));
            Assert.That(inst.Ignored, Is.False);
            Assert.That(inst.MissingOrNotDependingOnDefault, Is.EqualTo("true"));
        });
    }

    [Test]
    public void AllowedMissingByDefaultInClassWithMandatory() {
        var doc = Configs.ParseString(@"---
            Mandatory: 15
        ", "TestFilename");
        var inst = Activator.CreateInstance<AttributesClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.AllowMissingFields);
        Assert.Multiple(() => {
            Assert.That(inst.Mandatory, Is.EqualTo(15));
            Assert.That(inst.AllowedMissing, Is.EqualTo("initial"));
            Assert.That(inst.MissingOrNotDependingOnDefault, Is.EqualTo("initial2"));
        });
    }

    [Test]
    public void IgnoreAndSpecifyingFailsOnExtras() {
        var doc = Configs.ParseString(@"---
            Mandatory: 101
            AllowedMissing: herp
            Ignored: true
            MissingOrNotDependingOnDefault: whut
        ", "TestFilename");
        Assert.Throws<ExtraFieldsException>(() => {
            var inst = Activator.CreateInstance<AttributesClass>();
            reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.None);
        });
    }

    [Test]
    public void IgnoreAndMissingIgnored() {
        var doc = Configs.ParseString(@"---
            Mandatory: 102
            AllowedMissing: herpe
            MissingOrNotDependingOnDefault: whip
        ", "TestFilename");
        var inst = Activator.CreateInstance<AttributesClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.None);
        Assert.Multiple(() => {
            Assert.That(inst.Mandatory, Is.EqualTo(102));
            Assert.That(inst.AllowedMissing, Is.EqualTo("herpe"));
            Assert.That(inst.Ignored, Is.False);
            Assert.That(inst.MissingOrNotDependingOnDefault, Is.EqualTo("whip"));
        });
    }

    [Test]
    public void MandatoryClassAcceptsSetting() {
        var doc = Configs.ParseString(@"---
            intField: 10
            stringField: uh
        ", "TestFilename");
        var inst = Activator.CreateInstance<MandatoryClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.None);
        Assert.Multiple(() => {
            Assert.That(inst.intField, Is.EqualTo(10));
            Assert.That(inst.stringField, Is.EqualTo("uh"));
            Assert.That(inst.ignoreField, Is.EqualTo("initialignore"));
        });
    }

    [Test]
    public void MandatoryClassFailsOnMissing() {
        var doc = Configs.ParseString(@"---
            stringField: uh
        ", "TestFilename");
        Assert.Throws<MissingFieldsException>(() => {
            var inst = Activator.CreateInstance<MandatoryClass>();
            reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.None);
        });
    }

    [Test]
    public void MandatoryClassOverridesOptionsReifyParameter() {
        var doc = Configs.ParseString(@"---
            stringField: uh
        ", "TestFilename");
        Assert.Throws<MissingFieldsException>(() => {
            var inst = Activator.CreateInstance<MandatoryClass>();
            reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.AllowMissingFields);
        });
    }

    [Test]
    public void MandatoryClassAllowsMissingField() {
        var doc = Configs.ParseString(@"---
            intField: 99
        ", "TestFilename");
        var inst = Activator.CreateInstance<MandatoryClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.None);
        Assert.Multiple(() => {
            Assert.That(inst.intField, Is.EqualTo(99));
            Assert.That(inst.stringField, Is.EqualTo("initial"));
            Assert.That(inst.ignoreField, Is.EqualTo("initialignore"));
        });
    }

    [Test]
    public void AllowMissingClassAcceptsSetting() {
        var doc = Configs.ParseString(@"---
            stringField: hmm
            listField: [1]
        ", "TestFilename");
        var inst = Activator.CreateInstance<AllowMissingClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.None);
        Assert.Multiple(() => {
            Assert.That(inst.stringField, Is.EqualTo("hmm"));
            Assert.That(inst.listField[0], Is.EqualTo("1"));
        });
    }

    [Test]
    public void AllowMissingClassAllowsMissing() {
        var doc = Configs.ParseString(@"---
            stringField: wot
        ", "TestFilename");
        var inst = Activator.CreateInstance<AllowMissingClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.AllowMissingFields);
        Assert.Multiple(() => {
            Assert.That(inst.stringField, Is.EqualTo("wot"));
            Assert.That(inst.listField, Is.Null);
        });
    }

    [Test]
    public void AllowMissingClassOverridesOptions() {
        var doc = Configs.ParseString(@"---
            stringField: wot
        ", "TestFilename");
        var inst = Activator.CreateInstance<AllowMissingClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.None);
        Assert.Multiple(() => {
            Assert.That(inst.stringField, Is.EqualTo("wot"));
            Assert.That(inst.listField, Is.Null);
        });
    }

    [Test]
    public void AllowMissingClassChecksMandatoryField() {
        var doc = Configs.ParseString(@"---
            listField: [a,b]
        ", "TestFilename");
        Assert.Throws<MissingFieldsException>(() => {
            var inst = Activator.CreateInstance<AllowMissingClass>();
            reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.AllowMissingFields);
        });
    }

    [Test]
    public void AllowMissingClassDoesCheckExtraFieldsToo() {
        var doc = Configs.ParseString(@"---
            stringField: hi
            extra_field: 33333
        ", "TestFilename");
        Assert.Throws<ExtraFieldsException>(() => {
            var inst = Activator.CreateInstance<AllowMissingClass>();
            reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.AllowMissingFields);
        });
    }

    [Test]
    public void PropertiesClassAcceptsSetting() {
        var doc = Configs.ParseString(@"---
            int1Value: 10
            staticStringValue: newValue
            mandatoryValue: mandatory
        ", "TestFilename");
        var inst = Activator.CreateInstance<PropertiesClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.None);
        Assert.Multiple(() => {
            Assert.That(inst.int1Value, Is.EqualTo(10));
            Assert.That(inst.int2Value, Is.EqualTo(2));
            Assert.That(inst.backing3Int, Is.EqualTo(3));
            Assert.That(inst.int4Value, Is.EqualTo(4));
            Assert.That(PropertiesClass.staticStringValue, Is.EqualTo("newValue"));
            Assert.That(inst.allowMissing, Is.EqualTo("missing"));
            Assert.That(inst.mandatoryValue, Is.EqualTo("mandatory"));
        });
    }
}

[TestFixture]
class SetField : TestTypes {
    [Test]
    public void SetTrivialFieldOnObject() {
        var doc = Configs.ParseString("boolKeyDefaultFalse: true", "SetField.SetTrivialFieldOnObject");
        var tc = new TestClass();
        Configs.SetFieldOnObject(ref tc, "boolKeyDefaultFalse", doc, ReificationOptions.None);
        Assert.That(tc.boolKeyDefaultFalse, Is.True);
    }

    [Test]
    public void SetOptionalFieldOnObject() {
        var doc = Configs.ParseString("{}", "SetField.SetOptionalFieldOnObject");
        var tc = new TestClass();
        Configs.SetFieldOnObject(ref tc, "boolKeyDefaultFalse", doc, ReificationOptions.AllowMissingFields);
        Assert.That(tc.boolKeyDefaultFalse, Is.False);
    }

    [Test]
    public void SetRequiredFieldOnObject() {
        var doc = Configs.ParseString("Mandatory: 1", "SetField.SetRequiredFieldOnObject");
        var ac = new AttributesClass();
        Configs.SetFieldOnObject(ref ac, "Mandatory", doc, ReificationOptions.None);
        Assert.That(ac.Mandatory, Is.EqualTo(1));
    }

    [Test]
    public void SetRequiredFieldOnObjectButItsMissing() {
        var doc = Configs.ParseString("{}", "SetField.SetRequiredFieldOnObjectButItsMissing");
        var ac = new AttributesClass();
        Assert.Throws<MissingFieldsException>(() => {
            Configs.SetFieldOnObject(ref ac, "Mandatory", doc, ReificationOptions.None);
        });
    }

    [Test]
    public void SetDisallowedExtraFieldThrows() {
        var doc = Configs.ParseString(@"extraField: 1", "SetField.SetDisallowedExtraFieldThrows");
        var ac = new AttributesClass();
        Assert.Throws<ExtraFieldsException>(() => {
            Configs.SetFieldOnObject(ref ac, "extraField", doc, ReificationOptions.None);
        });
    }

    [Test]
    public void SetAllowedExtraFieldDoesNothing() {
        var doc = Configs.ParseString(@"extraField: 1", "SetField.SetAllowedExtraFieldDoesNothing");
        var ac = new AttributesClass();
        Configs.SetFieldOnObject(ref ac, "extraField", doc, ReificationOptions.AllowExtraFields);
    }

    [Test]
    public void SetIgnoredFieldDoesNothing() {
        var doc = Configs.ParseString(@"ignored: true", "SetField.SetIgnoredFieldDoesNothing");
        var ac = new AttributesClass();
        Configs.SetFieldOnObject(ref ac, "ignored", doc, ReificationOptions.None);
        Assert.That(ac.Ignored, Is.False);
    }
}
