using UnityEditor;
using DarkConfig;

public static class DemoEditorMenus {
    [MenuItem("Assets/DarkConfig/Autogenerate Index")]
    static void MenuGenerateIndex() {
        EditorUtils.GenerateIndex("/Demo/Resources/Configs");
        AssetDatabase.Refresh();
    }
}