using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// Exchange 버튼의 Normal/Hover가 서로 다른 크기로 타이트 슬라이스되어 생기는 비율 변형을 교정한다.
    /// 기존 Sprite ID와 이름은 유지하므로 씬의 참조를 다시 연결할 필요가 없다.
    /// </summary>
    public static class ExchangeSpriteRectNormalizer
    {
        private const string GearSheetPath = "Assets/Art/UI/ExchangePanel/GearSpriteSheet.png";
        private const string RightButtonSheetPath = "Assets/Art/UI/ExchangePanel/RightPanelButtonSPrite.png";

        public static void NormalizeAndWire()
        {
            NormalizeSpriteRects();
            PlayerPartShopPanelSetup.WirePartCarouselCardHoverSprites();
        }

        public static void NormalizeSpriteRects()
        {
            NormalizePairs(GearSheetPath, new[]
            {
                ("GearSpriteSheet_0", "GearSpriteSheet_1"),
                ("GearSpriteSheet_2", "GearSpriteSheet_3"),
                ("GearSpriteSheet_4", "GearSpriteSheet_5"),
                ("GearSpriteSheet_6", "GearSpriteSheet_7"),
                ("GearSpriteSheet_8", "GearSpriteSheet_9")
            });

            NormalizePairs(RightButtonSheetPath, new[]
            {
                ("RightPanelButtonSPrite_0", "RightPanelButtonSPrite_4"),
                ("RightPanelButtonSPrite_1", "RightPanelButtonSPrite_5"),
                ("RightPanelButtonSPrite_2", "RightPanelButtonSPrite_6"),
                ("RightPanelButtonSPrite_3", "RightPanelButtonSPrite_7")
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ExchangeSpriteRectNormalizer] Exchange Normal/Hover Rect와 Pivot 정규화 완료");
        }

        private static void NormalizePairs(string assetPath,
            IReadOnlyList<(string normal, string hover)> pairs)
        {
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
            {
                throw new InvalidOperationException($"스프라이트 시트 Importer를 찾지 못했습니다: {assetPath}");
            }

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null)
            {
                throw new InvalidOperationException($"Sprite Editor 데이터 공급자를 지원하지 않습니다: {assetPath}");
            }

            provider.InitSpriteEditorDataProvider();
            // Unity 6.3의 GetEditCapability 반환형은 구버전 예제의 EEditCapability가 아니라
            // EditCapability이므로, 특정 enum API에 결합하지 않고 공급자 지원 여부로 검증한다.
            if (!provider.HasDataProvider(typeof(ISpriteFrameEditCapability)))
            {
                throw new InvalidOperationException($"Sprite Frame 편집 기능을 지원하지 않습니다: {assetPath}");
            }

            SpriteRect[] rects = provider.GetSpriteRects();
            Dictionary<string, SpriteRect> byName = rects.ToDictionary(rect => rect.name);
            foreach ((string normalName, string hoverName) in pairs)
            {
                if (!byName.TryGetValue(normalName, out SpriteRect normal) ||
                    !byName.TryGetValue(hoverName, out SpriteRect hover))
                {
                    throw new InvalidOperationException(
                        $"Normal/Hover 쌍을 찾지 못했습니다: {normalName}, {hoverName}");
                }

                float width = Mathf.Max(normal.rect.width, hover.rect.width);
                float height = Mathf.Max(normal.rect.height, hover.rect.height);
                NormalizeRect(normal, width, height);
                NormalizeRect(hover, width, height);
            }

            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static void NormalizeRect(SpriteRect spriteRect, float width, float height)
        {
            Vector2 center = spriteRect.rect.center;
            spriteRect.rect = new Rect(
                Mathf.Round(center.x - width * 0.5f),
                Mathf.Round(center.y - height * 0.5f),
                width,
                height);
            spriteRect.alignment = (int)SpriteAlignment.Center;
            spriteRect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
