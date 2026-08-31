using System;
using UnityEditor;

public static class SqlitePluginConfiguration
{
    public const string AssetPath = "Assets/Plugins/SQLite/sqlite3.dll";

    [MenuItem("Puzzle/Configure SQLite Plugin")]
    public static void Configure()
    {
        PluginImporter importer = RequireImporter();
        importer.SetCompatibleWithAnyPlatform(false);
        importer.SetCompatibleWithEditor(true);
        importer.SetEditorData("CPU", "x86_64");
        importer.SetEditorData("OS", "Windows");
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, false);
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, false);
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, false);
        importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
        importer.SetCompatibleWithPlatform(BuildTarget.iOS, false);
        importer.SetCompatibleWithPlatform(BuildTarget.WebGL, false);
        importer.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64");
        importer.SaveAndReimport();
        Validate();
    }

    public static void Validate()
    {
        PluginImporter importer = RequireImporter();
        if (importer.GetCompatibleWithAnyPlatform())
            throw new InvalidOperationException(
                "sqlite3.dll must not be compatible with every platform.");
        if (!importer.GetCompatibleWithEditor() ||
            importer.GetEditorData("CPU") != "x86_64" ||
            importer.GetEditorData("OS") != "Windows")
            throw new InvalidOperationException(
                "sqlite3.dll must target the Windows x86_64 editor.");
        if (!importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64) ||
            importer.GetPlatformData(BuildTarget.StandaloneWindows64, "CPU") != "x86_64")
            throw new InvalidOperationException(
                "sqlite3.dll must target Windows Standalone x86_64.");
        if (importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows) ||
            importer.GetCompatibleWithPlatform(BuildTarget.StandaloneLinux64) ||
            importer.GetCompatibleWithPlatform(BuildTarget.StandaloneOSX) ||
            importer.GetCompatibleWithPlatform(BuildTarget.Android) ||
            importer.GetCompatibleWithPlatform(BuildTarget.iOS) ||
            importer.GetCompatibleWithPlatform(BuildTarget.WebGL))
            throw new InvalidOperationException(
                "sqlite3.dll is enabled for an unsupported build target.");
    }

    private static PluginImporter RequireImporter()
    {
        if (!(AssetImporter.GetAtPath(AssetPath) is PluginImporter importer))
            throw new InvalidOperationException(
                $"SQLite native plugin '{AssetPath}' is missing or has the wrong importer.");
        return importer;
    }
}
