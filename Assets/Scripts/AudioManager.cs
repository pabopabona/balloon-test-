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

    [Header("터짐 소리 피치 랜덤")]
    [Tooltip("풍선이 터질 때마다 피치(음높이)를 이 범위 안에서 무작위로 정합니다. 1이 원래 음높이예요. " +
             "너무 넓히면 어색해지니 0.9 ~ 1.1 정도로 살짝만 주는 걸 추천해요. 둘 다 1로 두면 랜덤이 꺼집니다.")]
    [Range(0.5f, 2f)] public float popPitchMin = 0.92f;
    [Range(0.5f, 2f)] public float popPitchMax = 1.08f;

    [Header("효과음 동시 재생")]
    [Tooltip("효과음을 겹쳐 재생하기 위한 오디오 소스 개수. 피치는 소스 단위로 적용되기 때문에, " +
             "소리마다 다른 피치를 주려면 소스를 여러 개 돌려 써야 합니다. 터짐이 연달아 일어나는 게임이라 넉넉하게 잡아두세요.")]
    [Range(2, 16)] public int sfxPoolSize = 8;

    private AudioSource[] sfxPool;
    private int sfxPoolIndex;

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

        BuildSfxPool();

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
    private void HandlePopped(int count)
    {
        float pitch = Random.Range(Mathf.Min(popPitchMin, popPitchMax), Mathf.Max(popPitchMin, popPitchMax));
        PlayClip(popClip, pitch);
    }
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
        PlayClip(clip, 1f);
    }

    /// <summary>
    /// 지정한 피치(1 = 원래 음높이)로 효과음을 재생합니다. 소스 풀에서 다음 소스를 골라
    /// 그 소스에만 피치를 적용하므로, 이미 재생 중인 다른 효과음의 음높이는 바뀌지 않습니다.
    /// </summary>
    public void PlayClip(AudioClip clip, float pitch)
    {
        if (clip == null) return;

        AudioSource source = NextPooledSource();
        if (source == null) return;

        source.pitch = pitch;
        source.volume = sfxVolume;
        source.clip = clip;
        source.Play();
    }

    private void BuildSfxPool()
    {
        sfxPool = new AudioSource[Mathf.Max(2, sfxPoolSize)];

        for (int i = 0; i < sfxPool.Length; i++)
        {
            // 첫 번째는 Inspector에서 연결했거나 위에서 만든 sfxSource를 그대로 재사용
            AudioSource src = (i == 0 && sfxSource != null)
                ? sfxSource
                : gameObject.AddComponent<AudioSource>();

            src.playOnAwake = false;
            src.loop = false;
            sfxPool[i] = src;
        }
    }

    private AudioSource NextPooledSource()
    {
        if (sfxPool == null || sfxPool.Length == 0) return sfxSource;

        // 아직 재생 중이 아닌 소스를 우선 사용하고, 전부 재생 중이면 가장 오래된 소스를 끊고 재사용
        for (int i = 0; i < sfxPool.Length; i++)
        {
            int idx = (sfxPoolIndex + i) % sfxPool.Length;
            if (!sfxPool[idx].isPlaying)
            {
                sfxPoolIndex = (idx + 1) % sfxPool.Length;
                return sfxPool[idx];
            }
        }

        AudioSource fallback = sfxPool[sfxPoolIndex];
        sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Length;
        return fallback;
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