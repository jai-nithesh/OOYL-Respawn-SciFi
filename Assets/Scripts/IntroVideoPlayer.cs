using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// IntroVideoPlayer – Plays an .mp4 video with audio, then loads Level 1.
/// ──────────────────────────────────────────────────────────────────────────────
/// SETUP:
///   1. Create a new scene called "Intro" and add it to Build Settings at Index 0.
///   2. Import your .mp4 into Assets/Videos/ (or anywhere in Assets/).
///   3. Create a Canvas (Screen Space – Overlay), add a RawImage that fills it.
///   4. Add this script to an empty GameObject in the Intro scene.
///   5. Assign the RawImage and the VideoClip in the Inspector.
///   6. Set firstLevelBuildIndex to 1 (Level 1).
///
/// BEHAVIOR:
///   • On Awake, creates a RenderTexture, assigns it to the VideoPlayer and RawImage.
///   • Plays the video with audio.
///   • When the video finishes (loopPointReached), loads the next scene.
///   • Pressing any key or clicking skips the video instantly.
/// ──────────────────────────────────────────────────────────────────────────────
/// </summary>
public class IntroVideoPlayer : MonoBehaviour
{
    [Header("Video")]
    [Tooltip("Drag the imported .mp4 VideoClip asset here.")]
    public VideoClip videoClip;

    [Tooltip("The RawImage on the Canvas that displays the video.")]
    public RawImage displayImage;

    [Header("Transition")]
    [Tooltip("Build Index of the first gameplay scene (Level 1).")]
    public int firstLevelBuildIndex = 1;

    [Tooltip("Allow skipping the video by pressing any key or clicking.")]
    public bool allowSkip = true;

    [Tooltip("Seconds to wait before allowing skip (prevents accidental skips).")]
    public float skipDelay = 1.5f;

    [Header("Fade")]
    [Tooltip("Fade to black before transitioning to Level 1.")]
    public bool fadeOutBeforeLoad = true;

    [Tooltip("Duration of the fade-out in seconds.")]
    public float fadeOutDuration = 0.5f;

    // ───────── Private State ─────────
    private VideoPlayer videoPlayer;
    private AudioSource audioSource;
    private RenderTexture renderTexture;
    private bool videoFinished = false;
    private bool isLoadingLevel = false;
    private float elapsedTime = 0f;

    // Optional: a black overlay for fade-out
    private Image fadeOverlay;

    private void Awake()
    {
        // ── Create VideoPlayer ──
        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.clip = videoClip;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.skipOnDrop = true;

        // ── Create RenderTexture ──
        renderTexture = new RenderTexture(1920, 1080, 0);
        renderTexture.Create();
        videoPlayer.targetTexture = renderTexture;

        // ── Assign RenderTexture to RawImage ──
        if (displayImage != null)
        {
            displayImage.texture = renderTexture;
        }

        // ── Audio Setup ──
        // Use an AudioSource for direct audio playback from the video
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = 1f;

        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, audioSource);

        // ── Subscribe to end-of-video event ──
        videoPlayer.loopPointReached += OnVideoFinished;

        // ── Create fade overlay (optional) ──
        if (fadeOutBeforeLoad)
        {
            CreateFadeOverlay();
        }
    }

    private void Start()
    {
        if (videoClip == null)
        {
            Debug.LogError("[IntroVideoPlayer] No VideoClip assigned! Skipping to Level 1.");
            LoadFirstLevel();
            return;
        }

        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += (VideoPlayer vp) =>
        {
            vp.Play();
        };
    }

    private void Update()
    {
        elapsedTime += Time.unscaledDeltaTime;

        // ── Skip Detection ──
        if (allowSkip && elapsedTime > skipDelay && !isLoadingLevel)
        {
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
            {
                OnVideoFinished(videoPlayer);
            }
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        if (videoFinished) return;
        videoFinished = true;

        if (fadeOutBeforeLoad)
        {
            StartCoroutine(FadeOutAndLoad());
        }
        else
        {
            LoadFirstLevel();
        }
    }

    private IEnumerator FadeOutAndLoad()
    {
        isLoadingLevel = true;

        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeOutDuration);
                fadeOverlay.color = new Color(0f, 0f, 0f, alpha);
                yield return null;
            }
            fadeOverlay.color = Color.black;
        }

        // Small buffer so the fade is visible
        yield return new WaitForSecondsRealtime(0.1f);

        LoadFirstLevel();
    }

    private void LoadFirstLevel()
    {
        // Stop video and clean up
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        SceneManager.LoadScene(firstLevelBuildIndex);
    }

    private void CreateFadeOverlay()
    {
        // Find or create a canvas
        Canvas canvas = displayImage != null
            ? displayImage.GetComponentInParent<Canvas>()
            : null;

        if (canvas == null)
        {
            // Create a dedicated canvas for the fade
            GameObject canvasObj = new GameObject("FadeCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create the black overlay image
        GameObject overlayObj = new GameObject("FadeOverlay");
        overlayObj.transform.SetParent(canvas.transform, false);

        fadeOverlay = overlayObj.AddComponent<Image>();
        fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
        fadeOverlay.raycastTarget = false;

        RectTransform rt = fadeOverlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        overlayObj.SetActive(false);
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
        }
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
        }
    }
}
