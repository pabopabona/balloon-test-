using UnityEngine;

/// <summary>
/// 게임 곳곳의 이벤트(발사, 부착, 터짐, 콤보, 게임오버)를 구독해서 알맞은 효과음을 재생하는
/// 중앙 오디오 매니저. 씬에 하나만 존재해야 하는 싱글턴입니다.
/// 각 클립은 Inspector에서 비워두면 그냥 재생을 건너뜁니다(에러 없음).
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("참조")]
    public LauncherController launcher;
    public HexGridManager gridManager;
    public GameManager gameManager;

    [Header("오디오 소스")]
    [Tooltip("효과음 재생용 AudioSource. 비워두면 자동으로 하나 추가합니다.")]
    public AudioSource sfxSource;

    [Tooltip("배경음악(BGM) 재생용 AudioSource. 비워두면 자동으로 하나 추가합니다.")]
    public AudioSource musicSource;

    [Header("배경음악")]
    [Tooltip("게임 시작과 동시에 반복 재생할 배경음악. 비워두면 재생하지 않습니다.")]
    public AudioClip bgmClip;

    [Range(0f, 1f)]
    public float musicVolume = 0.5f;

    [Header("효과음 클립")]
    public AudioClip shootClip;
    public AudioClip attachClip;
    public AudioClip popClip;
    public AudioClip comboClip;
    public AudioClip gameOverClip;
    public AudioClip buttonClip;

    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
        }
        musicSource.loop = true;
    }

    void Start()
    {
        if (bgmClip != null)
        {
            PlayMusic(bgmClip);
        }
    }

    void OnEnable()
    {
        if (launcher != null)
            launcher.OnShoot += HandleShoot;

        if (gridManager != null)
        {
            gridManager.OnBalloonAttached += HandleAttached;
            gridManager.OnBalloonsPopped += HandlePopped;
            gridManager.OnComboStep += HandleCombo;
        }

        if (gameManager != null)
            gameManager.OnGameOver += HandleGameOver;
    }

    void OnDisable()
    {
        if (launcher != null)
            launcher.OnShoot -= HandleShoot;

        if (gridManager != null)
        {
            gridManager.OnBalloonAttached -= HandleAttached;
            gridManager.OnBalloonsPopped -= HandlePopped;
            gridManager.OnComboStep -= HandleCombo;
        }

        if (gameManager != null)
            gameManager.OnGameOver -= HandleGameOver;
    }

    private void HandleShoot(Vector3 spawnPos, Vector2 velocity) => PlayClip(shootClip);
    private void HandleAttached() => PlayClip(attachClip);
    private void HandlePopped(int count) => PlayClip(popClip);
    private void HandleCombo(int comboDepth)
    {
        if (comboDepth >= 2)
            PlayClip(comboClip);
    }
    private void HandleGameOver(string reason)
    {
        PlayClip(gameOverClip);
        StopMusic();
    }

    /// <summary>
    /// 버튼 클릭 등 UI 상호작용용 효과음. Button의 On Click()에 직접 연결해서 쓸 수 있습니다.
    /// </summary>
    public void PlayButtonClick() => PlayClip(buttonClip);

    /// <summary>
    /// 임의의 클립을 재생합니다. 클립이 비어있으면 아무 동작도 하지 않습니다.
    /// </summary>
    public void PlayClip(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    /// <summary>
    /// 배경음악을 교체하고 반복 재생을 시작합니다. 이미 같은 클립이 재생 중이면 다시 시작하지 않습니다.
    /// </summary>
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null) return;

        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    /// <summary>
    /// 배경음악을 멈춥니다.
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }
}