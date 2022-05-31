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
        Assert.AreEqual(tc.stringKey, "right");
    }

    [Test]
    public void SetsBoolToTrue() {
        var tc = ReifyString<TestClass>(@"---
            boolKeyDefaultFalse: true
            ");
        Assert.AreEqual(tc.boolKeyDefaultFalse, true);
    }

    [Test]
    public void SetsBoolToTrueWithCapital() {
        var tc = ReifyString<TestClass>(@"---
            boolKeyDefaultFalse: True
            ");
        Assert.AreEqual(tc.boolKeyDefaultFalse, true);
    }

    [Test]
    public void SetsBoolToFalse() {
        var tc = ReifyString<TestClass>(@"---
            boolKeyDefaultTrue: false
            ");
        Assert.AreEqual(tc.boolKeyDefaultTrue, false);
    }

    [Test]
    public void SetsBoolToFalseWithCapital() {
        var tc = ReifyString<TestClass>(@"---
            boolKeyDefaultTrue: False
            ");
        Assert.AreEqual(tc.boolKeyDefaultTrue, false);
    }

    [Test]
    public void SetsInt() {
        var tc = ReifyString<TestClass>(@"---
            intKey: 100
            ");
        Assert.AreEqual(tc.intKey, 100);
    }

    [Test]
    public void SetsFloat() {
        var tc = ReifyString<TestClass>(@"---
            floatKey: 1.56
            ");
        Assert.AreEqual(tc.floatKey, 1.56f);
    }

    [Test]
    [SetCulture("pt-BR")]
    [SetUICulture("pt-BR")]
    public void SetsFloatWithCultureInvariant() {
        var tc = ReifyString<TestClass>(@"---
            floatKey: 1.56
            ");
        Assert.AreEqual(1.56f, tc.floatKey);
    }

    [Test]
    public void SetsDouble() {
        var tc = ReifyString<TestClass>(@"---
            doubleKey: 1.10101
            ");
        Assert.AreEqual(tc.doubleKey, 1.10101);
    }

    [Test]
    public void SetsByte() {
        var tc = ReifyString<TestClass>(@"---
            byteKey: 55
            ");
        Assert.AreEqual(tc.byteKey, (byte) 55);
    }

    [Test]
    public void SetsEnum() {
        var tc = ReifyString<TestClass>(@"---
            enumKey: Secondi
            ");
        Assert.AreEqual(tc.enumKey, TestEnum.Secondi);
    }

    [Test]
    public void SetsNullable() {
        var tc = ReifyString<TestClass>(@"---
            nullableIntKey: 194
            ");
        Assert.IsTrue(tc.nullableIntKey.HasValue);
        Assert.AreEqual(tc.nullableIntKey.Value, 194);
    }

    [Test]
    public void SetsNullableToNull() {
        var tc = ReifyString<TestClass>(@"---
            nullableIntKey: null
            ");
        Assert.AreEqual(tc.nullableIntKey, null);
        Assert.False(tc.nullableIntKey.HasValue);
    }

    [Test]
    public void SetsNullableStruct() {
        var tc = ReifyString<TestClass>(@"---
            nullableChildStructKey: { childIntKey: 4202 }
            ");
        Assert.True(tc.nullableChildStructKey.HasValue);
        Assert.AreEqual(tc.nullableChildStructKey.Value.childIntKey, 4202);
    }

    [Test]
    public void SetsNullableStructToNull() {
        var tc = ReifyString<TestClass>(@"---
            nullableChildStructKey: null
            ");
        Assert.AreEqual(tc.nullableChildStructKey, null);
        Assert.False(tc.nullableChildStructKey.HasValue);
    }

    [Test]
    public void SetsListOfInts() {
        var instance = ReifyString<TestClass>(@"---
            listIntKey: [0, 1, 2, 3, 4]
            ");
        Assert.AreEqual(instance.listIntKey, new[] {0, 1, 2, 3, 4});
    }

    [Test]
    public void SetsArrayOfInts() {
        var instance = ReifyString<TestClass>(@"---
            arrayIntKey: [0, 1, 2, 3, 4]
            ");
        Assert.AreEqual(instance.arrayIntKey, new[] {0, 1, 2, 3, 4});
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
        Assert.AreEqual(array.Length, 2);
        Assert.IsTrue(ReferenceEquals(array[0], saved));
        Assert.AreEqual(array[0].intKey, 4404);
        Assert.AreEqual(array[0].floatKey, 4.56f);
        Assert.AreEqual(array[1].intKey, 1234);
        Assert.AreEqual(array[1].floatKey, 5505f);
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
        Assert.AreEqual(array.Length, 3);
        Assert.IsTrue(ReferenceEquals(array[0], saved));
        Assert.AreEqual(array[0].intKey, 4404);
        Assert.AreEqual(array[0].floatKey, 4.56f);
        Assert.AreEqual(array[1].intKey, 1234);
        Assert.AreEqual(array[1].floatKey, 5505f);
        Assert.AreEqual(array[2].intKey, 1011);
        Assert.AreEqual(array[2].floatKey, -1f);
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
        Assert.AreEqual(array.Length, 1);
        Assert.IsTrue(ReferenceEquals(array[0], saved));
        Assert.AreEqual(array[0].intKey, 4404);
        Assert.AreEqual(array[0].floatKey, 4.56f);
    }

    [Test]
    public void SetsArray2DOfFloats() {
        var instance = ReifyString<TestClass>(@"---
            array2dFloatKey:
                - [9, 8, 7]
                - [1, 2, 3]
            ");
        Assert.AreEqual(instance.array2dFloatKey[0, 0], 9f);
        Assert.AreEqual(instance.array2dFloatKey[0, 1], 8f);
        Assert.AreEqual(instance.array2dFloatKey[0, 2], 7f);
        Assert.AreEqual(instance.array2dFloatKey[1, 0], 1f);
        Assert.AreEqual(instance.array2dFloatKey[1, 1], 2f);
        Assert.AreEqual(instance.array2dFloatKey[1, 2], 3f);
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
        Assert.AreEqual(instance.array3dFloatKey[0,0,0], 1f);
        Assert.AreEqual(instance.array3dFloatKey[0,0,1], 2f);
        Assert.AreEqual(instance.array3dFloatKey[0,0,2], 3f);
        Assert.AreEqual(instance.array3dFloatKey[0,1,0], 4f);
        Assert.AreEqual(instance.array3dFloatKey[0,1,1], 5f);
        Assert.AreEqual(instance.array3dFloatKey[0,1,2], 6f);
        Assert.AreEqual(instance.array3dFloatKey[1,0,0], 7f);
        Assert.AreEqual(instance.array3dFloatKey[1,0,1], 8f);
        Assert.AreEqual(instance.array3dFloatKey[1,0,2], 9f);
        Assert.AreEqual(instance.array3dFloatKey[1,1,0], 10f);
        Assert.AreEqual(instance.array3dFloatKey[1,1,1], 11f);
        Assert.AreEqual(instance.array3dFloatKey[1,1,2], 12f);
    }
    
    [Test]
    public void Array2DUpdatesInPlace() {
        var array = new TestClass[1, 2];
        array[0, 0] = new TestClass { intKey = 62, floatKey = 4.56f };
        array[0, 1] = new TestClass {intKey = 1234, floatKey = 8.98f};
        
        var saved = array[0, 0];
        
        UpdateFromString(ref array, @"---
            - - intKey: 4404
              - floatKey: 5505
            ");
        
        Assert.AreEqual(array.GetLength(0), 1);
        Assert.AreEqual(array.GetLength(1), 2);
        
        Assert.IsTrue(ReferenceEquals(array[0, 0], saved));
        Assert.AreEqual(array[0, 0].intKey, 4404);
        Assert.AreEqual(array[0, 0].floatKey, 4.56f);
        Assert.AreEqual(array[0, 1].intKey, 1234);
        Assert.AreEqual(array[0, 1].floatKey, 5505f);
    }

    [Test]
    public void Array2DUpdatesInPlaceWhenAddingItems() {
        var array = new TestClass[1, 2];
        array[0, 0] = new TestClass { intKey = 62, floatKey = 4.56f };
        array[0, 1] = new TestClass { intKey = 1234, floatKey = 8.98f };
        
        var saved = array[0, 0];
        UpdateFromString(ref array, @"---
            - - intKey: 4404
              - floatKey: 5505
              - intKey: 1011
            ");
        
        Assert.AreEqual(array.GetLength(0), 1);
        Assert.AreEqual(array.GetLength(1), 3);
        Assert.IsTrue(ReferenceEquals(array[0, 0], saved));
        Assert.AreEqual(array[0, 0].intKey, 4404);
        Assert.AreEqual(array[0, 0].floatKey, 4.56f);
        Assert.AreEqual(array[0, 1].intKey, 1234);
        Assert.AreEqual(array[0, 1].floatKey, 5505f);
        Assert.AreEqual(array[0, 2].intKey, 1011);
        Assert.AreEqual(array[0, 2].floatKey, -1f);
    }

    [Test]
    public void Array2DUpdatesInPlaceWhenRemovingItems() {
        var array = new TestClass[1, 3];
        array[0, 0] = new TestClass { intKey = 62, floatKey = 4.56f };
        array[0, 1] = new TestClass { intKey = 1234, floatKey = 8.98f };
        array[0, 2] = new TestClass { intKey = 392, floatKey = 44.55f };
        
        var saved = array[0, 0];
        UpdateFromString(ref array, @"---
            - - intKey: 4404
            ");
        Assert.AreEqual(array.GetLength(0), 1);
        Assert.AreEqual(array.GetLength(1), 1);
        Assert.IsTrue(ReferenceEquals(array[0, 0], saved));
        Assert.AreEqual(array[0, 0].intKey, 4404);
        Assert.AreEqual(array[0, 0].floatKey, 4.56f);
    }

    [Test]
    public void SetsStruct() {
        var pc = ReifyString<ParentClass>(@"---
            nestedStruct:
                childIntKey: 1201                
            ");
        Assert.AreEqual(pc.nestedStruct.childIntKey, 1201);
    }

    [Test]
    public void CreatesNestedObject() {
        var pc = ReifyString<ParentClass>(@"---
            nestedObject:
                intKey: 41
            ");
        Assert.AreEqual(pc.nestedObject.intKey, 41);
    }

    [Test]
    public void CreatesNestedListObjects() {
        var pc = ReifyString<ParentClass>(@"---
            nestedList:
                - intKey: 35
                - intKey: 23
                - intKey: 65
            ");
        Assert.AreEqual(pc.nestedList.Count, 3);
        Assert.AreEqual(pc.nestedList[0].intKey, 35);
        Assert.AreEqual(pc.nestedList[1].intKey, 23);
        Assert.AreEqual(pc.nestedList[2].intKey, 65);
    }

    [Test]
    public void NestedObjectUpdateDoesntCreateNewObject() {
        var o = new ParentClass();
        o.nestedObject = new TestClass();
        var saved = o.nestedObject;
        UpdateFromString(ref o, @"---
            nestedObject:
                intKey: 100
            ");
        Assert.IsTrue(ReferenceEquals(o.nestedObject, saved));
        Assert.AreEqual(o.nestedObject.intKey, 100);
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
        Assert.AreEqual(o.nestedStruct.childIntKey, 99);
        Assert.AreEqual(o.nestedStruct.childFloatKey, 10f);
    }

    [Test]
    public void NestedListUpdatesInPlace() {
        var o = new ParentClass();
        o.nestedList = new List<TestClass>();
        o.nestedList.Add(new TestClass {intKey = 78, floatKey = 1.2f});
        var saved = o.nestedList[0];
        UpdateFromString(ref o, @"---
            nestedList:
                - intKey: 4404
            ");
        Assert.AreEqual(o.nestedList.Count, 1);
        Assert.IsTrue(ReferenceEquals(o.nestedList[0], saved));
        Assert.AreEqual(o.nestedList[0].intKey, 4404);
        Assert.AreEqual(o.nestedList[0].floatKey, 1.2f);
    }

    [Test]
    public void NestedListUpdatesInPlaceWhenAddingItems() {
        var o = new ParentClass();
        o.nestedList = new List<TestClass>();
        o.nestedList.Add(new TestClass {intKey = 62, floatKey = 4.56f});
        o.nestedList.Add(new TestClass {intKey = 1234, floatKey = 8.98f});
        var saved = o.nestedList[0];
        UpdateFromString(ref o, @"---
            nestedList:
                - intKey: 4404
                - floatKey: 5505
                - intKey: 1011
            ");
        Assert.AreEqual(o.nestedList.Count, 3);
        Assert.IsTrue(ReferenceEquals(o.nestedList[0], saved));
        Assert.AreEqual(o.nestedList[0].intKey, 4404);
        Assert.AreEqual(o.nestedList[0].floatKey, 4.56f);
        Assert.AreEqual(o.nestedList[1].intKey, 1234);
        Assert.AreEqual(o.nestedList[1].floatKey, 5505f);
        Assert.AreEqual(o.nestedList[2].intKey, 1011);
        Assert.AreEqual(o.nestedList[2].floatKey, -1f);
    }

    [Test]
    public void NestedListUpdatesInPlaceWhenRemovingItems() {
        var o = new ParentClass();
        o.nestedList = new List<TestClass>();
        o.nestedList.Add(new TestClass {intKey = 62, floatKey = 4.56f});
        o.nestedList.Add(new TestClass {intKey = 1234, floatKey = 8.98f});
        o.nestedList.Add(new TestClass {intKey = 392, floatKey = 44.55f});
        var saved = o.nestedList[0];
        UpdateFromString(ref o, @"---
            nestedList:
                - intKey: 4404
            ");
        Assert.AreEqual(o.nestedList.Count, 1);
        Assert.IsTrue(ReferenceEquals(o.nestedList[0], saved));
        Assert.AreEqual(o.nestedList[0].intKey, 4404);
        Assert.AreEqual(o.nestedList[0].floatKey, 4.56f);
    }

    [Test]
    public void NestedListUpdatesInPlaceWhenTheSameCount() {
        var o = new ParentClass();
        o.nestedList = new List<TestClass>();
        o.nestedList.Add(new TestClass {intKey = 62, floatKey = 4.56f});
        o.nestedList.Add(new TestClass {intKey = 1234, floatKey = 8.98f});
        o.nestedList.Add(new TestClass {intKey = 11, floatKey = 55.24f});
        UpdateFromString(ref o, @"---
            nestedList:
                - intKey: 503
                - floatKey: 66
                - intKey: 67
            ");
        Assert.AreEqual(o.nestedList.Count, 3);
        Assert.AreEqual(o.nestedList[0].intKey, 503);
        Assert.AreEqual(o.nestedList[0].floatKey, 4.56f);
        Assert.AreEqual(o.nestedList[1].intKey, 1234);
        Assert.AreEqual(o.nestedList[1].floatKey, 66f);
        Assert.AreEqual(o.nestedList[2].intKey, 67);
        Assert.AreEqual(o.nestedList[2].floatKey, 55.24f);
    }

    [Test]
    public void DictEnumKeys() {
        var o = new Dictionary<TestEnum, int>();
        UpdateFromString(ref o, @"---
            Primi: 1024
            Secondi: 999
        ");
        Assert.AreEqual(2, o.Count);
        Assert.AreEqual(1024, o[TestEnum.Primi]);
        Assert.AreEqual(999, o[TestEnum.Secondi]);
    }

    [Test]
    public void DictIntKeys() {
        var o = new Dictionary<int, int>();
        UpdateFromString(ref o, @"---
            101: 1024
            504: 999
        ");
        Assert.AreEqual(2, o.Count);
        Assert.AreEqual(1024, o[101]);
        Assert.AreEqual(999, o[504]);
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
        Assert.AreEqual(2, o.Count);
        Assert.IsTrue(ReferenceEquals(saved, o[TestEnum.Primi]));
        Assert.AreEqual(99, o[TestEnum.Primi].intKey);
        Assert.AreEqual(12, o[TestEnum.Secondi].intKey);
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
        Assert.AreEqual(1, o.Count);
        Assert.IsFalse(o.ContainsKey(TestEnum.Secondi));
        Assert.IsTrue(ReferenceEquals(saved, o[TestEnum.Primi]));
        Assert.AreEqual(99, o[TestEnum.Primi].intKey);
    }

    [Test]
    public void NestedDictUpdatesInPlace() {
        var o = new ParentClass();
        o.nestedDict = new Dictionary<string, TestClass>();
        o.nestedDict["dictKey"] = new TestClass {intKey = 22, floatKey = 4.56f};
        var saved = o.nestedDict["dictKey"];
        UpdateFromString(ref o, @"---
            nestedDict:
                dictKey:
                    intKey: 56
            ");
        Assert.AreEqual(o.nestedDict.Count, 1);
        Assert.IsTrue(ReferenceEquals(o.nestedDict["dictKey"], saved));
        Assert.AreEqual(o.nestedDict["dictKey"].intKey, 56);
    }

    [Test]
    public void NestedDictUpdatesInPlaceStructs() {
        var o = new ParentClass();
        o.nestedStructDict = new Dictionary<string, ChildStruct>();
        o.nestedStructDict["dictKey"] = new ChildStruct {childIntKey = 11, childFloatKey = 6.54f};
        UpdateFromString(ref o, @"---
            nestedStructDict:
                dictKey:
                    childIntKey: 110
            ");
        Assert.AreEqual(o.nestedStructDict.Count, 1);
        Assert.AreEqual(o.nestedStructDict["dictKey"].childIntKey, 110);
        Assert.AreEqual(o.nestedStructDict["dictKey"].childFloatKey, 6.54f);
    }

    [Test]
    public void NestedDictAddsNewPairs() {
        var o = new ParentClass();
        o.nestedDict = new Dictionary<string, TestClass>();
        o.nestedDict["dictKey"] = new TestClass {intKey = 67, floatKey = 1.06f};
        UpdateFromString(ref o, @"---
            nestedDict:
                dictKey:
                    intKey: 42
                newKey:
                    intKey: 43
                    floatKey: 12
            ");
        Assert.AreEqual(o.nestedDict.Count, 2);
        Assert.AreEqual(o.nestedDict["dictKey"].intKey, 42);
        Assert.AreEqual(o.nestedDict["dictKey"].floatKey, 1.06f);
        Assert.AreEqual(o.nestedDict["newKey"].intKey, 43);
        Assert.AreEqual(o.nestedDict["newKey"].floatKey, 12f);
    }

    [Test]
    public void NestedDictRemovesMissingPairs() {
        var o = new ParentClass();
        o.nestedDict = new Dictionary<string, TestClass>();
        o.nestedDict["dictKey"] = new TestClass {intKey = 200, floatKey = 10099.2f};
        UpdateFromString(ref o, @"---
            nestedDict:
                newKey:
                    intKey: 999
            ");
        Assert.AreEqual(o.nestedDict.Count, 1);
        Assert.IsFalse(o.nestedDict.ContainsKey("dictKey"));
        Assert.AreEqual(o.nestedDict["newKey"].intKey, 999);
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
        Assert.AreEqual(tc.intKey, 99088);
    }

    [Test]
    public void SetFieldsOnObjectForACastedObject() {
        var tc = (object) new TestClass();
        var doc = Configs.ParseString(
            @"---
            intKey: 99077
            "
            , "TestFilename");
        reifier.SetFieldsOnObject(ref tc, doc);
        Assert.AreEqual(((TestClass) tc).intKey, 99077);
    }

    [Test]
    public void SetFieldsOnStructForATemplatedStructCall() {
        var s = new ChildStruct();
        s.childIntKey = 1;
        s.childFloatKey = 1;
        var doc = Configs.ParseString(
            @"---
            childIntKey: 12345
            "
            , "TestFilename");
        reifier.SetFieldsOnStruct(ref s, doc);
        Assert.AreEqual(s.childIntKey, 12345);
        Assert.AreEqual(s.childFloatKey, 1);
    }

    [Test]
    public void SetFieldsOnObjectForABoxedStructArgument() {
        var s = new ChildStruct();
        s.childIntKey = 1;
        s.childFloatKey = 1;
        var doc = Configs.ParseString(
            @"---
            childIntKey: 34567
            "
            , "TestFilename");
        object os = s;
        reifier.SetFieldsOnObject(ref os, doc);
        Assert.AreEqual(((ChildStruct) os).childIntKey, 34567);
        Assert.AreEqual(((ChildStruct) os).childFloatKey, 1);
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
        Assert.IsNotNull(doc);
        Assert.IsInstanceOf<DocNode>(doc);
    }

    [Test]
    public void EmptyDocStreamReturnsDocNode() {
        var doc = Configs.LoadDocFromStream(new System.IO.MemoryStream(), "EmptyDoc");
        Assert.IsNotNull(doc);
        Assert.IsInstanceOf<DocNode>(doc);
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
        Assert.AreEqual(TestClass.staticStringKey, "arbitrage");
        Assert.AreEqual(TestClass.staticIntArrKey, new[] {4, 4, 0, 0});
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
        Assert.AreEqual(ChildStruct.staticIntKey, 3049);
    }

    [Test]
    public void StaticClass() {
        var doc = Configs.ParseString(
            @"---
            staticStringList: [herp, derp]
            "
            , "TestFilename");
        Configs.ReifyStatic(typeof(PureStatic), doc);
        Assert.AreEqual(PureStatic.staticStringList[0], "herp");
        Assert.AreEqual(PureStatic.staticStringList[1], "derp");
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
        Assert.AreEqual(inst.SingleField, 8342);
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
        Assert.AreEqual(inst.SingleList[0], "a");
        Assert.AreEqual(inst.SingleList[1], "b");
        Assert.AreEqual(inst.SingleList[2], "c");
        Assert.AreEqual(inst.SingleList[3], "d");
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
        Assert.AreEqual(inst.floatKey, 1.56f);
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
        Assert.IsNotNull(exception);
        Assert.True(exception.Message.IndexOf("extraKey1", StringComparison.Ordinal) >= 0);
        Assert.True(exception.Message.IndexOf("extraKey2", StringComparison.Ordinal) >= 0);
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
        Assert.AreEqual(inst.childIntKey, 42);
        Assert.AreEqual(inst.childFloatKey, 1.25);
        Assert.AreEqual(ChildStruct.staticIntKey, 332);
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
        Assert.IsNotNull(exception);
        Assert.True(exception.Message.IndexOf("childFloatKey", StringComparison.Ordinal) >= 0);
        Assert.True(exception.Message.IndexOf("staticIntKey", StringComparison.Ordinal) >= 0);
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
        Assert.AreEqual(inst.childIntKey, 32);
        Assert.AreEqual(inst.childFloatKey, 11);
        Assert.AreEqual(ChildStruct.staticIntKey, 5);
    }

    [Test]
    public void RefiesCaseInsensitiveWithMissing() {
        var doc = Configs.ParseString(@"---
            childintkey: 32
        ", "TestFilename");
        var inst = Activator.CreateInstance<ChildStruct>();
        reifier.SetFieldsOnStruct(ref inst, doc, ReificationOptions.AllowMissingFields);
        Assert.AreEqual(inst.childIntKey, 32);
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
        Assert.AreEqual(inst.Mandatory, 10);
        Assert.AreEqual(inst.AllowedMissing, "derp");
        Assert.AreEqual(inst.Ignored, false);
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
        Assert.AreEqual(inst.Mandatory, 15);
        Assert.AreEqual(inst.AllowedMissing, "initial");
        Assert.AreEqual(inst.Ignored, false);
        Assert.AreEqual(inst.MissingOrNotDependingOnDefault, "true");
    }

    [Test]
    public void AllowedMissingByDefaultInClassWithMandatory() {
        var doc = Configs.ParseString(@"---
            Mandatory: 15
        ", "TestFilename");
        var inst = Activator.CreateInstance<AttributesClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.AllowMissingFields);
        Assert.AreEqual(inst.Mandatory, 15);
        Assert.AreEqual(inst.AllowedMissing, "initial");
        Assert.AreEqual(inst.MissingOrNotDependingOnDefault, "initial2");
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
        Assert.AreEqual(inst.Mandatory, 102);
        Assert.AreEqual(inst.AllowedMissing, "herpe");
        Assert.AreEqual(inst.Ignored, false);
        Assert.AreEqual(inst.MissingOrNotDependingOnDefault, "whip");
    }

    [Test]
    public void MandatoryClassAcceptsSetting() {
        var doc = Configs.ParseString(@"---
            intField: 10
            stringField: uh
        ", "TestFilename");
        var inst = Activator.CreateInstance<MandatoryClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.None);
        Assert.AreEqual(inst.intField, 10);
        Assert.AreEqual(inst.stringField, "uh");
        Assert.AreEqual(inst.ignoreField, "initialignore");
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
        Assert.AreEqual(inst.intField, 99);
        Assert.AreEqual(inst.stringField, "initial");
        Assert.AreEqual(inst.ignoreField, "initialignore");
    }

    [Test]
    public void AllowMissingClassAcceptsSetting() {
        var doc = Configs.ParseString(@"---
            stringField: hmm
            listField: [1]
        ", "TestFilename");
        var inst = Activator.CreateInstance<AllowMissingClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.None);
        Assert.AreEqual(inst.stringField, "hmm");
        Assert.AreEqual(inst.listField[0], "1");
    }

    [Test]
    public void AllowMissingClassAllowsMissing() {
        var doc = Configs.ParseString(@"---
            stringField: wot
        ", "TestFilename");
        var inst = Activator.CreateInstance<AllowMissingClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.AllowMissingFields);
        Assert.AreEqual(inst.stringField, "wot");
        Assert.AreEqual(inst.listField, null);
    }

    [Test]
    public void AllowMissingClassOverridesOptions() {
        var doc = Configs.ParseString(@"---
            stringField: wot
        ", "TestFilename");
        var inst = Activator.CreateInstance<AllowMissingClass>();
        reifier.SetFieldsOnObject(ref inst, doc, ReificationOptions.None);
        Assert.AreEqual(inst.stringField, "wot");
        Assert.AreEqual(inst.listField, null);
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
        Assert.AreEqual(inst.int1Value, 10);
        Assert.AreEqual(inst.int2Value, 2);
        Assert.AreEqual(inst.backing3Int, 3);
        Assert.AreEqual(inst.int4Value, 4);
        Assert.AreEqual(PropertiesClass.staticStringValue, "newValue");
        Assert.AreEqual(inst.allowMissing, "missing");
        Assert.AreEqual(inst.mandatoryValue, "mandatory");
    }
}

[TestFixture]
class SetField : TestTypes {
    [Test]
    public void SetTrivialFieldOnObject() {
        var doc = Configs.ParseString("boolKeyDefaultFalse: true", "SetField.SetTrivialFieldOnObject");
        var tc = new TestClass();
        Configs.SetFieldOnObject(ref tc, "boolKeyDefaultFalse", doc, ReificationOptions.None);
        Assert.AreEqual(tc.boolKeyDefaultFalse, true);
    }
    
    [Test]
    public void SetOptionalFieldOnObject() {
        var doc = Configs.ParseString("{}", "SetField.SetOptionalFieldOnObject");
        var tc = new TestClass();
        Configs.SetFieldOnObject(ref tc, "boolKeyDefaultFalse", doc, ReificationOptions.AllowMissingFields);
        Assert.AreEqual(tc.boolKeyDefaultFalse, false);
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