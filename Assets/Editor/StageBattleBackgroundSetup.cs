using System;
using System.Linq;
using RCCom.Definitions.Stage;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>생성한 CH1 전투 배경을 StageDefinition에 연결하고 공용 전장 규격으로 맞춘다.</summary>
    public static class StageBattleBackgroundSetup
    {
        private const float BackgroundScale = 2f;

        [MenuItem("RCCom/Stages/Apply Generated Battle Backgrounds")]
        public static void ApplyGeneratedBackgrounds()
        {
            Apply("ch1-02", "Assets/Art/Backgrounds/Stages/stage-ch1-02-fallen-route.png");
            Apply("ch1-03", "Assets/Art/Backgrounds/Stages/stage-ch1-03-deep-signal.png");
            Apply("ch1-06", "Assets/Art/Backgrounds/Stages/stage-ch1-06-breach-point.png");
            Apply("ch1-07", "Assets/Art/Backgrounds/Stages/stage-ch1-07-signal-core.png");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            StageCatalogBuilder.BuildCatalog();

            if (!StageAssetValidator.ValidateAll(out string report))
            {
                throw new InvalidOperationException(report);
            }

            Debug.Log($"[StageBattleBackgroundSetup] CH1 전투 배경 4종 연결 완료\n{report}");
        }

        private static void Apply(string stageId, string spritePath)
        {
            string stagePath = $"Assets/Data/Stages/CH1/{stageId}.asset";
            StageDefinition stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
            if (stage == null)
            {
                throw new InvalidOperationException($"StageDefinition을 찾을 수 없습니다: {stagePath}");
            }

            // 단일 스프라이트도 Multiple 모드로 임포트되어 메인 에셋이 Texture2D다.
            // LoadAssetAtPath<Sprite> 대신 서브 에셋에서 Sprite를 찾아야 안정적으로 연결된다.
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(spritePath)
                .OfType<Sprite>()
                .FirstOrDefault();
            if (sprite == null)
            {
                throw new InvalidOperationException($"전투 배경 Sprite를 찾을 수 없습니다: {spritePath}");
            }

            Undo.RecordObject(stage, "Apply Stage Battle Background");
            stage.battleBackground = sprite;
            stage.battleBackgroundPosition = Vector2.zero;
            // 기존 DefenseScene 배경과 같은 해상도·PPU이므로 검증된 월드 배율을 그대로 공유한다.
            stage.battleBackgroundScale = Vector2.one * BackgroundScale;
            stage.schemaVersion = StageDefinition.CurrentSchemaVersion;
            EditorUtility.SetDirty(stage);
        }
    }
}
