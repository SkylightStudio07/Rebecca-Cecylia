using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace RCCom.UI
{
    /// <summary>
    /// WebGL에서는 VideoPlayer가 VideoClip 에셋 재생을 지원하지 않는다(URL 재생만 가능) —
    /// 데스크톱에선 잘 나오다가 웹 빌드에서만 영상이 조용히 안 나오는 원인. 그래서 영상 파일을
    /// StreamingAssets 폴더(빌드에 원본 그대로 복사되고, WebGL에선 URL로 서빙됨)에 두고
    /// 이 스크립트가 Awake에서 URL 소스로 배선한다. 에디터/데스크톱에서도 동일하게 동작하므로
    /// 플랫폼 분기 없이 이 방식 하나로 통일.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class StreamingVideoSource : MonoBehaviour
    {
        [Tooltip("StreamingAssets 폴더 기준 파일명 (예: TitleLoop.mp4) — 브라우저 호환을 위해 H.264 코덱 mp4 권장")]
        [SerializeField] private string fileName = "TitleLoop.mp4";

        private void Awake()
        {
            VideoPlayer player = GetComponent<VideoPlayer>();
            player.source = VideoSource.Url;
            player.url = Path.Combine(Application.streamingAssetsPath, fileName);
        }
    }
}
