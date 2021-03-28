using UnityEngine;
using UnityEditor;
using System.Collections;
using DarkConfig;
using System.Threading;

public class DemoEditorMenus {
    public static string GetRelPath() {
        return "/Demo/Resources/Configs";
    }

    public static string GetFilePath() {
        return Application.dataPath + GetRelPath();
    }

    [MenuItem("Assets/DarkConfig/Autogenerate Index")]
    static void MenuGenerateIndex() {
        EditorUtils.GenerateIndex(GetRelPath());
        AssetDatabase.Refresh();
    }
}