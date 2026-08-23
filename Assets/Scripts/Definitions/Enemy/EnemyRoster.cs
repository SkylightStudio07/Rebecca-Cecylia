using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Definitions.Enemy
{
    /// <summary>
    /// 지금 게임에 존재하는 모든 적 종류(Definition)를 모아두는 프로젝트 창 에셋.
    /// enemyId로 조회 가능. WaveManager 등 여러 매니저가 각자 배열을 따로 들고 있지 않고
    /// 이 하나를 참조하게 해서, 새 적 종류가 추가될 때 한 곳만 고치면 되도록 한다.
    ///
    /// enemyIds만 직렬화하고 실제 Definition 참조(enemies)는 [NonSerialized]로 둔다 — 이
    /// 에셋이 EnemyDefinition을 직접(GUID) 참조하면 이 Roster가 포함된 빌드 그룹에 모든
    /// 적의 Definition이 하드 의존성으로 딸려 들어가, 개별 적을 원격 Addressable 그룹으로
    /// 분리해도 로컬 빌드에서 절대 빠지지 않는다. enemies는 이후 단계(BattleContentCache)가
    /// Addressables로 내려받아 런타임에 채워 넣는다 — 그 전까지는 비어 있는 것이 정상이다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Enemy/Enemy Roster")]
    public class EnemyRoster : ScriptableObject
    {
        public List<string> enemyIds = new();

        [NonSerialized]
        public List<EnemyDefinition> enemies = new();

        public EnemyDefinition FindById(string enemyId)
        {
            foreach (EnemyDefinition enemy in enemies)
            {
                if (enemy != null && enemy.data.enemyId == enemyId)
                {
                    return enemy;
                }
            }

            return null;
        }
    }
}
