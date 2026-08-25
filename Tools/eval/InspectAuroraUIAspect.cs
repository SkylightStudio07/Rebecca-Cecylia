foreach (UnityEngine.UI.Image image in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Image>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
    if (!image.name.Contains("Operator") && image.name != "CharacterStanding")
    {
        continue;
    }

    string path = image.name;
    UnityEngine.Transform parent = image.transform.parent;
    while (parent != null)
    {
        path = parent.name + "/" + path;
        parent = parent.parent;
    }

    UnityEngine.RectTransform rect = image.rectTransform;
    UnityEngine.Sprite sprite = image.sprite;
    string spriteInfo = sprite != null
        ? $"{sprite.name} {sprite.rect.width}x{sprite.rect.height}"
        : "null";
    UnityEngine.Debug.Log(
        $"[AuroraAspect] {path} rect={rect.rect.width}x{rect.rect.height} " +
        $"scale={rect.localScale} lossyScale={rect.lossyScale} " +
        $"preserveAspect={image.preserveAspect} type={image.type} sprite={spriteInfo}");
}
