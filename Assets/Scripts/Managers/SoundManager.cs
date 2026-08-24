using System.Collections.Generic;
using RCCom.Runtime;
using UnityEngine;
using UnityEngine.Audio;

namespace RCCom.Managers
{
    /// <summary>
    /// 사운드 재생 전담 — "게임 흐름 단계"당 1개 매니저 원칙(ARCHITECTURE.md 5단계)의 기존 4개
    /// (Game/Map/Wave/CardManager)와는 별개로 추가하는 5번째 매니저. 오디오 재생은 타워 이펙트/
    /// 플레이어/UI 버튼 등 여러 시스템이 공통으로 호출해야 하는 횡단 관심사라, 기존 4개 중
    /// 어디에도 억지로 끼워 넣지 않고 독립시키는 게 더 적절하다고 판단 (BaseController.Instance/
    /// GameManager.Instance와 같은 이유로 정적 싱글톤 노출 — SO 효과 자산이나 다른 시스템이
    /// 직접 호출해야 해서).
    ///
    /// 게임 씬과 타이틀 씬 둘 다 이 스크립트를 각자 재사용한다 — DontDestroyOnLoad 불필요.
    /// 씬마다 필요 없는 필드(예: 타이틀 씬에서 타워 공격음)는 그냥 비워두면 된다. 대신 볼륨
    /// 설정(Master/BGM/SFX)은 AudioMixer의 노출 파라미터로 처리하는데, AudioMixer 에셋은 씬에
    /// 종속되지 않는 프로젝트 에셋이라 한 번 SetFloat로 값을 바꾸면 씬이 전환돼도(심지어
    /// SoundManager 인스턴스 자체가 파괴/재생성돼도) 그 값이 세션 내내 유지된다 — 그래서
    /// DontDestroyOnLoad 없이도 설정 화면(타이틀 씬)에서 바꾼 볼륨이 게임 씬에도 그대로 적용됨.
    ///
    /// 이펙트 사운드(공격/스킬/버튼 등)는 AI 생성 음원이라 뒤에 불필요한 여백이 길게 붙어있는
    /// 경우가 많아, 클립 길이와 무관하게 effectCutoffDuration(기본 0.5초)에서 강제로 끊는다.
    /// AudioSource를 풀링해서 재사용 — 초당 여러 번 재생돼도 Instantiate/Destroy가 없다.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Tooltip("공격 타워 명중음의 청취 조건 판단에 사용 — 이 플레이어의 공격 사거리 안에 있는 타워만 소리가 들림 (타이틀 씬에서는 비워둘 것)")]
        [SerializeField] private PlayerController player;

        [Header("게임플레이 이펙트 사운드 클립")]
        [SerializeField] private AudioClip towerAttackClip;
        [SerializeField] private AudioClip playerAttackClip;
        [SerializeField] private AudioClip skillClip;
        [SerializeField] private AudioClip buttonClickClip;
        [SerializeField] private AudioClip towerBuildClip;
        [SerializeField] private AudioClip towerDemolishClip;

        [Header("공용 UI 사운드")]
        [Tooltip("버튼 클릭과 키보드/패드 Submit에 공통으로 사용하는 선택음")]
        [SerializeField] private AudioClip uiSelectClip;
        [Tooltip("UI 선택음은 짧은 꼬리까지 들려야 하므로 전투 효과음과 별도 컷오프를 사용한다")]
        [SerializeField] private float uiSelectCutoffDuration = 1f;

        [Header("타이틀 씬 전용 클립")]
        [SerializeField] private AudioClip titleClickClip;
        [SerializeField] private AudioClip mainMenuClickClip;
        [SerializeField] private AudioClip settingsButtonClickClip;

        [Header("이펙트 재생 설정")]
        [Tooltip("AI 생성 음원 뒤쪽 여백을 잘라내기 위한 강제 컷오프 — 클립 실제 길이와 무관하게 이 시간에서 끊는다")]
        [SerializeField] private float effectCutoffDuration = 0.5f;
        [SerializeField, Range(0f, 1f)] private float effectVolume = 1f;

