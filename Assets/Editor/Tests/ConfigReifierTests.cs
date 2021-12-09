﻿using UnityEngine;
using NUnit.Framework;
using DarkConfig;
using System.Collections.Generic;

[TestFixture]
class ConfigReifierFacts {
    // disable variable unused in function body warnings; there's a lot in here
    #pragma warning disable 168


    enum TestEnum {
        Primi, Secondi
    }

    class TestClass {
        // scalars
        public bool boolKeyDefaultFalse = false;
        public bool boolKeyDefaultTrue = true;
        public string stringKey = "wrong";
        public int intKey = -1;
        public float floatKey = -1f;
        public double doubleKey = -1.0;
        public byte byteKey = 0;
        public Vector2 vector2Key = Vector2.zero;
        public Vector3 vector3Key = Vector3.zero;
        public Color colorKey = Color.black;
        public TestEnum enumKey = TestEnum.Primi;
        public int? nullableIntKey = null;
        public ChildStruct? nullableChildStructKey = null;

        // collections of primitives
        public List<int> listIntKey = null;
        public int[] arrayIntKey = null;
        public float[,] array2dFloatKey = null;

        // static variables should work as well
        public static string staticStringKey = null;
        public static int[] staticIntArrKey = null;
    }

    struct ChildStruct {
        public int childIntKey;
        public float childFloatKey;

        public static int staticIntKey;
    }

    class ParentClass {
        #pragma warning disable 649
        public TestClass nestedObject;
        public List<TestClass> nestedList;
        public Dictionary<string, TestClass> nestedDict;
        public Dictionary<string, ChildStruct> nestedStructDict;
        public ChildStruct nestedStruct;
        #pragma warning restore 649
    }

    static class PureStatic {
        public static List<string> staticStringList = null;
    }

    class SingleFieldClass {
        public int SingleField = 0;
    }

    class SingleListClass {
        public List<string> SingleList = null;
    }

    class AttributesClass {
        [ConfigMandatory]
        public int Mandatory = -1;

        [ConfigAllowMissing]
        public string AllowedMissing = "initial";

        [ConfigIgnore]
        public bool Ignored = false;

        public string MissingOrNotDependingOnDefault = "initial2";
    }

    [ConfigMandatory]
    class MandatoryClass {
        public int intField = -1;

        [ConfigAllowMissing]
        public string stringField = "initial";

        [ConfigIgnore]
        public string ignoreField = "initialignore";
    }

    [ConfigAllowMissing]
    class AllowMissingClass {
        [ConfigMandatory]
        public string stringField = "init";

        public List<string> listField = null;
    }

    [ConfigMandatory]
    class MonoBehaviourSubclass : UnityEngine.MonoBehaviour {
        public int field1 = 0;
    }

    ConfigOptions defaults;

    [SetUp]
    public void DoSetup() {
        DefaultFromDocs.RegisterAll(); // needs to be called here because we can't be sure whether preload has been called before or not
        UnityFromDocs.RegisterAll();
        defaults = Config.DefaultOptions;
        Config.DefaultOptions = ConfigOptions.AllowMissingExtraFields;
    }

    [TearDown]
    public void TearDown() {
        Config.DefaultOptions = defaults;
    }

    T ReifyString<T>(string str) where T: new() {
        var doc = Config.LoadDocFromString(str, "ConfigReifierFacts_ReifyString_TestFilename");
        T tc = default(T);
        ConfigReifier.Reify(ref tc, doc);
        return tc;
    }

    T UpdateFromString<T>(ref T obj, string str) {
        var doc = Config.LoadDocFromString(str, "ConfigReifierFacts_UpdateFromString_TestFilename");
        ConfigReifier.Reify(ref obj, doc);
        return obj;
    }

