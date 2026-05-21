using System;
using UnityEngine;

namespace GreedLast
{
    public enum GreedLastRunChannel
    {
        None = 0,
        Left = 1,
        Center = 2,
        Right = 3,
    }

    public enum GreedLastTimingType
    {
        None = 0,
        Perfect = 1,
        Clutch = 2,
        Offbeat = 3,
    }

    public enum GreedLastJudgementResult
    {
        None = 0,
        Success = 1,
        Good = 2,
        Miss = 3,
    }

    public enum GreedLastRunPhase
    {
        None = 0,
        ChoiceSelecting = 1,
        RunPlaying = 2,
        ChapterWrapUp = 3,
        CoreRetrieve = 4,
        EscapeRunning = 5,
        ClearResolving = 6,
        FailResolving = 7,
    }

    public enum GreedLastChoiceKind
    {
        None = 0,
        StartGift = 1,
        Node = 2,
        Gift = 3,
        Relic = 4,
    }

    public enum GreedLastDevShortcut
    {
        GiftChoice = 0,
        RelicChoice = 1,
        AfterCore = 2,
        BeforeClear = 3,
    }

    public enum GreedLastRunAttemptOutcome
    {
        None = 0,
        Failed = 1,
        Abandoned = 2,
    }

    public readonly struct GreedLastRunRecord
    {
        public GreedLastRunRecord(
            bool isValid,
            int score,
            int health,
            int combo,
            int focus,
            float distance,
            string loadoutName,
            string startGift,
            string primaryGift,
            string relic,
            string choiceSummary,
            string timingProfile)
        {
            IsValid = isValid;
            Score = score;
            Health = health;
            Combo = combo;
            Focus = focus;
            Distance = distance;
            LoadoutName = loadoutName ?? string.Empty;
            StartGift = startGift ?? string.Empty;
            PrimaryGift = primaryGift ?? string.Empty;
            Relic = relic ?? string.Empty;
            ChoiceSummary = choiceSummary ?? string.Empty;
            TimingProfile = timingProfile ?? string.Empty;
        }

        public bool IsValid { get; }
        public int Score { get; }
        public int Health { get; }
        public int Combo { get; }
        public int Focus { get; }
        public float Distance { get; }
        public string LoadoutName { get; }
        public string StartGift { get; }
        public string PrimaryGift { get; }
        public string Relic { get; }
        public string ChoiceSummary { get; }
        public string TimingProfile { get; }

        public static GreedLastRunRecord Empty => new GreedLastRunRecord(
            false,
            0,
            0,
            0,
            0,
            0f,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    public readonly struct GreedLastRunAttemptRecord
    {
        public GreedLastRunAttemptRecord(
            bool isValid,
            GreedLastRunAttemptOutcome outcome,
            int score,
            int health,
            int combo,
            int focus,
            int maxCombo,
            int successCount,
            int goodCount,
            int missCount,
            float distance,
            int chapterIndex,
            int chapterProgress,
            int chapterTarget,
            bool coreRetrieved,
            string missReason,
            string choiceSummary,
            string timingProfile)
        {
            IsValid = isValid;
            Outcome = outcome;
            Score = score;
            Health = health;
            Combo = combo;
            Focus = focus;
            MaxCombo = maxCombo;
            SuccessCount = successCount;
            GoodCount = goodCount;
            MissCount = missCount;
            Distance = distance;
            ChapterIndex = chapterIndex;
            ChapterProgress = chapterProgress;
            ChapterTarget = chapterTarget;
            CoreRetrieved = coreRetrieved;
            MissReason = missReason ?? string.Empty;
            ChoiceSummary = choiceSummary ?? string.Empty;
            TimingProfile = timingProfile ?? string.Empty;
        }

        public bool IsValid { get; }
        public GreedLastRunAttemptOutcome Outcome { get; }
        public int Score { get; }
        public int Health { get; }
        public int Combo { get; }
        public int Focus { get; }
        public int MaxCombo { get; }
        public int SuccessCount { get; }
        public int GoodCount { get; }
        public int MissCount { get; }
        public float Distance { get; }
        public int ChapterIndex { get; }
        public int ChapterProgress { get; }
        public int ChapterTarget { get; }
        public bool CoreRetrieved { get; }
        public string MissReason { get; }
        public string ChoiceSummary { get; }
        public string TimingProfile { get; }

        public static GreedLastRunAttemptRecord Empty => new GreedLastRunAttemptRecord(
            false,
            GreedLastRunAttemptOutcome.None,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0f,
            0,
            0,
            0,
            false,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    public readonly struct GreedLastInfiniteRunRecord
    {
        public GreedLastInfiniteRunRecord(
            bool isValid,
            int score,
            float distance,
            int sectionsCleared,
            int maxThreatLevel,
            string loadoutName,
            int successCount,
            int goodCount,
            int missCount,
            int maxCombo,
            string timingProfile)
        {
            IsValid = isValid;
            Score = score;
            Distance = distance;
            SectionsCleared = sectionsCleared;
            MaxThreatLevel = maxThreatLevel;
            LoadoutName = loadoutName ?? string.Empty;
            SuccessCount = successCount;
            GoodCount = goodCount;
            MissCount = missCount;
            MaxCombo = maxCombo;
            TimingProfile = timingProfile ?? string.Empty;
        }

        public bool IsValid { get; }
        public int Score { get; }
        public float Distance { get; }
        public int SectionsCleared { get; }
        public int MaxThreatLevel { get; }
        public string LoadoutName { get; }
        public int SuccessCount { get; }
        public int GoodCount { get; }
        public int MissCount { get; }
        public int MaxCombo { get; }
        public string TimingProfile { get; }

        public static GreedLastInfiniteRunRecord Empty => new GreedLastInfiniteRunRecord(
            false,
            0,
            0f,
            0,
            0,
            string.Empty,
            0,
            0,
            0,
            0,
            string.Empty);
    }

    public readonly struct GreedLastInfiniteLoadoutBonus
    {
        public GreedLastInfiniteLoadoutBonus(int healthBonus, int focusBonus, int comboBonus, int scoreBonus)
        {
            HealthBonus = healthBonus;
            FocusBonus = focusBonus;
            ComboBonus = comboBonus;
            ScoreBonus = scoreBonus;
        }

        public int HealthBonus { get; }
        public int FocusBonus { get; }
        public int ComboBonus { get; }
        public int ScoreBonus { get; }
        public bool HasAnyBonus => HealthBonus > 0 || FocusBonus > 0 || ComboBonus > 0 || ScoreBonus > 0;
    }

    public readonly struct GreedLastPatternModel
    {
        public GreedLastPatternModel(
            string patternId,
            GreedLastRunChannel channel,
            GreedLastTimingType timingType,
            string prompt,
            float telegraphSeconds,
            float coreSeconds,
            float successWindowSeconds,
            float goodWindowSeconds,
            float hitSeconds)
        {
            PatternId = patternId ?? string.Empty;
            Channel = channel;
            TimingType = timingType;
            Prompt = prompt ?? string.Empty;
            TelegraphSeconds = telegraphSeconds;
            CoreSeconds = coreSeconds;
            SuccessWindowSeconds = successWindowSeconds;
            GoodWindowSeconds = goodWindowSeconds;
            HitSeconds = hitSeconds;
        }

        public string PatternId { get; }
        public GreedLastRunChannel Channel { get; }
        public GreedLastTimingType TimingType { get; }
        public string Prompt { get; }
        public float TelegraphSeconds { get; }
        public float CoreSeconds { get; }
        public float SuccessWindowSeconds { get; }
        public float GoodWindowSeconds { get; }
        public float HitSeconds { get; }

        public bool IsValid => Channel != GreedLastRunChannel.None;

        public static GreedLastPatternModel Empty => new GreedLastPatternModel(
            "none",
            GreedLastRunChannel.None,
            GreedLastTimingType.None,
            "다음 함정을 생성하세요.",
            0f,
            0f,
            0f,
            0f,
            0f);
    }

    public readonly struct GreedLastJudgementOutcome
    {
        public GreedLastJudgementOutcome(
            GreedLastJudgementResult result,
            GreedLastTimingType timingType,
            string message,
            string ignoreReason,
            string missReason)
        {
            Result = result;
            TimingType = timingType;
            Message = message ?? string.Empty;
            IgnoreReason = ignoreReason ?? string.Empty;
            MissReason = missReason ?? string.Empty;
        }

        public GreedLastJudgementResult Result { get; }
        public GreedLastTimingType TimingType { get; }
        public string Message { get; }
        public string IgnoreReason { get; }
        public string MissReason { get; }
    }

    public readonly struct GreedLastRunSnapshot
    {
        public GreedLastRunSnapshot(
            GreedLastPatternModel pattern,
            string judgementText,
            string ignoreReason,
            string missReason,
            int score,
            int health,
            int combo,
            int focus,
            int maxFocus,
            int maxCombo,
            int successCount,
            int goodCount,
            int missCount,
            int infiniteSectionsCleared,
            int infiniteThreatLevel,
            float distance,
            int chapterIndex,
            int chapterProgress,
            int chapterTarget,
            GreedLastRunPhase phase,
            GreedLastChoiceKind choiceKind,
            string choicePrompt,
            string choiceLeft,
            string choiceCenter,
            string choiceRight,
            bool coreRetrieved,
            bool escapeSucceeded,
            bool saveEligible,
            GreedLastJudgementResult lastResult,
            bool activePattern,
            bool autoFlow,
            bool stopped,
            bool paused,
            bool resumeCountdownActive,
            bool startCountdownActive,
            bool staggered,
            bool devInvincible,
            bool infiniteMode,
            float elapsedSeconds,
            float beatProgress,
            float coreDeltaSeconds,
            float resumeCountdownRemainingSeconds,
            float startCountdownRemainingSeconds,
            float inputTimingOffsetSeconds,
            string timingProfile)
        {
            Pattern = pattern;
            JudgementText = judgementText ?? string.Empty;
            IgnoreReason = ignoreReason ?? string.Empty;
            MissReason = missReason ?? string.Empty;
            Score = score;
            Health = health;
            Combo = combo;
            Focus = focus;
            MaxFocus = maxFocus;
            MaxCombo = maxCombo;
            SuccessCount = successCount;
            GoodCount = goodCount;
            MissCount = missCount;
            InfiniteSectionsCleared = infiniteSectionsCleared;
            InfiniteThreatLevel = infiniteThreatLevel;
            Distance = distance;
            ChapterIndex = chapterIndex;
            ChapterProgress = chapterProgress;
            ChapterTarget = chapterTarget;
            Phase = phase;
            ChoiceKind = choiceKind;
            ChoicePrompt = choicePrompt ?? string.Empty;
            ChoiceLeft = choiceLeft ?? string.Empty;
            ChoiceCenter = choiceCenter ?? string.Empty;
            ChoiceRight = choiceRight ?? string.Empty;
            CoreRetrieved = coreRetrieved;
            EscapeSucceeded = escapeSucceeded;
            SaveEligible = saveEligible;
            LastResult = lastResult;
            ActivePattern = activePattern;
            AutoFlow = autoFlow;
            Stopped = stopped;
            Paused = paused;
            ResumeCountdownActive = resumeCountdownActive;
            StartCountdownActive = startCountdownActive;
            Staggered = staggered;
            DevInvincible = devInvincible;
            InfiniteMode = infiniteMode;
            ElapsedSeconds = elapsedSeconds;
            BeatProgress = beatProgress;
            CoreDeltaSeconds = coreDeltaSeconds;
            ResumeCountdownRemainingSeconds = resumeCountdownRemainingSeconds;
            StartCountdownRemainingSeconds = startCountdownRemainingSeconds;
            InputTimingOffsetSeconds = inputTimingOffsetSeconds;
            TimingProfile = timingProfile ?? string.Empty;
        }

        public GreedLastPatternModel Pattern { get; }
        public string JudgementText { get; }
        public string IgnoreReason { get; }
        public string MissReason { get; }
        public int Score { get; }
        public int Health { get; }
        public int Combo { get; }
        public int Focus { get; }
        public int MaxFocus { get; }
        public int MaxCombo { get; }
        public int SuccessCount { get; }
        public int GoodCount { get; }
        public int MissCount { get; }
        public int InfiniteSectionsCleared { get; }
        public int InfiniteThreatLevel { get; }
        public float Distance { get; }
        public int ChapterIndex { get; }
        public int ChapterProgress { get; }
        public int ChapterTarget { get; }
        public GreedLastRunPhase Phase { get; }
        public GreedLastChoiceKind ChoiceKind { get; }
        public string ChoicePrompt { get; }
        public string ChoiceLeft { get; }
        public string ChoiceCenter { get; }
        public string ChoiceRight { get; }
        public bool ChoiceActive => Phase == GreedLastRunPhase.ChoiceSelecting;
        public bool CoreRetrieved { get; }
        public bool EscapeSucceeded { get; }
        public bool SaveEligible { get; }
        public GreedLastJudgementResult LastResult { get; }
        public bool ActivePattern { get; }
        public bool AutoFlow { get; }
        public bool Stopped { get; }
        public bool Paused { get; }
        public bool ResumeCountdownActive { get; }
        public bool StartCountdownActive { get; }
        public bool CountdownActive => ResumeCountdownActive || StartCountdownActive;
        public bool Staggered { get; }
        public bool DevInvincible { get; }
        public bool InfiniteMode { get; }
        public float ElapsedSeconds { get; }
        public float BeatProgress { get; }
        public float CoreDeltaSeconds { get; }
        public float ResumeCountdownRemainingSeconds { get; }
        public float StartCountdownRemainingSeconds { get; }
        public float CountdownRemainingSeconds => ResumeCountdownActive ? ResumeCountdownRemainingSeconds : StartCountdownRemainingSeconds;
        public float InputTimingOffsetSeconds { get; }
        public string TimingProfile { get; }
    }

    public sealed class GreedLastRunCore
    {
        private const int MaxHealth = 3;
        private const int BonusHealthCap = MaxHealth + 2;
        private const int MaxFocus = 3;
        private const int MaxChoiceHistory = 12;
        private const float StaggerSeconds = 0.55f;
        private const float AutoGapSeconds = 0.75f;
        private const float RunSpeedMetersPerSecond = 4.2f;
        private const float MinInputTimingOffsetSeconds = -0.2f;
        private const float MaxInputTimingOffsetSeconds = 0.2f;
        private const int ChapterTargetPatterns = 3;
        private const int InfiniteThreatMaxLevel = 5;
        private const float StartCountdownSeconds = 1.4f;
        private const float StartBufferSeconds = 0.25f;
        private const float ResumeCountdownSeconds = 1.4f;
        private const float ResumeBufferSeconds = 0.65f;
        private const int TimingRecommendationMinSamples = 3;
        private const string InputTimingOffsetPrefsKey = "GreedLast.RunCore.InputTimingOffsetSeconds";

        private readonly GreedLastPatternModel[] patternDeck =
        {
            new GreedLastPatternModel("left_floor_perfect", GreedLastRunChannel.Left, GreedLastTimingType.Perfect, "좌측 바닥 창살", 0.45f, 1.05f, 0.10f, 0.30f, 1.48f),
            new GreedLastPatternModel("center_gate_clutch", GreedLastRunChannel.Center, GreedLastTimingType.Clutch, "정면 압박문", 0.55f, 1.28f, 0.13f, 0.36f, 1.72f),
            new GreedLastPatternModel("right_blade_offbeat", GreedLastRunChannel.Right, GreedLastTimingType.Offbeat, "우측 엇박 칼날", 0.40f, 0.92f, 0.11f, 0.31f, 1.36f),
            new GreedLastPatternModel("left_wall_clutch", GreedLastRunChannel.Left, GreedLastTimingType.Clutch, "좌측 회전 벽", 0.50f, 1.18f, 0.12f, 0.34f, 1.58f),
            new GreedLastPatternModel("center_sand_perfect", GreedLastRunChannel.Center, GreedLastTimingType.Perfect, "중앙 모래 함몰", 0.44f, 1.00f, 0.10f, 0.29f, 1.42f),
            new GreedLastPatternModel("right_pillar_clutch", GreedLastRunChannel.Right, GreedLastTimingType.Clutch, "우측 낙하 기둥", 0.58f, 1.34f, 0.13f, 0.37f, 1.78f),
            new GreedLastPatternModel("left_falsebeat_offbeat", GreedLastRunChannel.Left, GreedLastTimingType.Offbeat, "좌측 엇박 발판", 0.38f, 0.88f, 0.11f, 0.30f, 1.32f),
            new GreedLastPatternModel("center_guard_clutch", GreedLastRunChannel.Center, GreedLastTimingType.Clutch, "정면 수호상 압박", 0.53f, 1.24f, 0.12f, 0.35f, 1.66f),
            new GreedLastPatternModel("right_spear_perfect", GreedLastRunChannel.Right, GreedLastTimingType.Perfect, "우측 황금 창", 0.46f, 1.08f, 0.10f, 0.30f, 1.50f),
            new GreedLastPatternModel("left_sand_clutch", GreedLastRunChannel.Left, GreedLastTimingType.Clutch, "좌측 모래 압류", 0.52f, 1.22f, 0.12f, 0.34f, 1.64f),
            new GreedLastPatternModel("center_falsebeat_offbeat", GreedLastRunChannel.Center, GreedLastTimingType.Offbeat, "중앙 엇박 석문", 0.39f, 0.90f, 0.11f, 0.30f, 1.34f),
            new GreedLastPatternModel("right_floor_perfect", GreedLastRunChannel.Right, GreedLastTimingType.Perfect, "우측 붕괴 바닥", 0.47f, 1.10f, 0.10f, 0.30f, 1.52f),
            new GreedLastPatternModel("left_blade_offbeat", GreedLastRunChannel.Left, GreedLastTimingType.Offbeat, "좌측 엇박 칼날", 0.37f, 0.86f, 0.10f, 0.29f, 1.28f),
            new GreedLastPatternModel("center_beam_perfect", GreedLastRunChannel.Center, GreedLastTimingType.Perfect, "중앙 광선 틈", 0.43f, 0.98f, 0.10f, 0.28f, 1.38f),
            new GreedLastPatternModel("right_guard_clutch", GreedLastRunChannel.Right, GreedLastTimingType.Clutch, "우측 수호상 압박", 0.56f, 1.30f, 0.13f, 0.36f, 1.74f),
        };

        private GreedLastPatternModel currentPattern = GreedLastPatternModel.Empty;
        private int patternIndex;
        private int score;
        private int health = MaxHealth;
        private int combo;
        private int focus;
        private int maxCombo;
        private int successCount;
        private int goodCount;
        private int missCount;
        private int timingSampleCount;
        private int earlyInputCount;
        private int lateInputCount;
        private int infiniteSectionsCleared;
        private int infiniteThreatTestOffset;
        private int maxInfiniteThreatLevel;
        private float timingSignedErrorTotal;
        private float timingAbsErrorTotal;
        private float patternStartedAt;
        private float staggerUntil;
        private float nextPatternAt;
        private float lastTickAt;
        private float distance;
        private bool activePattern;
        private bool autoFlow;
        private bool autoFlowBeforePause;
        private bool inputConsumed;
        private bool stopped;
        private bool paused;
        private bool resumeCountdownActive;
        private bool startCountdownActive;
        private float pausedAt;
        private float resumeCountdownEndsAt;
        private float startCountdownEndsAt;
        private int chapterIndex;
        private int chapterProgress;
        private GreedLastRunPhase phase;
        private GreedLastChoiceKind choiceKind;
        private bool coreRetrieved;
        private bool escapeSucceeded;
        private bool saveEligible;
        private bool devInvincible;
        private bool infiniteMode;
        private float inputTimingOffsetSeconds;
        private readonly string[] choiceHistory = new string[MaxChoiceHistory];
        private int choiceHistoryCount;
        private string startGift = string.Empty;
        private string lastNode = string.Empty;
        private string primaryGift = string.Empty;
        private string relic = string.Empty;
        private string activeInfiniteLoadoutName = string.Empty;
        private string choicePrompt = string.Empty;
        private string choiceLeft = string.Empty;
        private string choiceCenter = string.Empty;
        private string choiceRight = string.Empty;
        private string judgementText = "다음 함정을 생성하세요.";
        private string ignoreReason = string.Empty;
        private string missReason = string.Empty;
        private GreedLastJudgementResult lastResult = GreedLastJudgementResult.None;

        public event Action<GreedLastRunSnapshot> SnapshotChanged;

        public bool CanExitAfterClear => stopped && saveEligible && phase == GreedLastRunPhase.ClearResolving;
        public bool IsStoppedInfiniteRun => stopped && infiniteMode;
        public bool IsStoppedNormalRun => stopped && !infiniteMode;
        public bool IsInfiniteRun => infiniteMode;
        public bool IsPaused => paused;
        public float InputTimingOffsetSeconds => inputTimingOffsetSeconds;
        public bool HasTimingRecommendation => timingSampleCount >= TimingRecommendationMinSamples;
        public int TimingRecommendationMilliseconds => HasTimingRecommendation
            ? Mathf.RoundToInt(Mathf.Clamp(
                inputTimingOffsetSeconds - timingSignedErrorTotal / timingSampleCount,
                MinInputTimingOffsetSeconds,
                MaxInputTimingOffsetSeconds) * 1000f)
            : 0;

        private bool CanAutoStartPattern => phase == GreedLastRunPhase.RunPlaying || phase == GreedLastRunPhase.EscapeRunning;

        public GreedLastRunCore()
        {
            inputTimingOffsetSeconds = Mathf.Clamp(
                PlayerPrefs.GetFloat(InputTimingOffsetPrefsKey, 0f),
                MinInputTimingOffsetSeconds,
                MaxInputTimingOffsetSeconds);
        }

        private void ResetRunStats()
        {
            maxCombo = 0;
            successCount = 0;
            goodCount = 0;
            missCount = 0;
            timingSampleCount = 0;
            earlyInputCount = 0;
            lateInputCount = 0;
            timingSignedErrorTotal = 0f;
            timingAbsErrorTotal = 0f;
            infiniteSectionsCleared = 0;
            infiniteThreatTestOffset = 0;
            maxInfiniteThreatLevel = 1;
        }

        public void Enter()
        {
            score = 0;
            health = MaxHealth;
            combo = 0;
            focus = 0;
            ResetRunStats();
            currentPattern = GreedLastPatternModel.Empty;
            activePattern = false;
            inputConsumed = false;
            stopped = false;
            paused = false;
            resumeCountdownActive = false;
            startCountdownActive = false;
            autoFlowBeforePause = false;
            autoFlow = false;
            distance = 0f;
            staggerUntil = 0f;
            nextPatternAt = 0f;
            lastTickAt = Time.realtimeSinceStartup;
            chapterIndex = 1;
            chapterProgress = 0;
            coreRetrieved = false;
            escapeSucceeded = false;
            saveEligible = false;
            infiniteMode = false;
            activeInfiniteLoadoutName = string.Empty;
            ClearChoices();
            OpenChoice(GreedLastChoiceKind.StartGift);
            judgementText = "시작 기프트를 선택하세요.";
            ignoreReason = string.Empty;
            missReason = string.Empty;
            lastResult = GreedLastJudgementResult.None;
            Publish();
        }

        public void EnterInfiniteRun()
        {
            EnterInfiniteRun(GreedLastRunRecord.Empty);
        }

        public void EnterInfiniteRun(GreedLastRunRecord loadoutRecord)
        {
            score = 0;
            health = MaxHealth;
            combo = 0;
            focus = 0;
            ResetRunStats();
            distance = 0f;
            currentPattern = GreedLastPatternModel.Empty;
            activePattern = false;
            inputConsumed = false;
            stopped = false;
            paused = false;
            resumeCountdownActive = false;
            startCountdownActive = false;
            autoFlowBeforePause = false;
            autoFlow = false;
            staggerUntil = 0f;
            float now = Time.realtimeSinceStartup;
            nextPatternAt = 0f;
            lastTickAt = now;
            chapterIndex = 1;
            chapterProgress = 0;
            coreRetrieved = true;
            escapeSucceeded = false;
            saveEligible = false;
            infiniteMode = true;
            ClearChoices();
            phase = GreedLastRunPhase.RunPlaying;
            choiceKind = GreedLastChoiceKind.None;
            choicePrompt = string.Empty;
            choiceLeft = string.Empty;
            choiceCenter = string.Empty;
            choiceRight = string.Empty;
            ignoreReason = string.Empty;
            missReason = string.Empty;
            lastResult = GreedLastJudgementResult.None;
            string loadoutName = loadoutRecord.IsValid && !string.IsNullOrEmpty(loadoutRecord.LoadoutName)
                ? loadoutRecord.LoadoutName
                : "저장 조합 없음";
            activeInfiniteLoadoutName = loadoutName;
            startGift = loadoutRecord.IsValid ? loadoutRecord.StartGift : string.Empty;
            primaryGift = loadoutRecord.IsValid ? loadoutRecord.PrimaryGift : string.Empty;
            relic = loadoutRecord.IsValid ? loadoutRecord.Relic : string.Empty;
            string bonusText = ApplyInfiniteLoadoutBonus(loadoutRecord);
            PrepareStartCountdown(now, "무한모드 시작 준비 - " + loadoutName
                + "\n" + bonusText
                + "\n카운트 뒤 첫 함정이 내려옵니다.");
            Publish();
        }

        public void Exit()
        {
            currentPattern = GreedLastPatternModel.Empty;
            activePattern = false;
            inputConsumed = false;
            autoFlow = false;
            paused = false;
            resumeCountdownActive = false;
            startCountdownActive = false;
            autoFlowBeforePause = false;
            infiniteMode = false;
            activeInfiniteLoadoutName = string.Empty;
            phase = GreedLastRunPhase.None;
            Publish();
        }

        public GreedLastRunRecord CreateRunRecord()
        {
            if (!saveEligible)
            {
                return GreedLastRunRecord.Empty;
            }

            string gift = string.IsNullOrEmpty(primaryGift) ? "미정 기프트" : primaryGift;
            string chosenRelic = string.IsNullOrEmpty(relic) ? "미정 유물" : relic;
            string chosenStartGift = string.IsNullOrEmpty(startGift) ? "미정 시작" : startGift;
            string summary = choiceHistoryCount > 0
                ? string.Join(" / ", choiceHistory, 0, choiceHistoryCount)
                : "테스트 루트";

            return new GreedLastRunRecord(
                true,
                score,
                health,
                combo,
                focus,
                distance,
                gift + " + " + chosenRelic,
                chosenStartGift,
                gift,
                chosenRelic,
                summary,
                BuildTimingProfileText());
        }

        public GreedLastRunAttemptRecord CreateNormalAttemptRecord(GreedLastRunAttemptOutcome outcome)
        {
            bool hasStarted = !infiniteMode
                && (distance > 0.01f
                    || score > 0
                    || successCount > 0
                    || goodCount > 0
                    || missCount > 0
                    || choiceHistoryCount > 0);
            if (!hasStarted || outcome == GreedLastRunAttemptOutcome.None)
            {
                return GreedLastRunAttemptRecord.Empty;
            }

            string summary = choiceHistoryCount > 0
                ? string.Join(" / ", choiceHistory, 0, choiceHistoryCount)
                : "선택 없음";

            return new GreedLastRunAttemptRecord(
                true,
                outcome,
                score,
                health,
                combo,
                focus,
                maxCombo,
                successCount,
                goodCount,
                missCount,
                distance,
                chapterIndex,
                chapterProgress,
                ChapterTargetPatterns,
                coreRetrieved,
                missReason,
                summary,
                BuildTimingProfileText());
        }

        public GreedLastInfiniteRunRecord CreateInfiniteRunRecord()
        {
            bool hasRunStarted = infiniteMode
                && (distance > 0.01f
                    || score > 0
                    || successCount > 0
                    || goodCount > 0
                    || missCount > 0);
            if (!hasRunStarted)
            {
                return GreedLastInfiniteRunRecord.Empty;
            }

            return new GreedLastInfiniteRunRecord(
                true,
                score,
                distance,
                infiniteSectionsCleared,
                maxInfiniteThreatLevel,
                BuildCurrentInfiniteLoadoutName(),
                successCount,
                goodCount,
                missCount,
                maxCombo,
                BuildTimingProfileText());
        }

        public void Tick(float now)
        {
            float deltaTime = Mathf.Max(0f, now - lastTickAt);
            lastTickAt = now;

            if (startCountdownActive && !paused)
            {
                if (now >= startCountdownEndsAt)
                {
                    FinishStartCountdown(now);
                    return;
                }

                Publish();
                return;
            }

            if (paused)
            {
                if (resumeCountdownActive && now >= resumeCountdownEndsAt)
                {
                    ResumeFromPause(now);
                    return;
                }

                Publish();
                return;
            }

            if (!stopped)
            {
                distance += deltaTime * RunSpeedMetersPerSecond;
            }

            if (autoFlow
                && CanAutoStartPattern
                && !activePattern
                && !stopped
                && now >= nextPatternAt)
            {
                StartPattern(now);
            }

            if (!activePattern || stopped)
            {
                Publish();
                return;
            }

            float elapsed = now - patternStartedAt;
            if (!inputConsumed && elapsed > currentPattern.HitSeconds)
            {
                Resolve(new GreedLastJudgementOutcome(
                    GreedLastJudgementResult.Miss,
                    currentPattern.TimingType,
                    "MISS - 입력 없음\n늦음",
                    string.Empty,
                    "no_input"));
                return;
            }

            Publish();
        }

        public void ToggleAutoFlow(float now)
        {
            if (startCountdownActive)
            {
                judgementText = "시작 카운트 중입니다.";
                Publish();
                return;
            }

            if (paused)
            {
                BeginResumeCountdown(now);
                return;
            }

            if (stopped)
            {
                if (infiniteMode)
                {
                    EnterInfiniteRun();
                }
                else
                {
                    RestartAutoFlow(now);
                }

                return;
            }

            if (phase == GreedLastRunPhase.ChoiceSelecting)
            {
                judgementText = "선택을 먼저 확정해야 합니다.";
                Publish();
                return;
            }

            autoFlow = !autoFlow;
            if (autoFlow)
            {
                nextPatternAt = activePattern ? nextPatternAt : now + 0.2f;
                judgementText = "자동 진행 시작 - 함정이 연속으로 들어옵니다.";
            }
            else
            {
                judgementText = "자동 진행 정지";
            }

            Publish();
        }

        public void TogglePause(float now)
        {
            if (paused)
            {
                BeginResumeCountdown(now);
                return;
            }

            if (startCountdownActive)
            {
                PauseAt(now, "시작 카운트 일시정지\n재개를 누르면 카운트 뒤 이어집니다.");
                return;
            }

            if (!CanPauseNow())
            {
                judgementText = phase == GreedLastRunPhase.ChoiceSelecting
                    ? "선택 중에는 일시정지가 필요하지 않습니다."
                    : "현재 상태에서는 일시정지를 사용할 수 없습니다.";
                Publish();
                return;
            }

            PauseAt(now, "일시정지 - 재개를 누르면 같은 박자에서 이어집니다.");
        }

        public void PauseForAppBackground(float now)
        {
            if (!paused && startCountdownActive)
            {
                PauseAt(now, "앱 전환으로 시작 카운트가 멈췄습니다.\n재개를 누르면 이어집니다.");
                return;
            }

            if (!paused && CanPauseNow())
            {
                PauseAt(now, "앱 전환으로 일시정지되었습니다.\n재개를 누르면 이어집니다.");
            }
        }

        public void RequestNextPattern(float now)
        {
            if (startCountdownActive)
            {
                judgementText = "시작 카운트 뒤 첫 함정이 내려옵니다.";
                Publish();
                return;
            }

            if (paused)
            {
                judgementText = "일시정지 중 - 재개 후 진행할 수 있습니다.";
                Publish();
                return;
            }

            if (stopped)
            {
                judgementText = "체력 0 - 로비로 돌아가 다시 시작하세요.";
                Publish();
                return;
            }

            if (phase == GreedLastRunPhase.ChoiceSelecting)
            {
                judgementText = "선택을 먼저 확정해야 합니다.";
                Publish();
                return;
            }

            if (activePattern)
            {
                ignoreReason = "active_pattern";
                judgementText = "현재 함정이 끝난 뒤 다음 함정을 생성할 수 있습니다.";
                Publish();
                return;
            }

            StartPattern(now);
        }

        public void HandleInput(GreedLastRunChannel channel, float now)
        {
            if (stopped)
            {
                ignoreReason = "stopped";
                Publish();
                return;
            }

            if (startCountdownActive)
            {
                Ignore("start_countdown", "시작 카운트 중입니다.");
                return;
            }

            if (paused)
            {
                Ignore("paused", "일시정지 중입니다. 재개 후 입력하세요.");
                return;
            }

            if (phase == GreedLastRunPhase.ChoiceSelecting)
            {
                SelectChoice(channel, now);
                return;
            }

            if (now < staggerUntil)
            {
                Ignore("locked_by_stagger", "경직 중 입력은 무효입니다.");
                return;
            }

            if (!activePattern)
            {
                Ignore("no_active_pattern", "활성 함정이 없습니다.");
                return;
            }

            if (inputConsumed)
            {
                Ignore("duplicate_input", "같은 함정에는 최초 입력만 반영됩니다.");
                return;
            }

            inputConsumed = true;
            float elapsed = now - patternStartedAt;
            float judgedElapsed = elapsed + inputTimingOffsetSeconds;

            if (channel != currentPattern.Channel)
            {
                Resolve(new GreedLastJudgementOutcome(
                    GreedLastJudgementResult.Miss,
                    currentPattern.TimingType,
                    "MISS - 채널 오류\n" + FormatChannel(channel) + " 입력 / 정답 " + FormatChannel(currentPattern.Channel),
                    string.Empty,
                    "wrong_channel"));
                return;
            }

            float signedDelta = judgedElapsed - currentPattern.CoreSeconds;
            RecordTimingSample(signedDelta);
            float delta = Mathf.Abs(signedDelta);
            if (delta <= currentPattern.SuccessWindowSeconds)
            {
                Resolve(new GreedLastJudgementOutcome(
                    GreedLastJudgementResult.Success,
                    currentPattern.TimingType,
                    "SUCCESS / " + currentPattern.TimingType + "\n" + FormatTimingDelta(signedDelta),
                    string.Empty,
                    string.Empty));
                return;
            }

            if (delta <= currentPattern.GoodWindowSeconds)
            {
                Resolve(new GreedLastJudgementOutcome(
                    GreedLastJudgementResult.Good,
                    currentPattern.TimingType,
                    "GOOD - 생존 완충\n" + FormatTimingDelta(signedDelta),
                    string.Empty,
                    string.Empty));
                return;
            }

            string reason = judgedElapsed < currentPattern.CoreSeconds ? "too_early" : "too_late";
            Resolve(new GreedLastJudgementOutcome(
                GreedLastJudgementResult.Miss,
                currentPattern.TimingType,
                (judgedElapsed < currentPattern.CoreSeconds ? "MISS - 너무 빠름" : "MISS - 너무 늦음")
                    + "\n" + FormatTimingDelta(signedDelta),
                string.Empty,
                reason));
        }

        public void AdjustInputTimingOffset(float deltaSeconds)
        {
            inputTimingOffsetSeconds = Mathf.Clamp(
                inputTimingOffsetSeconds + deltaSeconds,
                MinInputTimingOffsetSeconds,
                MaxInputTimingOffsetSeconds);
            SaveInputTimingOffset();
            judgementText = "입력 보정 " + FormatOffset(inputTimingOffsetSeconds);
            Publish();
        }

        public void ResetInputTimingOffset()
        {
            inputTimingOffsetSeconds = 0f;
            SaveInputTimingOffset();
            judgementText = "입력 보정 0ms";
            Publish();
        }

        public void ApplyRecommendedInputTimingOffset()
        {
            if (!HasTimingRecommendation)
            {
                inputTimingOffsetSeconds = 0f;
                SaveInputTimingOffset();
                judgementText = "보정 추천 샘플 부족 - 입력 보정 0ms";
                Publish();
                return;
            }

            inputTimingOffsetSeconds = Mathf.Clamp(
                TimingRecommendationMilliseconds / 1000f,
                MinInputTimingOffsetSeconds,
                MaxInputTimingOffsetSeconds);
            SaveInputTimingOffset();
            judgementText = "추천 입력 보정 적용 " + FormatOffset(inputTimingOffsetSeconds);
            Publish();
        }

        public void FillFocusForTest()
        {
            if (stopped)
            {
                judgementText = "종료 상태에서는 집중 테스트를 적용하지 않습니다.";
                Publish();
                return;
            }

            focus = MaxFocus;
            judgementText = "테스트 집중 MAX - 다음 Miss에서 집중 보호를 확인할 수 있습니다.";
            Publish();
        }

        public void RaiseInfiniteThreatForTest()
        {
            if (!infiniteMode)
            {
                judgementText = "무한모드에서만 위협 테스트를 사용할 수 있습니다.";
                Publish();
                return;
            }

            if (stopped)
            {
                judgementText = "종료 상태에서는 위협 테스트를 적용하지 않습니다.";
                Publish();
                return;
            }

            int currentThreat = GetInfiniteThreatLevel();
            if (currentThreat >= InfiniteThreatMaxLevel)
            {
                judgementText = "위협 단계가 이미 최대입니다.";
                Publish();
                return;
            }

            infiniteThreatTestOffset += 1;
            maxInfiniteThreatLevel = Mathf.Max(maxInfiniteThreatLevel, GetInfiniteThreatLevel());
            judgementText = "테스트 위협 상승 - 다음 함정부터 위협 " + GetInfiniteThreatLevel() + " 적용";
            Publish();
        }

        public void LowerInfiniteThreatForTest()
        {
            if (!infiniteMode)
            {
                judgementText = "무한모드에서만 위협 테스트를 사용할 수 있습니다.";
                Publish();
                return;
            }

            if (stopped)
            {
                judgementText = "종료 상태에서는 위협 테스트를 적용하지 않습니다.";
                Publish();
                return;
            }

            int currentThreat = GetInfiniteThreatLevel();
            if (currentThreat <= 1)
            {
                judgementText = "위협 단계가 이미 최소입니다.";
                Publish();
                return;
            }

            infiniteThreatTestOffset -= 1;
            judgementText = "테스트 위협 하강 - 다음 함정부터 위협 " + GetInfiniteThreatLevel() + " 적용";
            Publish();
        }

        public void ForceStopInfiniteRunForTest()
        {
            if (!infiniteMode)
            {
                judgementText = "무한모드에서만 테스트 종료를 사용할 수 있습니다.";
                Publish();
                return;
            }

            if (stopped)
            {
                judgementText = "이미 무한모드가 종료되었습니다.";
                Publish();
                return;
            }

            activePattern = false;
            inputConsumed = false;
            autoFlow = false;
            paused = false;
            resumeCountdownActive = false;
            startCountdownActive = false;
            autoFlowBeforePause = false;
            stopped = true;
            phase = GreedLastRunPhase.FailResolving;
            score = Mathf.Max(score, 1);
            distance = Mathf.Max(distance, 1f);
            missReason = "test_stop";
            judgementText = "무한모드 테스트 종료\n무한 재시작 또는 로비로 돌아갈 수 있습니다.";
            Publish();
        }

        private bool CanPauseNow()
        {
            return !stopped
                && !startCountdownActive
                && phase != GreedLastRunPhase.None
                && phase != GreedLastRunPhase.ChoiceSelecting
                && phase != GreedLastRunPhase.ClearResolving
                && phase != GreedLastRunPhase.FailResolving;
        }

        private void PauseAt(float now, string message)
        {
            paused = true;
            resumeCountdownActive = false;
            pausedAt = now;
            autoFlowBeforePause = autoFlow;
            autoFlow = false;
            judgementText = message;
            Publish();
        }

        private void PrepareStartCountdown(float now, string message)
        {
            startCountdownActive = true;
            startCountdownEndsAt = now + StartCountdownSeconds;
            autoFlow = false;
            nextPatternAt = 0f;
            lastTickAt = now;
            judgementText = message;
        }

        private void FinishStartCountdown(float now)
        {
            startCountdownActive = false;
            autoFlow = true;
            nextPatternAt = now + StartBufferSeconds;
            lastTickAt = now;
            judgementText = "GO - 함정이 내려옵니다.";
            Publish();
        }

        private void BeginResumeCountdown(float now)
        {
            if (resumeCountdownActive)
            {
                return;
            }

            resumeCountdownActive = true;
            resumeCountdownEndsAt = now + ResumeCountdownSeconds;
            judgementText = "재개 준비 - 카운트 후 함정이 다시 내려옵니다.";
            Publish();
        }

        private void ResumeFromPause(float now)
        {
            float pauseDuration = Mathf.Max(0f, now - pausedAt);
            float timingBuffer = activePattern ? ResumeBufferSeconds : 0f;
            if (activePattern)
            {
                patternStartedAt += pauseDuration + timingBuffer;
            }

            if (nextPatternAt > 0f)
            {
                nextPatternAt += pauseDuration + (activePattern ? 0f : ResumeBufferSeconds);
            }

            if (staggerUntil > pausedAt)
            {
                staggerUntil += pauseDuration;
            }

            if (startCountdownActive)
            {
                startCountdownEndsAt += pauseDuration;
            }

            paused = false;
            resumeCountdownActive = false;
            autoFlow = autoFlowBeforePause;
            lastTickAt = now;
            judgementText = startCountdownActive
                ? "재개 - 시작 카운트를 이어갑니다."
                : autoFlow
                    ? "재개 - 앞 박자 여유 후 이어집니다."
                    : "재개 - 자동 진행은 정지 상태입니다.";
            Publish();
        }

        private void SaveInputTimingOffset()
        {
            PlayerPrefs.SetFloat(InputTimingOffsetPrefsKey, inputTimingOffsetSeconds);
            PlayerPrefs.Save();
        }

        private void RecordTimingSample(float signedDeltaSeconds)
        {
            timingSampleCount += 1;
            timingSignedErrorTotal += signedDeltaSeconds;
            timingAbsErrorTotal += Mathf.Abs(signedDeltaSeconds);

            if (signedDeltaSeconds < -0.02f)
            {
                earlyInputCount += 1;
            }
            else if (signedDeltaSeconds > 0.02f)
            {
                lateInputCount += 1;
            }
        }

        private void Resolve(GreedLastJudgementOutcome outcome)
        {
            activePattern = false;
            ignoreReason = outcome.IgnoreReason;
            missReason = outcome.MissReason;
            judgementText = outcome.Message;
            lastResult = outcome.Result;
            int comboBeforeResolve = combo;
            maxInfiniteThreatLevel = Mathf.Max(maxInfiniteThreatLevel, GetInfiniteThreatLevel());

            switch (outcome.Result)
            {
                case GreedLastJudgementResult.Success:
                    successCount += 1;
                    int comboGain = HasChoice(primaryGift, "연쇄 축") || HasChoice(relic, "파라오의 각인") ? 2 : 1;
                    combo += comboGain;
                    maxCombo = Mathf.Max(maxCombo, combo);
                    int successScore = 100 + combo * 10;
                    int focusGain = HasChoice(primaryGift, "집중 축") ? 2 : 1;
                    if (HasChoice(startGift, "고점 본능"))
                    {
                        successScore += 20;
                    }

                    if (HasChoice(relic, "황금 박동"))
                    {
                        successScore += 40;
                    }

                    if (HasChoice(relic, "파라오의 각인"))
                    {
                        successScore += 30;
                    }

                    score += successScore;
                    focus = Mathf.Min(MaxFocus, focus + focusGain);
                    AppendChoiceEffect("조합 효과: +" + successScore + "점 / 콤보 +" + comboGain + " / 집중 +" + focusGain);
                    int rhythmChainLevel = GetRhythmChainLevel(combo);
                    if (rhythmChainLevel > 0)
                    {
                        int chainBonus = 30 * rhythmChainLevel;
                        if (infiniteMode)
                        {
                            chainBonus += 10 * GetInfiniteThreatLevel();
                        }

                        score += chainBonus;
                        AppendChoiceEffect("연쇄 박자 x" + rhythmChainLevel + " / 보너스 +" + chainBonus + "점");
                    }
                    break;
                case GreedLastJudgementResult.Good:
                    goodCount += 1;
                    int goodScore = 25;
                    int goodFocusGain = 0;
                    int goodHealthGain = 0;
                    if (HasChoice(startGift, "박자 감각") || HasChoice(relic, "사막의 잔상"))
                    {
                        goodFocusGain = 1;
                        focus = Mathf.Min(MaxFocus, focus + goodFocusGain);
                    }

                    if (HasChoice(primaryGift, "생존 축") && health < BonusHealthCap)
                    {
                        goodHealthGain = 1;
                        health += goodHealthGain;
                    }

                    if (HasChoice(startGift, "고점 본능"))
                    {
                        goodScore += 15;
                    }

                    score += goodScore;
                    combo = 0;
                    string goodEffectText = "조합 효과: +" + goodScore + "점";
                    if (goodFocusGain > 0)
                    {
                        goodEffectText += " / 집중 +" + goodFocusGain;
                    }

                    if (goodHealthGain > 0)
                    {
                        goodEffectText += " / 체력 +" + goodHealthGain;
                    }

                    AppendChoiceEffect(goodEffectText);
                    AppendChainBreakText(comboBeforeResolve);
                    break;
                case GreedLastJudgementResult.Miss:
                    combo = 0;
                    if (devInvincible)
                    {
                        missCount += 1;
                        staggerUntil = Time.realtimeSinceStartup + StaggerSeconds;
                        health = Mathf.Max(1, health);
                        judgementText += "\n테스트 무적 - 피해를 무시했습니다.";
                        break;
                    }

                    if (focus >= MaxFocus)
                    {
                        focus = 0;
                        goodCount += 1;
                        score += 40;
                        missReason = "focus_guard";
                        lastResult = GreedLastJudgementResult.Good;
                        judgementText = "집중 보호 - 피해 무효\n" + outcome.Message;
                        AppendChainBreakText(comboBeforeResolve);
                        break;
                    }

                    missCount += 1;
                    staggerUntil = Time.realtimeSinceStartup + StaggerSeconds;
                    health = Mathf.Max(0, health - 1);
                    if (health == 0)
                    {
                        stopped = true;
                        autoFlow = false;
                        paused = false;
                        resumeCountdownActive = false;
                        startCountdownActive = false;
                        autoFlowBeforePause = false;
                        activePattern = false;
                        inputConsumed = false;
                        phase = GreedLastRunPhase.FailResolving;
                        saveEligible = false;
                        judgementText = infiniteMode
                            ? "무한모드 종료 - 체력 0\n무한 재시작 또는 로비로 돌아갈 수 있습니다."
                            : "탈출 실패 - 체력 0\n로비로 돌아가거나 다시 시작하세요.";
                    }

                    AppendChainBreakText(comboBeforeResolve);
                    break;
            }

            if (!stopped)
            {
                AdvanceRunAfterPattern(Time.realtimeSinceStartup);
            }

            Publish();
        }

        public void ToggleDevInvincible()
        {
            devInvincible = !devInvincible;
            if (devInvincible && health <= 0)
            {
                health = MaxHealth;
                stopped = false;
                paused = false;
                resumeCountdownActive = false;
                startCountdownActive = false;
                autoFlowBeforePause = false;
                saveEligible = false;
                escapeSucceeded = false;
                phase = GreedLastRunPhase.RunPlaying;
            }

            judgementText = devInvincible
                ? "테스트 무적 ON - 실패 확인 외에는 피해로 죽지 않습니다."
                : "테스트 무적 OFF";
            Publish();
        }

        public void JumpToDevShortcut(GreedLastDevShortcut shortcut, float now)
        {
            PrepareDevJump(now);

            switch (shortcut)
            {
                case GreedLastDevShortcut.GiftChoice:
                    chapterIndex = Mathf.Clamp(chapterIndex <= 0 || chapterIndex > 3 ? 1 : chapterIndex, 1, 3);
                    chapterProgress = 0;
                    SeedDevChoice(GreedLastChoiceKind.StartGift, "박자 감각");
                    SeedDevChoice(GreedLastChoiceKind.Node, "안정 루트");
                    OpenChoice(GreedLastChoiceKind.Gift);
                    judgementText = $"테스트 바로가기 - 챕터 {chapterIndex} 기프트 선택";
                    break;
                case GreedLastDevShortcut.RelicChoice:
                    chapterIndex = 2;
                    chapterProgress = 0;
                    SeedDevChoice(GreedLastChoiceKind.StartGift, "박자 감각");
                    SeedDevChoice(GreedLastChoiceKind.Node, "변주 루트");
                    SeedDevChoice(GreedLastChoiceKind.Gift, "집중 축");
                    OpenChoice(GreedLastChoiceKind.Relic);
                    judgementText = "테스트 바로가기 - 유물 선택";
                    break;
                case GreedLastDevShortcut.AfterCore:
                    chapterIndex = 4;
                    chapterProgress = 0;
                    coreRetrieved = true;
                    SeedFullDevRoute();
                    phase = GreedLastRunPhase.EscapeRunning;
                    PrepareStartCountdown(now, "테스트 바로가기 - 공명핵 회수 직후\n카운트 뒤 탈출 함정이 내려옵니다.");
                    break;
                case GreedLastDevShortcut.BeforeClear:
                    chapterIndex = 4;
                    chapterProgress = ChapterTargetPatterns - 1;
                    coreRetrieved = true;
                    SeedFullDevRoute();
                    phase = GreedLastRunPhase.EscapeRunning;
                    PrepareStartCountdown(now, "테스트 바로가기 - 탈출 성공 직전\n카운트 뒤 마지막 함정이 내려옵니다.");
                    break;
            }

            Publish();
        }

        private void PrepareDevJump(float now)
        {
            if (health <= 0)
            {
                health = MaxHealth;
            }

            ResetRunStats();
            infiniteMode = false;
            activeInfiniteLoadoutName = string.Empty;
            currentPattern = GreedLastPatternModel.Empty;
            activePattern = false;
            inputConsumed = false;
            stopped = false;
            paused = false;
            resumeCountdownActive = false;
            startCountdownActive = false;
            autoFlowBeforePause = false;
            autoFlow = false;
            staggerUntil = 0f;
            lastTickAt = now;
            coreRetrieved = false;
            escapeSucceeded = false;
            saveEligible = false;
            ClearChoices();
            choiceKind = GreedLastChoiceKind.None;
            choicePrompt = string.Empty;
            choiceLeft = string.Empty;
            choiceCenter = string.Empty;
            choiceRight = string.Empty;
            ignoreReason = string.Empty;
            missReason = string.Empty;
            lastResult = GreedLastJudgementResult.None;
        }

        private void SeedFullDevRoute()
        {
            SeedDevChoice(GreedLastChoiceKind.StartGift, "박자 감각");
            SeedDevChoice(GreedLastChoiceKind.Node, "안정 루트");
            SeedDevChoice(GreedLastChoiceKind.Gift, "집중 축");
            SeedDevChoice(GreedLastChoiceKind.Relic, "황금 박동");
        }

        private void SeedDevChoice(GreedLastChoiceKind kind, string selected)
        {
            switch (kind)
            {
                case GreedLastChoiceKind.StartGift:
                    if (!string.IsNullOrEmpty(startGift))
                    {
                        return;
                    }
                    break;
                case GreedLastChoiceKind.Node:
                    if (!string.IsNullOrEmpty(lastNode))
                    {
                        return;
                    }
                    break;
                case GreedLastChoiceKind.Gift:
                    if (!string.IsNullOrEmpty(primaryGift))
                    {
                        return;
                    }
                    break;
                case GreedLastChoiceKind.Relic:
                    if (!string.IsNullOrEmpty(relic))
                    {
                        return;
                    }
                    break;
            }

            RecordChoice(kind, selected);
        }

        private void RestartAutoFlow(float now)
        {
            score = 0;
            health = MaxHealth;
            combo = 0;
            focus = 0;
            ResetRunStats();
            distance = 0f;
            currentPattern = GreedLastPatternModel.Empty;
            activePattern = false;
            inputConsumed = false;
            stopped = false;
            paused = false;
            resumeCountdownActive = false;
            startCountdownActive = false;
            autoFlowBeforePause = false;
            autoFlow = true;
            staggerUntil = 0f;
            nextPatternAt = now + 0.2f;
            lastTickAt = now;
            chapterIndex = 1;
            chapterProgress = 0;
            coreRetrieved = false;
            escapeSucceeded = false;
            saveEligible = false;
            infiniteMode = false;
            ClearChoices();
            OpenChoice(GreedLastChoiceKind.StartGift);
            autoFlow = false;
            judgementText = "재도전 시작 - 시작 기프트를 선택하세요.";
            ignoreReason = string.Empty;
            missReason = string.Empty;
            lastResult = GreedLastJudgementResult.None;
            Publish();
        }

        private void OpenChoice(GreedLastChoiceKind kind)
        {
            phase = GreedLastRunPhase.ChoiceSelecting;
            choiceKind = kind;
            currentPattern = GreedLastPatternModel.Empty;
            activePattern = false;
            inputConsumed = false;
            autoFlow = false;
            paused = false;
            resumeCountdownActive = false;
            startCountdownActive = false;
            autoFlowBeforePause = false;

            switch (kind)
            {
                case GreedLastChoiceKind.StartGift:
                    choicePrompt = "시작 기프트를 선택하세요.";
                    choiceLeft = "박자 감각";
                    choiceCenter = "안정 호흡";
                    choiceRight = "고점 본능";
                    break;
                case GreedLastChoiceKind.Node:
                    choicePrompt = $"챕터 {chapterIndex} 노드 선택";
                    choiceLeft = "안정 루트";
                    choiceCenter = "변주 루트";
                    choiceRight = "압박 루트";
                    break;
                case GreedLastChoiceKind.Gift:
                    choicePrompt = $"챕터 {chapterIndex} 기프트 선택";
                    choiceLeft = "집중 축";
                    choiceCenter = "생존 축";
                    choiceRight = "연쇄 축";
                    break;
                case GreedLastChoiceKind.Relic:
                    choicePrompt = "유물 선택";
                    choiceLeft = "황금 박동";
                    choiceCenter = "사막의 잔상";
                    choiceRight = "파라오의 각인";
                    break;
                default:
                    choicePrompt = string.Empty;
                    choiceLeft = string.Empty;
                    choiceCenter = string.Empty;
                    choiceRight = string.Empty;
                    break;
            }
        }

        private void SelectChoice(GreedLastRunChannel channel, float now)
        {
            string selected = channel == GreedLastRunChannel.Left
                ? choiceLeft
                : channel == GreedLastRunChannel.Center
                    ? choiceCenter
                    : choiceRight;

            if (choiceKind == GreedLastChoiceKind.Node)
            {
                RecordChoice(choiceKind, selected);
                string rewardText = ApplyChoiceReward(choiceKind, selected);
                judgementText = selected + " 선택 - 챕터 보상 기프트를 고르세요.";
                if (!string.IsNullOrEmpty(rewardText))
                {
                    judgementText += "\n효과: " + rewardText;
                }

                OpenChoice(GreedLastChoiceKind.Gift);
                Publish();
                return;
            }

            if (choiceKind == GreedLastChoiceKind.Gift && chapterIndex == 2)
            {
                RecordChoice(choiceKind, selected);
                string rewardText = ApplyChoiceReward(choiceKind, selected);
                judgementText = selected + " 선택 - 중반 유물을 고르세요.";
                if (!string.IsNullOrEmpty(rewardText))
                {
                    judgementText += "\n효과: " + rewardText;
                }

                OpenChoice(GreedLastChoiceKind.Relic);
                Publish();
                return;
            }

            CompleteChoiceSequence(selected, now);
        }

        private void CompleteChoiceSequence(string selected, float now)
        {
            GreedLastChoiceKind completedKind = choiceKind;
            RecordChoice(completedKind, selected);
            string rewardText = ApplyChoiceReward(completedKind, selected);
            choiceKind = GreedLastChoiceKind.None;
            choicePrompt = string.Empty;
            choiceLeft = string.Empty;
            choiceCenter = string.Empty;
            choiceRight = string.Empty;

            if (completedKind != GreedLastChoiceKind.StartGift)
            {
                chapterIndex = Mathf.Min(4, chapterIndex + 1);
            }

            phase = GreedLastRunPhase.RunPlaying;
            string nextText = completedKind == GreedLastChoiceKind.StartGift
                ? selected + " 선택 - 챕터 1 시작"
                : selected + $" 선택 - 챕터 {chapterIndex} 시작";
            string readyText = string.IsNullOrEmpty(rewardText)
                ? nextText
                : nextText + "\n효과: " + rewardText;
            PrepareStartCountdown(now, readyText + "\n카운트 뒤 첫 함정이 내려옵니다.");
            Publish();
        }

        private void RecordChoice(GreedLastChoiceKind completedKind, string selected)
        {
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            switch (completedKind)
            {
                case GreedLastChoiceKind.StartGift:
                    startGift = selected;
                    break;
                case GreedLastChoiceKind.Node:
                    lastNode = selected;
                    break;
                case GreedLastChoiceKind.Gift:
                    primaryGift = selected;
                    break;
                case GreedLastChoiceKind.Relic:
                    relic = selected;
                    break;
            }

            if (choiceHistoryCount < choiceHistory.Length)
            {
                choiceHistory[choiceHistoryCount] = selected;
                choiceHistoryCount += 1;
            }
        }

        private void ClearChoices()
        {
            for (int i = 0; i < choiceHistory.Length; i += 1)
            {
                choiceHistory[i] = string.Empty;
            }

            choiceHistoryCount = 0;
            startGift = string.Empty;
            lastNode = string.Empty;
            primaryGift = string.Empty;
            relic = string.Empty;
        }

        private string ApplyChoiceReward(GreedLastChoiceKind completedKind, string selected)
        {
            switch (selected)
            {
                case "박자 감각":
                    focus = Mathf.Min(MaxFocus, focus + 1);
                    return "집중 +1";
                case "안정 호흡":
                    health = Mathf.Min(BonusHealthCap, health + 1);
                    return "체력 +1";
                case "고점 본능":
                    score += 80;
                    return "점수 +80";
                case "안정 루트":
                    health = Mathf.Min(BonusHealthCap, health + 1);
                    return "체력 +1";
                case "변주 루트":
                    focus = Mathf.Min(MaxFocus, focus + 1);
                    return "집중 +1";
                case "압박 루트":
                    combo += 1;
                    maxCombo = Mathf.Max(maxCombo, combo);
                    score += 120;
                    return "콤보 +1 / 점수 +120";
                case "집중 축":
                    focus = Mathf.Min(MaxFocus, focus + 2);
                    return "집중 +2";
                case "생존 축":
                    health = Mathf.Min(BonusHealthCap, health + 1);
                    return "체력 +1";
                case "연쇄 축":
                    combo += 2;
                    maxCombo = Mathf.Max(maxCombo, combo);
                    score += 60;
                    return "콤보 +2 / 점수 +60";
                case "황금 박동":
                    score += 200;
                    return "점수 +200";
                case "사막의 잔상":
                    health = Mathf.Min(BonusHealthCap, health + 1);
                    focus = Mathf.Min(MaxFocus, focus + 1);
                    return "체력 +1 / 집중 +1";
                case "파라오의 각인":
                    combo += 3;
                    maxCombo = Mathf.Max(maxCombo, combo);
                    focus = MaxFocus;
                    return "콤보 +3 / 집중 최대";
                default:
                    return completedKind == GreedLastChoiceKind.None ? string.Empty : "기본 흐름 유지";
            }
        }

        public static string BuildInfiniteLoadoutBonusPreview(GreedLastRunRecord loadoutRecord)
        {
            GreedLastInfiniteLoadoutBonus bonus = CalculateInfiniteLoadoutBonus(loadoutRecord);
            if (!loadoutRecord.IsValid || !bonus.HasAnyBonus)
            {
                return "무한 보너스: 기본 진입";
            }

            string text = "무한 보너스:";
            AppendBonusText(ref text, "체력", bonus.HealthBonus);
            AppendBonusText(ref text, "집중", bonus.FocusBonus);
            AppendBonusText(ref text, "콤보", bonus.ComboBonus);
            AppendBonusText(ref text, "점수", bonus.ScoreBonus);
            return text;
        }

        private string ApplyInfiniteLoadoutBonus(GreedLastRunRecord loadoutRecord)
        {
            GreedLastInfiniteLoadoutBonus bonus = CalculateInfiniteLoadoutBonus(loadoutRecord);
            if (!loadoutRecord.IsValid || !bonus.HasAnyBonus)
            {
                return "무한 보너스: 기본 진입";
            }

            health = Mathf.Clamp(health + bonus.HealthBonus, 1, BonusHealthCap);
            focus = Mathf.Clamp(focus + bonus.FocusBonus, 0, MaxFocus);
            combo = Mathf.Clamp(combo + bonus.ComboBonus, 0, 9);
            maxCombo = Mathf.Max(maxCombo, combo);
            score += bonus.ScoreBonus;
            return BuildInfiniteLoadoutBonusPreview(loadoutRecord);
        }

        private static GreedLastInfiniteLoadoutBonus CalculateInfiniteLoadoutBonus(GreedLastRunRecord loadoutRecord)
        {
            if (!loadoutRecord.IsValid)
            {
                return new GreedLastInfiniteLoadoutBonus(0, 0, 0, 0);
            }

            int healthBonus = 0;
            int focusBonus = 0;
            int comboBonus = 0;
            int scoreBonus = Mathf.Clamp(Mathf.RoundToInt(loadoutRecord.Score * 0.10f), 0, 200);

            AccumulateLoadoutKeywordBonus(loadoutRecord.StartGift, ref healthBonus, ref focusBonus, ref comboBonus, ref scoreBonus);
            AccumulateLoadoutKeywordBonus(loadoutRecord.PrimaryGift, ref healthBonus, ref focusBonus, ref comboBonus, ref scoreBonus);
            AccumulateLoadoutKeywordBonus(loadoutRecord.Relic, ref healthBonus, ref focusBonus, ref comboBonus, ref scoreBonus);

            if (ContainsChoice(loadoutRecord.ChoiceSummary, "안정 루트"))
            {
                healthBonus += 1;
            }

            if (ContainsChoice(loadoutRecord.ChoiceSummary, "변주 루트"))
            {
                focusBonus += 1;
            }

            if (ContainsChoice(loadoutRecord.ChoiceSummary, "압박 루트"))
            {
                comboBonus += 1;
                scoreBonus += 50;
            }

            return new GreedLastInfiniteLoadoutBonus(
                Mathf.Clamp(healthBonus, 0, 2),
                Mathf.Clamp(focusBonus, 0, MaxFocus),
                Mathf.Clamp(comboBonus, 0, 5),
                Mathf.Clamp(scoreBonus, 0, 300));
        }

        private static void AccumulateLoadoutKeywordBonus(
            string keyword,
            ref int healthBonus,
            ref int focusBonus,
            ref int comboBonus,
            ref int scoreBonus)
        {
            switch (keyword)
            {
                case "박자 감각":
                    focusBonus += 1;
                    break;
                case "안정 호흡":
                    healthBonus += 1;
                    break;
                case "고점 본능":
                    scoreBonus += 80;
                    break;
                case "집중 축":
                    focusBonus += 2;
                    break;
                case "생존 축":
                    healthBonus += 1;
                    break;
                case "연쇄 축":
                    comboBonus += 2;
                    scoreBonus += 60;
                    break;
                case "황금 박동":
                    scoreBonus += 120;
                    break;
                case "사막의 잔상":
                    healthBonus += 1;
                    focusBonus += 1;
                    break;
                case "파라오의 각인":
                    comboBonus += 3;
                    focusBonus += MaxFocus;
                    break;
            }
        }

        private static bool ContainsChoice(string choiceSummary, string choice)
        {
            return !string.IsNullOrEmpty(choiceSummary)
                && choiceSummary.IndexOf(choice, StringComparison.Ordinal) >= 0;
        }

        private static bool HasChoice(string selected, string expected)
        {
            return string.Equals(selected, expected, StringComparison.Ordinal);
        }

        private static int GetRhythmChainLevel(int comboValue)
        {
            return Mathf.Clamp(comboValue / 3, 0, 5);
        }

        private void AppendChoiceEffect(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                judgementText += "\n" + text;
            }
        }

        private string BuildCurrentInfiniteLoadoutName()
        {
            return string.IsNullOrEmpty(activeInfiniteLoadoutName)
                ? "저장 조합 없음"
                : activeInfiniteLoadoutName;
        }

        private void AppendChainBreakText(int previousCombo)
        {
            int previousLevel = GetRhythmChainLevel(previousCombo);
            if (previousLevel > 0)
            {
                judgementText += "\n연쇄 끊김 x" + previousLevel;
            }
        }

        private static void AppendBonusText(ref string text, string label, int value)
        {
            if (value <= 0)
            {
                return;
            }

            text += " " + label + " +" + value;
        }

        private void AdvanceRunAfterPattern(float now)
        {
            chapterProgress += 1;
            if (infiniteMode)
            {
                if (chapterProgress >= ChapterTargetPatterns)
                {
                    chapterProgress = 0;
                    infiniteSectionsCleared += 1;
                    maxInfiniteThreatLevel = Mathf.Max(maxInfiniteThreatLevel, GetInfiniteThreatLevel());
                    int sectionBonus = 50 * GetInfiniteThreatLevel();
                    score += sectionBonus;
                    judgementText += "\n무한 구간 유지 보너스 +" + sectionBonus
                        + " / 위협 " + GetInfiniteThreatLevel();
                }

                phase = GreedLastRunPhase.RunPlaying;
                autoFlow = true;
                nextPatternAt = now + AutoGapSeconds;
                return;
            }

            if (chapterProgress < ChapterTargetPatterns)
            {
                nextPatternAt = now + AutoGapSeconds;
                return;
            }

            autoFlow = false;
            chapterProgress = 0;

            if (chapterIndex <= 3)
            {
                phase = GreedLastRunPhase.ChapterWrapUp;
                judgementText = $"챕터 {chapterIndex} 종료 - 선택으로 방향을 정리합니다.";
                OpenChoice(GreedLastChoiceKind.Node);
                return;
            }

            if (!coreRetrieved)
            {
                phase = GreedLastRunPhase.CoreRetrieve;
                coreRetrieved = true;
                phase = GreedLastRunPhase.EscapeRunning;
                PrepareStartCountdown(now, "공명핵 회수 - 탈출 검증 준비\n카운트 뒤 마지막 구간이 시작됩니다.");
                return;
            }

            escapeSucceeded = true;
            saveEligible = true;
            stopped = true;
            autoFlow = false;
            phase = GreedLastRunPhase.ClearResolving;
            judgementText = "탈출 성공 - 일반 런 클리어\n저장 가능한 기프트 방향이 만들어졌습니다.";
        }

        private void StartPattern(float now)
        {
            currentPattern = BuildActivePattern(SelectPatternFromDeck());
            maxInfiniteThreatLevel = Mathf.Max(maxInfiniteThreatLevel, GetInfiniteThreatLevel());
            patternIndex += 1;
            patternStartedAt = now;
            activePattern = true;
            inputConsumed = false;
            ignoreReason = string.Empty;
            missReason = string.Empty;
            judgementText = "예고를 보고 같은 채널을 누르세요.";
            lastResult = GreedLastJudgementResult.None;
            Publish();
        }

        private GreedLastPatternModel SelectPatternFromDeck()
        {
            int deckIndex = patternIndex;
            if (infiniteMode)
            {
                deckIndex += infiniteSectionsCleared + GetInfiniteThreatLevel();
            }
            else
            {
                deckIndex += Mathf.Max(0, chapterIndex - 1) * 2;
                if (phase == GreedLastRunPhase.EscapeRunning)
                {
                    deckIndex += 7;
                }

                deckIndex = ApplyRoutePatternBias(deckIndex);
            }

            return patternDeck[Mathf.Abs(deckIndex) % patternDeck.Length];
        }

        private int ApplyRoutePatternBias(int deckIndex)
        {
            if (HasChoice(lastNode, "변주 루트"))
            {
                return FindPatternIndexByTiming(deckIndex + 1, GreedLastTimingType.Offbeat);
            }

            if (HasChoice(lastNode, "압박 루트"))
            {
                return FindPatternIndexByTiming(deckIndex + 2, GreedLastTimingType.Clutch);
            }

            if (HasChoice(lastNode, "안정 루트"))
            {
                return FindPatternIndexAvoidingTiming(deckIndex, GreedLastTimingType.Offbeat);
            }

            return deckIndex;
        }

        private int FindPatternIndexByTiming(int startIndex, GreedLastTimingType timingType)
        {
            for (int i = 0; i < patternDeck.Length; i += 1)
            {
                int candidate = Mathf.Abs(startIndex + i) % patternDeck.Length;
                if (patternDeck[candidate].TimingType == timingType)
                {
                    return candidate;
                }
            }

            return startIndex;
        }

        private int FindPatternIndexAvoidingTiming(int startIndex, GreedLastTimingType timingType)
        {
            for (int i = 0; i < patternDeck.Length; i += 1)
            {
                int candidate = Mathf.Abs(startIndex + i) % patternDeck.Length;
                if (patternDeck[candidate].TimingType != timingType)
                {
                    return candidate;
                }
            }

            return startIndex;
        }

        private GreedLastPatternModel BuildActivePattern(GreedLastPatternModel source)
        {
            if (!infiniteMode)
            {
                int pressureLevel = phase == GreedLastRunPhase.EscapeRunning
                    ? 4
                    : Mathf.Clamp(chapterIndex, 1, 4);
                float normalSpeedScale = Mathf.Clamp(1f - (pressureLevel - 1) * 0.035f, 0.88f, 1f);
                float normalWindowScale = Mathf.Clamp(1f - (pressureLevel - 1) * 0.025f, 0.90f, 1f);
                string routeSuffix = string.Empty;
                if (HasChoice(lastNode, "안정 루트"))
                {
                    normalSpeedScale = Mathf.Min(1.05f, normalSpeedScale * 1.04f);
                    normalWindowScale = Mathf.Min(1.12f, normalWindowScale * 1.08f);
                    routeSuffix = " [안정]";
                }
                else if (HasChoice(lastNode, "변주 루트"))
                {
                    normalSpeedScale = Mathf.Clamp(normalSpeedScale * 0.98f, 0.84f, 1f);
                    normalWindowScale = Mathf.Clamp(normalWindowScale * 0.98f, 0.86f, 1f);
                    routeSuffix = " [변주]";
                }
                else if (HasChoice(lastNode, "압박 루트"))
                {
                    normalSpeedScale = Mathf.Clamp(normalSpeedScale * 0.92f, 0.80f, 1f);
                    normalWindowScale = Mathf.Clamp(normalWindowScale * 0.94f, 0.84f, 1f);
                    routeSuffix = " [압박]";
                }

                string suffix = phase == GreedLastRunPhase.EscapeRunning
                    ? " [탈출]"
                    : pressureLevel >= 3
                        ? " [심층]"
                        : string.Empty;
                return new GreedLastPatternModel(
                    source.PatternId + "_normal_" + pressureLevel,
                    source.Channel,
                    source.TimingType,
                    source.Prompt + suffix + routeSuffix,
                    source.TelegraphSeconds * normalSpeedScale,
                    source.CoreSeconds * normalSpeedScale,
                    source.SuccessWindowSeconds * normalWindowScale,
                    source.GoodWindowSeconds * normalWindowScale,
                    source.HitSeconds * normalSpeedScale);
            }

            int threatLevel = GetInfiniteThreatLevel();
            float speedScale = Mathf.Clamp(1f - (threatLevel - 1) * 0.07f, 0.72f, 1f);
            float windowScale = Mathf.Clamp(1f - (threatLevel - 1) * 0.05f, 0.80f, 1f);
            return new GreedLastPatternModel(
                source.PatternId + "_threat_" + threatLevel,
                source.Channel,
                source.TimingType,
                source.Prompt + " [위협 " + threatLevel + "]",
                source.TelegraphSeconds * speedScale,
                source.CoreSeconds * speedScale,
                source.SuccessWindowSeconds * windowScale,
                source.GoodWindowSeconds * windowScale,
                source.HitSeconds * speedScale);
        }

        private int GetInfiniteThreatLevel()
        {
            if (!infiniteMode)
            {
                return 1;
            }

            int baseThreat = Mathf.Clamp(1 + infiniteSectionsCleared / 2, 1, InfiniteThreatMaxLevel);
            return Mathf.Clamp(baseThreat + infiniteThreatTestOffset, 1, InfiniteThreatMaxLevel);
        }

        private void Ignore(string reason, string message)
        {
            ignoreReason = reason;
            judgementText = message;
            Publish();
        }

        private static string FormatTimingDelta(float signedDeltaSeconds)
        {
            int milliseconds = Mathf.RoundToInt(Mathf.Abs(signedDeltaSeconds) * 1000f);
            if (milliseconds <= 20)
            {
                return "정확";
            }

            return signedDeltaSeconds < 0f
                ? "빠름 " + milliseconds + "ms"
                : "늦음 " + milliseconds + "ms";
        }

        private string BuildTimingProfileText()
        {
            if (timingSampleCount <= 0)
            {
                return "타이밍 기록 없음 / 추천 샘플 0/" + TimingRecommendationMinSamples;
            }

            float averageSigned = timingSignedErrorTotal / timingSampleCount;
            float averageAbs = timingAbsErrorTotal / timingSampleCount;
            string recommendation = HasTimingRecommendation
                ? "추천 보정 " + FormatOffset(TimingRecommendationMilliseconds / 1000f)
                : "추천 샘플 " + timingSampleCount + "/" + TimingRecommendationMinSamples;
            return "평균 " + FormatTimingDelta(averageSigned)
                + " / 오차 " + Mathf.RoundToInt(averageAbs * 1000f) + "ms"
                + " / 빠름 " + earlyInputCount
                + " 늦음 " + lateInputCount
                + " / " + recommendation;
        }

        private static string FormatOffset(float seconds)
        {
            int milliseconds = Mathf.RoundToInt(seconds * 1000f);
            if (milliseconds == 0)
            {
                return "0ms";
            }

            return milliseconds > 0
                ? "+" + milliseconds + "ms"
                : milliseconds + "ms";
        }

        private static string FormatChannel(GreedLastRunChannel channel)
        {
            switch (channel)
            {
                case GreedLastRunChannel.Left:
                    return "좌";
                case GreedLastRunChannel.Center:
                    return "중";
                case GreedLastRunChannel.Right:
                    return "우";
                default:
                    return "-";
            }
        }

        private void Publish()
        {
            float realtimeNow = Time.realtimeSinceStartup;
            float now = paused ? pausedAt : realtimeNow;
            float elapsedSeconds = activePattern ? now - patternStartedAt : 0f;
            float hitSeconds = Mathf.Max(0.01f, currentPattern.HitSeconds);
            float beatProgress = activePattern ? Mathf.Clamp01(elapsedSeconds / hitSeconds) : 0f;
            float coreDelta = activePattern ? elapsedSeconds - currentPattern.CoreSeconds : 0f;
            float resumeCountdownRemaining = resumeCountdownActive
                ? Mathf.Max(0f, resumeCountdownEndsAt - realtimeNow)
                : 0f;
            float startCountdownRemaining = startCountdownActive
                ? Mathf.Max(0f, startCountdownEndsAt - realtimeNow)
                : 0f;
            SnapshotChanged?.Invoke(new GreedLastRunSnapshot(
                currentPattern,
                judgementText,
                ignoreReason,
                missReason,
                score,
                health,
                combo,
                focus,
                MaxFocus,
                maxCombo,
                successCount,
                goodCount,
                missCount,
                infiniteSectionsCleared,
                GetInfiniteThreatLevel(),
                distance,
                chapterIndex,
                chapterProgress,
                ChapterTargetPatterns,
                phase,
                choiceKind,
                choicePrompt,
                choiceLeft,
                choiceCenter,
                choiceRight,
                coreRetrieved,
                escapeSucceeded,
                saveEligible,
                lastResult,
                activePattern,
                autoFlow,
                stopped,
                paused,
                resumeCountdownActive,
                startCountdownActive,
                now < staggerUntil,
                devInvincible,
                infiniteMode,
                elapsedSeconds,
                beatProgress,
                coreDelta,
                resumeCountdownRemaining,
                startCountdownRemaining,
                inputTimingOffsetSeconds,
                BuildTimingProfileText()));
        }
    }
}
