# ReleaseStates

배포한 플레이어 빌드별 Addressables 콘텐츠 상태(`addressables_content_state.bin`) 보관소.

```
ReleaseStates/{BuildTarget}/{bundleVersion}/
  addressables_content_state.bin   # 콘텐츠 업데이트의 기준점
  release.json                     # 어떤 빌드와 짝인지 (버전/타깃/유니티/원격 경로)
```

## 왜 커밋하는가

이 파일이 없으면 **이미 배포된 플레이어에는 두 번 다시 콘텐츠를 내려보낼 수 없다.**
Addressables 기본 경로(`Assets/AddressableAssetsData/{Platform}/`)의 상태 파일은
`.gitignore` 대상이고 콘텐츠를 다시 구울 때마다 덮어써지므로, 릴리스 시점의 사본을
여기에 남긴다. 바이너리지만 수십 KB 수준이라 저장소 부담은 없다.

## 흐름

1. 플레이어 빌드 (`BuildScript.BuildWebGL` 등) → 성공 시 이 폴더에 자동 보관
2. 콘텐츠만 수정 → `RCCom/Addressables/Build Content Update (Live Drop)`
3. `RCCom/Addressables/Package ServerData Zip` → CDN 업로드

`bundleVersion`을 올리지 않고 플레이어를 다시 빌드하면 같은 폴더를 덮어쓴다.
**배포한 버전은 반드시 bundleVersion을 올린 뒤 다음 작업을 시작할 것.**