    [Test]
    public void ConfigReifier_SetsString() {
        var tc = ReifyString<TestClass>(@"---
            stringKey: right
            ");
        Assert.AreEqual(tc.stringKey, "right");
    }
    
    [Test]
    public void ConfigReifier_SetsBool_True() {
        var tc = ReifyString<TestClass>(@"---
            boolKeyDefaultFalse: true
            ");
        Assert.AreEqual(tc.boolKeyDefaultFalse, true);
    }

    [Test]
    public void ConfigReifier_SetsBool_True_Capital() {
        var tc = ReifyString<TestClass>(@"---
            boolKeyDefaultFalse: True
            ");
        Assert.AreEqual(tc.boolKeyDefaultFalse, true);
    }

    [Test]
    public void ConfigReifier_SetsBool_False() {
        var tc = ReifyString<TestClass>(@"---
            boolKeyDefaultTrue: false
            ");
        Assert.AreEqual(tc.boolKeyDefaultTrue, false);
    }

    [Test]
    public void ConfigReifier_SetsBool_False_Capital() {
        var tc = ReifyString<TestClass>(@"---
            boolKeyDefaultTrue: False
            ");
        Assert.AreEqual(tc.boolKeyDefaultTrue, false);
    }

    [Test]
    public void ConfigReifier_SetsInt() {
        var tc = ReifyString<TestClass>(@"---
            intKey: 100
            ");
        Assert.AreEqual(tc.intKey, 100);
    }

    [Test]
    public void ConfigReifier_SetsFloat() {
        var tc = ReifyString<TestClass>(@"---
            floatKey: 1.56
            ");
        Assert.AreEqual(tc.floatKey, 1.56f);
    }

    [Test]
    public void ConfigReifier_SetsFloat_CultureInvariant() {
        var oldCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        var oldUICulture = System.Threading.Thread.CurrentThread.CurrentUICulture;
        
        var portugese = new System.Globalization.CultureInfo("pt-BR");
        System.Threading.Thread.CurrentThread.CurrentCulture = portugese;
        System.Threading.Thread.CurrentThread.CurrentUICulture = portugese;

        var tc = ReifyString<TestClass>(@"---
            floatKey: 1.56
            ");
        Assert.AreEqual(1.56f, tc.floatKey);

        System.Threading.Thread.CurrentThread.CurrentCulture = oldCulture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = oldUICulture;
    }

    [Test]
    public void ConfigReifier_SetsDouble() {
        var tc = ReifyString<TestClass>(@"---
            doubleKey: 1.10101
            ");
        Assert.AreEqual(tc.doubleKey, 1.10101);
    }

    [Test]
    public void ConfigReifier_SetsByte() {
        var tc = ReifyString<TestClass>(@"---
            byteKey: 55
            ");
        Assert.AreEqual(tc.byteKey, (byte)55);
    }

    [Test]
    public void ConfigReifier_SetsVector2() {
        var tc = ReifyString<TestClass>(@"---
            vector2Key: [2, 10]
            ");
        Assert.AreEqual(tc.vector2Key, new Vector2(2, 10));
    }

    [Test]
    public void ConfigReifier_SetsVector2_ScalarArgument() {
        var tc = ReifyString<TestClass>(@"---
            vector2Key: 1.1
            ");
        Assert.AreEqual(tc.vector2Key, new Vector2(1.1f, 1.1f));
    }

    [Test]
    public void ConfigReifier_SetsVector2_OneArgument() {
        var tc = ReifyString<TestClass>(@"---
            vector2Key: [5]
            ");
        Assert.AreEqual(tc.vector2Key, new Vector2(5, 5));
    }

    [Test]
    public void ConfigReifier_SetsVector3() {
        var tc = ReifyString<TestClass>(@"---
            vector3Key: [10, 4, 0.5]
            ");
        Assert.AreEqual(tc.vector3Key, new Vector3(10, 4, 0.5f));
    }

    [Test]
    public void ConfigReifier_SetsVector3_ScalarArgument() {
        var tc = ReifyString<TestClass>(@"---
            vector3Key: 2
            ");
        Assert.AreEqual(tc.vector3Key, new Vector3(2, 2, 2));
    }

    [Test]
    public void ConfigReifier_SetsVector3_OneArgument() {
        var tc = ReifyString<TestClass>(@"---
            vector3Key: [3]
            ");
        Assert.AreEqual(tc.vector3Key, new Vector3(3, 3, 3));
    }

    [Test]
    public void ConfigReifier_SetsVector3_TwoArguments() {
        var tc = ReifyString<TestClass>(@"---
            vector3Key: [6, 7]
            ");
        Assert.AreEqual(tc.vector3Key, new Vector3(6, 7, 0));
    }

    [Test]
    public void ConfigReifier_SetsColor() {
        var tc = ReifyString<TestClass>(@"---
            colorKey: [1, 1, 1, 1]
            ");
        Assert.AreEqual(tc.colorKey, Color.white);
    }

    [Test]
    public void ConfigReifier_SetsColor_ThreeArguments() {
        var tc = ReifyString<TestClass>(@"---
            colorKey: [1, 0, 0]
            ");
        Assert.AreEqual(tc.colorKey, Color.red);
    }

    [Test]
    public void ConfigReifier_SetsEnum() {
        var tc = ReifyString<TestClass>(@"---
            enumKey: Secondi
            ");
        Assert.AreEqual(tc.enumKey, TestEnum.Secondi);
    }

    [Test]
    public void ConfigReifier_SetsNullable() {
        var tc = ReifyString<TestClass>(@"---
            nullableIntKey: 194
            ");
        Assert.AreEqual(tc.nullableIntKey.Value, 194);
    }

    [Test]
    public void ConfigReifier_SetsNullabletoNull() {
        var tc = ReifyString<TestClass>(@"---
            nullableIntKey: null
            ");
        Assert.AreEqual(tc.nullableIntKey, null);
        Assert.False(tc.nullableIntKey.HasValue);
    }

    [Test]
    public void ConfigReifier_SetsNullableStruct() {
        var tc = ReifyString<TestClass>(@"---
            nullableChildStructKey: { childIntKey: 4202 }
            ");
        Assert.AreEqual(tc.nullableChildStructKey.Value.childIntKey, 4202);
    }

    [Test]
    public void ConfigReifier_SetsNullableStructToNull() {
        var tc = ReifyString<TestClass>(@"---
            nullableChildStructKey: null
            ");
        Assert.AreEqual(tc.nullableChildStructKey, null);
        Assert.False(tc.nullableChildStructKey.HasValue);
    }

    [Test]
    public void ConfigReifier_SetsListOfInts() {
        var tc = ReifyString<TestClass>(@"---
            listIntKey: [0, 1, 2, 3, 4]
            ");
        Assert.AreEqual(tc.listIntKey, new int[] {0, 1, 2, 3, 4});
    }

    [Test]
    public void ConfigReifier_SetsArrayOfInts() {
        var tc = ReifyString<TestClass>(@"---
            arrayIntKey: [0, 1, 2, 3, 4]
            ");
        Assert.AreEqual(tc.arrayIntKey, new int[] {0, 1, 2, 3, 4});
    }

    [Test]
    public void ConfigReifier_Array_UpdatesInPlace() {
        var arr = new TestClass[] {
            new TestClass { intKey = 62, floatKey = 4.56f },
            new TestClass { intKey = 1234, floatKey = 8.98f }
        };
        TestClass saved = arr[0];
        UpdateFromString(ref arr, @"---
            - intKey: 4404
            - floatKey: 5505
            ");
        Assert.AreEqual(arr.Length, 2);
        Assert.IsTrue(object.ReferenceEquals(arr[0], saved));
        Assert.AreEqual(arr[0].intKey, 4404);
        Assert.AreEqual(arr[0].floatKey, 4.56f);
        Assert.AreEqual(arr[1].intKey, 1234);
        Assert.AreEqual(arr[1].floatKey, 5505f);
    }

    [Test]
    public void ConfigReifier_Array_UpdatesInPlace_AddItems() {
        var arr = new TestClass[] {
            new TestClass { intKey = 62, floatKey = 4.56f },
            new TestClass { intKey = 1234, floatKey = 8.98f }
        };
        var saved = arr[0];
        UpdateFromString(ref arr, @"---
            - intKey: 4404
            - floatKey: 5505
            - intKey: 1011
            ");
        Assert.AreEqual(arr.Length, 3);
        Assert.IsTrue(object.ReferenceEquals(arr[0], saved));
        Assert.AreEqual(arr[0].intKey, 4404);
        Assert.AreEqual(arr[0].floatKey, 4.56f);
        Assert.AreEqual(arr[1].intKey, 1234);
        Assert.AreEqual(arr[1].floatKey, 5505f);
        Assert.AreEqual(arr[2].intKey, 1011);
        Assert.AreEqual(arr[2].floatKey, -1f);
    }

    [Test]
    public void ConfigReifier_Array_UpdatesInPlace_RemoveItems() {
        var arr = new TestClass[] {
            new TestClass { intKey = 62, floatKey = 4.56f },
            new TestClass { intKey = 1234, floatKey = 8.98f },
            new TestClass { intKey = 392, floatKey = 44.55f }
        };
        var saved = arr[0];
        UpdateFromString(ref arr, @"---
            - intKey: 4404
            ");
        Assert.AreEqual(arr.Length, 1);
        Assert.IsTrue(object.ReferenceEquals(arr[0], saved));
        Assert.AreEqual(arr[0].intKey, 4404);
        Assert.AreEqual(arr[0].floatKey, 4.56f);
    }

    [Test]
    public void ConfigReifier_SetsArray2DOfFloats() {
        var tc = ReifyString<TestClass>(@"---
            array2dFloatKey:
                - [9, 8, 7]
                - [1, 2, 3]
            ");
        Assert.AreEqual(tc.array2dFloatKey[0, 0], 9f);
        Assert.AreEqual(tc.array2dFloatKey[1, 0], 8f);
        Assert.AreEqual(tc.array2dFloatKey[2, 0], 7f);
        Assert.AreEqual(tc.array2dFloatKey[0, 1], 1f);
        Assert.AreEqual(tc.array2dFloatKey[1, 1], 2f);
        Assert.AreEqual(tc.array2dFloatKey[2, 1], 3f);
    }

    [Test]
    public void ConfigReifier_SetsStruct() {
        var pc = ReifyString<ParentClass>(@"---
            nestedStruct:
                childIntKey: 1201                
            ");
        Assert.AreEqual(pc.nestedStruct.childIntKey, 1201);
    }

    [Test]
    public void ConfigReifier_CreatesNestedObject() {
        var pc = ReifyString<ParentClass>(@"---
            nestedObject:
                intKey: 41
            ");
        Assert.AreEqual(pc.nestedObject.intKey, 41);
    }

    [Test]
    public void ConfigReifier_CreatesNestedListObjects() {
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
    public void ConfigReifier_NestedObject_UpdateDoesntCreateNewObject() {
        var o = new ParentClass();
        o.nestedObject = new TestClass();
        var saved = o.nestedObject;
        UpdateFromString(ref o, @"---
            nestedObject:
                intKey: 100
            ");
        Assert.IsTrue(object.ReferenceEquals(o.nestedObject, saved));
        Assert.AreEqual(o.nestedObject.intKey, 100);
    }

    [Test]
    public void ConfigReifier_NestedObject_UpdateDoesntClobberStruct() {
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
    public void ConfigReifier_NestedList_UpdatesInPlace() {
        var o = new ParentClass();
        o.nestedList = new List<TestClass>();
        o.nestedList.Add(new TestClass { intKey = 78, floatKey = 1.2f });
        var saved = o.nestedList[0];
        UpdateFromString(ref o, @"---
            nestedList:
                - intKey: 4404
            ");
        Assert.AreEqual(o.nestedList.Count, 1);
        Assert.IsTrue(object.ReferenceEquals(o.nestedList[0], saved));
        Assert.AreEqual(o.nestedList[0].intKey, 4404);
        Assert.AreEqual(o.nestedList[0].floatKey, 1.2f);
    }

    [Test]
    public void ConfigReifier_NestedList_UpdatesInPlace_AddItems() {
        var o = new ParentClass();
        o.nestedList = new List<TestClass>();
        o.nestedList.Add(new TestClass { intKey = 62, floatKey = 4.56f });
        o.nestedList.Add(new TestClass { intKey = 1234, floatKey = 8.98f });
        var saved = o.nestedList[0];
        UpdateFromString(ref o, @"---
            nestedList:
                - intKey: 4404
                - floatKey: 5505
                - intKey: 1011
            ");
        Assert.AreEqual(o.nestedList.Count, 3);
        Assert.IsTrue(object.ReferenceEquals(o.nestedList[0], saved));
        Assert.AreEqual(o.nestedList[0].intKey, 4404);
        Assert.AreEqual(o.nestedList[0].floatKey, 4.56f);
        Assert.AreEqual(o.nestedList[1].intKey, 1234);
        Assert.AreEqual(o.nestedList[1].floatKey, 5505f);
        Assert.AreEqual(o.nestedList[2].intKey, 1011);
        Assert.AreEqual(o.nestedList[2].floatKey, -1f);
    }

    [Test]
    public void ConfigReifier_NestedList_UpdatesInPlace_RemoveItems() {
        var o = new ParentClass();
        o.nestedList = new List<TestClass>();
        o.nestedList.Add(new TestClass { intKey = 62, floatKey = 4.56f });
        o.nestedList.Add(new TestClass { intKey = 1234, floatKey = 8.98f });
        o.nestedList.Add(new TestClass { intKey = 392, floatKey = 44.55f });
        var saved = o.nestedList[0];
        UpdateFromString(ref o, @"---
            nestedList:
                - intKey: 4404
            ");
        Assert.AreEqual(o.nestedList.Count, 1);
        Assert.IsTrue(object.ReferenceEquals(o.nestedList[0], saved));
        Assert.AreEqual(o.nestedList[0].intKey, 4404);
        Assert.AreEqual(o.nestedList[0].floatKey, 4.56f);
    }

    [Test]
    public void ConfigReifier_NestedList_UpdatesInPlace_SameCount() {
        var o = new ParentClass();
        o.nestedList = new List<TestClass>();
        o.nestedList.Add(new TestClass { intKey = 62, floatKey = 4.56f });
        o.nestedList.Add(new TestClass { intKey = 1234, floatKey = 8.98f });
        o.nestedList.Add(new TestClass { intKey = 11, floatKey = 55.24f });
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
    public void ConfigReifier_Dict_EnumKeys() {
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
    public void ConfigReifier_Dict_IntKeys() {
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
    public void ConfigReifier_Dict_EnumKeys_AddsNewWithoutDeletingExisting() {
        var o = new Dictionary<TestEnum, TestClass>();
        var saved = new TestClass { intKey = 101 };
        o[TestEnum.Primi] = saved;
        UpdateFromString(ref o, @"---
            Primi: { intKey: 99 }
            Secondi: { intKey: 12 }
        ");
        Assert.AreEqual(2, o.Count);
        Assert.IsTrue(object.ReferenceEquals(saved, o[TestEnum.Primi]));
        Assert.AreEqual(99, o[TestEnum.Primi].intKey);
        Assert.AreEqual(12, o[TestEnum.Secondi].intKey);
    }

    [Test]
    public void ConfigReifier_Dict_EnumKeys_RemovesMissingWithoutDeletingExisting() {
        var o = new Dictionary<TestEnum, TestClass>();
        var saved = new TestClass { intKey = 101 };
        o[TestEnum.Primi] = saved;
        o[TestEnum.Secondi] = new TestClass { intKey = 1200 };

        UpdateFromString(ref o, @"---
            Primi: { intKey: 99 }
        ");
        Assert.AreEqual(1, o.Count);
        Assert.IsFalse(o.ContainsKey(TestEnum.Secondi));
        Assert.IsTrue(object.ReferenceEquals(saved, o[TestEnum.Primi]));
        Assert.AreEqual(99, o[TestEnum.Primi].intKey);
    }

    [Test]
    public void ConfigReifier_NestedDict_UpdatesInPlace() {
        var o = new ParentClass();
        o.nestedDict = new Dictionary<string, TestClass>();
        o.nestedDict["dictKey"] = new TestClass { intKey = 22, floatKey = 4.56f };
        var saved = o.nestedDict["dictKey"];
        UpdateFromString(ref o, @"---
            nestedDict:
                dictKey:
                    intKey: 56
            ");
        Assert.AreEqual(o.nestedDict.Count, 1);
        Assert.IsTrue(object.ReferenceEquals(o.nestedDict["dictKey"], saved));
        Assert.AreEqual(o.nestedDict["dictKey"].intKey, 56);
    }

    [Test]
    public void ConfigReifier_NestedDict_UpdatesInPlaceStructs() {
        var o = new ParentClass();
        o.nestedStructDict = new Dictionary<string, ChildStruct>();
        o.nestedStructDict["dictKey"] = new ChildStruct { childIntKey = 11, childFloatKey = 6.54f };
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
    public void ConfigReifier_NestedDict_AddsNewPairs() {
        var o = new ParentClass();
        o.nestedDict = new Dictionary<string, TestClass>();
        o.nestedDict["dictKey"] = new TestClass { intKey = 67, floatKey = 1.06f };
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
    public void ConfigReifier_NestedDict_RemovesMissingPairs() {
        var o = new ParentClass();
        o.nestedDict = new Dictionary<string, TestClass>();
        o.nestedDict["dictKey"] = new TestClass { intKey = 200, floatKey = 10099.2f };
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
    public void ConfigReifier_SetFieldsOnObject_PlainObject() {
        var tc = new TestClass();
        var doc = Config.LoadDocFromString(
            @"---
            intKey: 99088
            "
            , "ConfigReifierFacts_ReifyString_TestFilename");
        ConfigReifier.SetFieldsOnObject(ref tc, doc);
        Assert.AreEqual(tc.intKey, 99088);
    }

    [Test]
    public void ConfigReifier_SetFieldsOnObject_CastedObject() {
        var tc = (object)new TestClass();
        var doc = Config.LoadDocFromString(
            @"---
            intKey: 99077
            "
            , "ConfigReifierFacts_ReifyString_TestFilename");
        ConfigReifier.SetFieldsOnObject(ref tc, doc);
        Assert.AreEqual(((TestClass)tc).intKey, 99077);
    }

    [Test]
    public void ConfigReifier_SetFieldsOnStruct_TemplatedStructCall() {
        var s = new ChildStruct();
        s.childIntKey = 1;
        s.childFloatKey = 1;
        var doc = Config.LoadDocFromString(
            @"---
            childIntKey: 12345
            "
            , "ConfigReifierFacts_ReifyString_TestFilename");
        ConfigReifier.SetFieldsOnStruct(ref s, doc);
        Assert.AreEqual(s.childIntKey, 12345);
        Assert.AreEqual(s.childFloatKey, 1);
    }

    [Test]
    public void ConfigReifier_SetFieldsOnObject_BoxedStructArgument() {
        var s = new ChildStruct();
        s.childIntKey = 1;
        s.childFloatKey = 1;
        var doc = Config.LoadDocFromString(
            @"---
            childIntKey: 34567
            "
            , "ConfigReifierFacts_ReifyString_TestFilename");
        object os = (object)s;
        ConfigReifier.SetFieldsOnObject(ref os, doc);
        Assert.AreEqual(((ChildStruct)os).childIntKey, 34567);
        Assert.AreEqual(((ChildStruct)os).childFloatKey, 1);
    }

    [Test]
    public void ReifiesStatic_Class() {
        var doc = Config.LoadDocFromString(
            @"---
            staticStringKey: arbitrage
            staticIntArrKey: [4, 4, 0, 0]
            intKey: 10   # test non-static fields
            "
            , "ConfigReifierFacts_ReifyString_TestFilename");
        ConfigReifier.ReifyStatic<TestClass>(doc);
        Assert.AreEqual(TestClass.staticStringKey, "arbitrage");
        Assert.AreEqual(TestClass.staticIntArrKey, new int[] {4, 4, 0, 0});
    }

    [Test]
    public void ReifiesStatic_IgnoresNonStaticFields() {
        var doc = Config.LoadDocFromString(
            @"---
            intKey: 10   # try to bogusly set a non-static field
            "
            , "ConfigReifierFacts_ReifyString_TestFilename");

        // passes if there are no exceptions
        ConfigReifier.ReifyStatic<TestClass>(doc);
    }

    [Test]
    public void ReifiesStatic_Struct() {
        var doc = Config.LoadDocFromString(
            @"---
            staticIntKey: 3049
            "
            , "ConfigReifierFacts_ReifyString_TestFilename");
        ConfigReifier.ReifyStatic<ChildStruct>(doc);
        Assert.AreEqual(ChildStruct.staticIntKey, 3049);
    }

    [Test]
    public void ReifiesStatic_StaticClass() {
        var doc = Config.LoadDocFromString(
            @"---
            staticStringList: [herp, derp]
            "
            , "ConfigReifierFacts_ReifyString_TestFilename");
        ConfigReifier.ReifyStatic(typeof(PureStatic), doc);
        Assert.AreEqual(PureStatic.staticStringList[0], "herp");
        Assert.AreEqual(PureStatic.staticStringList[1], "derp");
    }

    [Test]
    public void ReifiesSingle_CreateSingleFieldClass() {
        var doc = Config.LoadDocFromString(
            @"---
            8342
            "
            , "ConfigReifierFacts_ReifySingle_TestFilename");
        var inst = ConfigReifier.CreateInstance<SingleFieldClass>(doc);
        Assert.AreEqual(inst.SingleField, 8342);
    }

    [Test]
    public void ReifiesSingle_CreateSingleListClass() {
        var doc = Config.LoadDocFromString(
            @"---
            [a, b, c, d]
            "
            , "ConfigReifierFacts_ReifySingle_TestFilename");
        var inst = ConfigReifier.CreateInstance<SingleListClass>(doc);
        Assert.AreEqual(inst.SingleList[0], "a");
        Assert.AreEqual(inst.SingleList[1], "b");
        Assert.AreEqual(inst.SingleList[2], "c");
        Assert.AreEqual(inst.SingleList[3], "d");
    }

    [Test]
    public void RefiesExtraFields_NoException() {
        var doc = Config.LoadDocFromString(@"---
            floatKey: 1.56
        ", "ConfigReifier_ReifiesExtraFields_TestFilename");
        var inst = ConfigReifier.CreateInstance<TestClass>(doc, ConfigOptions.AllowMissingFields);
        Assert.AreEqual(inst.floatKey, 1.56f);
    }

    [Test]
    public void RefiesExtraFields_Raises() {
        var doc = Config.LoadDocFromString(@"---
            floatKey: 1.56
            extraKey1: derp
            extraKey2: herp
        ", "ConfigReifier_ReifiesExtraFields_TestFilename");
        var ex = Assert.Throws<ParseException>(
            () => {
                ConfigReifier.CreateInstance<TestClass>(doc, ConfigOptions.CaseSensitive);
            });
        // check that it found all the extra keys
        Assert.True(ex.Message.IndexOf("extraKey1") >= 0);
        Assert.True(ex.Message.IndexOf("extraKey2") >= 0);
    }

    [Test]
    public void RefiesMissingFields_NoException() {
        var doc = Config.LoadDocFromString(@"---
            childIntKey: 42
            childFloatKey: 1.25
            staticIntKey: 332
        ", "ConfigReifier_ReifiesMissingFields_TestFilename");
        var inst = ConfigReifier.CreateInstance<ChildStruct>(doc, ConfigOptions.None);
        Assert.AreEqual(inst.childIntKey, 42);
        Assert.AreEqual(inst.childFloatKey, 1.25);
        Assert.AreEqual(ChildStruct.staticIntKey, 332);
    }

    [Test]
    public void RefiesMissingFields_Raises() {
        var doc = Config.LoadDocFromString(@"---
            childIntKey: 32
        ", "ConfigReifier_ReifiesMissingFields_TestFilename");
        var ex = Assert.Throws<ParseException>(
            () => {
                ConfigReifier.CreateInstance<ChildStruct>(doc, ConfigOptions.CaseSensitive);
            });
        // check that it found all the missing keys
        Assert.True(ex.Message.IndexOf("childFloatKey") >= 0);
        Assert.True(ex.Message.IndexOf("staticIntKey") >= 0);
    }

    [Test]
    public void RefiesCaseInsensitive() {
        var doc = Config.LoadDocFromString(@"---
            childintkey: 32
            CHILDFLOATKEY: 11
            StaticiNtKey: 5
        ", "ConfigReifier_ReifiesCaseInsensitive_TestFilename");
        var inst = ConfigReifier.CreateInstance<ChildStruct>(doc, ConfigOptions.None);
        Assert.AreEqual(inst.childIntKey, 32);
        Assert.AreEqual(inst.childFloatKey, 11);
        Assert.AreEqual(ChildStruct.staticIntKey, 5);
    }

    [Test]
    public void RefiesCaseInsensitive_Missing() {
        var doc = Config.LoadDocFromString(@"---
            childintkey: 32
        ", "ConfigReifier_ReifiesCaseInsensitive_TestFilename");
        var inst = ConfigReifier.CreateInstance<ChildStruct>(doc, ConfigOptions.AllowMissingFields);
        Assert.AreEqual(inst.childIntKey, 32);
    }

    [Test]
    public void ReifierAttributes_Mandatory_AllowsSetting() {
        var doc = Config.LoadDocFromString(@"---
            Mandatory: 10
            AllowedMissing: derp
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = ConfigReifier.CreateInstance<AttributesClass>(doc);
        Assert.AreEqual(inst.Mandatory, 10);
        Assert.AreEqual(inst.AllowedMissing, "derp");
        Assert.AreEqual(inst.Ignored, false);
    }

    [Test]
    public void ReifierAttributes_Mandatory_ExceptsIfNotSet() {
        var doc = Config.LoadDocFromString(@"---
            AllowedMissing: derp
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        Assert.Throws<ParseException>(
            () => {
                ConfigReifier.CreateInstance<AttributesClass>(doc, ConfigOptions.AllowMissingFields);
            });
    }

    [Test]
    public void ReifierAttributes_AllowedMissing_NoExceptionIfMissing() {
        var doc = Config.LoadDocFromString(@"---
            Mandatory: 15
            MissingOrNotDependingOnDefault: true
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = ConfigReifier.CreateInstance<AttributesClass>(doc, ConfigOptions.None);
        Assert.AreEqual(inst.Mandatory, 15);
        Assert.AreEqual(inst.AllowedMissing, "initial");
        Assert.AreEqual(inst.Ignored, false);
        Assert.AreEqual(inst.MissingOrNotDependingOnDefault, "true");
    }

    [Test]
    public void ReifierAttributes_AllowedMissing_ByDefaultInClassWithMandatory() {
        var doc = Config.LoadDocFromString(@"---
            Mandatory: 15
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = ConfigReifier.CreateInstance<AttributesClass>(doc, ConfigOptions.AllowMissingFields);
        Assert.AreEqual(inst.Mandatory, 15);
        Assert.AreEqual(inst.AllowedMissing, "initial");
        Assert.AreEqual(inst.MissingOrNotDependingOnDefault, "initial2");
    }

    [Test]
    public void ReifierAttributes_Ignore_NotSet() {
        var doc = Config.LoadDocFromString(@"---
            Mandatory: 101
            AllowedMissing: herp
            Ignored: true
            MissingOrNotDependingOnDefault: whut
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = ConfigReifier.CreateInstance<AttributesClass>(doc, ConfigOptions.None);
        Assert.AreEqual(inst.Mandatory, 101);
        Assert.AreEqual(inst.AllowedMissing, "herp");
        Assert.AreEqual(inst.Ignored, false);
        Assert.AreEqual(inst.MissingOrNotDependingOnDefault, "whut");
    }

    [Test]
    public void ReifierAttributes_Ignore_MissingIgnored() {
        var doc = Config.LoadDocFromString(@"---
            Mandatory: 102
            AllowedMissing: herpe
            MissingOrNotDependingOnDefault: whip
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = ConfigReifier.CreateInstance<AttributesClass>(doc, ConfigOptions.None);
        Assert.AreEqual(inst.Mandatory, 102);
        Assert.AreEqual(inst.AllowedMissing, "herpe");
        Assert.AreEqual(inst.Ignored, false);
        Assert.AreEqual(inst.MissingOrNotDependingOnDefault, "whip");
    }

    [Test]
    public void ReifierAttributes_MandatoryClass_AcceptsSetting() {
        var doc = Config.LoadDocFromString(@"---
            intField: 10
            stringField: uh
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = ConfigReifier.CreateInstance<MandatoryClass>(doc, ConfigOptions.None);
        Assert.AreEqual(inst.intField, 10);
        Assert.AreEqual(inst.stringField, "uh");
        Assert.AreEqual(inst.ignoreField, "initialignore");
    }

    [Test]
    public void ReifierAttributes_MandatoryClass_FailsOnMissing() {
        var doc = Config.LoadDocFromString(@"---
            stringField: uh
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        Assert.Throws<ParseException>(
            () => {
                ConfigReifier.CreateInstance<MandatoryClass>(doc, ConfigOptions.None);
            });
    }

    [Test]
    public void ReifierAttributes_MandatoryClass_OverridesOptions() {
        var doc = Config.LoadDocFromString(@"---
            stringField: uh
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        Assert.Throws<ParseException>(
            () => {
                ConfigReifier.CreateInstance<MandatoryClass>(doc, ConfigOptions.AllowMissingFields);
            });
    }

    [Test]
    public void ReifierAttributes_MandatoryClass_AllowsMissingField() {
        var doc = Config.LoadDocFromString(@"---
            intField: 99
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = ConfigReifier.CreateInstance<MandatoryClass>(doc, ConfigOptions.None);
        Assert.AreEqual(inst.intField, 99);
        Assert.AreEqual(inst.stringField, "initial");
        Assert.AreEqual(inst.ignoreField, "initialignore");
    }

    [Test]
    public void ReifierAttributes_AllowMissingClass_AcceptsSetting() {
        var doc = Config.LoadDocFromString(@"---
            stringField: hmm
            listField: [1]
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = ConfigReifier.CreateInstance<AllowMissingClass>(doc, ConfigOptions.None);
        Assert.AreEqual(inst.stringField, "hmm");
        Assert.AreEqual(inst.listField[0], "1");
    }

    [Test]
    public void ReifierAttributes_AllowMissingClass_AllowsMissing() {
        var doc = Config.LoadDocFromString(@"---
            stringField: wot
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = ConfigReifier.CreateInstance<AllowMissingClass>(doc, ConfigOptions.AllowMissingFields);
        Assert.AreEqual(inst.stringField, "wot");
        Assert.AreEqual(inst.listField, null);
    }

    [Test]
    public void ReifierAttributes_AllowMissingClass_OverridesOptions() {
        var doc = Config.LoadDocFromString(@"---
            stringField: wot
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = ConfigReifier.CreateInstance<AllowMissingClass>(doc, ConfigOptions.None);
        Assert.AreEqual(inst.stringField, "wot");
        Assert.AreEqual(inst.listField, null);
    }

    [Test]
    public void ReifierAttributes_AllowMissingClass_ChecksMandatoryField() {
        var doc = Config.LoadDocFromString(@"---
            listField: [a,b]
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        Assert.Throws<ParseException>(
            () => {
                ConfigReifier.CreateInstance<AllowMissingClass>(doc, ConfigOptions.AllowMissingFields);
            });
    }

    [Test]
    public void ReifierAttributes_AllowMissingClass_DoesCheckExtraFieldsToo() {
        var doc = Config.LoadDocFromString(@"---
            stringField: hi
            extra_field: 33333
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        Assert.Throws<ParseException>(
            () => {
                ConfigReifier.CreateInstance<AllowMissingClass>(doc, ConfigOptions.AllowMissingFields);
            });
    }

    [Test]
    public void ReifierAttributes_MonoBehaviour_ForcesAllowMissing() {
        var doc = Config.LoadDocFromString(@"---
            field1: 1
        ", "ConfigReifier_ReifierAttributes_TestFilename");

        var obj = new GameObject("Test_ReifierAttributes");
        var mb = obj.AddComponent<MonoBehaviourSubclass>();
        ConfigReifier.Reify(ref mb, doc, ConfigOptions.None);
        Assert.AreEqual(mb.field1, 1);

        Object.DestroyImmediate(obj);
    }

    [Test]
    public void Reify_DocNode() {
        var doc = Config.LoadDocFromString(@"---
            ugh: bugh
        ", "ConfigReifier_DocNode_TestFilename");

        DocNode d = null;
        ConfigReifier.Reify(ref d, doc);
        Assert.True(d.ContainsKey("ugh"));
        Assert.AreEqual(d["ugh"].AsString(), "bugh");
    }

    [Test]
    public void ParseException_EmptyEnum() {
        Assert.Throws<ParseException>(() => {
            ReifyString<TestClass>("enumKey: \"\"");
        });
    }

    [Test]
    public void ParseException_BadInt() {
        Assert.Throws<ParseException>(() => {
            ReifyString<TestClass>("intKey: incorrect");
        });
    }

    [Test]
    public void ParseException_BadBool() {
        Assert.Throws<ParseException>(() => {
            ReifyString<TestClass>("boolKeyDefaultFalse: incorrect");
        });
    }

    [Test]
    public void EmptyDoc_ReturnsDocNode() {
        var doc = Config.LoadDocFromString("", "EmptyDoc");
        Assert.IsNotNull(doc);
        Assert.IsTrue(doc is DocNode);
    }
    
    [Test]
    public void EmptyDoc_Stream_ReturnsDocNode() {
        var doc = Config.LoadDocFromStream(new System.IO.MemoryStream(), "EmptyDoc");
        Assert.IsNotNull(doc);
        Assert.IsTrue(doc is DocNode);
    }
}
