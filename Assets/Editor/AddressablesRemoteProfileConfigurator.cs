using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 원격 서버 주소를 저장소에 하드코딩하지 않고 환경 변수에서 활성 Addressables 프로필로 주입한다.
    /// CI와 로컬 최종 빌드가 같은 진입점을 쓰게 해 수동 Profile 창 편집 누락을 막는다.
    /// </summary>
    public static class AddressablesRemoteProfileConfigurator
    {
        public const string RemoteLoadPathEnvironmentVariable = "RCCOM_REMOTE_LOAD_PATH";
        public const string RemoteBuildPathEnvironmentVariable = "RCCOM_REMOTE_BUILD_PATH";
        public const string DefaultRemoteBuildPath = "ServerData/[BuildTarget]";

        [MenuItem("RCCom/Addressables/Configure Active Remote Profile From Environment")]
        public static void ConfigureFromEnvironment()
        {
            string remoteLoadPath = Environment.GetEnvironmentVariable(RemoteLoadPathEnvironmentVariable);
            string remoteBuildPath = Environment.GetEnvironmentVariable(RemoteBuildPathEnvironmentVariable);
            ConfigureActiveProfile(remoteLoadPath, remoteBuildPath);
        }

        public static void ConfigureActiveProfile(string remoteLoadPath, string remoteBuildPath = null)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings가 없습니다.");
            }

            string normalizedLoadPath = NormalizeRemoteLoadPath(remoteLoadPath);
            string normalizedBuildPath = string.IsNullOrWhiteSpace(remoteBuildPath)
                ? DefaultRemoteBuildPath
                : remoteBuildPath.Trim().TrimEnd('/', '\\');

            AddressableAssetProfileSettings profiles = settings.profileSettings;
            var loadVariable = profiles.GetProfileDataByName(AddressableAssetSettings.kRemoteLoadPath);
            var buildVariable = profiles.GetProfileDataByName(AddressableAssetSettings.kRemoteBuildPath);
            string loadVariableId = loadVariable?.Id;
            string buildVariableId = buildVariable?.Id;
            if (string.IsNullOrEmpty(loadVariableId) || string.IsNullOrEmpty(buildVariableId))
            {
                throw new InvalidOperationException("Addressables 원격 경로 프로필 변수를 찾지 못했습니다.");
            }

            profiles.SetValue(settings.activeProfileId, loadVariableId, normalizedLoadPath);
            profiles.SetValue(settings.activeProfileId, buildVariableId, normalizedBuildPath);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AddressablesProfile] 활성 프로필 원격 경로 설정 완료: {normalizedLoadPath}");
        }

        public static string NormalizeRemoteLoadPath(string remoteLoadPath)
        {
            if (string.IsNullOrWhiteSpace(remoteLoadPath))
            {
                throw new InvalidOperationException(
                    $"원격 로드 주소가 없습니다. {RemoteLoadPathEnvironmentVariable} 환경 변수를 설정하세요.");
            }

            string normalized = remoteLoadPath.Trim().TrimEnd('/');
            bool isHttps = normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            bool isLocalHttp = normalized.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) ||
                               normalized.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase);
            if (!isHttps && !isLocalHttp)
            {
                throw new InvalidOperationException(
                    "WebGL 원격 콘텐츠 주소는 HTTPS여야 합니다. 로컬 스파이크만 localhost HTTP를 허용합니다.");
            }

            return normalized;
        }
    }
}
