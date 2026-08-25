using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Enemy;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 무한 모드 보스의 런타임 승급 규칙을 계산한다. 별도 EnemyDefinition을 만들지 않고 선택된
    /// 적의 데이터만 복제하므로 스프라이트·Effects·미지정 전투 수치는 원래 적 조립을 그대로 쓴다.
    /// </summary>
    public static class EndlessBossPromotion
    {
        public const float HealthFromWaveMultiplier = 1.75f;
        public const float FixedMoveSpeed = 0.75f;
        public const float DamageMultiplier = 1.75f;
        public const float RewardMultiplier = 3f;
        public const float VisualSizeMultiplier = 1.5f;

        public static bool ShouldPromote(BattleMode mode, int waveNumber, int interval)
        {
            return mode == BattleMode.Endless && interval > 0 && waveNumber > 0 &&
                   waveNumber % interval == 0;
        }

        /// <summary>
        /// 체력은 웨이브 체력 배율 적용 전 합계를 사용한다. WaveManager의 공통 체력 배율 경로를
        /// 이후 한 번 통과시켜 일반 적과 보스가 같은 무한 성장 곡선을 정확히 공유하게 한다.
        /// </summary>
        public static EnemyData CreateRuntimeData(
            IReadOnlyList<EnemyDefinition> wave,
            int selectedIndex)
        {
            if (wave == null || selectedIndex < 0 || selectedIndex >= wave.Count)
            {
                return null;
            }

            EnemyDefinition selectedDefinition = wave[selectedIndex];
            if (selectedDefinition == null || selectedDefinition.data == null)
            {
                return null;
            }

            float totalHealth = 0f;
            float strongestDamage = 0f;
            EnemyData rewardSource = null;

            for (int i = 0; i < wave.Count; i++)
            {
                EnemyData candidate = wave[i] != null ? wave[i].data : null;
                if (candidate == null)
                {
                    continue;
                }

                totalHealth += Mathf.Max(0f, candidate.maxHealth);
                strongestDamage = Mathf.Max(strongestDamage, Mathf.Max(0f, candidate.contactDamage));

                if (rewardSource == null || candidate.goldReward > rewardSource.goldReward ||
                    (candidate.goldReward == rewardSource.goldReward &&
                     candidate.expReward > rewardSource.expReward))
                {
                    rewardSource = candidate;
                }
            }

            EnemyData selected = selectedDefinition.data;
            var runtimeData = new EnemyData
            {
                enemyId = selected.enemyId,
                displayName = selected.displayName,
                kind = selected.kind,
                maxHealth = totalHealth * HealthFromWaveMultiplier,
                moveSpeed = FixedMoveSpeed,
                contactDamage = strongestDamage * DamageMultiplier,
                attackRange = selected.attackRange,
                attackInterval = selected.attackInterval,
                waveCost = selected.waveCost,
                minWave = selected.minWave,
                goldReward = rewardSource != null
                    ? Mathf.RoundToInt(rewardSource.goldReward * RewardMultiplier)
                    : selected.goldReward,
                expReward = rewardSource != null
                    ? Mathf.RoundToInt(rewardSource.expReward * RewardMultiplier)
                    : selected.expReward,
            };

            return runtimeData;
        }
    }
}
