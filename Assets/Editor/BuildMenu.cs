using UnityEditor;
using UnityEngine;

public class VersionManager
{
    [MenuItem("Build/Check Current Version", false, 2)]
    private static void CheckCurrentVersion()
    {
        Debug.Log("Build v" + PlayerSettings.bundleVersion +
            " (" + PlayerSettings.Android.bundleVersionCode + ")");
    }

    [MenuItem("Build/Increase Major Version", false, 51)]
    private static void IncreaseMajor()
    {
        string[] lines = PlayerSettings.bundleVersion.Split('.');
        EditVersion(1, -int.Parse(lines[1]), -int.Parse(lines[2]));
    }

    [MenuItem("Build/Increase Minor Version", false, 52)]
    private static void IncreaseMinor()
    {
        string[] lines = PlayerSettings.bundleVersion.Split('.');
        EditVersion(0, 1, -int.Parse(lines[2]));
    }

    [MenuItem("Build/Increase Patch Version", false, 53)]
    private static void IncreasePatch()
    {
        EditVersion(0, 0, 1);
    }

    static void EditVersion(int majorIncr, int minorIncr, int buildIncr)
    {
        string[] lines = PlayerSettings.bundleVersion.Split('.');

        int MajorVersion = int.Parse(lines[0]) + majorIncr;
        int MinorVersion = int.Parse(lines[1]) + minorIncr;
        int Build = int.Parse(lines[2]) + buildIncr;

        PlayerSettings.bundleVersion = MajorVersion.ToString("0") + "." +
                                       MinorVersion.ToString("0") + "." +
                                       Build.ToString("0");
        PlayerSettings.Android.bundleVersionCode =
            MajorVersion * 10000 + MinorVersion * 1000 + Build;
        // 빌드 완료 뒤 자동으로 다음 번호를 쓰면 방금 만든 플레이어와 ReleaseStates 폴더가
        // 서로 다른 버전을 기록한다. 릴리스 번호는 빌드 전에 명시적으로 바꾸고 즉시 저장한다.
        AssetDatabase.SaveAssets();
        CheckCurrentVersion();
    }
}
