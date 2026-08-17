using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChromaBlast
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        public event System.Action<bool> MuteChanged;

        [Header("Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource clearSfxSource;
        [SerializeField] private AudioListener audioListener;

        [Header("Clips")]
        [SerializeField] private AudioClip musicClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] private AudioClip pickupClip;
        [SerializeField] private AudioClip placeClip;
        [SerializeField] private AudioClip invalidClip;
        [SerializeField] private AudioClip clearClip;
        [SerializeField] private AudioClip strongClearClip;
        [SerializeField] private AudioClip pureClip;
        [SerializeField] private AudioClip popClip;
        [SerializeField] private AudioClip gameOverClip;
        [SerializeField] private AudioClip comboSmallClip;
        [SerializeField] private AudioClip comboBigClip;
        [SerializeField] private AudioClip toggleClip;
        [SerializeField] private AudioClip[] dragGridTickClips;

        public bool Muted { get; private set; }
        public bool SoundEnabled => !Muted;
        public bool MusicEnabled { get; private set; } = true;

        private const int SampleRate = 44100;
        private const float DragGridTickCooldown = 0.04f;
        private const float DragGridTickVolume = 0.20f;

        private int lastDragGridTickIndex = -1;
        private float lastDragGridTickTime = float.NegativeInfinity;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureAudioListener();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            EnsureSources();
            EnsureSingleAudioListener();
            GenerateDefaultClipsIfNeeded();
            bool legacySoundEnabled = SaveManager.Instance == null || !SaveManager.Instance.Data.soundMuted;
            Muted = PlayerPrefs.GetInt("SoundEnabled", legacySoundEnabled ? 1 : 0) == 0;
            MusicEnabled = PlayerPrefs.GetInt("MusicEnabled", legacySoundEnabled ? 1 : 0) != 0;
            ApplyMute();
            PlayMusic();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureSingleAudioListener();
            PlayMusic();
        }

        public void ToggleMute()
        {
            SetSoundEnabled(!SoundEnabled);
        }

        public void SetMuted(bool muted)
        {
            bool changed = Muted != muted;
            Muted = muted;
            ApplyMute();
            SaveManager.Instance?.SetMuted(muted);
            PlayerPrefs.SetInt("SoundEnabled", muted ? 0 : 1);
            PlayerPrefs.Save();
            if (changed)
            {
                MuteChanged?.Invoke(Muted);
            }
        }

        public void SetSoundEnabled(bool enabled)
        {
            SetMuted(!enabled);
        }

        public void ToggleMusic()
        {
            SetMusicEnabled(!MusicEnabled);
        }

        public void SetMusicEnabled(bool enabled)
        {
            MusicEnabled = enabled;
            ApplyMute();
            PlayerPrefs.SetInt("MusicEnabled", enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void PlayClick()
        {
            PlayOneShot(clickClip, 0.32f);
        }

        public void PlayPickup()
        {
            PlayOneShot(pickupClip != null ? pickupClip : clickClip, 0.32f);
        }

        public void PlayPlace()
        {
            PlayOneShot(placeClip, 0.62f);
        }

        public void PlayInvalid()
        {
            PlayOneShot(invalidClip != null ? invalidClip : clickClip, 0.32f);
        }

        public void PlayClear(int lines)
        {
            int noteCount = Mathf.Clamp(lines, 1, 4);
            AudioClip baseClip = lines >= 2 && strongClearClip != null
                ? strongClearClip
                : clearClip;
            if (baseClip == null)
            {
                return;
            }

            StartCoroutine(PlayClearArpeggio(baseClip, noteCount));
        }

        public void PlayPure()
        {
            PlayOneShot(pureClip, 0.72f);
        }

        public void PlayPop()
        {
            PlayOneShot(popClip, 0.78f);
        }

        public void PlayGameOver()
        {
            PlayOneShot(gameOverClip, 0.72f);
        }

        public void PlayComboSmall()
        {
            PlayOneShot(comboSmallClip, 0.72f);
        }

        public void PlayComboBig()
        {
            PlayOneShot(comboBigClip, 0.72f);
        }

        public void PlayToggle()
        {
            PlayOneShot(toggleClip, 0.32f);
        }

        public void PlayDragGridTick()
        {
            if (Muted || dragGridTickClips == null || dragGridTickClips.Length == 0)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - lastDragGridTickTime < DragGridTickCooldown)
            {
                return;
            }

            int startIndex = Random.Range(0, dragGridTickClips.Length);
            int selectedIndex = -1;
            for (int offset = 0; offset < dragGridTickClips.Length; offset++)
            {
                int candidateIndex = (startIndex + offset) % dragGridTickClips.Length;
                if (candidateIndex != lastDragGridTickIndex && dragGridTickClips[candidateIndex] != null)
                {
                    selectedIndex = candidateIndex;
                    break;
                }
            }

            if (selectedIndex < 0)
            {
                for (int i = 0; i < dragGridTickClips.Length; i++)
                {
                    if (dragGridTickClips[i] != null)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            if (selectedIndex < 0)
            {
                return;
            }

            lastDragGridTickIndex = selectedIndex;
            lastDragGridTickTime = now;
            PlayOneShot(dragGridTickClips[selectedIndex], DragGridTickVolume);
        }

        private void PlayMusic()
        {
            if (musicSource == null || musicClip == null)
            {
                return;
            }

            musicSource.clip = musicClip;
            musicSource.loop = true;
            musicSource.volume = 0.16f;
            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        private IEnumerator PlayClearArpeggio(AudioClip clip, int notes)
        {
            float[] pitches = { 0.94f, 1.06f, 1.18f, 1.34f };
            for (int i = 0; i < notes; i++)
            {
                if (clearSfxSource != null)
                {
                    clearSfxSource.pitch = pitches[i];
                }

                PlayOneShot(clearSfxSource, clip, 0.54f);
                yield return new WaitForSeconds(0.028f);
            }

            if (clearSfxSource != null)
            {
                clearSfxSource.pitch = 1f;
            }
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            PlayOneShot(sfxSource, clip, volume);
        }

        private void PlayOneShot(AudioSource source, AudioClip clip, float volume)
        {
            if (Muted || clip == null || source == null)
            {
                return;
            }

            source.PlayOneShot(clip, volume);
        }

        private void EnsureSources()
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }

            if (clearSfxSource == null)
            {
                Transform clearSourceTransform = transform.Find("ClearSfxSource");
                if (clearSourceTransform == null)
                {
                    GameObject clearSourceObject = new GameObject("ClearSfxSource");
                    clearSourceObject.transform.SetParent(transform, false);
                    clearSfxSource = clearSourceObject.AddComponent<AudioSource>();
                }
                else
                {
                    clearSfxSource = clearSourceTransform.GetComponent<AudioSource>();
                    if (clearSfxSource == null)
                    {
                        clearSfxSource = clearSourceTransform.gameObject.AddComponent<AudioSource>();
                    }
                }
            }

            musicSource.playOnAwake = false;
            sfxSource.playOnAwake = false;
            clearSfxSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            sfxSource.spatialBlend = 0f;
            clearSfxSource.spatialBlend = 0f;
            musicSource.volume = 0.16f;
            sfxSource.volume = 0.82f;
            clearSfxSource.volume = 0.82f;
            clearSfxSource.pitch = 1f;
        }

        private void EnsureAudioListener()
        {
            if (audioListener == null)
            {
                audioListener = GetComponent<AudioListener>();
            }

            if (audioListener == null)
            {
                audioListener = gameObject.AddComponent<AudioListener>();
            }

            audioListener.enabled = true;
        }

        private void EnsureSingleAudioListener()
        {
            EnsureAudioListener();

            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null && listeners[i] != audioListener)
                {
                    listeners[i].enabled = false;
                }
            }

            audioListener.enabled = true;
        }

        private void GenerateDefaultClipsIfNeeded()
        {
            clickClip ??= CreateToneClip("Soft_Click", 620f, 0.038f, 0.13f, Wave.Sine);
            pickupClip ??= clickClip;
            placeClip ??= CreateToneClip("Soft_Place", 246.94f, 0.07f, 0.22f, Wave.Triangle, 329.63f);
            invalidClip ??= clickClip;
            clearClip ??= CreateToneClip("Soft_Clear", 493.88f, 0.10f, 0.22f, Wave.Triangle);
            strongClearClip ??= clearClip;
            pureClip ??= CreateChordClip("Soft_Pure", new[] { 392f, 493.88f, 587.33f, 783.99f }, 0.36f, 0.24f);
            popClip ??= CreatePopClip();
            gameOverClip ??= CreateDescendingClip();
            comboSmallClip ??= clearClip;
            comboBigClip ??= pureClip;
            toggleClip ??= clickClip;
            musicClip ??= CreateMusicLoop();
        }

        private enum Wave
        {
            Sine,
            Square,
            Triangle
        }

        private AudioClip CreateToneClip(string clipName, float frequency, float duration, float volume, Wave wave, float secondFrequency = 0f)
        {
            int samples = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float time = i / (float)SampleRate;
                float envelope = Mathf.Exp(-time * 16f) * Mathf.Clamp01(1f - time / duration);
                float sample = Oscillate(frequency, time, wave);
                if (secondFrequency > 0f)
                {
                    sample = (sample + Oscillate(secondFrequency, time, wave)) * 0.5f;
                }

                data[i] = sample * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateChordClip(string clipName, float[] frequencies, float duration, float volume)
        {
            int samples = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float time = i / (float)SampleRate;
                float envelope = Mathf.Clamp01(time * 18f) * Mathf.Exp(-time * 2.8f) * Mathf.Clamp01(1f - time / duration);
                float sample = 0f;
                for (int f = 0; f < frequencies.Length; f++)
                {
                    sample += Mathf.Sin(2f * Mathf.PI * frequencies[f] * time);
                }

                data[i] = sample / frequencies.Length * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreatePopClip()
        {
            const float duration = 0.28f;
            int samples = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[samples];
            uint seed = 912367u;
            for (int i = 0; i < samples; i++)
            {
                float time = i / (float)SampleRate;
                seed = seed * 1664525u + 1013904223u;
                float noise = ((seed & 0xffff) / 32768f) - 1f;
                float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(130f, 58f, time / duration) * time);
                float click = Mathf.Sin(2f * Mathf.PI * 520f * time) * Mathf.Exp(-time * 32f);
                float envelope = Mathf.Exp(-time * 9f) * Mathf.Clamp01(1f - time / duration);
                data[i] = (noise * 0.12f + tone * 0.72f + click * 0.16f) * envelope * 0.48f;
            }

            AudioClip clip = AudioClip.Create("Default_POP", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateDescendingClip()
        {
            const float duration = 0.62f;
            int samples = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float time = i / (float)SampleRate;
                float t = time / duration;
                float frequency = Mathf.Lerp(330f, 146.83f, t);
                float envelope = Mathf.Exp(-time * 2.2f) * Mathf.Clamp01(1f - t);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.26f;
            }

            AudioClip clip = AudioClip.Create("Default_GameOver", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateMusicLoop()
        {
            const float duration = 12f;
            int samples = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[samples];
            float[] roots = { 146.83f, 174.61f, 196f, 130.81f };
            float[] arpeggio = { 1f, 1.5f, 2f, 1.25f, 1.5f, 2f, 1.25f, 1.5f };
            for (int i = 0; i < samples; i++)
            {
                float time = i / (float)SampleRate;
                int chord = Mathf.FloorToInt(time / 3f) % roots.Length;
                float root = roots[chord];

                int arpStep = Mathf.FloorToInt(time * 2f) % arpeggio.Length;
                float arpPhase = Mathf.Repeat(time * 2f, 1f);
                float arpEnv = Mathf.Exp(-arpPhase * 4.2f);
                float arp = Oscillate(root * arpeggio[arpStep], time, Wave.Sine) * arpEnv * 0.055f;

                float slowFade = 0.76f + 0.24f * Mathf.Sin(2f * Mathf.PI * time / duration);
                float pad = Mathf.Sin(2f * Mathf.PI * root * time) * 0.13f
                    + Mathf.Sin(2f * Mathf.PI * root * 1.5f * time) * 0.045f
                    + Mathf.Sin(2f * Mathf.PI * root * 2f * time) * 0.025f;

                float pulse = Mathf.Sin(2f * Mathf.PI * root * 0.5f * time) * 0.018f;
                data[i] = Mathf.Clamp((pad + arp + pulse) * slowFade * 0.62f, -0.55f, 0.55f);
            }

            AudioClip clip = AudioClip.Create("Soft_NeonLoop", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private float Oscillate(float frequency, float time, Wave wave)
        {
            float phase = frequency * time;
            switch (wave)
            {
                case Wave.Square:
                    return Mathf.Sin(2f * Mathf.PI * phase) >= 0f ? 1f : -1f;
                case Wave.Triangle:
                    return 1f - 4f * Mathf.Abs(Mathf.Round(phase - 0.25f) - (phase - 0.25f));
                default:
                    return Mathf.Sin(2f * Mathf.PI * phase);
            }
        }

        private void ApplyMute()
        {
            if (musicSource != null)
            {
                musicSource.mute = !MusicEnabled;
            }

            if (sfxSource != null)
            {
                sfxSource.mute = Muted;
            }

            if (clearSfxSource != null)
            {
                clearSfxSource.mute = Muted;
            }
        }
    }
}
