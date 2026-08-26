using System;
using System.Collections.Generic;
using System.Reflection;
using RCCom.Runtime;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>씬 재로드 뒤 static 풀에 남은 파괴 참조를 안전하게 건너뛰는지 검증한다.</summary>
    public static class SpriteFlipbookPoolVerifier
    {
        [MenuItem("RCCom/Verify/Sprite Flipbook Pool")]
        public static void Verify()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo poolField = typeof(SpriteFlipbook).GetField("_availablePool", flags);
            MethodInfo getOrCreate = typeof(SpriteFlipbook).GetMethod("GetOrCreate", flags);
            if (poolField == null || getOrCreate == null)
            {
                throw new InvalidOperationException("SpriteFlipbook 풀 검증 진입점을 찾지 못했습니다.");
            }

            var pool = poolField.GetValue(null) as Dictionary<GameObject, Queue<SpriteFlipbook>>;
            if (pool == null)
            {
                throw new InvalidOperationException("SpriteFlipbook static 풀 타입이 올바르지 않습니다.");
            }

            SpriteFlipbook.ClearPool();
            var prefab = new GameObject("VerifySpriteFlipbookPrefab");
            SpriteFlipbook prefabComponent = prefab.AddComponent<SpriteFlipbook>();
            var destroyedObject = new GameObject("DestroyedPooledSpriteFlipbook");
            SpriteFlipbook destroyedFlipbook = destroyedObject.AddComponent<SpriteFlipbook>();

            try
            {
                var queue = new Queue<SpriteFlipbook>();
                queue.Enqueue(destroyedFlipbook);
                pool[prefab] = queue;
                UnityEngine.Object.DestroyImmediate(destroyedObject);

                SpriteFlipbook replacement = getOrCreate.Invoke(null, new object[] { prefab }) as SpriteFlipbook;
                if (replacement == null || replacement == prefabComponent)
                {
                    throw new InvalidOperationException("파괴된 풀 참조 대신 새 SpriteFlipbook을 만들지 못했습니다.");
                }

                UnityEngine.Object.DestroyImmediate(replacement.gameObject);
                Debug.Log("[SpriteFlipbookPoolVerifier] PASS — 파괴된 static 풀 참조 폐기 후 새 인스턴스 생성 확인");
            }
            finally
            {
                SpriteFlipbook.ClearPool();
                if (destroyedObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(destroyedObject);
                }
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        public static void VerifyEndlessRegressionSuite()
        {
            Verify();
            EnemySelfDestructVerifier.Verify();
            EndlessBossPromotionVerifier.Verify();
        }
    }
}
