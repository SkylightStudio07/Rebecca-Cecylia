string[] paths =
{
    "/Canvas/MainMenuBackground",
    "/Canvas/MainMenuBackground/Image",
    "/Canvas/MainMenuBackground/NewGameText",
    "/Canvas/MainMenuBackground/PreferenceText",
    "/Canvas/MainMenuBackground/ReturntoTitleText",
    "/Canvas/HomeBackground",
    "/Canvas/HomeBackground/OperatorImage",
    "/Canvas/HomeBackground/Image",
};

foreach (string path in paths)
{
    UnityEngine.GameObject gameObject = UnityEngine.GameObject.Find(path);
    if (gameObject == null)
    {
        UnityEngine.Debug.Log($"[CommandLobbyInspect] {path} = MISSING");
        continue;
    }

    UnityEngine.RectTransform rect = gameObject.transform as UnityEngine.RectTransform;
    UnityEngine.UI.Image image = gameObject.GetComponent<UnityEngine.UI.Image>();
    string spritePath = image != null && image.sprite != null
        ? UnityEditor.AssetDatabase.GetAssetPath(image.sprite)
        : "(none)";

    UnityEngine.Debug.Log(
        $"[CommandLobbyInspect] {path} active={gameObject.activeSelf} sibling={gameObject.transform.GetSiblingIndex()} " +
        $"anchor=({rect.anchorMin.x:F3},{rect.anchorMin.y:F3})-({rect.anchorMax.x:F3},{rect.anchorMax.y:F3}) " +
        $"pivot=({rect.pivot.x:F3},{rect.pivot.y:F3}) pos=({rect.anchoredPosition.x:F1},{rect.anchoredPosition.y:F1}) " +
        $"size=({rect.sizeDelta.x:F1},{rect.sizeDelta.y:F1}) scale=({rect.localScale.x:F2},{rect.localScale.y:F2}) " +
        $"sprite={spritePath} raycast={(image != null && image.raycastTarget)}");
}
