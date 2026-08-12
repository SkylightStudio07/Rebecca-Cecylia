using System;
using RCCom.Core;
using RCCom.Data;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// PlayerProfile을 JSON 한 덩어리로 PlayerPrefs에 저장하는 로컬 구현체.
    /// System.IO를 사용하지 않아 WebGL에서도 Unity가 제공하는 영속 저장 경로를 그대로 탄다.
    /// 필드가 늘어나도 PlayerPrefs 키를 여러 개 흩뿌리지 않고 스키마 버전 하나로 관리하기 위해
    /// 개별 값 저장 대신 JSON 직렬화를 사용한다.
    /// </summary>
    public sealed class PlayerPrefsProfileStorage : IProfileStorage
    {
        public const string DefaultStorageKey = "RCCom.PlayerProfile";

        private readonly string _storageKey;

        public PlayerPrefsProfileStorage(string storageKey = DefaultStorageKey)
        {
            if (string.IsNullOrWhiteSpace(storageKey))
            {
                throw new ArgumentException("프로필 저장 키는 비어 있을 수 없습니다.", nameof(storageKey));
            }

            _storageKey = storageKey;
        }

        public PlayerProfile Load()
        {
            if (!PlayerPrefs.HasKey(_storageKey))
            {
                return new PlayerProfile();
            }

            string json = PlayerPrefs.GetString(_storageKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PlayerProfile();
            }

            try
            {
                PlayerProfile profile = JsonUtility.FromJson<PlayerProfile>(json);
                if (profile == null)
                {
                    return new PlayerProfile();
                }

                // 이전/손상 데이터의 기본값을 정규화해 이후 소비 코드가 음수나 null을
                // 매번 방어하지 않게 한다. 미래 버전 마이그레이션은 스키마가 실제로 바뀔 때 추가한다.
                profile.schemaVersion = Math.Max(1, profile.schemaVersion);
                profile.bestWave = Math.Max(0, profile.bestWave);
                profile.selectedOperatorId ??= string.Empty;
                return profile;
            }
            catch (ArgumentException exception)
            {
                // 손상된 로컬 값 때문에 게임 진입 전체가 막히지 않게 기본 프로필로 복구한다.
                // 원문을 로그에 출력하면 향후 계정 데이터가 노출될 수 있어 예외 메시지만 남긴다.
                Debug.LogWarning($"[Profile] 저장된 프로필을 읽지 못해 기본값을 사용합니다: {exception.Message}");
                return new PlayerProfile();
            }
        }

        public void Save(PlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            profile.schemaVersion = PlayerProfile.CurrentSchemaVersion;
            string json = JsonUtility.ToJson(profile);
            PlayerPrefs.SetString(_storageKey, json);

            // 프로필 저장은 매 프레임 호출되는 경로가 아니라 게임 결과/선택 확정 시점의
            // 체크포인트이므로, 브라우저 종료 전에 IndexedDB 반영을 보장하도록 즉시 확정한다.
            PlayerPrefs.Save();
        }
    }
}
