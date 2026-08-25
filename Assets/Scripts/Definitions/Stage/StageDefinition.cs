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
        public const int CurrentSchemaVersion = 2;

        [Header("식별·진행")]
        [HideInInspector] public int schemaVersion;
        public string stageId = string.Empty;
        public string chapterId = "ch1";
        public string displayName = string.Empty;
        public string subtitle = string.Empty;
        [Min(1)] public int recommendedLevel = 1;
        [Min(0)] public int order;
        [Min(0)] public int requiredBestWave;

        [Tooltip("켜면 이 스테이지를 원격 그룹으로 패키징해 CDN에서 내려받는다. " +
                 "오퍼레이터는 JSON 레시피에 같은 값이 있지만 스테이지는 이 SO가 제작 원본이라 여기에 둔다.")]
        public bool remoteContent;

        [Header("작전 브리핑")]
        [TextArea(2, 4)]
        public string description = string.Empty;
        [Tooltip("스테이지 선택 화면의 STAGE DESCRIPTION 영역에 표시할 가로형 배경")]
        public Sprite descriptionBackground;

        [Header("전장 구성")]
        [Tooltip("DefenseScene 월드 공간에 표시할 전투 배경. 스테이지 선택 설명 배경과 별도다.")]
        public Sprite battleBackground;
        public Vector2 battleBackgroundPosition = Vector2.zero;
        public Vector2 battleBackgroundScale = Vector2.one;
        [Tooltip("첫 점은 적 생성점, 마지막 점은 거점과 아군 출격점이다.")]
        public List<Vector2> routePoints = new();
        [Range(0f, 1f)] public float pathSmoothness = 1f;
        [Range(0.25f, 5f)] public float maxPointSpacing = 0.25f;
        [Tooltip("타워를 설치할 수 있는 슬롯 타일 좌표(Tilemap 셀 좌표). 비어 있으면 DefenseScene에 " +
                 "기본으로 칠해진 슬롯 레이아웃을 그대로 쓴다(엔드리스 모드와 동일한 하위호환 경로). " +
                 "Stage Studio의 Map 탭에서 Test Scene을 열어 Tile Palette로 칠한 뒤 Capture한다.")]
        public List<Vector3Int> buildableCells = new();

        [Header("클리어 보상")]
        public List<StageReward> rewards = new();

        [Header("웨이브 편성")]
        public List<StageWaveDefinition> waves = new();

        public bool IsPlayable => !string.IsNullOrWhiteSpace(stageId) && waves != null && waves.Count > 0;
    }
}
