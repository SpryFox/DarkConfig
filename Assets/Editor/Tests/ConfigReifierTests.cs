using System;
using NUnit.Framework;
using DarkConfig;
using DarkConfig.Internal;
using System.Collections.Generic;

[TestFixture]
class ConfigReifierTests {
    // disable variable unused in function body warnings; there's a lot in here
#pragma warning disable 168
    enum TestEnum {
        Primi,
        Secondi
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
        [ConfigMandatory] public int Mandatory = -1;

        [ConfigAllowMissing] public string AllowedMissing = "initial";

        [ConfigIgnore] public bool Ignored = false;

        public string MissingOrNotDependingOnDefault = "initial2";
    }

    [ConfigMandatory]
    class MandatoryClass {
        public int intField = -1;

        [ConfigAllowMissing] public string stringField = "initial";

        [ConfigIgnore] public string ignoreField = "initialignore";
    }

    [ConfigAllowMissing]
    class AllowMissingClass {
        [ConfigMandatory] public string stringField = "init";

        public List<string> listField = null;
    }

    class PropertiesClass {
        [ConfigIgnore]
        public int backing3Int = 3;
        
        // Normal auto-property
        public int int1Value { get; set; } = 1;
        
        // Get-only auto-property
        public int int2Value { get; } = 2;
        
        // Set-only property
        public int int3Value { set { backing3Int = value; } }
        
        // Computed property
        public int int4Value => 4;

        // Static property
        public static string staticStringValue { get; set; } = "static str";
        
        [ConfigIgnore]
        public string ignoredValue { get; set; }

        [ConfigAllowMissing]
        public string allowMissing { get; set; } = "missing";
        
        [ConfigMandatory]
        public string mandatoryValue { get; set; }
    }
    
    [Test]
    public void ReifierAttributes_PropertiesClass() {
        var doc = Config.LoadDocFromString(@"---
            int1Value: 10
            staticStringValue: newValue
            mandatoryValue: mandatory
        ", "ReifierAttributes_PropertiesClass_AcceptsSetting");
        var inst = Activator.CreateInstance<PropertiesClass>();
        ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.None);
        Assert.AreEqual(inst.int1Value, 10);
        Assert.AreEqual(inst.int2Value, 2);
        Assert.AreEqual(inst.backing3Int, 3);
        Assert.AreEqual(inst.int4Value, 4);
        Assert.AreEqual(PropertiesClass.staticStringValue, "newValue");
        Assert.AreEqual(inst.allowMissing, "missing");
        Assert.AreEqual(inst.mandatoryValue, "mandatory");
    }
    

    ConfigOptions defaults;

    [SetUp]
    public void DoSetup() {
        // needs to be called here because we can't be sure whether preload has been called before or not
        DarkConfig.Internal.BuiltInTypeRefiers.RegisterAll(); 
        
        UnityTypeReifiers.RegisterAll();
        defaults = DarkConfig.Settings.DefaultReifierOptions;
        DarkConfig.Settings.DefaultReifierOptions = ConfigOptions.AllowMissingExtraFields;
    }

    [TearDown]
    public void TearDown() {
        DarkConfig.Settings.DefaultReifierOptions = defaults;
    }

    T ReifyString<T>(string str) where T : new() {
        var doc = Config.LoadDocFromString(str, "ConfigReifierTests_ReifyString_TestFilename");
        T tc = default(T);
        Config.Reify(ref tc, doc);
        return tc;
    }

