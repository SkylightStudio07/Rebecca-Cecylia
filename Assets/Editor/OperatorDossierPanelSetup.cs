using System;
using RCCom.Definitions.Operator;
using RCCom.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 사람이 잡아 둔 DossierPanel 레이아웃은 보존하고 런타임 참조와 클릭 영역만 연결한다.
    /// 같은 계층에 반복 실행해도 컴포넌트가 중복되지 않아 협업 중 재배선에 사용할 수 있다.
    /// </summary>
    public static class OperatorDossierPanelSetup
    {
        private const string CatalogPath = "Assets/Data/Operators/OperatorCatalog.asset";
        private const string LeftPanelSheetPath = "Assets/Art/UI/UnderPanel/LeftPanelSheet.png";
        private const int BondRecordCount = 5;

        [MenuItem("RCCom/UI/Setup Operator Dossier Panel")]
        public static void Setup()
        {
            EnsureEditMode();
            GameObject canvas = RequireRoot("Canvas");
            Transform mainMenu = Require(canvas.transform, "MainMenuBackground").transform;
            Transform shop = Require(canvas.transform, "ShopPanelBackground").transform;
            Transform operatorPanel = Require(shop, "OperatorPanel").transform;
            Transform underPanel = Require(operatorPanel, "UnderPanel").transform;
            GameObject dossierPanel = Require(shop, "DossierPanel");
            Transform dossier = dossierPanel.transform;

            OperatorCatalog catalog = AssetDatabase.LoadAssetAtPath<OperatorCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"OperatorCatalog를 찾지 못했습니다: {CatalogPath}");
            }

            OperatorDossierUI controller = shop.GetComponent<OperatorDossierUI>();
            if (controller == null) { controller = Undo.AddComponent<OperatorDossierUI>(shop.gameObject); }
            SerializedObject serialized = new(controller);
            Assign(serialized, "catalog", catalog);
            Assign(serialized, "operatorPortrait", ImageAt(operatorPanel, "OperatorPortrait"));
            Assign(serialized, "operatorUpperBodyPortrait", ImageAt(underPanel, "OperatorUpperbodyPortrait"));
            Assign(serialized, "operatorUpperBodyPortraitLeft", ImageAt(underPanel, "OperatorUpperbodyPortrait_Left"));
            Assign(serialized, "operatorUpperBodyPortraitRight", ImageAt(underPanel, "OperatorUpperbodyPortrait_Right"));
            Assign(serialized, "lockSpriteLeft", Require(underPanel, "LockSprite_Left"));
            Assign(serialized, "lockSpriteRight", Require(underPanel, "LockSprite_Right"));
            Assign(serialized, "operatorName", TextAt(underPanel, "OperatorName"));
            Assign(serialized, "operatorNameLeft", TextAt(underPanel, "OperatorName_Left"));
            Assign(serialized, "operatorNameRight", TextAt(underPanel, "OperatorName_Right"));
            Assign(serialized, "anotherNameText", TextAt(underPanel, "AnotherNameText"));
            Assign(serialized, "previousButton", ButtonAt(operatorPanel, "LeftButton"));
            Assign(serialized, "nextButton", ButtonAt(operatorPanel, "RightButton"));
            Assign(serialized, "rightOperatorNameText", TextAt(dossier, "OperatorNameText - RightPanel"));
            Assign(serialized, "operatorDialogue", TextAt(dossier, "OperatorDialouge"));
            Assign(serialized, "codenameText", TextAt(dossier, "CodenameText"));
            Assign(serialized, "roleText", TextAt(dossier, "RoleText"));
            Assign(serialized, "factionText", TextAt(dossier, "FactionText"));
            Assign(serialized, "heightText", TextAt(dossier, "HeightText"));
            Assign(serialized, "birthdayText", TextAt(dossier, "BirthdayText"));
            Assign(serialized, "specialityText", TextAt(dossier, "SpecialityText"));
            Assign(serialized, "weaponText", TextAt(dossier, "WeaponText"));
            Assign(serialized, "originText", TextAt(dossier, "OriginText"));

            SerializedProperty buttons = serialized.FindProperty("bondButtons");
            SerializedProperty locks = serialized.FindProperty("materialLockedSprites");
            buttons.arraySize = BondRecordCount;
            locks.arraySize = BondRecordCount;
            Transform layout = Require(dossier, "VerticalLayout").transform;
            for (int i = 0; i < BondRecordCount; i++)
            {
                GameObject row = Require(layout, $"CommuData{i + 1}");
                Button rowButton = EnsureGraphicButton(row, false);
                buttons.GetArrayElementAtIndex(i).objectReferenceValue = rowButton;
                locks.GetArrayElementAtIndex(i).objectReferenceValue =
                    Require(row.transform, "MaterialLockedSprite");
            }

            GameObject detailPanel = Require(dossier, "SmallDosierForCommuText");
            Assign(serialized, "detailPanel", detailPanel);
            Assign(serialized, "detailText", TextAt(detailPanel.transform, "SmallPanel/CommuText"));
            SerializedProperty closeButtons = serialized.FindProperty("detailCloseButtons");
            closeButtons.arraySize = 2;
            closeButtons.GetArrayElementAtIndex(0).objectReferenceValue =
                EnsureGraphicButton(Require(detailPanel.transform, "Dim"), false);
            closeButtons.GetArrayElementAtIndex(1).objectReferenceValue =
                EnsureGraphicButton(Require(detailPanel.transform, "SmallPanel/클릭시닫기"), false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            controller.enabled = false;

            LobbyShopPanelUI lobbyController = canvas.GetComponent<LobbyShopPanelUI>();
            if (lobbyController == null)
            {
                throw new InvalidOperationException("Canvas에 LobbyShopPanelUI가 없습니다. 기존 Shop Setup을 먼저 실행하세요.");
            }

            Button lobbyMaterial = EnsureGraphicButton(Require(mainMenu, "underPanel/MaterialButton"), true);
            Button shopMaterial = EnsureGraphicButton(
                Require(shop, "LeftFrame/VerticalLayout/MaterialButton"), true);
            SerializedObject lobbySerialized = new(lobbyController);
            Assign(lobbySerialized, "materialEntryButton", lobbyMaterial);
            Assign(lobbySerialized, "shopMaterialTabButton", shopMaterial);
            Assign(lobbySerialized, "dossierPanel", dossierPanel);
            Assign(lobbySerialized, "dossierController", controller);
            Assign(lobbySerialized, "shopMaterialTabImage", shopMaterial.GetComponent<Image>());
            Assign(lobbySerialized, "materialNormalSprite", LoadSheetSprite(3));
            Assign(lobbySerialized, "materialSelectedSprite", LoadSheetSprite(7));
            lobbySerialized.ApplyModifiedPropertiesWithoutUndo();

            // 편집 중 켜 둔 팝업과 패널이 타이틀 진입 직후 잠깐 보이지 않게 기본 상태를 고정한다.
            detailPanel.SetActive(false);
            dossierPanel.SetActive(false);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(lobbyController);
            EditorUtility.SetDirty(dossierPanel);
            EditorSceneManager.MarkSceneDirty(canvas.scene);
            EditorSceneManager.SaveScene(canvas.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("[OperatorDossierPanelSetup] 보유 오퍼레이터 자료·인연 기록 UI 배선 완료");
        }

        [MenuItem("RCCom/UI/Validate Operator Dossier Panel")]
        public static void Validate()
        {
            GameObject canvas = RequireRoot("Canvas");
            Transform shop = Require(canvas.transform, "ShopPanelBackground").transform;
            GameObject dossierPanel = Require(shop, "DossierPanel");
            OperatorDossierUI controller = shop.GetComponent<OperatorDossierUI>();
            LobbyShopPanelUI lobbyController = canvas.GetComponent<LobbyShopPanelUI>();
            if (controller == null || lobbyController == null)
            {
                throw new InvalidOperationException("Dossier 또는 Shop 전환 컨트롤러가 없습니다.");
            }

            SerializedObject serialized = new(controller);
            string[] required =
            {
                "catalog", "operatorPortrait", "operatorUpperBodyPortrait",
                "operatorUpperBodyPortraitLeft", "operatorUpperBodyPortraitRight",
                "lockSpriteLeft", "lockSpriteRight", "operatorName", "operatorNameLeft",
                "operatorNameRight", "anotherNameText", "previousButton", "nextButton",
                "rightOperatorNameText", "operatorDialogue", "codenameText", "roleText",
                "factionText", "heightText", "birthdayText", "specialityText", "weaponText",
                "originText", "detailPanel", "detailText"
            };
            foreach (string propertyName in required)
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    throw new InvalidOperationException($"OperatorDossierUI.{propertyName} 연결이 비어 있습니다.");
                }
            }

            ValidateArray(serialized, "bondButtons", BondRecordCount);
            ValidateArray(serialized, "materialLockedSprites", BondRecordCount);
            ValidateArray(serialized, "detailCloseButtons", 2);

            SerializedObject lobbySerialized = new(lobbyController);
            string[] lobbyRequired =
            {
                "materialEntryButton", "shopMaterialTabButton", "dossierPanel",
                "dossierController", "shopMaterialTabImage", "materialNormalSprite",
                "materialSelectedSprite"
            };
            foreach (string propertyName in lobbyRequired)
            {
                SerializedProperty property = lobbySerialized.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    throw new InvalidOperationException($"LobbyShopPanelUI.{propertyName} 연결이 비어 있습니다.");
                }
            }

            if (dossierPanel.activeSelf || Require(dossierPanel.transform, "SmallDosierForCommuText").activeSelf)
            {
                throw new InvalidOperationException("DossierPanel과 상세 팝업은 TitleScene 기본 상태에서 비활성이어야 합니다.");
            }

            Debug.Log("[OperatorDossierPanelSetup] DossierPanel 배선 검증 통과");
        }

        private static void ValidateArray(SerializedObject serialized, string propertyName, int size)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.arraySize != size)
            {
                throw new InvalidOperationException($"{propertyName} 배열은 {size}개여야 합니다.");
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    throw new InvalidOperationException($"{propertyName}[{i}] 연결이 비어 있습니다.");
                }
            }
        }

        private static Button EnsureGraphicButton(GameObject target, bool spriteSwap)
        {
            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic == null)
            {
                Image image = Undo.AddComponent<Image>(target);
                image.color = new Color(1f, 1f, 1f, 0f);
                graphic = image;
            }

            graphic.raycastTarget = true;
            Button button = target.GetComponent<Button>();
            if (button == null) { button = Undo.AddComponent<Button>(target); }
            button.targetGraphic = graphic;
            if (!spriteSwap) { button.transition = Selectable.Transition.None; }
            return button;
        }

        private static Sprite LoadSheetSprite(int index)
        {
            string expected = $"LeftPanelSheet_{index}";
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(LeftPanelSheetPath))
            {
                if (asset is Sprite sprite && sprite.name == expected) { return sprite; }
            }

            throw new InvalidOperationException($"{expected}를 찾지 못했습니다.");
        }

        private static GameObject RequireRoot(string name)
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name) { return root; }
            }

            throw new InvalidOperationException($"루트 오브젝트를 찾지 못했습니다: {name}");
        }

        private static GameObject Require(Transform root, string path)
        {
            Transform target = root.Find(path);
            return target != null
                ? target.gameObject
                : throw new InvalidOperationException($"오브젝트를 찾지 못했습니다: {root.name}/{path}");
        }

        private static Image ImageAt(Transform root, string path) =>
            RequireComponent<Image>(Require(root, path));

        private static TMP_Text TextAt(Transform root, string path) =>
            RequireComponent<TMP_Text>(Require(root, path));

        private static Button ButtonAt(Transform root, string path) =>
            RequireComponent<Button>(Require(root, path));

        private static T RequireComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException($"{target.name}에 {typeof(T).Name}이 없습니다.");
        }

        private static void Assign(SerializedObject serialized, string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"직렬화 필드를 찾지 못했습니다: {propertyName}");
            }

            property.objectReferenceValue = value;
        }

        private static void EnsureEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Dossier UI Setup은 Edit Mode에서만 실행할 수 있습니다.");
            }
        }
    }
}
