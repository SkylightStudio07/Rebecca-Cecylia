using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Definitions.Operator
{
    /// <summary>
    /// Definition을 내려받기 전 선택 화면이 보여줘야 하는 최소 메타데이터.
    /// 원격 Definition 안에 이 정보를 넣으면 다운로드 전에는 선택 화면을 만들 수 없으므로
    /// 로컬 카탈로그에 별도로 둔다.
    /// </summary>
    [Serializable]
    public sealed class OperatorCatalogEntry
    {
        public string operatorId;
        public string displayName;

        [TextArea(2, 4)]
        public string playStyleDescription;

        [Tooltip("다운로드 전 선택 화면에 표시할 작은 초상화. 미완성 콘텐츠는 비워도 된다.")]
        public Sprite previewPortrait;

        [Tooltip("오퍼레이터 관리 카드에 표시할 전신·반신 초상화. 원격 콘텐츠는 다운로드 전 비워둔다.")]
        public Sprite managementPortrait;

        [Tooltip("OperatorDefinition의 Addressables 주소")]
        public string address;

        [Tooltip("선택 화면에서 다운로드 콘텐츠임을 안내하기 위한 표시값")]
        public bool remoteContent;

        [Min(0)]
        public int requiredBestWave;

        [Tooltip("Definition을 내려받기 전 로스터 패널에 표시할 경량 유닛 정보")]
        public List<OperatorUnitPreview> unitPreviews = new();

        public bool IsUnlocked(int bestWave) => bestWave >= requiredBestWave;
    }
}
