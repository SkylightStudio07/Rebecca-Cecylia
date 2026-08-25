using System;
using System.Collections.Generic;
using RCCom.Data;
using UnityEngine;

namespace RCCom.Definitions.Stage
{
    /// <summary>
    /// 챕터 맵에서 즉시 표시할 수 있는 가벼운 스테이지 메타데이터.
    /// UI 표시 정보와 실행 Definition을 분리해, Definition 참조를 Addressables 키로 교체할 수 있게 한다.
    ///
    /// 원래 여기 있던 것은 StageDefinition 직접 참조뿐이었다. 그 상태로는 스테이지가 전부
    /// 플레이어 빌드에 박히는 것은 물론이고, 선택 화면이 목록을 그리는 것만으로 모든 스테이지의
    /// 웨이브·배경·적 참조가 통째로 메모리에 올라왔다. 오퍼레이터 카탈로그가 이미 쓰고 있는
    /// "표시용 경량 메타데이터는 카탈로그에, 실행 데이터는 주소로" 구조를 그대로 따른다.
    /// </summary>
    [Serializable]
    public sealed class StageCatalogEntry
    {
        public string stageId = string.Empty;
        public string chapterId = "ch1";
        public string displayName = "1-1";
        public string subtitle = "FIRST CONTACT";
        public string description = string.Empty;
        public int order;
        public int requiredBestWave;

        [Tooltip("Definition을 내려받기 전 선택 화면이 보여줘야 하는 권장 레벨")]
        public int recommendedLevel = 1;

        [Tooltip("작전 브리핑 배경. 원격 스테이지는 다운로드 전 비워두고 아래 주소를 쓴다.")]
        public Sprite descriptionBackground;

        [Tooltip("descriptionBackground가 원격이라 비어 있을 때 내려받을 Addressables 주소")]
        public string descriptionBackgroundAddress = string.Empty;

        [Tooltip("웨이브 편성이 하나라도 있는지. Definition 없이 실행 가능 여부를 판정하기 위한 값이다.")]
        public bool hasWaves;

        [Tooltip("StageDefinition의 Addressables 주소")]
        public string address = string.Empty;

        [Tooltip("선택 화면에서 다운로드 콘텐츠임을 안내하기 위한 표시값")]
        public bool remoteContent;

        [Tooltip("Definition을 받기 전 ENEMY PREVIEW에 표시할 적 편성 요약")]
        public List<StageEnemyPreview> enemyPreviews = new();

        [Tooltip("Definition을 받기 전 REWARDS에 표시할 일반 보상 요약")]
        public List<StageReward> rewards = new();

        /// <summary>
        /// 로컬 스테이지의 직접 참조. 주소 기반 로딩으로 옮겨간 뒤에도 남겨 둔 이유는 두 가지다.
        /// 하나는 카탈로그를 아직 다시 굽지 않은 저장소에서도 기존 스테이지가 그대로 돌아가게
        /// 하기 위해서고, 다른 하나는 이미 빌드에 들어 있는 스테이지를 굳이 Addressables 왕복으로
        /// 다시 얻을 이유가 없기 때문이다. 원격 스테이지에서는 항상 null이다.
        /// </summary>
        public StageDefinition stageDefinition;

        public bool IsUnlocked(int bestWave)
        {
            return bestWave >= requiredBestWave;
        }

        /// <summary>
        /// 전투 데이터를 확보할 수 있는 상태인지(해금 여부와는 무관).
        /// 직접 참조가 있으면 그것이 가장 확실한 판정 근거다. 없으면(원격 스테이지는 Definition을
        /// 본체 빌드로 새어 나가지 않게 하려고 의도적으로 비운다) 카탈로그에 복사해 둔 경량
        /// 값으로 판정한다.
        /// </summary>
        public bool HasBattleData =>
            stageDefinition != null
                ? stageDefinition.IsPlayable
                : hasWaves && !string.IsNullOrWhiteSpace(address);

        public bool IsPlayable(int bestWave)
        {
            return IsUnlocked(bestWave) && HasBattleData;
        }
    }
}
