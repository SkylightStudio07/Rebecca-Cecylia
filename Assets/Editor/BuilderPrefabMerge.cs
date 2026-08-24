using System;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 프리팹을 굽는 Builder들이 공통으로 겪는 문제: 데이터 SO(ShockwaveRingVisualEffect 등)는
    /// "존재하면 절대 재생성 안 함" 패턴으로 사람이 손으로 튜닝한 값을 보존하지만, 프리팹은
    /// 계층 구조를 코드로 매번 새로 짓다 보니 그 패턴을 그대로 쓸 수 없어 항상 통째로
    /// 덮어썼다. 그 결과 VFX 담당이 파티클 커브·소팅오더 등을 프리팹에서 직접 튜닝해도
    /// 다음 Build 실행에서 조용히 사라졌다.
    ///
    /// 이 헬퍼는 "구조 비교 → 같으면 스킵, 다르면 값만 이식 후 재생성"으로 그 구멍을 메운다.
    /// 참조(Object Reference) 필드는 항상 이번에 새로 지은 배선을 쓴다 — 구조가 바뀌어
    /// 새로 추가된 슬롯을 옛 배선으로 되돌리면 안 되기 때문이다. 그 외 값 필드(색상·곡선·
    /// 소팅오더 등)만 기존 프리팹에서 옮겨 심는다.
    /// </summary>
    internal static class BuilderPrefabMerge
    {
        /// <summary>
        /// 두 루트의 자식 이름·컴포넌트 타입 구성이 (순서 무관, 이름 기준) 완전히 같은지 본다.
        /// 같다면 빌더가 이번에 만들려는 구조와 기존 프리팹 구조가 동일하다는 뜻이므로,
        /// 호출한 쪽은 재생성 자체를 건너뛰고 기존 프리팹을 그대로 반환하면 된다.
        /// </summary>
        public static bool HasSameShape(GameObject existing, GameObject fresh)
        {
            return existing != null && fresh != null && HasSameShape(existing.transform, fresh.transform);
        }

        private static bool HasSameShape(Transform existing, Transform fresh)
        {
            if (!HasSameComponentTypes(existing.gameObject, fresh.gameObject))
            {
                return false;
            }

            if (existing.childCount != fresh.childCount)
            {
                return false;
            }

            for (int i = 0; i < fresh.childCount; i++)
            {
                Transform freshChild = fresh.GetChild(i);
                Transform existingChild = FindChildByName(existing, freshChild.name);
                if (existingChild == null || !HasSameShape(existingChild, freshChild))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasSameComponentTypes(GameObject existing, GameObject fresh)
        {
            Component[] existingComponents = existing.GetComponents<Component>();
            Component[] freshComponents = fresh.GetComponents<Component>();
            if (existingComponents.Length != freshComponents.Length)
            {
                return false;
            }

            foreach (Component freshComponent in freshComponents)
            {
                if (freshComponent == null || existing.GetComponent(freshComponent.GetType()) == null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// existingRoot(디스크에 있던 이전 프리팹의 임시 인스턴스)에서 freshRoot(빌더가 이번에
        /// 새로 지은 구조)로, 이름이 일치하는 자식·컴포넌트 타입에 한해 값 필드를 이식한다.
        /// 이름 경로가 어긋난 자식은 대상이 아니다 — 엉뚱한 자식에 값을 잘못 옮기는 사고를
        /// 피하기 위해서다. 반환값은 실제로 값을 옮긴 컴포넌트 수(로그용).
        /// </summary>
        public static int CopyTunedValues(GameObject existingRoot, GameObject freshRoot)
        {
            int touchedComponents = 0;
            CopyTunedValuesRecursive(existingRoot.transform, freshRoot.transform, ref touchedComponents);
            return touchedComponents;
        }

        private static void CopyTunedValuesRecursive(Transform existing, Transform fresh, ref int touchedComponents)
        {
            Component[] freshComponents = fresh.GetComponents<Component>();
            foreach (Component freshComponent in freshComponents)
            {
                if (freshComponent == null)
                {
                    continue;
                }

                Component existingComponent = existing.GetComponent(freshComponent.GetType());
                if (existingComponent != null && CopyValueLeaves(existingComponent, freshComponent))
                {
                    touchedComponents++;
                }
            }

            for (int i = 0; i < fresh.childCount; i++)
            {
                Transform freshChild = fresh.GetChild(i);
                Transform existingChild = FindChildByName(existing, freshChild.name);
                if (existingChild != null)
                {
                    CopyTunedValuesRecursive(existingChild, freshChild, ref touchedComponents);
                }
            }
        }

        /// <summary>
        /// SerializedProperty 트리를 리프까지 내려가며 값 타입만 복사한다. Object Reference/
        /// Managed Reference는 배선이므로 건드리지 않고 새 값을 그대로 둔다. ParticleSystem처럼
        /// 내부 직렬화가 특수한 컴포넌트에서 특정 경로가 예외를 던지면 그 필드 하나만 포기하고
        /// 나머지 이식은 계속한다 — 값 이식 실패가 전체 Build를 막으면 안 되기 때문이다.
        /// </summary>
        private static bool CopyValueLeaves(Component from, Component to)
        {
            var fromSerialized = new SerializedObject(from);
            var toSerialized = new SerializedObject(to);
            bool changedAny = false;

            SerializedProperty prop = fromSerialized.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (prop.name == "m_Script")
                {
                    continue;
                }

                if (prop.propertyType == SerializedPropertyType.ObjectReference ||
                    prop.propertyType == SerializedPropertyType.ManagedReference)
                {
                    enterChildren = false;
                    continue;
                }

                if (prop.propertyType == SerializedPropertyType.Generic)
                {
                    // 구조체/배열은 리프가 아니므로 값을 옮기지 않고 자식으로 계속 내려간다.
                    continue;
                }

                try
                {
                    SerializedProperty target = toSerialized.FindProperty(prop.propertyPath);
                    if (target == null || target.propertyType != prop.propertyType)
                    {
                        continue;
                    }

                    toSerialized.CopyFromSerializedProperty(prop);
                    changedAny = true;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[BuilderPrefabMerge] {from.GetType().Name}.{prop.propertyPath} 값 이식 실패, " +
                        $"새 기본값을 그대로 둡니다: {exception.Message}");
                }
            }

            if (changedAny)
            {
                toSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return changedAny;
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
