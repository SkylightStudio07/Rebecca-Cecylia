using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace RCCom.Runtime
{
    /// <summary>
    /// 상점·선택 화면처럼 원격 오퍼레이터/유닛의 "얼굴"만 미리 보여줘야 하는 화면을 위한
    /// 경량 로더다. OperatorContentLoader는 Definition 전체(대사·이펙트·유닛 포함)를 받아야
    /// 하는 배치 확정 시점 전용이라 브라우징 화면에서 쓰면 안 되고, 그렇다고 초상화를 영원히
    /// 비워두면 상점에서 캐릭터 얼굴도 안 보여주고 팔라는 꼴이 된다. 그래서 초상화 한 장만
    /// 담긴 독립 Addressables 번들(OperatorCatalogBuilder/AllyUnitCatalogBuilder가
    /// PackSeparately로 분리해 둔다)을 이 화면 전용으로 따로 내려받는다.
    /// </summary>
    public static class RemotePreviewSpriteLoader
    {
        // 주소 하나당 핸들을 하나만 유지해, 상점과 로스터 카드가 같은 오퍼레이터를 동시에
        // 그려도 같은 번들을 두 번 받지 않는다. 세션 내내 캐시를 들고 있는 대신
        // Release를 하지 않는데, 현재 원격 오퍼레이터가 한 명뿐이라 감내할 수 있는
        // 단순화다 — 원격 오퍼레이터가 늘어나면 참조 카운팅을 추가해야 한다.
        private static readonly Dictionary<string, AsyncOperationHandle<Sprite>> _handles = new();

        /// <summary>
        /// localSprite가 있으면(로컬 오퍼레이터, 또는 이미 로드된 원격 콘텐츠) 그대로 쓰고,
        /// 없고 remoteAddress가 있으면(다운로드 전 원격 콘텐츠) 그 주소의 경량 번들을 비동기로
        /// 내려받아 완료되는 대로 반영한다. 로드 중이거나 대상이 전혀 없으면 fallbackColor로
        /// 칠해진 빈 이미지를 보여준다.
        /// </summary>
        public static void LoadInto(Image image, Sprite localSprite, string remoteAddress, Color fallbackColor)
        {
            if (image == null)
            {
                return;
            }

            if (localSprite != null)
            {
                Apply(image, localSprite, fallbackColor);
                return;
            }

            if (string.IsNullOrWhiteSpace(remoteAddress))
            {
                Apply(image, null, fallbackColor);
                return;
            }

            if (_handles.TryGetValue(remoteAddress, out AsyncOperationHandle<Sprite> existing) && existing.IsValid())
            {
                if (existing.IsDone)
                {
                    Apply(image, existing.Status == AsyncOperationStatus.Succeeded ? existing.Result : null,
                        fallbackColor);
                }
                else
                {
                    Apply(image, null, fallbackColor);
                    existing.Completed += handle => Apply(
                        image, handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null, fallbackColor);
                }

                return;
            }

            Apply(image, null, fallbackColor);
            AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(remoteAddress);
            _handles[remoteAddress] = handle;
            handle.Completed += result => Apply(
                image, result.Status == AsyncOperationStatus.Succeeded ? result.Result : null, fallbackColor);
        }

        private static void Apply(Image image, Sprite sprite, Color fallbackColor)
        {
            // 콜백이 돌아왔을 때 이미지가 이미 파괴됐을 수 있다(카드가 재활용되거나 화면이
            // 닫힌 경우) — Unity의 오버로드 null 비교로 그 경우를 걸러낸다.
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallbackColor;
            // fallbackColor가 불투명이면(유닛 로스터의 tint 스와치처럼) 로딩 중에도 그
            // 색으로 채워진 자리표시자를 계속 보여준다. 투명(Color.clear)을 넘긴 화면은
            // 기존과 같이 실제 이미지가 준비되기 전까지 아예 꺼둔다.
            image.enabled = sprite != null || fallbackColor.a > 0f;
        }
    }
}
