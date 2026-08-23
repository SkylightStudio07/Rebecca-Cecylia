using System;
using RCCom.Data;
using UnityEngine;

namespace RCCom.Definitions.Enemy
{
    /// <summary>
    /// EnemyCatalog 한 줄의 데이터. 오퍼레이터의 OperatorCatalogEntry와 같은 이유로 별도 클래스로
    /// 둔다 — EnemyDefinition을 내려받기 전에도 카탈로그가 웨이브 구성·표시용 메타데이터를
    /// 제공해야 하고, 원격 콘텐츠의 실제 스프라이트가 로컬 카탈로그에 새어 들어가지 않게
    /// previewSprite를 별도 필드로 분리해 둔다.
    /// </summary>
    [Serializable]
    public sealed class EnemyCatalogEntry
    {
        public string enemyId;
        public string displayName;
        public EnemyKind kind;

        [Tooltip("EnemyDefinition의 Addressables 주소")]
        public string address;

        [Tooltip("다운로드가 필요한 원격 콘텐츠인지 여부")]
        public bool remoteContent;

        [Header("웨이브 예산 시스템 (GDD 확정값)")]
        public float waveCost;
        public int minWave;

        [Header("처치 보상")]
        public int goldReward;
        public int expReward;

        [Tooltip("Definition을 내려받기 전 표시할 미리보기 스프라이트. 원격 콘텐츠는 다운로드 전 비워둔다.")]
        public Sprite previewSprite;
    }
}
