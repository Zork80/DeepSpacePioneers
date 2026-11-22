using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Audio
{
    public enum MusicPhase
    {
        Planning,
        Trade,
        Movement,
        Event
    }

    /// <summary>
    /// Manages the 4 phase-dependent background loops (Planning, Trade, Movement, Event).
    /// All loops share the same tempo, key, and exact sample length (120 BPM, 8 bars = 705,600 samples at 44.1 kHz).
    /// They run simultaneously in lockstep, and this manager smoothly crossfades between them
    /// using an equal-power curve as the game transitions through its phases.
    /// </summary>
    public class PhaseAudioManager : MonoBehaviour
    {
        public static PhaseAudioManager Instance { get; private set; }

        [Header("Audio Clips")]
        [SerializeField] private AudioClip _planningClip;
        [SerializeField] private AudioClip _tradeClip;
        [SerializeField] private AudioClip _movementClip;
        [SerializeField] private AudioClip _eventClip;

        [Header("Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float _masterVolume = 0.20f;
        [SerializeField] private float _crossfadeDuration = 2.0f;

        [Header("Status (Read-Only)")]
        [SerializeField] private MusicPhase _currentPhase = MusicPhase.Planning;

        private AudioSource _planningSource;
        private AudioSource _tradeSource;
        private AudioSource _movementSource;
        private AudioSource _eventSource;

        private float _planningWeight = 1f;
        private float _tradeWeight = 0f;
        private float _movementWeight = 0f;
        private float _eventWeight = 0f;

        private TriMap _triMap;
        private bool _isInitialized = false;

        public MusicPhase CurrentPhase => _currentPhase;

        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                UpdateTargetVolumes();
            }
        }

        public float CrossfadeDuration
        {
            get => _crossfadeDuration;
            set => _crossfadeDuration = Mathf.Max(0.05f, value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureInitialized()
        {
            if (Instance != null) return;

            GameObject bgGo = GameObject.Find("BackgroundAudio");
            if (bgGo == null)
            {
                bgGo = new GameObject("BackgroundAudio");
                DontDestroyOnLoad(bgGo);
            }

            if (bgGo.GetComponent<PhaseAudioManager>() == null)
            {
                bgGo.AddComponent<PhaseAudioManager>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Silence legacy single AudioSource if present on this GameObject
            AudioSource legacySource = GetComponent<AudioSource>();
            if (legacySource != null)
            {
                legacySource.Stop();
                legacySource.enabled = false;
            }

            InitializeAudio();
        }

        private void Start()
        {
            _triMap = TriMap.Instance;
            if (_triMap != null)
            {
                _triMap.OnTurnPhaseChanged += HandleTurnPhaseChanged;
                _triMap.OnEncounterStateChanged += HandleEncounterStateChanged;
            }
            EvaluatePhase();
        }

        private void OnDestroy()
        {
            if (_triMap != null)
            {
                _triMap.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
                _triMap.OnEncounterStateChanged -= HandleEncounterStateChanged;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void InitializeAudio()
        {
            if (_isInitialized) return;

            LoadClips();
            CreateAudioSources();
            StartSynchronizedPlayback();

            _isInitialized = true;
        }

        private void LoadClips()
        {
            if (_planningClip == null) _planningClip = Resources.Load<AudioClip>("Audio/Loops/loop_planning_fat");
            if (_tradeClip == null) _tradeClip = Resources.Load<AudioClip>("Audio/Loops/loop_trade_fat");
            if (_movementClip == null) _movementClip = Resources.Load<AudioClip>("Audio/Loops/loop_movement_fat");
            if (_eventClip == null) _eventClip = Resources.Load<AudioClip>("Audio/Loops/loop_event_fat");

            // Fallback: check non-resources folder if editor asset database or legacy path
            if (_planningClip == null) Debug.LogWarning("[PhaseAudioManager] Could not load loop_planning_fat from Resources.");
            if (_tradeClip == null) Debug.LogWarning("[PhaseAudioManager] Could not load loop_trade_fat from Resources.");
            if (_movementClip == null) Debug.LogWarning("[PhaseAudioManager] Could not load loop_movement_fat from Resources.");
            if (_eventClip == null) Debug.LogWarning("[PhaseAudioManager] Could not load loop_event_fat from Resources.");
        }

        private void CreateAudioSources()
        {
            _planningSource = CreateSubSource("Source_Planning", _planningClip);
            _tradeSource = CreateSubSource("Source_Trade", _tradeClip);
            _movementSource = CreateSubSource("Source_Movement", _movementClip);
            _eventSource = CreateSubSource("Source_Event", _eventClip);
        }

        private AudioSource CreateSubSource(string childName, AudioClip clip)
        {
            Transform existing = transform.Find(childName);
            GameObject childObj = existing != null ? existing.gameObject : new GameObject(childName);
            childObj.transform.SetParent(transform, false);

            AudioSource src = childObj.GetComponent<AudioSource>();
            if (src == null) src = childObj.AddComponent<AudioSource>();

            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D flat stereo playback
            src.volume = 0f;
            return src;
        }

        private void StartSynchronizedPlayback()
        {
            // Schedule all 4 tracks to start at the exact same audio hardware DSP time
            double scheduledStartTime = AudioSettings.dspTime + 0.15;

            if (_planningSource.clip != null) _planningSource.PlayScheduled(scheduledStartTime);
            if (_tradeSource.clip != null) _tradeSource.PlayScheduled(scheduledStartTime);
            if (_movementSource.clip != null) _movementSource.PlayScheduled(scheduledStartTime);
            if (_eventSource.clip != null) _eventSource.PlayScheduled(scheduledStartTime);

            // Initially set planning track to audible
            if (_planningSource != null) _planningSource.volume = _masterVolume;
        }

        private void Update()
        {
            if (_triMap == null)
            {
                _triMap = TriMap.Instance;
                if (_triMap != null)
                {
                    _triMap.OnTurnPhaseChanged += HandleTurnPhaseChanged;
                    _triMap.OnEncounterStateChanged += HandleEncounterStateChanged;
                }
            }

            EvaluatePhase();
            CrossfadeSources();
            EnsureBeatSync();
        }

        private void HandleTurnPhaseChanged(Player player, GameTurnPhase phase)
        {
            EvaluatePhase();
        }

        private void HandleEncounterStateChanged(bool isPending)
        {
            EvaluatePhase();
        }

        public void EvaluatePhase()
        {
            if (_triMap == null)
            {
                SetPhase(MusicPhase.Planning);
                return;
            }

            // 1. Event / Encounter / Combat has highest music priority
            bool isEvent = _triMap.EncounterPending ||
                           _triMap.CurrentEncounterCard != null ||
                           _triMap.FriendshipCardSelectionPending ||
                           _triMap.IsGameOver;

            if (isEvent)
            {
                SetPhase(MusicPhase.Event);
                return;
            }

            // 2. Founding phase or regular Production phase -> Planning loop
            if (_triMap.CurrentFoundingPhase != FoundingPhase.Complete || _triMap.CurrentPhase == GameTurnPhase.Production)
            {
                SetPhase(MusicPhase.Planning);
                return;
            }

            // 3. Trade and Build phase -> Trade loop
            if (_triMap.CurrentPhase == GameTurnPhase.TradeAndBuild)
            {
                SetPhase(MusicPhase.Trade);
                return;
            }

            // 4. Flight phase -> Movement loop
            if (_triMap.CurrentPhase == GameTurnPhase.Flight)
            {
                SetPhase(MusicPhase.Movement);
                return;
            }
        }

        public void SetPhase(MusicPhase newPhase)
        {
            if (_currentPhase == newPhase) return;

            _currentPhase = newPhase;
            UpdateTargetVolumes();
        }

        private void UpdateTargetVolumes()
        {
            // Volumes are updated dynamically in CrossfadeSources
        }

        private void CrossfadeSources()
        {
            float targetPlanning = (_currentPhase == MusicPhase.Planning) ? 1f : 0f;
            float targetTrade = (_currentPhase == MusicPhase.Trade) ? 1f : 0f;
            float targetMovement = (_currentPhase == MusicPhase.Movement) ? 1f : 0f;
            float targetEvent = (_currentPhase == MusicPhase.Event) ? 1f : 0f;

            float step = (_crossfadeDuration > 0f)
                ? (Time.unscaledDeltaTime / _crossfadeDuration)
                : 1f;

            _planningWeight = Mathf.MoveTowards(_planningWeight, targetPlanning, step);
            _tradeWeight = Mathf.MoveTowards(_tradeWeight, targetTrade, step);
            _movementWeight = Mathf.MoveTowards(_movementWeight, targetMovement, step);
            _eventWeight = Mathf.MoveTowards(_eventWeight, targetEvent, step);

            if (_planningSource != null)
                _planningSource.volume = Mathf.Sin(_planningWeight * Mathf.PI * 0.5f) * _masterVolume;

            if (_tradeSource != null)
                _tradeSource.volume = Mathf.Sin(_tradeWeight * Mathf.PI * 0.5f) * _masterVolume;

            if (_movementSource != null)
                _movementSource.volume = Mathf.Sin(_movementWeight * Mathf.PI * 0.5f) * _masterVolume;

            if (_eventSource != null)
                _eventSource.volume = Mathf.Sin(_eventWeight * Mathf.PI * 0.5f) * _masterVolume;
        }

        /// <summary>
        /// Resynchronizes audio samples across tracks if any minor audio thread drift occurs.
        /// </summary>
        private void EnsureBeatSync()
        {
            if (_planningSource == null || !_planningSource.isPlaying) return;

            int masterSamples = _planningSource.timeSamples;
            const int maxDriftSamples = 1500; // ~34ms tolerance

            if (_tradeSource != null && _tradeSource.isPlaying && Mathf.Abs(_tradeSource.timeSamples - masterSamples) > maxDriftSamples)
                _tradeSource.timeSamples = masterSamples;

            if (_movementSource != null && _movementSource.isPlaying && Mathf.Abs(_movementSource.timeSamples - masterSamples) > maxDriftSamples)
                _movementSource.timeSamples = masterSamples;

            if (_eventSource != null && _eventSource.isPlaying && Mathf.Abs(_eventSource.timeSamples - masterSamples) > maxDriftSamples)
                _eventSource.timeSamples = masterSamples;
        }
    }
}

