using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 이미 배포된 플레이어를 대상으로 하는 Addressables 콘텐츠 업데이트 빌드.
    ///
    /// 지금까지 이 프로젝트는 BuildPlayerContent(=New Build)만 써 왔다. New Build는 카탈로그를
    /// 통째로 새로 만들기 때문에, 플레이어를 다시 빌드하지 않고 그 결과만 서버에 올리면 구
    /// 플레이어가 자기 StreamingAssets에 없는 번들 파일명을 가리키는 카탈로그를 받게 된다.
    /// 콘텐츠 업데이트 빌드는 이전 빌드의 상태 파일을 기준으로 삼아, static이 아닌 그룹의
    /// 변경분만 새 원격 번들로 굽고 나머지 엔트리는 이전 번들 참조를 그대로 유지한다.
    ///
    /// 그래서 상태 파일(addressables_content_state.bin)이 전부다. 이 파일이 없으면 이미
    /// 배포한 빌드에는 두 번 다시 콘텐츠를 내려보낼 수 없다. 기본 경로의 상태 파일은
    /// .gitignore 대상이고 콘텐츠를 다시 구울 때마다 덮어써지므로, 플레이어 빌드 직후
    /// 버전별 폴더로 복사해 보관한다(ArchiveContentState).
    /// </summary>
    public static class AddressablesContentUpdateBuilder
    {
        /// <summary>버전별 콘텐츠 상태 보관 폴더. Assets 바깥이라 Unity가 임포트하지 않는다.</summary>
        public const string ReleaseStateRoot = "ReleaseStates";

        private const string ContentStateFileName = "addressables_content_state.bin";
        private const string ManifestFileName = "release.json";

        [MenuItem("RCCom/Addressables/Archive Content State For Current Version")]
        public static void ArchiveContentStateMenu()
        {
            ArchiveContentState(EditorUserBuildSettings.activeBuildTarget);
        }

        [MenuItem("RCCom/Addressables/Build Content Update (Live Drop)")]
        public static void BuildContentUpdateMenu()
        {
            BuildContentUpdate(EditorUserBuildSettings.activeBuildTarget);
        }

        /// <summary>
        /// 지정 타깃·버전의 보관 폴더 경로. 버전은 PlayerSettings.bundleVersion을 쓴다 —
        /// "어떤 플레이어 빌드와 짝인가"를 사람이 이미 관리하는 값에 붙여야 어긋나지 않는다.
        /// </summary>
        public static string GetReleaseFolder(BuildTarget target, string version)
        {
            return Path.Combine(ReleaseStateRoot, target.ToString(), SanitizeVersion(version));
        }

        public static string GetArchivedContentStatePath(BuildTarget target, string version)
        {
            return Path.Combine(GetReleaseFolder(target, version), ContentStateFileName);
        }

        /// <summary>
        /// 방금 만든 콘텐츠 상태 파일을 현재 bundleVersion 폴더로 복사하고 매니페스트를 남긴다.
        /// 보관본이 있으면 덮어쓴다 — 플레이어를 다시 빌드했다는 뜻이고, 그 순간 이전 상태는
        /// 어차피 폐기 대상이기 때문이다. 다만 조용히 지우면 사고이므로 로그로 알린다.
        /// </summary>
        public static string ArchiveContentState(BuildTarget target)
        {
            return ArchiveContentState(target, PlayerSettings.bundleVersion);
        }

        public static string ArchiveContentState(BuildTarget target, string version)
        {
            AddressableAssetSettings settings = RequireSettings();
            string sourcePath = ContentUpdateScript.GetContentStateDataPath(false, settings);
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                throw new InvalidOperationException(
                    $"콘텐츠 상태 파일이 없습니다: {sourcePath}\n" +
                    "Addressables 콘텐츠를 한 번 빌드한 뒤에 보관할 수 있습니다.");
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new InvalidOperationException(
                    "PlayerSettings.bundleVersion이 비어 있습니다. 릴리스 버전을 먼저 지정하세요.");
            }

            string folder = GetReleaseFolder(target, version);
            string destinationPath = Path.Combine(folder, ContentStateFileName);
            if (File.Exists(destinationPath))
            {
                Debug.LogWarning(
                    $"[ContentUpdate] {target} {version} 보관본을 덮어씁니다. " +
                    "이 버전으로 이미 배포한 플레이어가 있다면 그 빌드에는 더 이상 콘텐츠 업데이트를 " +
                    "내려보낼 수 없게 됩니다. 배포 후에는 bundleVersion을 올리세요.");
            }

            Directory.CreateDirectory(folder);
            File.Copy(sourcePath, destinationPath, true);

            var manifest = new AddressablesReleaseManifest
            {
                version = version,
                buildTarget = target.ToString(),
                unityVersion = Application.unityVersion,
                archivedAtUtc = DateTime.UtcNow.ToString("o"),
                remoteLoadPath = settings.profileSettings.GetValueByName(
                    settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath),
                remoteCatalogLoadPath = settings.RemoteCatalogLoadPath.GetValue(settings),
            };
            File.WriteAllText(
                Path.Combine(folder, ManifestFileName),
                JsonUtility.ToJson(manifest, true),
                new UTF8Encoding(false));

            Debug.Log($"[ContentUpdate] 콘텐츠 상태 보관 완료: {destinationPath}");
            return destinationPath;
        }

        /// <summary>
        /// 현재 bundleVersion으로 보관된 상태 파일을 기준으로 콘텐츠 업데이트를 빌드한다.
        /// 산출물은 New Build와 같은 위치(ServerData/[BuildTarget])에 떨어지므로, 이후
        /// AddressablesServerDataPackager로 zip을 만들어 올리는 절차는 동일하다.
        /// </summary>
        public static void BuildContentUpdate(BuildTarget target)
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Play Mode에서는 콘텐츠 업데이트를 빌드할 수 없습니다.");
            }

            AddressableAssetSettings settings = RequireSettings();
            string version = PlayerSettings.bundleVersion;
            string statePath = GetArchivedContentStatePath(target, version);
            if (!File.Exists(statePath))
            {
                throw new InvalidOperationException(
                    $"{target} {version}의 보관된 콘텐츠 상태가 없습니다: {statePath}\n" +
                    "이 버전으로 플레이어를 빌드한 적이 없거나 보관본이 유실됐습니다. " +
                    "보관본 없이는 이미 배포된 빌드에 콘텐츠를 내려보낼 수 없습니다.");
            }

            AddressablesBuildValidator.ValidateOrThrow(target);
            ReportModifiedEntries(settings, statePath);

            AddressablesPlayerBuildResult result = ContentUpdateScript.BuildContentUpdate(settings, statePath);
            if (result == null)
            {
                // BuildContentUpdate는 상태 파일이 현재 설정과 호환되지 않으면 예외 대신 null을
                // 돌려주고 이유는 콘솔에만 남긴다. 여기서 삼키면 "실패했는데 성공처럼 보이는"
                // 상황이 되므로 반드시 예외로 올린다.
                throw new InvalidOperationException(
                    "콘텐츠 업데이트 빌드가 시작되지 못했습니다. 이전 상태와 현재 Addressables 설정이 " +
                    "호환되지 않습니다(원격 카탈로그 경로 변경 등). 상세 사유는 콘솔 오류를 확인하세요.");
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new InvalidOperationException($"콘텐츠 업데이트 빌드 실패: {result.Error}");
            }

            Debug.Log(
                $"[ContentUpdate] {target} {version} 콘텐츠 업데이트 빌드 완료. " +
                "RCCom/Addressables/Package ServerData Zip으로 업로드본을 만드세요.");
        }

        /// <summary>
        /// 이전 빌드 이후 바뀐 엔트리를 콘솔에 남기고, 그중 static(로컬) 그룹에 있는 것을 경고한다.
        ///
        /// static 그룹의 변경은 콘텐츠 업데이트로 나가지 않는다 — 의도된 동작이지만, 만든 사람은
        /// "고쳤는데 플레이어에 반영이 안 된다"로 겪게 된다. 빌드 시점에 이름을 짚어주는 편이
        /// 배포 후에 원인을 찾는 것보다 훨씬 싸다.
        /// </summary>
        private static void ReportModifiedEntries(AddressableAssetSettings settings, string statePath)
        {
            List<AddressableAssetEntry> modified = ContentUpdateScript.GatherModifiedEntries(settings, statePath);
            if (modified == null || modified.Count == 0)
            {
                Debug.Log("[ContentUpdate] 이전 빌드 이후 변경된 엔트리가 없습니다.");
                return;
            }

            var deliverable = new List<string>();
            var blocked = new List<string>();
            foreach (AddressableAssetEntry entry in modified)
            {
                if (entry == null)
                {
                    continue;
                }

                AddressableAssetGroup group = entry.parentGroup;
                ContentUpdateGroupSchema updateSchema = group != null ? group.GetSchema<ContentUpdateGroupSchema>() : null;
                bool isStatic = updateSchema != null && updateSchema.StaticContent;
                string line = $"{entry.address} ({(group != null ? group.Name : "그룹 없음")})";
                if (isStatic)
                {
                    blocked.Add(line);
                }
                else
                {
                    deliverable.Add(line);
                }
            }

            Debug.Log($"[ContentUpdate] 배포될 변경 {deliverable.Count}건:\n  " + string.Join("\n  ", deliverable));
            if (blocked.Count > 0)
            {
                Debug.LogWarning(
                    $"[ContentUpdate] static(로컬) 그룹이라 이번 업데이트로 나가지 않는 변경 {blocked.Count}건:\n  " +
                    string.Join("\n  ", blocked) +
                    "\n이 변경을 반영하려면 플레이어를 다시 빌드해야 합니다.");
            }
        }

        private static AddressableAssetSettings RequireSettings()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables 설정을 찾을 수 없습니다.");
            }

            return settings;
        }

        /// <summary>버전 문자열이 폴더명으로 쓰일 수 있게 경로 금지 문자를 치환한다.</summary>
        private static string SanitizeVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return "unversioned";
            }

            var builder = new StringBuilder(version.Length);
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in version)
            {
                builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            return builder.ToString();
        }
    }
}
