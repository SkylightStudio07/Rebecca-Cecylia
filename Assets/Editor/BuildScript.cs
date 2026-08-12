using System;
using System.Collections.Generic;
using RCCom.EditorTools;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Unity CLI와 배치 모드에서 동일하게 호출하는 플레이어 빌드 진입점.
/// Addressables 콘텐츠를 먼저 한 번 명시적으로 만들고 플레이어 빌드의 자동 중복 생성을 막는다.
/// </summary>
public static class BuildScript
{
    private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
    private const string DefenseScenePath = "Assets/Scenes/DefenseScene.unity";
    private const string WebGlOutputPath = "Builds/WebGL";
    private const string WindowsOutputPath = "Builds/Windows/RCCom.exe";

    public static void BuildWebGL()
    {
        BuildPlayer(BuildTarget.WebGL, WebGlOutputPath);
    }

    public static void BuildWindows()
    {
        BuildPlayer(BuildTarget.StandaloneWindows64, WindowsOutputPath);
    }

    public static void BuildStandaloneWindows64()
    {
        BuildWindows();
    }

    public static void ValidateWebGL()
    {
        ValidateConfiguration(BuildTarget.WebGL);
    }

    public static void ValidateWindows()
    {
        ValidateConfiguration(BuildTarget.StandaloneWindows64);
    }

    private static void BuildPlayer(BuildTarget target, string outputPath)
    {
        string[] scenes = ValidateConfiguration(target);
        if (EditorUserBuildSettings.activeBuildTarget != target)
        {
            throw new InvalidOperationException(
                $"활성 빌드 타깃은 {EditorUserBuildSettings.activeBuildTarget}입니다. " +
                $"Unity CLI의 --target {target} 옵션으로 먼저 전환하세요.");
        }

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        AddressableAssetSettings.PlayerBuildOption previousOption = settings.BuildAddressablesWithPlayerBuild;

        try
        {
            // 콘텐츠 생성 실패를 플레이어 빌드 결과와 섞지 않고 먼저 드러내며,
            // 이어지는 BuildPipeline 훅이 같은 콘텐츠를 다시 만들지 않게 한다.
            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult addressablesResult);
            if (!string.IsNullOrEmpty(addressablesResult.Error))
            {
                throw new InvalidOperationException($"Addressables 콘텐츠 빌드 실패: {addressablesResult.Error}");
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.DetailedBuildReport,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"{target} 플레이어 빌드 실패: {report.summary.result}, 오류 {report.summary.totalErrors}건");
            }

            Debug.Log($"[BuildScript] {target} 빌드 완료: {outputPath} ({report.summary.totalSize} bytes)");
        }
        finally
        {
            // 빌드 메서드 실행 전 프로젝트 설정을 그대로 돌려놓아 에디터에서 다음 수동 빌드의
            // 동작을 바꾸지 않는다. 같은 값으로 복원되므로 별도 SaveAssets도 필요 없다.
            settings.BuildAddressablesWithPlayerBuild = previousOption;
        }
    }

    private static string[] ValidateConfiguration(BuildTarget target)
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Play Mode에서는 빌드 또는 빌드 검증을 실행할 수 없습니다.");
        }

        AddressablesBuildValidator.ValidateOrThrow(target);

        var scenes = new List<string>();
        var uniquePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(scene.path) || !uniquePaths.Add(scene.path))
            {
                throw new InvalidOperationException($"Build Settings에 비어 있거나 중복된 씬이 있습니다: {scene.path}");
            }

            scenes.Add(scene.path);
        }

        if (!uniquePaths.Contains(TitleScenePath) || !uniquePaths.Contains(DefenseScenePath))
        {
            throw new InvalidOperationException(
                $"Build Settings에 필수 씬이 활성화되어야 합니다: {TitleScenePath}, {DefenseScenePath}");
        }

        Debug.Log($"[BuildScript] {target} 구성 검증 통과 (씬 {scenes.Count}개)");
        return scenes.ToArray();
    }
}