        [Header("BGM (하나 끝나면 배열에서 랜덤으로 다음 곡 재생, 직전 곡과 겹치지 않게 함)")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioClip[] bgmPlaylist;

        [Header("배경 앰비언스 (예: 타이틀 씬 바람소리 — 루프 재생, BGM 볼륨 그룹 공유)")]
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioClip ambienceClip;

        [Header("오디오 믹서 (Master는 그룹 참조 불필요 — BGM/SFX가 자식이라 자동 반영됨)")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioMixerGroup bgmMixerGroup;
        [SerializeField] private AudioMixerGroup sfxMixerGroup;

        private const string MasterVolumeKey = "Audio_MasterVolume";
        private const string BgmVolumeKey = "Audio_BgmVolume";
        private const string SfxVolumeKey = "Audio_SfxVolume";
        private const string MasterVolumeParam = "MasterVolume";
        private const string BgmVolumeParam = "BGMVolume";
        private const string SfxVolumeParam = "SFXVolume";

        /// <summary>현재 슬라이더 값(0~1) — Configuration 화면이 열릴 때 슬라이더 초기값으로 참조.</summary>
        public float MasterVolume { get; private set; } = 1f;
        public float BgmVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;

        private readonly List<AudioSource> _sfxSources = new();
        private readonly List<SfxTimer> _activeSfx = new();
        private int _lastBgmIndex = -1;
        private int _lastUiSelectFrame = -1;
        private AudioClip _resumeBgmClip;
        private float _resumeBgmTime;
        private bool _hasTemporaryBgm;

        private class SfxTimer
        {
            public AudioSource source;
            public float remaining;
        }

        private void Awake()
        {
            Instance = this;

            if (bgmSource != null && bgmMixerGroup != null)
            {
                bgmSource.outputAudioMixerGroup = bgmMixerGroup;
            }

            if (ambienceSource != null && bgmMixerGroup != null)
            {
                ambienceSource.outputAudioMixerGroup = bgmMixerGroup;
            }

            ApplyPersistedVolumes();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            PlayNextBgm();

            if (ambienceSource != null && ambienceClip != null)
            {
                ambienceSource.clip = ambienceClip;
                ambienceSource.loop = true;
                ambienceSource.Play();
            }
        }

        private void Update()
        {
            // 이펙트 사운드가 게임오버/튜토리얼 같은 Time.timeScale=0 화면의 버튼 클릭음으로도
            // 쓰이므로, 컷오프 타이머는 unscaledDeltaTime으로 — 안 그러면 일시정지 중엔 컷오프
            // 자체가 멈춰서(0.5초가 절대 안 지나서) 원본 클립 끝(최대 2초)까지 그대로 재생돼버림.
            TickSfxCutoff();

            if (!_hasTemporaryBgm && bgmSource != null && bgmPlaylist != null && bgmPlaylist.Length > 0 && !bgmSource.isPlaying)
            {
                PlayNextBgm();
            }
        }

        private void TickSfxCutoff()
        {
            for (int i = _activeSfx.Count - 1; i >= 0; i--)
            {
                SfxTimer timer = _activeSfx[i];
                timer.remaining -= Time.unscaledDeltaTime;

                if (timer.remaining <= 0f)
                {
                    timer.source.Stop();
                    _activeSfx.RemoveAt(i);
                }
            }
        }

        private void PlayNextBgm()
        {
            if (bgmPlaylist == null || bgmPlaylist.Length == 0)
            {
                return;
            }

            int index = Random.Range(0, bgmPlaylist.Length);
            while (bgmPlaylist.Length > 1 && index == _lastBgmIndex)
            {
                index = Random.Range(0, bgmPlaylist.Length);
            }

            _lastBgmIndex = index;
            bgmSource.loop = false;
            bgmSource.clip = bgmPlaylist[index];
            bgmSource.Play();
        }

        /// <summary>
        /// 현재 BGM의 재생 위치를 보존하고 화면 전용 BGM을 반복 재생한다.
        /// 로딩 연출 뒤에 호출하면 화면과 음악 전환 시점을 일치시킬 수 있다.
        /// </summary>
        public void PlayTemporaryLoopingBgm(AudioClip clip)
        {
            if (bgmSource == null || clip == null)
            {
                return;
            }

            if (_hasTemporaryBgm && bgmSource.clip == clip)
            {
                return;
            }

            // 임시 BGM 안에서 다시 전환하더라도 최초 로비곡의 복귀 지점을 덮어쓰지 않는다.
            if (!_hasTemporaryBgm)
            {
                _resumeBgmClip = bgmSource.clip;
                _resumeBgmTime = bgmSource.isPlaying ? bgmSource.time : 0f;
            }

            _hasTemporaryBgm = true;
            bgmSource.Stop();
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.time = 0f;
            bgmSource.Play();
        }

        /// <summary>화면 전용 반복 BGM을 끝내고 진입 전 로비곡의 재생 위치로 복귀한다.</summary>
        public void RestoreBgmAfterTemporaryLoop()
        {
            if (!_hasTemporaryBgm || bgmSource == null)
            {
                return;
            }

            AudioClip resumeClip = _resumeBgmClip;
            float resumeTime = _resumeBgmTime;
            _hasTemporaryBgm = false;
            _resumeBgmClip = null;
            _resumeBgmTime = 0f;

            bgmSource.Stop();
            bgmSource.loop = false;

            if (resumeClip == null)
            {
                PlayNextBgm();
                return;
            }

            bgmSource.clip = resumeClip;
            bgmSource.time = Mathf.Clamp(resumeTime, 0f, Mathf.Max(0f, resumeClip.length - 0.01f));
            bgmSource.Play();
        }

        /// <summary>공격 타워 명중 시 호출 — 플레이어 공격 사거리 밖이면 조용히 무시(포탑이 많아져도 소리가 감당 안 되는 것 방지).</summary>
        public void PlayTowerAttack(Vector2 towerPosition)
        {
            if (player == null)
            {
                return;
            }

            float range = player.data.attackRange;
            float sqrDistance = ((Vector2)player.transform.position - towerPosition).sqrMagnitude;

            if (sqrDistance <= range * range)
            {
                PlayEffect(towerAttackClip);
            }
        }

        public void PlayPlayerAttack() => PlayEffect(playerAttackClip);

        public void PlaySkill() => PlayEffect(skillClip);

        public void PlayButtonClick() => PlayUiSelect();

        public void PlayTowerBuild() => PlayEffect(towerBuildClip);

        public void PlayTowerDemolish() => PlayEffect(towerDemolishClip);

        public void PlayTitleClick() => PlayEffect(titleClickClip);

        public void PlayMainMenuClick() => PlayUiSelect();

        public void PlaySettingsButtonClick() => PlayUiSelect();

        /// <summary>공용 UI 선택음 — 한 버튼에 명시적 호출과 공용 Emitter가 함께 있어도 한 번만 재생한다.</summary>
        public void PlayUiSelect()
        {
            if (_lastUiSelectFrame == Time.frameCount)
            {
                return;
            }

            _lastUiSelectFrame = Time.frameCount;
            PlayEffect(uiSelectClip, uiSelectCutoffDuration);
        }

        private void PlayEffect(AudioClip clip)
        {
            PlayEffect(clip, effectCutoffDuration);
        }

        private void PlayEffect(AudioClip clip, float cutoffDuration)
        {
            if (clip == null)
            {
                return;
            }

            AudioSource source = GetIdleSfxSource();
            source.clip = clip;
            source.volume = effectVolume;
            source.Play();

            // 클립보다 긴 타이머를 남겨 풀의 AudioSource가 불필요하게 점유되는 일을 막는다.
            float duration = Mathf.Min(Mathf.Max(cutoffDuration, 0.01f), clip.length);
            _activeSfx.Add(new SfxTimer { source = source, remaining = duration });
        }

        private AudioSource GetIdleSfxSource()
        {
            foreach (AudioSource source in _sfxSources)
            {
                if (!source.isPlaying)
                {
                    return source;
                }
            }

            AudioSource newSource = gameObject.AddComponent<AudioSource>();
            newSource.playOnAwake = false;

            if (sfxMixerGroup != null)
            {
                newSource.outputAudioMixerGroup = sfxMixerGroup;
            }

            _sfxSources.Add(newSource);
            return newSource;
        }

        // ── 볼륨 설정 (Configuration 화면의 Master/BGM/SFX 슬라이더가 호출) ──────────────

        private void ApplyPersistedVolumes()
        {
            SetMasterVolume(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            SetBgmVolume(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
            SetSfxVolume(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
        }

        public void SetMasterVolume(float value)
        {
            MasterVolume = value;
            ApplyMixerVolume(MasterVolumeParam, value);
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
        }

        public void SetBgmVolume(float value)
        {
            BgmVolume = value;
            ApplyMixerVolume(BgmVolumeParam, value);
            PlayerPrefs.SetFloat(BgmVolumeKey, value);
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = value;
            ApplyMixerVolume(SfxVolumeParam, value);
            PlayerPrefs.SetFloat(SfxVolumeKey, value);
        }

        /// <summary>Configuration 화면의 APPLY 버튼이 호출 — 슬라이더 조작 중엔 매번 디스크에 쓰지 않고, 여기서 한 번에 확정 저장.</summary>
        public void SaveVolumeSettings()
        {
            PlayerPrefs.Save();
        }

        private void ApplyMixerVolume(string exposedParam, float linearValue)
        {
            if (audioMixer == null)
            {
                return;
            }

            // AudioMixer의 볼륨 파라미터는 데시벨 단위라, 0~1 슬라이더 값을 로그 스케일로 변환.
            // 0에 가까우면 -80dB(사실상 무음)로 바닥을 둬 Log10(0) = -infinity를 피한다.
            float dB = linearValue > 0.0001f ? Mathf.Log10(linearValue) * 20f : -80f;
            audioMixer.SetFloat(exposedParam, dB);
        }
    }
}