    T UpdateFromString<T>(ref T obj, string str) {
        var doc = Config.LoadDocFromString(str, "ConfigReifierTests_UpdateFromString_TestFilename");
        Config.Reify(ref obj, doc);
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
        Assert.AreEqual(tc.byteKey, (byte) 55);
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
        var instance = ReifyString<TestClass>(@"---
            listIntKey: [0, 1, 2, 3, 4]
            ");
        Assert.AreEqual(instance.listIntKey, new[] {0, 1, 2, 3, 4});
    }

    [Test]
    public void ConfigReifier_SetsArrayOfInts() {
        var instance = ReifyString<TestClass>(@"---
            arrayIntKey: [0, 1, 2, 3, 4]
            ");
        Assert.AreEqual(instance.arrayIntKey, new[] {0, 1, 2, 3, 4});
    }

    [Test]
    public void ConfigReifier_Array_UpdatesInPlace() {
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
    public void ConfigReifier_Array_UpdatesInPlace_AddItems() {
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
    public void ConfigReifier_Array_UpdatesInPlace_RemoveItems() {
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
    public void ConfigReifier_SetsArray2DOfFloats() {
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
    public void ConfigReifier_SetsArray3DOfFloats() {
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
    public void ConfigReifier_Array2D_UpdatesInPlace() {
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
    public void ConfigReifier_Array2D_UpdatesInPlace_AddItems() {
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
    public void ConfigReifier_Array2D_UpdatesInPlace_RemoveItems() {
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
        o.nestedList.Add(new TestClass {intKey = 78, floatKey = 1.2f});
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
        o.nestedList.Add(new TestClass {intKey = 62, floatKey = 4.56f});
        o.nestedList.Add(new TestClass {intKey = 1234, floatKey = 8.98f});
        o.nestedList.Add(new TestClass {intKey = 392, floatKey = 44.55f});
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
        var saved = new TestClass {intKey = 101};
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
        var saved = new TestClass {intKey = 101};
        o[TestEnum.Primi] = saved;
        o[TestEnum.Secondi] = new TestClass {intKey = 1200};

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
        o.nestedDict["dictKey"] = new TestClass {intKey = 22, floatKey = 4.56f};
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
    public void ConfigReifier_NestedDict_AddsNewPairs() {
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
    public void ConfigReifier_NestedDict_RemovesMissingPairs() {
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
    public void ConfigReifier_SetFieldsOnObject_PlainObject() {
        var tc = new TestClass();
        var doc = Config.LoadDocFromString(
            @"---
            intKey: 99088
            "
            , "ConfigReifierTests_ReifyString_TestFilename");
        ConfigReifier.SetFieldsOnObject(ref tc, doc);
        Assert.AreEqual(tc.intKey, 99088);
    }

    [Test]
    public void ConfigReifier_SetFieldsOnObject_CastedObject() {
        var tc = (object) new TestClass();
        var doc = Config.LoadDocFromString(
            @"---
            intKey: 99077
            "
            , "ConfigReifierTests_ReifyString_TestFilename");
        ConfigReifier.SetFieldsOnObject(ref tc, doc);
        Assert.AreEqual(((TestClass) tc).intKey, 99077);
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
            , "ConfigReifierTests_ReifyString_TestFilename");
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
            , "ConfigReifierTests_ReifyString_TestFilename");
        object os = (object) s;
        ConfigReifier.SetFieldsOnObject(ref os, doc);
        Assert.AreEqual(((ChildStruct) os).childIntKey, 34567);
        Assert.AreEqual(((ChildStruct) os).childFloatKey, 1);
    }

    [Test]
    public void ReifiesStatic_Class() {
        var doc = Config.LoadDocFromString(
            @"---
            staticStringKey: arbitrage
            staticIntArrKey: [4, 4, 0, 0]
            intKey: 10   # test non-static fields
            "
            , "ConfigReifierTests_ReifyString_TestFilename");
        Config.ReifyStatic<TestClass>(doc);
        Assert.AreEqual(TestClass.staticStringKey, "arbitrage");
        Assert.AreEqual(TestClass.staticIntArrKey, new int[] {4, 4, 0, 0});
    }

    [Test]
    public void ReifiesStatic_IgnoresNonStaticFields() {
        var doc = Config.LoadDocFromString(
            @"---
            intKey: 10   # try to bogusly set a non-static field
            "
            , "ConfigReifierTests_ReifyString_TestFilename");

        // passes if there are no exceptions
        Config.ReifyStatic<TestClass>(doc);
    }

    [Test]
    public void ReifiesStatic_Struct() {
        var doc = Config.LoadDocFromString(
            @"---
            staticIntKey: 3049
            "
            , "ConfigReifierTests_ReifyString_TestFilename");
        Config.ReifyStatic<ChildStruct>(doc);
        Assert.AreEqual(ChildStruct.staticIntKey, 3049);
    }

    [Test]
    public void ReifiesStatic_StaticClass() {
        var doc = Config.LoadDocFromString(
            @"---
            staticStringList: [herp, derp]
            "
            , "ConfigReifierTests_ReifyString_TestFilename");
        Config.ReifyStatic(typeof(PureStatic), doc);
        Assert.AreEqual(PureStatic.staticStringList[0], "herp");
        Assert.AreEqual(PureStatic.staticStringList[1], "derp");
    }

    [Test]
    public void ReifiesSingle_CreateSingleFieldClass() {
        var doc = Config.LoadDocFromString(
            @"---
            8342
            "
            , "ConfigReifierTests_ReifySingle_TestFilename");
        var inst = Activator.CreateInstance<SingleFieldClass>();
        ConfigReifier.SetFieldsOnObject(ref inst, doc);
        Assert.AreEqual(inst.SingleField, 8342);
    }

    [Test]
    public void ReifiesSingle_CreateSingleListClass() {
        var doc = Config.LoadDocFromString(
            @"---
            [a, b, c, d]
            "
            , "ConfigReifierTests_ReifySingle_TestFilename");
        var inst = Activator.CreateInstance<SingleListClass>();
        ConfigReifier.SetFieldsOnObject(ref inst, doc);
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
        var inst = Activator.CreateInstance<TestClass>();
        ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.AllowMissingFields);
        Assert.AreEqual(inst.floatKey, 1.56f);
    }

    [Test]
    public void RefiesExtraFields_Raises() {
        var doc = Config.LoadDocFromString(@"---
            floatKey: 1.56
            extraKey1: derp
            extraKey2: herp
        ", "ConfigReifier_ReifiesExtraFields_TestFilename");
        var exception = Assert.Throws<ExtraFieldsException>(() => {
            var inst = Activator.CreateInstance<TestClass>();
            ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.CaseSensitive);
        });
        // check that it found all the extra keys
        Assert.True(exception.Message.IndexOf("extraKey1", StringComparison.Ordinal) >= 0);
        Assert.True(exception.Message.IndexOf("extraKey2", StringComparison.Ordinal) >= 0);
    }

    [Test]
    public void RefiesMissingFields_NoException() {
        var doc = Config.LoadDocFromString(@"---
            childIntKey: 42
            childFloatKey: 1.25
            staticIntKey: 332
        ", "ConfigReifier_ReifiesMissingFields_TestFilename");
        var inst = Activator.CreateInstance<ChildStruct>();
        ConfigReifier.SetFieldsOnStruct(ref inst, doc, ConfigOptions.None);
        Assert.AreEqual(inst.childIntKey, 42);
        Assert.AreEqual(inst.childFloatKey, 1.25);
        Assert.AreEqual(ChildStruct.staticIntKey, 332);
    }

    [Test]
    public void RefiesMissingFields_Raises() {
        var doc = Config.LoadDocFromString(@"---
            childIntKey: 32
        ", "ConfigReifier_ReifiesMissingFields_TestFilename");
        var exception = Assert.Throws<MissingFieldsException>(() => {
            var inst = Activator.CreateInstance<ChildStruct>();
            ConfigReifier.SetFieldsOnStruct(ref inst, doc, ConfigOptions.CaseSensitive);
        });
        // check that it found all the missing keys
        Assert.True(exception.Message.IndexOf("childFloatKey", StringComparison.Ordinal) >= 0);
        Assert.True(exception.Message.IndexOf("staticIntKey", StringComparison.Ordinal) >= 0);
    }

    [Test]
    public void RefiesCaseInsensitive() {
        var doc = Config.LoadDocFromString(@"---
            childintkey: 32
            CHILDFLOATKEY: 11
            StaticiNtKey: 5
        ", "ConfigReifier_ReifiesCaseInsensitive_TestFilename");
        var inst = Activator.CreateInstance<ChildStruct>();
        ConfigReifier.SetFieldsOnStruct(ref inst, doc, ConfigOptions.None);
        Assert.AreEqual(inst.childIntKey, 32);
        Assert.AreEqual(inst.childFloatKey, 11);
        Assert.AreEqual(ChildStruct.staticIntKey, 5);
    }

    [Test]
    public void RefiesCaseInsensitive_Missing() {
        var doc = Config.LoadDocFromString(@"---
            childintkey: 32
        ", "ConfigReifier_ReifiesCaseInsensitive_TestFilename");
        var inst = Activator.CreateInstance<ChildStruct>();
        ConfigReifier.SetFieldsOnStruct(ref inst, doc, ConfigOptions.AllowMissingFields);
        Assert.AreEqual(inst.childIntKey, 32);
    }

    [Test]
    public void ReifierAttributes_Mandatory_AllowsSetting() {
        var doc = Config.LoadDocFromString(@"---
            Mandatory: 10
            AllowedMissing: derp
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = Activator.CreateInstance<AttributesClass>();
        ConfigReifier.SetFieldsOnObject(ref inst, doc);
        Assert.AreEqual(inst.Mandatory, 10);
        Assert.AreEqual(inst.AllowedMissing, "derp");
        Assert.AreEqual(inst.Ignored, false);
    }

    [Test]
    public void ReifierAttributes_Mandatory_ExceptsIfNotSet() {
        var doc = Config.LoadDocFromString(@"---
            AllowedMissing: derp
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        Assert.Throws<MissingFieldsException>(() => {
            var inst = Activator.CreateInstance<AttributesClass>();
            ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.AllowMissingFields);
        });
    }

    [Test]
    public void ReifierAttributes_AllowedMissing_NoExceptionIfMissing() {
        var doc = Config.LoadDocFromString(@"---
            Mandatory: 15
            MissingOrNotDependingOnDefault: true
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = Activator.CreateInstance<AttributesClass>();
        ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.None);
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
        var inst = Activator.CreateInstance<AttributesClass>();
        ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.AllowMissingFields);
        Assert.AreEqual(inst.Mandatory, 15);
        Assert.AreEqual(inst.AllowedMissing, "initial");
        Assert.AreEqual(inst.MissingOrNotDependingOnDefault, "initial2");
    }

    [Test]
    public void ReifierAttributes_Ignore_SpecifyingFailsOnExtras() {
        var doc = Config.LoadDocFromString(@"---
            Mandatory: 101
            AllowedMissing: herp
            Ignored: true
            MissingOrNotDependingOnDefault: whut
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        Assert.Throws<ExtraFieldsException>(() => {
            var inst = Activator.CreateInstance<AttributesClass>();
            ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.None);
        });
    }

    [Test]
    public void ReifierAttributes_Ignore_MissingIgnored() {
        var doc = Config.LoadDocFromString(@"---
            Mandatory: 102
            AllowedMissing: herpe
            MissingOrNotDependingOnDefault: whip
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = Activator.CreateInstance<AttributesClass>();
        ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.None);
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
        var inst = Activator.CreateInstance<MandatoryClass>();
        ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.None);
        Assert.AreEqual(inst.intField, 10);
        Assert.AreEqual(inst.stringField, "uh");
        Assert.AreEqual(inst.ignoreField, "initialignore");
    }

    [Test]
    public void ReifierAttributes_MandatoryClass_FailsOnMissing() {
        var doc = Config.LoadDocFromString(@"---
            stringField: uh
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        Assert.Throws<MissingFieldsException>(() => {
            var inst = Activator.CreateInstance<MandatoryClass>();
            ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.None);
        });
    }

    [Test]
    public void ReifierAttributes_MandatoryClass_OverridesOptions() {
        var doc = Config.LoadDocFromString(@"---
            stringField: uh
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        Assert.Throws<MissingFieldsException>(() => {
            var inst = Activator.CreateInstance<MandatoryClass>();
            ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.AllowMissingFields);
        });
    }

    [Test]
    public void ReifierAttributes_MandatoryClass_AllowsMissingField() {
        var doc = Config.LoadDocFromString(@"---
            intField: 99
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = Activator.CreateInstance<MandatoryClass>();
        ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.None);
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
        var inst = Activator.CreateInstance<AllowMissingClass>();
        ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.None);
        Assert.AreEqual(inst.stringField, "hmm");
        Assert.AreEqual(inst.listField[0], "1");
    }

    [Test]
    public void ReifierAttributes_AllowMissingClass_AllowsMissing() {
        var doc = Config.LoadDocFromString(@"---
            stringField: wot
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = Activator.CreateInstance<AllowMissingClass>();
        ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.AllowMissingFields);
        Assert.AreEqual(inst.stringField, "wot");
        Assert.AreEqual(inst.listField, null);
    }

    [Test]
    public void ReifierAttributes_AllowMissingClass_OverridesOptions() {
        var doc = Config.LoadDocFromString(@"---
            stringField: wot
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        var inst = Activator.CreateInstance<AllowMissingClass>();
        ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.None);
        Assert.AreEqual(inst.stringField, "wot");
        Assert.AreEqual(inst.listField, null);
    }

    [Test]
    public void ReifierAttributes_AllowMissingClass_ChecksMandatoryField() {
        var doc = Config.LoadDocFromString(@"---
            listField: [a,b]
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        Assert.Throws<MissingFieldsException>(() => {
            var inst = Activator.CreateInstance<AllowMissingClass>();
            ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.AllowMissingFields);
        });
    }

    [Test]
    public void ReifierAttributes_AllowMissingClass_DoesCheckExtraFieldsToo() {
        var doc = Config.LoadDocFromString(@"---
            stringField: hi
            extra_field: 33333
        ", "ConfigReifier_ReifierAttributes_TestFilename");
        Assert.Throws<ExtraFieldsException>(() => {
            var inst = Activator.CreateInstance<AllowMissingClass>();
            ConfigReifier.SetFieldsOnObject(ref inst, doc, ConfigOptions.AllowMissingFields);
        });
    }

    [Test]
    public void ParseException_EmptyEnum() {
        Assert.Throws<ParseException>(() => { ReifyString<TestClass>("enumKey: \"\""); });
    }

    [Test]
    public void ParseException_BadInt() {
        Assert.Throws<ParseException>(() => { ReifyString<TestClass>("intKey: incorrect"); });
    }

    [Test]
    public void ParseException_BadBool() {
        Assert.Throws<ParseException>(() => { ReifyString<TestClass>("boolKeyDefaultFalse: incorrect"); });
    }

    [Test]
    public void EmptyDoc_ReturnsDocNode() {
        var doc = Config.LoadDocFromString("", "EmptyDoc");
        Assert.IsNotNull(doc);
        Assert.IsInstanceOf<DocNode>(doc);
    }

    [Test]
    public void EmptyDoc_Stream_ReturnsDocNode() {
        var doc = Config.LoadDocFromStream(new System.IO.MemoryStream(), "EmptyDoc");
        Assert.IsNotNull(doc);
        Assert.IsInstanceOf<DocNode>(doc);
    }
}