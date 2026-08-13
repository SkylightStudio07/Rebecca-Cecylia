using System;
using RCCom.Data;
using RCCom.Definitions.Card;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Tower;
using RCCom.Definitions.Unit;
using RCCom.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace RCCom.Runtime
{
    /// <summary>
    /// 타이틀의 오퍼레이터 선택을 DefenseScene까지 전달하는 세션 경계.
    /// 선택 상태는 씬 전환을 넘어야 하므로 static으로 두되, GameManager.Awake가
    /// PrepareForGameplay를 가장 먼저 호출해 파괴된 Addressable 참조나 필수 연결 누락을
    /// 다른 컴포넌트의 Awake보다 앞서 검출한다.
    /// </summary>
    public static class OperatorLoadoutSession
    {
        public static OperatorDefinition SelectedDefinition { get; private set; }

        private static AsyncOperationHandle<OperatorDefinition> _addressableHandle;
        private static bool _ownsAddressableHandle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewApplicationRun()
        {
            // 에디터에서 도메인 리로드를 꺼도 이전 Play 세션의 선택이 새 실행으로 새지 않게 한다.
            // 씬 재로드에는 호출되지 않으므로 TitleScene에서 고른 값과 Retry 로드아웃은 유지된다.
            ClearSelection();
        }

        public static void Select(OperatorDefinition definition)
        {
            ValidateDefinition(definition);
            ReleaseOwnedHandle();
            SelectedDefinition = definition;
        }

        /// <summary>
        /// Addressables로 불러온 Definition과 그 의존 에셋이 DefenseScene에서도 살아 있도록
        /// 로드 핸들의 소유권을 세션으로 이전한다.
        /// </summary>
        public static void SelectAddressable(AsyncOperationHandle<OperatorDefinition> handle)
        {
            if (!handle.IsValid() || handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                throw new InvalidOperationException("완료되지 않은 오퍼레이터 Addressables 핸들을 선택할 수 없습니다.");
            }

            ValidateDefinition(handle.Result);
            ReleaseOwnedHandle();
            _addressableHandle = handle;
            _ownsAddressableHandle = true;
            SelectedDefinition = handle.Result;
        }

        /// <summary>
        /// 타이틀로 돌아온 뒤 다른 오퍼레이터를 고르기 전 선택을 명시적으로 비울 때 사용한다.
        /// Retry에서는 같은 로드아웃을 유지해야 하므로 GameManager가 자동으로 비우지 않는다.
        /// </summary>
        public static void ClearSelection()
        {
            ReleaseOwnedHandle();
            SelectedDefinition = null;
        }

        public static void PrepareForGameplay()
        {
            if (SelectedDefinition != null)
            {
                ValidateDefinition(SelectedDefinition);
            }
        }

        public static PlayerData CreatePlayerData(PlayerData fallback)
        {
            PlayerData source = SelectedDefinition != null ? SelectedDefinition.playerData : fallback;
            if (source == null)
            {
                throw new InvalidOperationException("플레이어 로드아웃 데이터가 없습니다.");
            }

            // 플레이어 강화 카드는 data를 직접 수정하므로 Definition 안의 원본을 그대로
            // 넘기지 않고 매 게임플레이 씬마다 새 값 객체를 만든다.
            return new PlayerData
            {
                maxHealth = source.maxHealth,
                moveSpeed = source.moveSpeed,
                hitInvulnerabilityDuration = source.hitInvulnerabilityDuration,
                attackDamage = source.attackDamage,
                attackRange = source.attackRange,
                attackInterval = source.attackInterval,
                projectileSpeed = source.projectileSpeed,
                skillCooldown = source.skillCooldown,
                skillRange = source.skillRange,
                skillDamage = source.skillDamage,
            };
        }

        public static TowerRoster ResolveTowerRoster(TowerRoster fallback)
        {
            TowerRoster resolved = SelectedDefinition != null ? SelectedDefinition.towerRoster : fallback;
            return resolved != null
                ? resolved
                : throw new InvalidOperationException("오퍼레이터 TowerRoster가 없습니다.");
        }

        public static CardRoster ResolveCardRoster(CardRoster fallback)
        {
            CardRoster resolved = SelectedDefinition != null ? SelectedDefinition.cardRoster : fallback;
            return resolved != null
                ? resolved
                : throw new InvalidOperationException("오퍼레이터 CardRoster가 없습니다.");
        }

        /// <summary>
        /// 유닛 로스터는 타워 전용 오퍼레이터에서 비어 있을 수 있다. 호출자는 null이면
        /// 유닛 배치 UI와 입력을 숨기고, 별도 기본 로스터를 암묵적으로 섞지 않는다.
        /// </summary>
        public static AllyUnitRoster ResolveAllyUnitRoster(AllyUnitRoster fallback = null)
        {
            return SelectedDefinition != null ? SelectedDefinition.allyUnitRoster : fallback;
        }

        public static OperatorDialogueSet ResolveDialogueSet(OperatorDialogueSet fallback)
        {
            OperatorDialogueSet resolved = SelectedDefinition != null ? SelectedDefinition.dialogueSet : fallback;
            return resolved != null
                ? resolved
                : throw new InvalidOperationException("오퍼레이터 DialogueSet이 없습니다.");
        }

        private static void ValidateDefinition(OperatorDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(definition.operatorId) || definition.playerData == null ||
                definition.towerRoster == null || definition.cardRoster == null || definition.dialogueSet == null)
            {
                throw new InvalidOperationException($"오퍼레이터 필수 로드아웃 참조가 비어 있습니다: {definition.name}");
            }
        }

        private static void ReleaseOwnedHandle()
        {
            if (_ownsAddressableHandle && _addressableHandle.IsValid())
            {
                Addressables.Release(_addressableHandle);
            }

            _addressableHandle = default;
            _ownsAddressableHandle = false;
        }
    }
}
