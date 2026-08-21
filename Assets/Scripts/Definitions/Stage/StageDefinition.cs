using System.Collections.Generic;
using RCCom.Data;
using UnityEngine;

namespace RCCom.Definitions.Stage
{
    /// <summary>
    /// DefenseScene에서 순서대로 실행할 유한 스테이지 웨이브 정의.
    /// 적 타입별 전투 로직은 기존 EnemyDefinition에 남기고, 이 에셋은 편성 순서만 조립한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Stage/Stage Definition")]
    public sealed class StageDefinition : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [Header("식별·진행")]
        [HideInInspector] public int schemaVersion;
        public string stageId = string.Empty;
        public string chapterId = "ch1";
        public string displayName = string.Empty;
        public string subtitle = string.Empty;
        [Min(1)] public int recommendedLevel = 1;
        [Min(0)] public int order;
        [Min(0)] public int requiredBestWave;

        [Header("작전 브리핑")]
        [TextArea(2, 4)]
        public string description = string.Empty;
        [Tooltip("스테이지 선택 화면의 STAGE DESCRIPTION 영역에 표시할 가로형 배경")]
        public Sprite descriptionBackground;

        [Header("클리어 보상")]
        public List<StageReward> rewards = new();

        [Header("웨이브 편성")]
        public List<StageWaveDefinition> waves = new();

        public bool IsPlayable => !string.IsNullOrWhiteSpace(stageId) && waves != null && waves.Count > 0;
    }
}
