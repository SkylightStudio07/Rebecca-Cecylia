UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEngine.Transform canvas = null;
foreach (UnityEngine.GameObject root in scene.GetRootGameObjects())
{
    if (root.name == "Canvas")
    {
        canvas = root.transform;
        break;
    }
}

if (canvas == null)
{
    throw new System.InvalidOperationException("TitleScene Canvas를 찾지 못했습니다.");
}

string[] paths =
{
    "ShopPanelBackground",
    "ShopPanelBackground/LeftFrame",
    "ShopPanelBackground/LeftFrame/VerticalLayout/RecruitButton",
    "ShopPanelBackground/LeftFrame/VerticalLayout/EnhanceButton",
    "ShopPanelBackground/OperatorPanel",
    "ShopPanelBackground/OperatorPanel/UnderPanel",
    "ShopPanelBackground/StrategistPanel"
};

foreach (string path in paths)
{
    UnityEngine.Transform target = canvas.Find(path);
    if (target == null)
    {
        UnityEngine.Debug.LogWarning("[EnhanceLayout] missing " + path);
        continue;
    }

    UnityEngine.RectTransform rect = target as UnityEngine.RectTransform;
    UnityEngine.Debug.Log(
        $"[EnhanceLayout] {path} active={target.gameObject.activeSelf} " +
        $"anchor={rect.anchorMin}/{rect.anchorMax} pivot={rect.pivot} " +
        $"pos={rect.anchoredPosition} size={rect.sizeDelta} scale={rect.localScale}");
}
