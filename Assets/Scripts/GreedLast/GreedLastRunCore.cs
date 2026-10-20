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

    public enum GreedLastAvoidanceType
    {
        None = 0,
        Jump = 1,
        Slide = 2,
        Twist = 3,
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
        ReviewTargetCheck = 4,
        RouteCycle = 5,
        AvoidanceCycle = 6,
        VisualDropPreview = 7,
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
            int maxCombo,
            int successCount,
            int goodCount,
            int missCount,
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
            MaxCombo = maxCombo;
            SuccessCount = successCount;
            GoodCount = goodCount;
            MissCount = missCount;
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
        public int MaxCombo { get; }
        public int SuccessCount { get; }
        public int GoodCount { get; }
        public int MissCount { get; }
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
            string loadoutProfile,
            int successCount,
            int goodCount,
            int missCount,
            int maxCombo,
            string missReason,
            string timingProfile)
        {
            IsValid = isValid;
            Score = score;
            Distance = distance;
            SectionsCleared = sectionsCleared;
            MaxThreatLevel = maxThreatLevel;
            LoadoutName = loadoutName ?? string.Empty;
            LoadoutProfile = loadoutProfile ?? string.Empty;
            SuccessCount = successCount;
            GoodCount = goodCount;
            MissCount = missCount;
            MaxCombo = maxCombo;
            MissReason = missReason ?? string.Empty;
            TimingProfile = timingProfile ?? string.Empty;
        }

        public bool IsValid { get; }
        public int Score { get; }
        public float Distance { get; }
        public int SectionsCleared { get; }
        public int MaxThreatLevel { get; }
        public string LoadoutName { get; }
        public string LoadoutProfile { get; }
        public int SuccessCount { get; }
        public int GoodCount { get; }
        public int MissCount { get; }
        public int MaxCombo { get; }
        public string MissReason { get; }
        public string TimingProfile { get; }

        public static GreedLastInfiniteRunRecord Empty => new GreedLastInfiniteRunRecord(
            false,
            0,
            0f,
            0,
            0,
            string.Empty,
            string.Empty,
            0,
            0,
            0,
            0,
            string.Empty,
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
            float hitSeconds,
            GreedLastAvoidanceType avoidanceType = GreedLastAvoidanceType.None)
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
            AvoidanceType = avoidanceType;
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
        public GreedLastAvoidanceType AvoidanceType { get; }

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
            string missReason,
            bool centerHit = false)
        {
            Result = result;
            TimingType = timingType;
            Message = message ?? string.Empty;
            IgnoreReason = ignoreReason ?? string.Empty;
            MissReason = missReason ?? string.Empty;
            CenterHit = centerHit;
        }

        public GreedLastJudgementResult Result { get; }
        public GreedLastTimingType TimingType { get; }
        public string Message { get; }
        public string IgnoreReason { get; }
        public string MissReason { get; }
        public bool CenterHit { get; }
    }

    public readonly struct GreedLastRunSnapshot
    {
        public GreedLastRunSnapshot(
            GreedLastPatternModel pattern,
            GreedLastPatternModel lastJudgedPattern,
            string judgementText,
            string ignoreReason,
            string missReason,
            int score,
            int health,
            int maxHealth,
            int combo,
            int focus,
            int maxFocus,
            bool focusStateActive,
            float focusStateRemainingSeconds,
            int focusStateActivationCount,
            int focusStateSuccessCount,
            float focusStateActiveSeconds,
            int maxCombo,
            int precisionStreak,
            int maxPrecisionStreak,
            int recoveryPrecisionCount,
            int perfectFocusChargeCount,
            int clutchCounterCount,
            int offbeatFlowCount,
            int stableRouteRecoveryCount,
            int variationRouteFlowCount,
            int pressureRouteCounterCount,
            int stableRouteBundleCount,
            int variationRouteBundleCount,
            int pressureRouteBundleCount,
            bool lastCenterHit,
            int successCount,
            int perfectSuccessCount,
            int clutchSuccessCount,
            int offbeatSuccessCount,
            int goodCount,
            int missCount,
            int noInputMissCount,
            int infiniteSectionsCleared,
            int infiniteThreatLevel,
            int maxInfiniteThreatLevel,
            float distance,
            int chapterIndex,
            int chapterProgress,
            int chapterTarget,
            string lastCompletedSegment,
            int lastCompletedSegmentExactCount,
            int lastCompletedSegmentGoodCount,
            int lastCompletedSegmentMissCount,
            int currentSegmentExactCount,
            int currentSegmentGoodCount,
            int currentSegmentMissCount,
            int currentBundleAvoidanceMask,
            int threeActionExactBundleCount,
            bool perfectBundleIntact,
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
            float staggerRemainingSeconds,
            bool devInvincible,
            bool infiniteMode,
            bool refillRun,
            bool practiceMode,
            float elapsedSeconds,
            float beatProgress,
            float coreDeltaSeconds,
            float resumeCountdownRemainingSeconds,
            float startCountdownRemainingSeconds,
            float inputTimingOffsetSeconds,
            bool hasTimingRecommendation,
            int timingRecommendationMilliseconds,
            int timingRecommendationSampleCount,
            int timingRecommendationSampleTarget,
            bool timingRecommendationApplied,
            int appliedTimingRecommendationMilliseconds,
            int focusGiftStacks,
            int survivalGiftStacks,
            int chainGiftStacks,
            string activeRelic,
            string activeChoiceProfile,
            string activeRouteProfile,
            string activeInfiniteLoadoutName,
            string activeInfiniteLoadoutProfile,
            string reviewTargetText,
            string recentPatternFlow,
            string timingProfile,
            int lastJudgementScoreGain,
            int lastJudgementHealthChange,
            bool hasJudgedInputTiming,
            float lastInputTimingDeltaSeconds,
            GreedLastRunChannel lastJudgedInputChannel,
            int centerHitScoreGainPreview = 0,
            int goodScoreGainPreview = 0,
            int goodHealthGainPreview = 0,
            int nextInfiniteThreatLevel = 0,
            int choiceHealthGainPreview = 0,
            bool lastJudgementStartedInFocusState = false,
            float lastJudgementFocusStateExtensionSeconds = 0f,
            int lastJudgementProfileScoreBonus = -1,
            int lastJudgementThreatScoreBonus = -1,
            bool lastJudgementScoreLimited = false)
        {
            Pattern = pattern;
            LastJudgedPattern = lastJudgedPattern;
            JudgementText = judgementText ?? string.Empty;
            IgnoreReason = ignoreReason ?? string.Empty;
            MissReason = missReason ?? string.Empty;
            Score = score;
            Health = health;
            MaxHealth = Mathf.Max(1, maxHealth);
            Combo = combo;
            Focus = focus;
            MaxFocus = maxFocus;
            FocusStateActive = focusStateActive;
            FocusStateRemainingSeconds = Mathf.Max(0f, focusStateRemainingSeconds);
            FocusStateActivationCount = Mathf.Max(0, focusStateActivationCount);
            FocusStateSuccessCount = Mathf.Max(0, focusStateSuccessCount);
            FocusStateActiveSeconds = Mathf.Max(0f, focusStateActiveSeconds);
            MaxCombo = maxCombo;
            PrecisionStreak = Mathf.Max(0, precisionStreak);
            MaxPrecisionStreak = Mathf.Max(0, maxPrecisionStreak);
            RecoveryPrecisionCount = Mathf.Max(0, recoveryPrecisionCount);
            PerfectFocusChargeCount = Mathf.Max(0, perfectFocusChargeCount);
            ClutchCounterCount = Mathf.Max(0, clutchCounterCount);
            OffbeatFlowCount = Mathf.Max(0, offbeatFlowCount);
            StableRouteRecoveryCount = Mathf.Max(0, stableRouteRecoveryCount);
            VariationRouteFlowCount = Mathf.Max(0, variationRouteFlowCount);
            PressureRouteCounterCount = Mathf.Max(0, pressureRouteCounterCount);
            StableRouteBundleCount = Mathf.Max(0, stableRouteBundleCount);
            VariationRouteBundleCount = Mathf.Max(0, variationRouteBundleCount);
            PressureRouteBundleCount = Mathf.Max(0, pressureRouteBundleCount);
            LastCenterHit = lastCenterHit;
            SuccessCount = successCount;
            PerfectSuccessCount = Mathf.Max(0, perfectSuccessCount);
            ClutchSuccessCount = Mathf.Max(0, clutchSuccessCount);
            OffbeatSuccessCount = Mathf.Max(0, offbeatSuccessCount);
            GoodCount = goodCount;
            MissCount = missCount;
            NoInputMissCount = Mathf.Max(0, noInputMissCount);
            InfiniteSectionsCleared = infiniteSectionsCleared;
            InfiniteThreatLevel = infiniteThreatLevel;
            MaxInfiniteThreatLevel = Mathf.Max(InfiniteThreatLevel, maxInfiniteThreatLevel);
            Distance = distance;
            ChapterIndex = chapterIndex;
            ChapterProgress = chapterProgress;
            ChapterTarget = chapterTarget;
            LastCompletedSegment = lastCompletedSegment ?? string.Empty;
            LastCompletedSegmentExactCount = Mathf.Max(0, lastCompletedSegmentExactCount);
            LastCompletedSegmentGoodCount = Mathf.Max(0, lastCompletedSegmentGoodCount);
            LastCompletedSegmentMissCount = Mathf.Max(0, lastCompletedSegmentMissCount);
            CurrentSegmentExactCount = Mathf.Max(0, currentSegmentExactCount);
            CurrentSegmentGoodCount = Mathf.Max(0, currentSegmentGoodCount);
            CurrentSegmentMissCount = Mathf.Max(0, currentSegmentMissCount);
            CurrentBundleAvoidanceMask = Mathf.Max(0, currentBundleAvoidanceMask);
            ThreeActionExactBundleCount = Mathf.Max(0, threeActionExactBundleCount);
            PerfectBundleIntact = perfectBundleIntact;
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
            StaggerRemainingSeconds = Mathf.Max(0f, staggerRemainingSeconds);
            DevInvincible = devInvincible;
            InfiniteMode = infiniteMode;
            RefillRun = refillRun;
            PracticeMode = practiceMode;
            ElapsedSeconds = elapsedSeconds;
            BeatProgress = beatProgress;
            CoreDeltaSeconds = coreDeltaSeconds;
            ResumeCountdownRemainingSeconds = resumeCountdownRemainingSeconds;
            StartCountdownRemainingSeconds = startCountdownRemainingSeconds;
            InputTimingOffsetSeconds = inputTimingOffsetSeconds;
            HasTimingRecommendation = hasTimingRecommendation;
            TimingRecommendationMilliseconds = timingRecommendationMilliseconds;
            TimingRecommendationSampleCount = Mathf.Max(0, timingRecommendationSampleCount);
            TimingRecommendationSampleTarget = Mathf.Max(1, timingRecommendationSampleTarget);
            TimingRecommendationApplied = timingRecommendationApplied;
            AppliedTimingRecommendationMilliseconds = appliedTimingRecommendationMilliseconds;
            FocusGiftStacks = Mathf.Max(0, focusGiftStacks);
            SurvivalGiftStacks = Mathf.Max(0, survivalGiftStacks);
            ChainGiftStacks = Mathf.Max(0, chainGiftStacks);
            ActiveRelic = activeRelic ?? string.Empty;
            ActiveChoiceProfile = activeChoiceProfile ?? string.Empty;
            ActiveRouteProfile = activeRouteProfile ?? string.Empty;
            ActiveInfiniteLoadoutName = activeInfiniteLoadoutName ?? string.Empty;
            ActiveInfiniteLoadoutProfile = activeInfiniteLoadoutProfile ?? string.Empty;
            ReviewTargetText = reviewTargetText ?? string.Empty;
            RecentPatternFlow = recentPatternFlow ?? string.Empty;
            TimingProfile = timingProfile ?? string.Empty;
            LastJudgementScoreGain = Mathf.Max(0, lastJudgementScoreGain);
            LastJudgementHealthChange = lastJudgementHealthChange;
            HasJudgedInputTiming = hasJudgedInputTiming;
            LastInputTimingDeltaSeconds = lastInputTimingDeltaSeconds;
            LastJudgedInputChannel = lastJudgedInputChannel;
            CenterHitScoreGainPreview = Mathf.Max(0, centerHitScoreGainPreview);
            GoodScoreGainPreview = Mathf.Max(0, goodScoreGainPreview);
            GoodHealthGainPreview = Mathf.Max(0, goodHealthGainPreview);
            NextInfiniteThreatLevel = Mathf.Clamp(nextInfiniteThreatLevel, 0, 5);
            ChoiceHealthGainPreview = Mathf.Max(0, choiceHealthGainPreview);
            LastJudgementStartedInFocusState = lastJudgementStartedInFocusState;
            LastJudgementFocusStateExtensionSeconds = Mathf.Max(0f, lastJudgementFocusStateExtensionSeconds);
            LastJudgementProfileScoreBonus = Mathf.Max(-1, lastJudgementProfileScoreBonus);
            LastJudgementThreatScoreBonus = Mathf.Max(-1, lastJudgementThreatScoreBonus);
            LastJudgementScoreLimited = lastJudgementScoreLimited;
        }

        public GreedLastPatternModel Pattern { get; }
        public GreedLastPatternModel LastJudgedPattern { get; }
        public string JudgementText { get; }
        public string IgnoreReason { get; }
        public string MissReason { get; }
        public int Score { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public int Combo { get; }
        public int Focus { get; }
        public int MaxFocus { get; }
        public bool FocusStateActive { get; }
        public float FocusStateRemainingSeconds { get; }
        public int FocusStateActivationCount { get; }
        public int FocusStateSuccessCount { get; }
        public float FocusStateActiveSeconds { get; }
        public int MaxCombo { get; }
        public int PrecisionStreak { get; }
        public int MaxPrecisionStreak { get; }
        public int RecoveryPrecisionCount { get; }
        public int PerfectFocusChargeCount { get; }
        public int ClutchCounterCount { get; }
        public int OffbeatFlowCount { get; }
        public int StableRouteRecoveryCount { get; }
        public int VariationRouteFlowCount { get; }
        public int PressureRouteCounterCount { get; }
        public int StableRouteBundleCount { get; }
        public int VariationRouteBundleCount { get; }
        public int PressureRouteBundleCount { get; }
        public bool LastCenterHit { get; }
        public int LastJudgementScoreGain { get; }
        public int CenterHitScoreGainPreview { get; }
        public int GoodScoreGainPreview { get; }
        public int GoodHealthGainPreview { get; }
        public int ChoiceHealthGainPreview { get; }
        public bool LastJudgementStartedInFocusState { get; }
        public float LastJudgementFocusStateExtensionSeconds { get; }
        // Negative bonus defaults distinguish older constructed snapshots from live accounting.
        public int LastJudgementProfileScoreBonus { get; }
        public int LastJudgementThreatScoreBonus { get; }
        public bool LastJudgementScoreLimited { get; }
        public int LastJudgementHealthChange { get; }
        public bool HasJudgedInputTiming { get; }
        public float LastInputTimingDeltaSeconds { get; }
        public GreedLastRunChannel LastJudgedInputChannel { get; }
        public int SuccessCount { get; }
        public int PerfectSuccessCount { get; }
        public int ClutchSuccessCount { get; }
        public int OffbeatSuccessCount { get; }
        public int GoodCount { get; }
        public int MissCount { get; }
        public int NoInputMissCount { get; }
        public int InfiniteSectionsCleared { get; }
        public int InfiniteThreatLevel { get; }
        public int NextInfiniteThreatLevel { get; }
        public int MaxInfiniteThreatLevel { get; }
        public float Distance { get; }
        public int ChapterIndex { get; }
        public int ChapterProgress { get; }
        public int ChapterTarget { get; }
        public string LastCompletedSegment { get; }
        public int LastCompletedSegmentExactCount { get; }
        public int LastCompletedSegmentGoodCount { get; }
        public int LastCompletedSegmentMissCount { get; }
        public int CurrentSegmentExactCount { get; }
        public int CurrentSegmentGoodCount { get; }
        public int CurrentSegmentMissCount { get; }
        public int CurrentBundleAvoidanceMask { get; }
        public int ThreeActionExactBundleCount { get; }
        public bool PerfectBundleIntact { get; }
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
        public float StaggerRemainingSeconds { get; }
        public bool DevInvincible { get; }
        public bool InfiniteMode { get; }
        public bool RefillRun { get; }
        public bool PracticeMode { get; }
        public float ElapsedSeconds { get; }
        public float BeatProgress { get; }
        public float CoreDeltaSeconds { get; }
        public float ResumeCountdownRemainingSeconds { get; }
        public float StartCountdownRemainingSeconds { get; }
        public float CountdownRemainingSeconds => ResumeCountdownActive ? ResumeCountdownRemainingSeconds : StartCountdownRemainingSeconds;
        public float InputTimingOffsetSeconds { get; }
        public bool HasTimingRecommendation { get; }
        public int TimingRecommendationMilliseconds { get; }
        public int TimingRecommendationSampleCount { get; }
        public int TimingRecommendationSampleTarget { get; }
        public bool TimingRecommendationApplied { get; }
        public int AppliedTimingRecommendationMilliseconds { get; }
        public int FocusGiftStacks { get; }
        public int SurvivalGiftStacks { get; }
        public int ChainGiftStacks { get; }
        public string ActiveRelic { get; }
        public string ActiveChoiceProfile { get; }
        public string ActiveRouteProfile { get; }
        public string ActiveInfiniteLoadoutName { get; }
        public string ActiveInfiniteLoadoutProfile { get; }
        public string ReviewTargetText { get; }
        public string RecentPatternFlow { get; }
        public string TimingProfile { get; }
    }

    public sealed class GreedLastRunCore
    {
        private const int MaxHealth = 3;
        private const int MaxFocus = 3;
        private const float FocusStateDurationSeconds = 5f;
        private const float FocusStateSuccessExtensionSeconds = 0.35f;
        private const float FocusStateBundleExtensionSeconds = 1f;
        private const float VariationRouteBundleExtensionSeconds = 1.5f;
        private const float FocusStateMaxDurationSeconds = 7f;
        private const float FocusStateScoreMultiplier = 1.20f;
        private const int MaxGrowthLevel = 3;
        private const int MaxChoiceHistory = 12;
        private const float StaggerSeconds = 0.55f;
        private const float ChapterOneGapSeconds = 0.82f;
        private const float ChapterTwoGapSeconds = 0.64f;
        private const float ChapterThreeGapSeconds = 0.40f;
        private const float ChapterFourGapSeconds = 0.54f;
        private const float EscapeGapSeconds = 0.44f;
        private const float InfiniteBaseGapSeconds = 0.68f;
        private const float InfiniteThreatRiseBreatherSeconds = 0.45f;
        private const float RunSpeedMetersPerSecond = 4.2f;
        private const float MinInputTimingOffsetSeconds = -0.2f;
        private const float MaxInputTimingOffsetSeconds = 0.2f;
        private const int ChapterTargetPatterns = 3;
        private const int InfiniteThreatMaxLevel = 5;
        private const float StartCountdownSeconds = 1.4f;
        private const float StartBufferSeconds = 0.25f;
        private const float ResumeCountdownSeconds = 1.4f;
        private const float ResumeBufferSeconds = 0.65f;
        public const float MaxInteractiveFrameGapSeconds = 0.5f;
        private const int TimingRecommendationMinSamples = 3;
        private const float CenterHitWindowScale = 0.45f;
        private const int PrecisionStreakFocusStep = 3;
        private const int RecentPatternMemory = 4;
        public const string InputTimingOffsetPrefsKey = "GreedLast.RunCore.InputTimingOffsetSeconds";

        public const int RecoveryPrecisionScoreBonus = 75;
        public const int RecoveryPrecisionFocusBonus = 1;
        public const int RecoveryPrecisionComboBonus = 1;
        public const int PerfectFocusChargeBonus = 1;
        public const int ClutchCounterScoreBonus = 50;
        public const int OffbeatFlowComboBonus = 1;
        public const int StableRouteGoodHealthBonus = 1;
        public const int VariationRouteOffbeatFocusBonus = 1;
        public const int PressureRouteClutchScoreBonus = 35;
        public const int StableRouteBundleHealthBonus = 1;
        public const int VariationRouteBundleFocusBonus = 2;
        public const int PressureRouteBundleScoreBonus = 150;
        public const float FinalValidationTempoEaseScale = 1.04f;
        public const float FinalValidationWindowEaseScale = 1.05f;

        private readonly GreedLastPatternModel[] patternDeck =
        {
            new GreedLastPatternModel("left_floor_perfect", GreedLastRunChannel.Left, GreedLastTimingType.Perfect, "좌측 바닥 창살", 0.45f, 1.05f, 0.10f, 0.30f, 1.48f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("center_gate_clutch", GreedLastRunChannel.Center, GreedLastTimingType.Clutch, "정면 압박문", 0.55f, 1.28f, 0.13f, 0.36f, 1.72f, GreedLastAvoidanceType.Slide),
            new GreedLastPatternModel("right_blade_offbeat", GreedLastRunChannel.Right, GreedLastTimingType.Offbeat, "우측 엇박 칼날", 0.40f, 0.92f, 0.11f, 0.31f, 1.36f, GreedLastAvoidanceType.Twist),
            new GreedLastPatternModel("left_wall_clutch", GreedLastRunChannel.Left, GreedLastTimingType.Clutch, "좌측 회전 벽", 0.50f, 1.18f, 0.12f, 0.34f, 1.58f, GreedLastAvoidanceType.Twist),
            new GreedLastPatternModel("left_rotate_corridor_perfect", GreedLastRunChannel.Left, GreedLastTimingType.Perfect, "좌회전 통로 정렬", 0.46f, 1.05f, 0.10f, 0.30f, 1.48f, GreedLastAvoidanceType.Twist),
            new GreedLastPatternModel("right_rotate_corridor_clutch", GreedLastRunChannel.Right, GreedLastTimingType.Clutch, "우회전 통로 압박", 0.52f, 1.22f, 0.12f, 0.34f, 1.64f, GreedLastAvoidanceType.Twist),
            new GreedLastPatternModel("center_sand_perfect", GreedLastRunChannel.Center, GreedLastTimingType.Perfect, "중앙 모래 함몰", 0.44f, 1.00f, 0.10f, 0.29f, 1.42f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("right_pillar_clutch", GreedLastRunChannel.Right, GreedLastTimingType.Clutch, "우측 낙하 기둥", 0.58f, 1.34f, 0.13f, 0.37f, 1.78f, GreedLastAvoidanceType.Slide),
            new GreedLastPatternModel("left_falsebeat_offbeat", GreedLastRunChannel.Left, GreedLastTimingType.Offbeat, "좌측 엇박 발판", 0.38f, 0.88f, 0.11f, 0.30f, 1.32f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("center_guard_clutch", GreedLastRunChannel.Center, GreedLastTimingType.Clutch, "정면 수호상 압박", 0.53f, 1.24f, 0.12f, 0.35f, 1.66f, GreedLastAvoidanceType.Slide),
            new GreedLastPatternModel("right_spear_perfect", GreedLastRunChannel.Right, GreedLastTimingType.Perfect, "우측 황금 창", 0.46f, 1.08f, 0.10f, 0.30f, 1.50f, GreedLastAvoidanceType.Twist),
            new GreedLastPatternModel("left_sand_clutch", GreedLastRunChannel.Left, GreedLastTimingType.Clutch, "좌측 모래 압류", 0.52f, 1.22f, 0.12f, 0.34f, 1.64f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("center_falsebeat_offbeat", GreedLastRunChannel.Center, GreedLastTimingType.Offbeat, "중앙 엇박 석문", 0.39f, 0.90f, 0.11f, 0.30f, 1.34f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("right_floor_perfect", GreedLastRunChannel.Right, GreedLastTimingType.Perfect, "우측 붕괴 바닥", 0.47f, 1.10f, 0.10f, 0.30f, 1.52f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("left_blade_offbeat", GreedLastRunChannel.Left, GreedLastTimingType.Offbeat, "좌측 엇박 칼날", 0.37f, 0.86f, 0.10f, 0.29f, 1.28f, GreedLastAvoidanceType.Twist),
            new GreedLastPatternModel("center_beam_perfect", GreedLastRunChannel.Center, GreedLastTimingType.Perfect, "중앙 광선 틈", 0.43f, 0.98f, 0.10f, 0.28f, 1.38f, GreedLastAvoidanceType.Slide),
            new GreedLastPatternModel("right_guard_clutch", GreedLastRunChannel.Right, GreedLastTimingType.Clutch, "우측 수호상 압박", 0.56f, 1.30f, 0.13f, 0.36f, 1.74f, GreedLastAvoidanceType.Slide),
            new GreedLastPatternModel("left_drum_perfect", GreedLastRunChannel.Left, GreedLastTimingType.Perfect, "좌측 박동 낙석", 0.42f, 0.96f, 0.10f, 0.28f, 1.36f, GreedLastAvoidanceType.Slide),
            new GreedLastPatternModel("center_pulse_clutch", GreedLastRunChannel.Center, GreedLastTimingType.Clutch, "중앙 박동 기둥", 0.57f, 1.32f, 0.13f, 0.36f, 1.76f, GreedLastAvoidanceType.Slide),
            new GreedLastPatternModel("right_vortex_offbeat", GreedLastRunChannel.Right, GreedLastTimingType.Offbeat, "우측 모래 와류", 0.36f, 0.84f, 0.10f, 0.29f, 1.26f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("left_hidden_clutch", GreedLastRunChannel.Left, GreedLastTimingType.Clutch, "좌측 숨은 창", 0.54f, 1.26f, 0.12f, 0.35f, 1.68f, GreedLastAvoidanceType.Twist),
            new GreedLastPatternModel("center_crack_offbeat", GreedLastRunChannel.Center, GreedLastTimingType.Offbeat, "중앙 엇박 균열", 0.38f, 0.87f, 0.10f, 0.29f, 1.30f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("right_threshold_perfect", GreedLastRunChannel.Right, GreedLastTimingType.Perfect, "우측 정박 문턱", 0.45f, 1.04f, 0.10f, 0.30f, 1.46f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("left_mirror_perfect", GreedLastRunChannel.Left, GreedLastTimingType.Perfect, "좌측 거울 발판", 0.44f, 1.02f, 0.10f, 0.29f, 1.44f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("center_chain_clutch", GreedLastRunChannel.Center, GreedLastTimingType.Clutch, "중앙 사슬 압박", 0.59f, 1.36f, 0.13f, 0.37f, 1.80f, GreedLastAvoidanceType.Slide),
            new GreedLastPatternModel("right_echo_offbeat", GreedLastRunChannel.Right, GreedLastTimingType.Offbeat, "우측 잔향 발판", 0.37f, 0.85f, 0.10f, 0.29f, 1.28f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("left_scarab_offbeat", GreedLastRunChannel.Left, GreedLastTimingType.Offbeat, "좌측 엇박 성충", 0.36f, 0.83f, 0.10f, 0.28f, 1.24f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("center_weight_perfect", GreedLastRunChannel.Center, GreedLastTimingType.Perfect, "중앙 무게판", 0.48f, 1.12f, 0.10f, 0.31f, 1.54f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("right_hidden_clutch", GreedLastRunChannel.Right, GreedLastTimingType.Clutch, "우측 숨은 창", 0.55f, 1.27f, 0.12f, 0.35f, 1.70f, GreedLastAvoidanceType.Twist),
            new GreedLastPatternModel("left_drop_perfect", GreedLastRunChannel.Left, GreedLastTimingType.Perfect, "좌측 낙하 석판", 0.46f, 1.07f, 0.10f, 0.30f, 1.50f, GreedLastAvoidanceType.Slide),
            new GreedLastPatternModel("center_echo_offbeat", GreedLastRunChannel.Center, GreedLastTimingType.Offbeat, "중앙 잔향 틈", 0.39f, 0.89f, 0.10f, 0.29f, 1.32f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("right_chain_clutch", GreedLastRunChannel.Right, GreedLastTimingType.Clutch, "우측 사슬 압박", 0.58f, 1.33f, 0.13f, 0.36f, 1.77f, GreedLastAvoidanceType.Slide),
            new GreedLastPatternModel("left_echo_offbeat", GreedLastRunChannel.Left, GreedLastTimingType.Offbeat, "좌측 잔향 틈", 0.37f, 0.86f, 0.10f, 0.29f, 1.29f, GreedLastAvoidanceType.Jump),
            new GreedLastPatternModel("center_obelisk_perfect", GreedLastRunChannel.Center, GreedLastTimingType.Perfect, "중앙 오벨리스크 틈", 0.47f, 1.09f, 0.10f, 0.30f, 1.51f, GreedLastAvoidanceType.Slide),
            new GreedLastPatternModel("right_scarab_offbeat", GreedLastRunChannel.Right, GreedLastTimingType.Offbeat, "우측 엇박 성충", 0.36f, 0.84f, 0.10f, 0.28f, 1.25f, GreedLastAvoidanceType.Jump),
        };

        private GreedLastPatternModel currentPattern = GreedLastPatternModel.Empty;
        private GreedLastPatternModel lastJudgedPattern = GreedLastPatternModel.Empty;
        private GreedLastPatternModel devNextPatternOverride = GreedLastPatternModel.Empty;
        private string lastSelectedPatternId = string.Empty;
        private string previousSelectedPatternId = string.Empty;
        private readonly string[] recentPatternFamilyKeys = new string[RecentPatternMemory];
        private readonly GreedLastRunChannel[] recentPatternChannels = new GreedLastRunChannel[RecentPatternMemory];
        private readonly GreedLastTimingType[] recentPatternTimingTypes = new GreedLastTimingType[RecentPatternMemory];
        private readonly GreedLastAvoidanceType[] recentPatternAvoidanceTypes = new GreedLastAvoidanceType[RecentPatternMemory];
        private int recentPatternCount;
        private int devAvoidanceCycleIndex;
        private int patternIndex;
        private int score;
        private int health = MaxHealth;
        private int maxHealthThisRun = MaxHealth;
        private int combo;
        private int focus;
        private float focusStateRemainingSeconds;
        private int focusStateActivationCount;
        private int focusStateSuccessCount;
        private float focusStateActiveSeconds;
        private float runTimeAdvancedAt;
        private int maxCombo;
        private int precisionStreak;
        private int maxPrecisionStreak;
        private int recoveryPrecisionCount;
        private int perfectFocusChargeCount;
        private int clutchCounterCount;
        private int offbeatFlowCount;
        private int stableRouteRecoveryCount;
        private int variationRouteFlowCount;
        private int pressureRouteCounterCount;
        private int stableRouteBundleCount;
        private int variationRouteBundleCount;
        private int pressureRouteBundleCount;
        private int threeActionExactBundleCount;
        private int successCount;
        private int perfectSuccessCount;
        private int clutchSuccessCount;
        private int offbeatSuccessCount;
        private int goodCount;
        private int missCount;
        private int noInputMissCount;
        private readonly int[] avoidanceExactCounts = new int[4];
        private readonly int[] avoidanceGoodCounts = new int[4];
        private readonly int[] avoidanceMissCounts = new int[4];
        private readonly int[] chapterExactCounts = new int[4];
        private readonly int[] chapterGoodCounts = new int[4];
        private readonly int[] chapterMissCounts = new int[4];
        private int escapeExactCount;
        private int escapeGoodCount;
        private int escapeMissCount;
        private string lastCompletedSegment = string.Empty;
        private int lastCompletedSegmentExactCount;
        private int lastCompletedSegmentGoodCount;
        private int lastCompletedSegmentMissCount;
        private int timingSampleCount;
        private int earlyInputCount;
        private int lateInputCount;
        private bool timingRecommendationApplied;
        private int appliedTimingRecommendationMilliseconds;
        private int infiniteSectionsCleared;
        private int infiniteThreatTestOffset;
        private int maxInfiniteThreatLevel;
        private float timingSignedErrorTotal;
        private float timingAbsErrorTotal;
        private float timingRecommendedOffsetTotal;
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
        private int patternBundleBreakCount;
        private int currentBundleExactCount;
        private int currentBundleGoodCount;
        private int currentBundleMissCount;
        private int currentBundleAvoidanceMask;
        private GreedLastRunPhase phase;
        private GreedLastChoiceKind choiceKind;
        private bool coreRetrieved;
        private bool escapeSucceeded;
        private bool saveEligible;
        private bool devInvincible;
        private bool infiniteMode;
        private bool refillRunMode;
        private bool practiceMode;
        private bool infiniteThreatBreatherPatternPending;
        private bool recoveryBreatherPatternPending;
        private int growthSurvivalLevel;
        private int growthFocusLevel;
        private int growthTimingLevel;
        private float inputTimingOffsetSeconds;
        private readonly string[] choiceHistory = new string[MaxChoiceHistory];
        private int choiceHistoryCount;
        private string startGift = string.Empty;
        private string lastNode = string.Empty;
        private string primaryGift = string.Empty;
        private string relic = string.Empty;
        private string activeInfiniteLoadoutName = string.Empty;
        private string activeInfiniteLoadoutProfile = string.Empty;
        private string activeInfiniteLoadoutDisplayProfile = string.Empty;
        private string reviewTargetText = string.Empty;
        private string lastPracticeOpeningContext = string.Empty;
        private int activeInfiniteFocusGiftStacks;
        private int activeInfiniteSurvivalGiftStacks;
        private int activeInfiniteChainGiftStacks;
        private string choicePrompt = string.Empty;
        private string choiceLeft = string.Empty;
        private string choiceCenter = string.Empty;
        private string choiceRight = string.Empty;
        private string judgementText = "다음 함정을 생성하세요.";
        private string ignoreReason = string.Empty;
        private string missReason = string.Empty;
        private GreedLastJudgementResult lastResult = GreedLastJudgementResult.None;
        private bool lastCenterHit;
        private int lastJudgementScoreGain;
        private int lastJudgementHealthChange;
        private bool lastJudgementStartedInFocusState;
        private float lastJudgementFocusStateExtensionSeconds;
        private int lastJudgementProfileScoreBonus, lastJudgementThreatScoreBonus;
        private bool lastJudgementScoreLimited;
        private bool hasJudgedInputTiming;
        private float lastInputTimingDeltaSeconds;
        private GreedLastRunChannel lastJudgedInputChannel;
        private string cachedTimingProfileText;
        private string cachedTimingProfileFocusDuration;
        private float cachedTimingProfileFocusSeconds;
        private string cachedRecentPatternFlowText;

        public event Action<GreedLastRunSnapshot> SnapshotChanged;

        public bool CanExitAfterClear => stopped && saveEligible && phase == GreedLastRunPhase.ClearResolving;
        public bool CanExitAfterPractice => practiceMode && stopped;
        public bool IsStoppedInfiniteRun => stopped && infiniteMode;
        public bool IsStoppedNormalRun => stopped && !infiniteMode && !practiceMode;
        public bool IsPracticeRun => practiceMode;
        public bool IsPracticeComplete => practiceMode && stopped && phase == GreedLastRunPhase.ClearResolving;
        public bool IsInfiniteRun => infiniteMode;
        public bool IsPaused => paused;
        public float InputTimingOffsetSeconds => inputTimingOffsetSeconds;
        public bool HasTimingRecommendation => timingSampleCount >= TimingRecommendationMinSamples;
        public int TimingRecommendationMilliseconds => HasTimingRecommendation
            ? Mathf.RoundToInt(Mathf.Clamp(
                timingRecommendedOffsetTotal / timingSampleCount,
                MinInputTimingOffsetSeconds,
                MaxInputTimingOffsetSeconds) * 1000f)
            : 0;

        private bool CanAutoStartPattern => phase == GreedLastRunPhase.RunPlaying || phase == GreedLastRunPhase.EscapeRunning;

        public void ConfigureGrowth(int survivalLevel, int focusLevel, int timingLevel)
        {
            growthSurvivalLevel = Mathf.Clamp(survivalLevel, 0, MaxGrowthLevel);
            growthFocusLevel = Mathf.Clamp(focusLevel, 0, MaxGrowthLevel);
            growthTimingLevel = Mathf.Clamp(timingLevel, 0, MaxGrowthLevel);
        }

        public GreedLastRunCore()
        {
            inputTimingOffsetSeconds = LoadStoredInputTimingOffsetSeconds();
        }

        public static float LoadStoredInputTimingOffsetSeconds()
        {
            float value = PlayerPrefs.GetFloat(InputTimingOffsetPrefsKey, 0f);
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f : Mathf.Clamp(value, MinInputTimingOffsetSeconds, MaxInputTimingOffsetSeconds);
        }

        public static string BuildStoredInputTimingOffsetChecklistText()
        {
            return "보정: " + FormatOffset(LoadStoredInputTimingOffsetSeconds());
        }

        private void ResetRunStats()
        {
            maxCombo = 0;
            precisionStreak = 0;
            maxPrecisionStreak = 0;
            recoveryPrecisionCount = 0;
            perfectFocusChargeCount = 0;
            clutchCounterCount = 0;
            offbeatFlowCount = 0;
            stableRouteRecoveryCount = 0;
            variationRouteFlowCount = 0;
            pressureRouteCounterCount = 0;
            stableRouteBundleCount = 0;
            variationRouteBundleCount = 0;
            pressureRouteBundleCount = 0;
            threeActionExactBundleCount = 0;
            lastCenterHit = false;
            lastJudgementScoreGain = 0;
            lastJudgementHealthChange = 0;
            lastJudgementStartedInFocusState = false;
            lastJudgementFocusStateExtensionSeconds = 0f;
            lastJudgementProfileScoreBonus = 0;
            lastJudgementThreatScoreBonus = 0;
            lastJudgementScoreLimited = false;
            hasJudgedInputTiming = false;
            lastInputTimingDeltaSeconds = 0f;
            lastJudgedInputChannel = GreedLastRunChannel.None;
            successCount = 0;
            perfectSuccessCount = 0;
            clutchSuccessCount = 0;
            offbeatSuccessCount = 0;
            goodCount = 0;
            missCount = 0;
            noInputMissCount = 0;
            for (int i = 0; i < avoidanceExactCounts.Length; i += 1)
            {
                avoidanceExactCounts[i] = 0;
                avoidanceGoodCounts[i] = 0;
                avoidanceMissCounts[i] = 0;
            }

            for (int i = 0; i < chapterMissCounts.Length; i += 1)
            {
                chapterExactCounts[i] = 0;
                chapterGoodCounts[i] = 0;
                chapterMissCounts[i] = 0;
            }

            escapeExactCount = 0;
            escapeGoodCount = 0;
            escapeMissCount = 0;
            lastCompletedSegment = string.Empty;
            lastCompletedSegmentExactCount = 0;
            lastCompletedSegmentGoodCount = 0;
            lastCompletedSegmentMissCount = 0;
            timingSampleCount = 0;
            earlyInputCount = 0;
            lateInputCount = 0;
            timingRecommendationApplied = false;
            appliedTimingRecommendationMilliseconds = 0;
            timingSignedErrorTotal = 0f;
            timingAbsErrorTotal = 0f;
            timingRecommendedOffsetTotal = 0f;
            focusStateActivationCount = 0;
            focusStateSuccessCount = 0;
            focusStateActiveSeconds = 0f;
            runTimeAdvancedAt = 0f;
            infiniteSectionsCleared = 0;
            infiniteThreatTestOffset = 0;
            maxInfiniteThreatLevel = 1;
            patternBundleBreakCount = 0;
            currentBundleExactCount = 0;
            currentBundleGoodCount = 0;
            currentBundleMissCount = 0;
            infiniteThreatBreatherPatternPending = false;
            recoveryBreatherPatternPending = false;
            lastSelectedPatternId = string.Empty;
            previousSelectedPatternId = string.Empty;
            ClearRecentPatternHistory();
        }

        private void ResetRunVitals()
        {
            int maxHealth = BuildGrowthMaxHealth();
            health = maxHealth;
            maxHealthThisRun = maxHealth;
            focus = BuildGrowthStartFocus();
            focusStateRemainingSeconds = 0f;
            if (focus >= MaxFocus)
            {
                focus = 0;
                focusStateRemainingSeconds = FocusStateDurationSeconds;
            }
        }

        private int BuildGrowthMaxHealth()
        {
            return MaxHealth + growthSurvivalLevel;
        }

        private int BuildBonusHealthCap()
        {
            return BuildGrowthMaxHealth() + 2;
        }

        private int BuildGrowthStartFocus()
        {
            return Mathf.Clamp(growthFocusLevel, 0, MaxFocus);
        }

        private float BuildGrowthSuccessWindowBonus()
        {
            return growthTimingLevel * 0.005f;
        }

        private float BuildGrowthGoodWindowBonus()
        {
            return growthTimingLevel * 0.012f;
        }

        private string BuildGrowthAppliedText()
        {
            if (growthSurvivalLevel <= 0 && growthFocusLevel <= 0 && growthTimingLevel <= 0)
            {
                return string.Empty;
            }

            string text = "\n성장 보정:";
            if (growthSurvivalLevel > 0)
            {
                text += " 체력 +" + growthSurvivalLevel;
            }

            if (growthFocusLevel > 0)
            {
                text += " 집중 +" + growthFocusLevel;
            }

            if (growthTimingLevel > 0)
            {
                text += " 판정 +" + (growthTimingLevel * 5) + "/" + (growthTimingLevel * 12) + "ms";
            }

            return text;
        }

        public void Enter()
        {
            Enter(string.Empty);
        }

        public void Enter(string openingContext)
        {
            Enter(openingContext, false);
        }

        public void Enter(string openingContext, bool refillRun)
        {
            ResetNormalRunState(refillRun);
            reviewTargetText = ExtractReviewTargetText(openingContext);
            OpenChoice(GreedLastChoiceKind.StartGift);
            judgementText = (string.IsNullOrEmpty(openingContext) ? string.Empty : openingContext + "\n")
                + "시작 기프트를 선택하세요."
                + BuildGrowthAppliedText();
            ignoreReason = string.Empty;
            missReason = string.Empty;
            lastResult = GreedLastJudgementResult.None;
            Publish();
        }

        public void EnterPractice()
        {
            EnterPractice(string.Empty);
        }

        public void EnterPractice(string openingContext)
        {
            StartPractice(Time.realtimeSinceStartup, openingContext);
        }

        public void RestartPractice(float now)
        {
            StartPractice(now, lastPracticeOpeningContext);
        }

        private void StartPractice(float now, string openingContext)
        {
            lastPracticeOpeningContext = openingContext ?? string.Empty;
            ResetNormalRunState(refillRun: false);
            reviewTargetText = ExtractReviewTargetText(lastPracticeOpeningContext);
            practiceMode = true;
            chapterProgress = ResolvePracticeOpeningStep(lastPracticeOpeningContext);
            patternIndex = chapterProgress;
            startGift = "기초 연습";
            primaryGift = string.Empty;
            relic = string.Empty;
            phase = GreedLastRunPhase.RunPlaying;
            choiceKind = GreedLastChoiceKind.None;
            string contextPrefix = string.IsNullOrEmpty(lastPracticeOpeningContext) ? string.Empty : lastPracticeOpeningContext + "\n";
            PrepareStartCountdown(
                now,
                contextPrefix
                + "기초연습 준비\n좌·중·우는 이동이 아니라 위험에 대응하는 채널입니다."
                + "\n" + BuildPracticeOpeningStepGuide(chapterProgress));
            ignoreReason = string.Empty;
            missReason = string.Empty;
            lastResult = GreedLastJudgementResult.None;
            Publish(now);
        }

        private static int ResolvePracticeOpeningStep(string openingContext)
        {
            if (string.IsNullOrWhiteSpace(openingContext))
            {
                return 0;
            }

            if (openingContext.Contains("비틀기 단계", StringComparison.Ordinal)
                || openingContext.Contains("측면→↔비틀기", StringComparison.Ordinal)
                || openingContext.Contains("엇박 비틀기", StringComparison.Ordinal))
            {
                return 2;
            }

            if (openingContext.Contains("숙이기 단계", StringComparison.Ordinal)
                || openingContext.Contains("상단→↓숙이기", StringComparison.Ordinal)
                || openingContext.Contains("클러치 숙이기", StringComparison.Ordinal))
            {
                return 1;
            }

            return 0;
        }

        private static string BuildPracticeOpeningStepGuide(int chapterProgress)
        {
            switch (Mathf.Clamp(chapterProgress, 0, ChapterTargetPatterns - 1))
            {
                case 1:
                    return "복기 시작: 클러치 상단→↓숙이기부터 남은 단계를 확인합니다. 정확→반격.";
                case 2:
                    return "복기 시작: 엇박 측면→↔비틀기 단계를 바로 확인합니다. 정확→연쇄.";
                default:
                    return "정박 바닥→↑점프 정확→집중 · 클러치 상단→↓숙이기 정확→반격 · 엇박 측면→↔비틀기 정확→연쇄를 차례로 연습합니다.";
            }
        }

        private void ResetNormalRunState(bool refillRun)
        {
            score = 0;
            combo = 0;
            ResetRunVitals();
            ResetRunStats();
            RegisterInitialFocusState();
            currentPattern = GreedLastPatternModel.Empty;
            lastJudgedPattern = GreedLastPatternModel.Empty;
            activePattern = false;
            devNextPatternOverride = GreedLastPatternModel.Empty;
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
            refillRunMode = refillRun;
            practiceMode = false;
            activeInfiniteLoadoutName = string.Empty;
            devAvoidanceCycleIndex = 0;
            activeInfiniteLoadoutProfile = string.Empty;
            activeInfiniteLoadoutDisplayProfile = string.Empty;
            reviewTargetText = string.Empty;
            ClearChoices();
        }

        public void EnterInfiniteRun()
        {
            EnterInfiniteRun(GreedLastRunRecord.Empty);
        }

        public void EnterInfiniteRun(GreedLastRunRecord loadoutRecord)
        {
            EnterInfiniteRun(loadoutRecord, string.Empty);
        }

        public void EnterInfiniteRun(GreedLastRunRecord loadoutRecord, string openingContext)
        {
            score = 0;
            combo = 0;
            ResetRunVitals();
            ResetRunStats();
            RegisterInitialFocusState();
            distance = 0f;
            currentPattern = GreedLastPatternModel.Empty;
            lastJudgedPattern = GreedLastPatternModel.Empty;
            activePattern = false;
            devNextPatternOverride = GreedLastPatternModel.Empty;
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
            refillRunMode = false;
            practiceMode = false;
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
            activeInfiniteFocusGiftStacks = BuildStoredGiftStackCount(loadoutRecord, "집중 축");
            activeInfiniteSurvivalGiftStacks = BuildStoredGiftStackCount(loadoutRecord, "생존 축");
            activeInfiniteChainGiftStacks = BuildStoredGiftStackCount(loadoutRecord, "연쇄 축");
            activeInfiniteLoadoutProfile = BuildInfiniteLoadoutProfile(loadoutRecord);
            activeInfiniteLoadoutDisplayProfile = BuildInfiniteLoadoutDisplayProfile(loadoutRecord);
            reviewTargetText = ExtractReviewTargetText(openingContext);
            string bonusText = ApplyInfiniteLoadoutBonus(loadoutRecord);
            string contextPrefix = string.IsNullOrEmpty(openingContext) ? string.Empty : openingContext + "\n";
            PrepareStartCountdown(now, contextPrefix + "무한모드 시작 준비 - " + loadoutName
                + BuildChoiceProfileResultLine()
                + (string.IsNullOrEmpty(relic) ? string.Empty : "\n시작 유물: " + relic + " / 이번 무한 런")
                + "\n" + bonusText
                + "\n" + BuildInfiniteLoadoutStartStatePreview(
                    loadoutRecord,
                    growthSurvivalLevel,
                    growthFocusLevel,
                    growthTimingLevel)
                + BuildGrowthAppliedText()
                + "\n카운트 뒤 첫 함정이 내려옵니다.");
            Publish();
        }

        public void Exit()
        {
            currentPattern = GreedLastPatternModel.Empty;
            lastJudgedPattern = GreedLastPatternModel.Empty;
            activePattern = false;
            inputConsumed = false;
            autoFlow = false;
            paused = false;
            resumeCountdownActive = false;
            startCountdownActive = false;
            autoFlowBeforePause = false;
            infiniteMode = false;
            refillRunMode = false;
            activeInfiniteLoadoutName = string.Empty;
            activeInfiniteLoadoutProfile = string.Empty;
            activeInfiniteLoadoutDisplayProfile = string.Empty;
            reviewTargetText = string.Empty;
            phase = GreedLastRunPhase.None;
            Publish();
        }

        private static string ExtractReviewTargetText(string context)
        {
            if (string.IsNullOrWhiteSpace(context))
            {
                return string.Empty;
            }

            string[] lines = context.Replace("\r", string.Empty).Split('\n');
            string reviewTarget = string.Empty;
            string trainingHint = string.Empty;
            for (int i = 0; i < lines.Length; i += 1)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("재도전 목표:", StringComparison.Ordinal))
                {
                    return line.Substring("재도전 목표:".Length).Trim();
                }

                if (line.StartsWith("목표:", StringComparison.Ordinal))
                {
                    return line.Substring("목표:".Length).Trim();
                }

                if (line.StartsWith("복기 목표:", StringComparison.Ordinal))
                {
                    reviewTarget = ExtractTargetFromReviewResultLine(line.Substring("복기 목표:".Length).Trim());
                    continue;
                }

                if (line.StartsWith("훈련 힌트:", StringComparison.Ordinal))
                {
                    trainingHint = line.Substring("훈련 힌트:".Length).Trim();
                    continue;
                }

                if (line.StartsWith("힌트:", StringComparison.Ordinal))
                {
                    trainingHint = line.Substring("힌트:".Length).Trim();
                }
            }

            return !string.IsNullOrEmpty(reviewTarget) ? reviewTarget : trainingHint;
        }

        private static string ExtractTargetFromReviewResultLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            string[] markers =
            {
                " / 실패 ",
                " / 달성 ",
                " / 진행 ",
                " / 기록됨",
            };
            for (int i = 0; i < markers.Length; i += 1)
            {
                int markerIndex = line.IndexOf(markers[i], StringComparison.Ordinal);
                if (markerIndex > 0)
                {
                    return line.Substring(0, markerIndex).Trim();
                }
            }

            return line.Trim();
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
            string summary = BuildChoiceHistorySummary("테스트 루트");

            return new GreedLastRunRecord(
                true,
                score,
                health,
                combo,
                focus,
                maxCombo,
                successCount,
                goodCount,
                missCount,
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

            string summary = BuildChoiceHistorySummary("선택 없음");

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

        private string BuildChoiceHistorySummary(string fallbackSummary)
        {
            string summary = choiceHistoryCount > 0
                ? string.Join(" / ", choiceHistory, 0, choiceHistoryCount)
                : fallbackSummary;
            string profile = BuildActiveChoiceProfile();
            return IsRecordableChoiceProfile(profile)
                ? summary + "\n조합 성향: " + profile
                : summary;
        }

        private static bool IsRecordableChoiceProfile(string profile)
        {
            return !string.IsNullOrEmpty(profile)
                && profile != "루트 미정"
                && profile != "저장 조합";
        }

        internal static bool HasRecordableInfiniteProgress(float traveledDistance, int successes, int goods, int misses)
        {
            return traveledDistance > 0.01f || successes > 0 || goods > 0 || misses > 0;
        }

        public GreedLastInfiniteRunRecord CreateInfiniteRunRecord()
        {
            // Loadout score is granted before play; movement or judgements establish an attempt.
            bool hasRunStarted = infiniteMode
                && HasRecordableInfiniteProgress(distance, successCount, goodCount, missCount);
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
                BuildCurrentInfiniteLoadoutProfile(),
                successCount,
                goodCount,
                missCount,
                maxCombo,
                missReason,
                BuildTimingProfileText());
        }

        public bool PauseForInterruptedFrame(float now)
        {
            if (float.IsNaN(now) || float.IsInfinity(now)
                || now - lastTickAt <= MaxInteractiveFrameGapSeconds
                || stopped || phase == GreedLastRunPhase.None || phase == GreedLastRunPhase.ChoiceSelecting)
                return false;

            if (paused)
            {
                if (!resumeCountdownActive) return false;
                resumeCountdownActive = false;
                judgementText = "화면 갱신이 멈춰 재개 카운트를 정지했습니다.\n재개하면 2-1-GO부터 다시 이어집니다.";
                lastTickAt = now;
                Publish(now);
                return true;
            }

            if (!startCountdownActive && !CanPauseNow()) return false;
            // Freeze at the last presented frame, not after charging the unseen wall-clock gap.
            PauseAt(lastTickAt, "화면 갱신이 오래 멈춰 일시정지했습니다.\n재개하면 2-1-GO 뒤 같은 구간을 이어갑니다.");
            lastTickAt = now;
            return true;
        }

        public void Tick(float now)
        {
            AdvanceRunTime(now);
            lastTickAt = now;

            if (startCountdownActive && !paused)
            {
                if (now >= startCountdownEndsAt)
                {
                    FinishStartCountdown(now);
                    return;
                }

                Publish(now, reuseTimingProfile: true);
                return;
            }

            if (paused)
            {
                if (resumeCountdownActive && now >= resumeCountdownEndsAt)
                {
                    ResumeFromPause(now);
                    return;
                }

                if (resumeCountdownActive) Publish(now, reuseTimingProfile: true);
                return;
            }

            if (stopped || phase == GreedLastRunPhase.ChoiceSelecting || phase == GreedLastRunPhase.None)
            {
                // Commands publish immediately; idle menus only need the last hit's transient expiry.
                if (staggerUntil > 0f)
                {
                    if (now >= staggerUntil) staggerUntil = 0f;
                    Publish(now, reuseTimingProfile: true);
                }
                return;
            }

            if (autoFlow
                && CanAutoStartPattern
                && !activePattern
                && !stopped
                && now >= nextPatternAt)
            {
                StartPattern(now);
                return;
            }

            if (!activePattern || stopped)
            {
                Publish(now, reuseTimingProfile: true);
                return;
            }

            float elapsed = now - patternStartedAt;
            // Calibration can move a valid late Good beyond the authored impact time.
            float inputDeadline = Mathf.Max(currentPattern.HitSeconds,
                currentPattern.CoreSeconds + currentPattern.GoodWindowSeconds - inputTimingOffsetSeconds);
            if (!inputConsumed && elapsed > inputDeadline)
            {
                Resolve(new GreedLastJudgementOutcome(
                    GreedLastJudgementResult.Miss,
                    currentPattern.TimingType,
                    "실수 - 입력 없음",
                    string.Empty,
                    "no_input"), now);
                return;
            }

            Publish(now, reuseTimingProfile: true);
        }

        private void AdvanceRunTime(float now)
        {
            // Apply gameplay time once, before an input changes rewards or ends the run.
            float deltaTime = Mathf.Max(0f, now - Mathf.Max(lastTickAt, runTimeAdvancedAt));
            runTimeAdvancedAt = now;
            if (paused || startCountdownActive || stopped || !CanAutoStartPattern)
                return;
            distance += deltaTime * RunSpeedMetersPerSecond;
            if (focusStateRemainingSeconds <= 0f) return;
            float previous = focusStateRemainingSeconds;
            focusStateRemainingSeconds = Mathf.Max(0f, previous - deltaTime);
            focusStateActiveSeconds += Mathf.Max(0f, previous - focusStateRemainingSeconds);
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
                    RestartAutoFlow(now, "이전 시도 기록 저장 완료");
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

        public void RestartStoppedNormalRun(float now, string restartPrefix)
        {
            if (!IsStoppedNormalRun)
            {
                ToggleAutoFlow(now);
                return;
            }

            RestartAutoFlow(now, string.IsNullOrEmpty(restartPrefix) ? "이전 시도 기록 저장 완료" : restartPrefix);
        }

        public void TogglePause(float now)
        {
            if (paused)
            {
                BeginResumeCountdown(now);
                return;
            }

            // Visible pause commands share the live frame boundary; background loss freezes the last presentation instead.
            if ((startCountdownActive || CanPauseNow()) && now > lastTickAt)
            {
                Tick(now);
                if (stopped || phase == GreedLastRunPhase.ChoiceSelecting) return;
            }

            if (startCountdownActive)
            {
                PauseAt(now, "시작 카운트 일시정지\n재개를 누르면 2-1-GO 뒤 이어집니다.");
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

            PauseAt(now, "일시정지 - 재개를 누르면 2-1-GO 뒤 앞 박자 여유를 둡니다.");
        }

        public void PauseForAppBackground(float now)
        {
            if (paused)
            {
                if (resumeCountdownActive)
                {
                    resumeCountdownActive = false;
                    judgementText = startCountdownActive
                        ? "앱 전환으로 재개 카운트가 멈췄습니다.\n재개를 누르면 시작 카운트부터 다시 이어집니다."
                        : "앱 전환으로 재개 카운트가 멈췄습니다.\n재개를 누르면 2-1-GO 뒤 이어집니다.";
                    Publish(now);
                }

                return;
            }

            if (!paused && startCountdownActive)
            {
                PauseAt(now, "앱 전환으로 시작 카운트가 멈췄습니다.\n재개를 누르면 2-1-GO 뒤 이어집니다.");
                return;
            }

            if (!paused && CanPauseNow())
            {
                PauseAt(now, "앱 전환으로 일시정지되었습니다.\n재개를 누르면 2-1-GO 뒤 앞 박자 여유를 둡니다.");
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
                Ignore("stopped", BuildStoppedInputMessage(), now);
                return;
            }

            if (paused)
            {
                Ignore("paused", "일시정지 중입니다. 재개 후 입력하세요.", now);
                return;
            }

            if (startCountdownActive)
            {
                Ignore("start_countdown", "시작 카운트 중입니다.", now);
                return;
            }

            if (phase == GreedLastRunPhase.ChoiceSelecting)
            {
                SelectChoice(channel, now);
                return;
            }

            if (now < staggerUntil)
            {
                Ignore("locked_by_stagger", BuildStaggerInputMessage(now), now);
                return;
            }

            if (!activePattern)
            {
                Ignore("no_active_pattern", "활성 함정이 없습니다.", now);
                return;
            }

            if (inputConsumed)
            {
                Ignore("duplicate_input", "같은 함정에는 최초 입력만 반영됩니다.", now);
                return;
            }

            // Resume can rewind a trap before its start; padding is not a judgement window.
            if (now < patternStartedAt)
            {
                Ignore("resume_buffer", "재개 준비 중입니다. 함정이 내려오면 입력하세요.", now);
                return;
            }

            AdvanceRunTime(now);
            inputConsumed = true;
            float elapsed = now - patternStartedAt;
            float judgedElapsed = elapsed + inputTimingOffsetSeconds;
            float signedDelta = judgedElapsed - currentPattern.CoreSeconds;

            if (channel != currentPattern.Channel)
            {
                Resolve(new GreedLastJudgementOutcome(
                    GreedLastJudgementResult.Miss,
                    currentPattern.TimingType,
                    "실수 - 채널 오류\n" + FormatChannel(channel) + " 입력 / 정답 " + FormatChannel(currentPattern.Channel)
                        + "\n" + FormatTimingDelta(signedDelta),
                    string.Empty,
                    "wrong_channel"), now, signedDelta, channel);
                return;
            }

            RecordTimingSample(signedDelta);
            float delta = Mathf.Abs(signedDelta);
            if (delta <= currentPattern.SuccessWindowSeconds)
            {
                bool centerHit = delta <= BuildCenterHitWindow(currentPattern);
                Resolve(new GreedLastJudgementOutcome(
                    GreedLastJudgementResult.Success,
                    currentPattern.TimingType,
                    "정확 / " + FormatTimingType(currentPattern.TimingType) + "\n" + FormatTimingDelta(signedDelta),
                    string.Empty,
                    string.Empty,
                    centerHit), now, signedDelta, channel);
                return;
            }

            if (delta <= currentPattern.GoodWindowSeconds)
            {
                Resolve(new GreedLastJudgementOutcome(
                    GreedLastJudgementResult.Good,
                    currentPattern.TimingType,
                    "보정 - 생존 완충\n" + FormatTimingDelta(signedDelta),
                    string.Empty,
                    string.Empty), now, signedDelta, channel);
                return;
            }

            string reason = judgedElapsed < currentPattern.CoreSeconds ? "too_early" : "too_late";
            Resolve(new GreedLastJudgementOutcome(
                GreedLastJudgementResult.Miss,
                currentPattern.TimingType,
                (judgedElapsed < currentPattern.CoreSeconds ? "실수 - 너무 빠름" : "실수 - 너무 늦음")
                    + "\n" + FormatTimingDelta(signedDelta),
                string.Empty,
                reason), now, signedDelta, channel);
        }

        public void AdjustInputTimingOffset(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            inputTimingOffsetSeconds = Mathf.Clamp(
                inputTimingOffsetSeconds + deltaSeconds,
                MinInputTimingOffsetSeconds,
                MaxInputTimingOffsetSeconds);
            timingRecommendationApplied = false;
            appliedTimingRecommendationMilliseconds = 0;
            SaveInputTimingOffset();
            judgementText = "입력 보정 " + FormatOffset(inputTimingOffsetSeconds);
            Publish();
        }

        public void ResetInputTimingOffset()
        {
            inputTimingOffsetSeconds = 0f;
            timingRecommendationApplied = false;
            appliedTimingRecommendationMilliseconds = 0;
            SaveInputTimingOffset();
            judgementText = "입력 보정 0ms";
            Publish();
        }

        public void ApplyRecommendedInputTimingOffset()
        {
            if (!HasTimingRecommendation)
            {
                inputTimingOffsetSeconds = 0f;
                timingRecommendationApplied = false;
                appliedTimingRecommendationMilliseconds = 0;
                SaveInputTimingOffset();
                judgementText = "보정 추천 샘플 부족 - 입력 보정 0ms";
                Publish();
                return;
            }

            int recommendationMilliseconds = TimingRecommendationMilliseconds;
            inputTimingOffsetSeconds = Mathf.Clamp(
                recommendationMilliseconds / 1000f,
                MinInputTimingOffsetSeconds,
                MaxInputTimingOffsetSeconds);
            timingRecommendationApplied = true;
            appliedTimingRecommendationMilliseconds = recommendationMilliseconds;
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

            bool wasActive = IsFocusStateActive;
            focus = 0;
            focusStateRemainingSeconds = FocusStateDurationSeconds;
            if (!wasActive)
            {
                focusStateActivationCount += 1;
            }

            judgementText = "테스트 집중 상태 발동 - 정확 성공의 점수와 연쇄 유지 효과를 확인합니다.";
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
            infiniteThreatBreatherPatternPending = true;
            judgementText = "테스트 위협 상승 - " + BuildInfiniteThreatChangeSummary()
                + "\n첫 함정은 유예 패턴으로 폭·보상 확인";
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
            infiniteThreatBreatherPatternPending = true;
            judgementText = "테스트 위협 하강 - " + BuildInfiniteThreatChangeSummary()
                + "\n첫 함정은 유예 패턴으로 폭·보상 확인";
            Publish();
        }

        public void ForceStopInfiniteRunForTest()
        {
            if (!infiniteMode)
            {
                judgementText = "무한모드에서만 정산 종료를 사용할 수 있습니다.";
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
            judgementText = "무한모드 정산 종료\n기록을 확인한 뒤 재시작하거나 로비로 돌아갈 수 있습니다.";
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
            judgementText = "시작 - 함정이 내려옵니다.";
            Publish(now);
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
            Publish(now);
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
            Publish(now);
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
            timingRecommendedOffsetTotal += inputTimingOffsetSeconds - signedDeltaSeconds;

            if (signedDeltaSeconds < -0.02f)
            {
                earlyInputCount += 1;
            }
            else if (signedDeltaSeconds > 0.02f)
            {
                lateInputCount += 1;
            }

            if (timingRecommendationApplied && HasTimingRecommendation)
            {
                int currentMilliseconds = Mathf.RoundToInt(inputTimingOffsetSeconds * 1000f);
                if (Mathf.Abs(currentMilliseconds - TimingRecommendationMilliseconds) >= 25)
                {
                    timingRecommendationApplied = false;
                    appliedTimingRecommendationMilliseconds = 0;
                }
            }
        }

        private void Resolve(GreedLastJudgementOutcome outcome, float now, float? inputTimingDeltaSeconds = null,
            GreedLastRunChannel inputChannel = GreedLastRunChannel.None)
        {
            int scoreBeforeResolve = score;
            int healthBeforeResolve = health;
            lastJudgementProfileScoreBonus = 0;
            lastJudgementThreatScoreBonus = 0;
            lastJudgementScoreLimited = false;
            hasJudgedInputTiming = inputTimingDeltaSeconds.HasValue;
            lastInputTimingDeltaSeconds = inputTimingDeltaSeconds.GetValueOrDefault();
            lastJudgedInputChannel = inputTimingDeltaSeconds.HasValue ? inputChannel : GreedLastRunChannel.None;
            lastJudgedPattern = currentPattern;
            activePattern = false;
            ignoreReason = outcome.IgnoreReason;
            missReason = outcome.MissReason;
            judgementText = outcome.Message;
            lastResult = outcome.Result;
            lastCenterHit = outcome.Result == GreedLastJudgementResult.Success && outcome.CenterHit;
            int comboBeforeResolve = combo;
            bool focusStateWasActive = IsFocusStateActive;
            float focusSecondsBeforeResolve = focusStateRemainingSeconds;
            lastJudgementStartedInFocusState = focusStateWasActive;
            bool recoveryPattern = IsRecoveryPattern(currentPattern);
            bool routeBundleWasIntact = patternBundleBreakCount <= 0;
            maxInfiniteThreatLevel = Mathf.Max(maxInfiniteThreatLevel, GetInfiniteThreatLevel());
            RecordCurrentBundleJudgement(outcome.Result);
            RecordCurrentSegmentJudgement(outcome.Result);
            RecordAvoidanceJudgement(currentPattern.AvoidanceType, outcome.Result);

            switch (outcome.Result)
            {
                case GreedLastJudgementResult.Success:
                    successCount += 1;
                    switch (currentPattern.TimingType)
                    {
                        case GreedLastTimingType.Perfect:
                            perfectSuccessCount += 1;
                            break;
                        case GreedLastTimingType.Clutch:
                            clutchSuccessCount += 1;
                            break;
                        case GreedLastTimingType.Offbeat:
                            offbeatSuccessCount += 1;
                            break;
                    }

                    if (focusStateWasActive)
                    {
                        focusStateSuccessCount += 1;
                    }

                    int chainGiftStacks = GetActiveGiftStackCount("연쇄 축");
                    int comboGain = BuildSuccessComboGain(outcome.CenterHit);

                    combo += comboGain;
                    maxCombo = Mathf.Max(maxCombo, combo);
                    int successScore = 100 + combo * 10;
                    int focusGiftStacks = GetActiveGiftStackCount("집중 축");
                    int focusGain = 1 + Mathf.Min(2, focusGiftStacks);
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

                    int threatPrecisionBonus = BuildThreatPrecisionBonus(currentPattern.TimingType);
                    int variationPrecisionBonus = BuildPatternVariationPrecisionBonus(currentPattern);
                    int profileSuccessBonus = BuildChoiceProfileSuccessScoreBonus(out string profileSuccessText);
                    lastJudgementProfileScoreBonus = profileSuccessBonus;
                    lastJudgementThreatScoreBonus = threatPrecisionBonus;
                    int centerHitBonus = 0;
                    int centerHitFocusGain = 0;
                    int recoveryPrecisionBonus = 0;
                    int recoveryFocusGain = 0;
                    int perfectFocusChargeGain = 0;
                    int clutchCounterBonus = 0;
                    int variationRouteFocusGain = 0;
                    int pressureRouteCounterBonus = 0;
                    if (outcome.CenterHit)
                    {
                        precisionStreak += 1;
                        maxPrecisionStreak = Mathf.Max(maxPrecisionStreak, precisionStreak);
                        centerHitBonus = BuildCenterHitScoreBonus();
                        if (precisionStreak % PrecisionStreakFocusStep == 0)
                        {
                            centerHitFocusGain = 1;
                        }

                        if (recoveryPattern)
                        {
                            recoveryPrecisionCount += 1;
                            recoveryPrecisionBonus = RecoveryPrecisionScoreBonus;
                            recoveryFocusGain = RecoveryPrecisionFocusBonus;
                        }

                        if (currentPattern.TimingType == GreedLastTimingType.Clutch)
                        {
                            clutchCounterCount += 1;
                            clutchCounterBonus = ClutchCounterScoreBonus;
                        }
                        else if (currentPattern.TimingType == GreedLastTimingType.Perfect)
                        {
                            perfectFocusChargeCount += 1;
                            perfectFocusChargeGain = PerfectFocusChargeBonus;
                        }
                        else if (currentPattern.TimingType == GreedLastTimingType.Offbeat)
                        {
                            offbeatFlowCount += 1;
                        }

                        if (currentPattern.TimingType == GreedLastTimingType.Offbeat
                            && HasActiveRoute("변주 루트"))
                        {
                            variationRouteFlowCount += 1;
                            variationRouteFocusGain = VariationRouteOffbeatFocusBonus;
                        }

                        if (currentPattern.TimingType == GreedLastTimingType.Clutch
                            && HasActiveRoute("압박 루트"))
                        {
                            pressureRouteCounterCount += 1;
                            pressureRouteCounterBonus = PressureRouteClutchScoreBonus;
                        }
                    }
                    else
                    {
                        BreakPrecisionStreak();
                    }

                    int totalSuccessScore = successScore
                        + threatPrecisionBonus
                        + variationPrecisionBonus
                        + profileSuccessBonus
                        + centerHitBonus
                        + recoveryPrecisionBonus
                        + clutchCounterBonus
                        + pressureRouteCounterBonus;
                    int focusStateScoreBonus = BuildFocusStateScoreBonus(totalSuccessScore, focusStateWasActive);
                    totalSuccessScore += focusStateScoreBonus;
                    totalSuccessScore = AddScore(totalSuccessScore, trackJudgementLimit: true);
                    float focusSecondsBeforeGain = focusStateRemainingSeconds;
                    GainFocus(
                        focusGain + centerHitFocusGain + recoveryFocusGain + perfectFocusChargeGain + variationRouteFocusGain,
                        extendActiveState: true,
                        out bool focusStateActivated,
                        out bool focusStateExtended);
                    string successEffect = "조합 효과: +" + successScore + "점 / 콤보 +" + comboGain;
                    if (!focusStateWasActive)
                    {
                        successEffect += " / 집중 +" + (focusGain + centerHitFocusGain + recoveryFocusGain
                            + perfectFocusChargeGain + variationRouteFocusGain);
                    }

                    AppendChoiceEffect(successEffect);
                    if (focusGiftStacks > 0)
                    {
                        AppendChoiceEffect("집중 축 x" + focusGiftStacks + (focusStateWasActive
                            ? " - 집중 상태 유지" : " - 정확 집중 +" + focusGain));
                    }

                    if (chainGiftStacks > 0)
                    {
                        AppendChoiceEffect("연쇄 축 x" + chainGiftStacks + " - 정확 콤보 +" + comboGain);
                    }

                    if (HasChoice(relic, "황금 박동"))
                    {
                        AppendChoiceEffect("황금 박동 - 정확 고점 +40점");
                    }
                    else if (chainGiftStacks > 0 && HasChoice(relic, "파라오의 각인"))
                    {
                        AppendChoiceEffect("파라오의 각인 - 연쇄 증폭 +1");
                    }

                    if (profileSuccessBonus > 0)
                    {
                        AppendChoiceEffect(profileSuccessText + " +" + profileSuccessBonus + "점");
                    }

                    if (threatPrecisionBonus > 0)
                    {
                        AppendChoiceEffect("무한 위협 정확 보너스 +" + threatPrecisionBonus + "점");
                    }

                    if (variationPrecisionBonus > 0)
                    {
                        AppendChoiceEffect("함정 변주 정확 보너스 +" + variationPrecisionBonus + "점");
                    }

                    if (centerHitBonus > 0)
                    {
                        string centerHitText = "정박 연속 " + precisionStreak + " / 보너스 +" + centerHitBonus + "점";
                        AppendChoiceEffect(centerHitText);
                    }

                    if (recoveryPrecisionBonus > 0)
                    {
                        string recoveryText = "복구 정박 - 흐름 재점화 / 보너스 +" + recoveryPrecisionBonus
                            + "점 / 콤보 +" + RecoveryPrecisionComboBonus;
                        if (!focusStateWasActive)
                        {
                            recoveryText += " / 집중 +" + recoveryFocusGain;
                        }

                        AppendChoiceEffect(recoveryText);
                    }
                    else if (recoveryPattern)
                    {
                        AppendChoiceEffect("복구 성공 - 흐름 재진입");
                    }

                    if (perfectFocusChargeGain > 0)
                    {
                        AppendChoiceEffect("정박 중앙 - 집중 충전 / " + (focusStateWasActive
                            ? "집중 상태 유지" : "집중 +" + perfectFocusChargeGain));
                    }

                    if (clutchCounterBonus > 0)
                    {
                        AppendChoiceEffect("압박 정박 - 반격 / 보너스 +" + clutchCounterBonus + "점");
                    }
                    else if (outcome.CenterHit && currentPattern.TimingType == GreedLastTimingType.Offbeat)
                    {
                        AppendChoiceEffect("엇박 정박 - 연쇄 가속 / 콤보 +" + OffbeatFlowComboBonus);
                    }

                    if (variationRouteFocusGain > 0)
                    {
                        AppendChoiceEffect("변주 루트 - 엇박 공명 / " + (focusStateWasActive
                            ? "집중 상태 유지" : "집중 +" + variationRouteFocusGain));
                    }

                    if (pressureRouteCounterBonus > 0)
                    {
                        AppendChoiceEffect("압박 루트 - 클러치 증폭 / 보너스 +" + pressureRouteCounterBonus + "점");
                    }

                    if (focusStateScoreBonus > 0)
                    {
                        AppendChoiceEffect("집중 상태 점수 배율 x" + FocusStateScoreMultiplier.ToString("0.00") + " / +" + focusStateScoreBonus + "점");
                    }

                    if (focusStateActivated)
                    {
                        AppendChoiceEffect("집중 상태 발동 " + FocusStateDurationSeconds.ToString("0.0") + "초");
                    }
                    else if (focusStateExtended)
                    {
                        AppendChoiceEffect("집중 상태 연장 +" + (focusStateRemainingSeconds - focusSecondsBeforeGain).ToString("0.00") + "초");
                    }

                    AppendRunStateResult("결과: 점수 +" + totalSuccessScore + " / 콤보 " + combo + " / " + BuildFocusStateResultText());
                    int rhythmChainLevel = GetRhythmChainLevel(combo);
                    if (rhythmChainLevel > 0)
                    {
                        int chainBonus = 30 * rhythmChainLevel;
                        if (infiniteMode)
                        {
                            chainBonus += 10 * GetInfiniteThreatLevel();
                        }

                        int awardedChainBonus = AddScore(chainBonus, trackJudgementLimit: true);
                        AppendChoiceEffect("연쇄 박자 x" + rhythmChainLevel + " / 보너스 +" + awardedChainBonus + "점");
                    }
                    break;
                case GreedLastJudgementResult.Good:
                    patternBundleBreakCount += 1;
                    goodCount += 1;
                    int goodScore = 25;
                    int profileGoodBonus = BuildChoiceProfileGoodScoreBonus(out string profileGoodText);
                    lastJudgementProfileScoreBonus = profileGoodBonus;
                    int goodFocusGain = 0;
                    int goodHealthGain = 0;
                    if (HasChoice(startGift, "박자 감각") || HasChoice(relic, "사막의 잔상"))
                    {
                        goodFocusGain = 1;
                    }

                    int survivalGiftStacks = GetActiveGiftStackCount("생존 축");
                    CalculateGoodRecovery(out int survivalGiftHealthGain, out int stableRouteHealthGain);
                    goodHealthGain = survivalGiftHealthGain + stableRouteHealthGain;
                    if (goodHealthGain > 0)
                    {
                        health += goodHealthGain;
                        maxHealthThisRun = Mathf.Max(maxHealthThisRun, health);
                    }
                    stableRouteRecoveryCount += stableRouteHealthGain > 0 ? 1 : 0;

                    if (HasChoice(startGift, "고점 본능"))
                    {
                        goodScore += 15;
                    }

                    goodScore += profileGoodBonus;
                    int goodFocusStateScoreBonus = BuildFocusStateScoreBonus(goodScore, focusStateWasActive);
                    goodScore += goodFocusStateScoreBonus;
                    goodScore = AddScore(goodScore, trackJudgementLimit: true);
                    bool focusChainSoftened = focusStateWasActive && comboBeforeResolve > 0;
                    combo = focusChainSoftened ? Mathf.Max(0, comboBeforeResolve - 1) : 0;
                    BreakPrecisionStreak();
                    GainFocus(
                        goodFocusGain,
                        extendActiveState: false,
                        out bool goodFocusStateActivated,
                        out _);
                    string goodEffectText = "조합 효과: +" + goodScore + "점";
                    if (goodFocusGain > 0 && !focusStateWasActive)
                    {
                        goodEffectText += " / 집중 +" + goodFocusGain;
                    }

                    if (goodHealthGain > 0)
                    {
                        goodEffectText += " / 체력 +" + goodHealthGain;
                    }

                    AppendChoiceEffect(goodEffectText);
                    if (recoveryPattern)
                    {
                        AppendChoiceEffect("복구 보정 - 생존 흐름 유지");
                    }
                    if (survivalGiftStacks > 0 && survivalGiftHealthGain > 0)
                    {
                        AppendChoiceEffect("생존 축 x" + survivalGiftStacks + " - 보정 회복 +" + survivalGiftHealthGain);
                    }

                    if (stableRouteHealthGain > 0)
                    {
                        AppendChoiceEffect("안정 루트 - 보정 회복 / 체력 +" + stableRouteHealthGain);
                    }

                    if (HasChoice(relic, "사막의 잔상") && goodFocusGain > 0)
                    {
                        AppendChoiceEffect(focusStateWasActive ? "사막의 잔상 - 보정 집중 유지"
                            : "사막의 잔상 - 보정 집중 +" + goodFocusGain);
                    }

                    if (profileGoodBonus > 0)
                    {
                        AppendChoiceEffect(profileGoodText + " +" + profileGoodBonus + "점");
                    }

                    if (goodFocusStateScoreBonus > 0)
                    {
                        AppendChoiceEffect("집중 상태 점수 배율 / +" + goodFocusStateScoreBonus + "점");
                    }

                    if (focusChainSoftened)
                    {
                        AppendChoiceEffect("집중 상태 - 연쇄 " + comboBeforeResolve + " → " + combo);
                    }

                    if (goodFocusStateActivated)
                    {
                        AppendChoiceEffect("집중 상태 발동 " + FocusStateDurationSeconds.ToString("0.0") + "초");
                    }

                    AppendRunStateResult("결과: 점수 +" + goodScore + " / 체력 " + health + "/" + maxHealthThisRun + " / " + BuildFocusStateResultText());
                    if (!focusChainSoftened)
                    {
                        AppendChainBreakText(comboBeforeResolve);
                    }
                    break;
                case GreedLastJudgementResult.Miss:
                    patternBundleBreakCount += 1;
                    combo = 0;
                    BreakPrecisionStreak();
                    focusStateRemainingSeconds = 0f;
                    missCount += 1;
                    if (outcome.MissReason == "no_input")
                    {
                        noInputMissCount += 1;
                    }
                    if (devInvincible)
                    {
                        staggerUntil = now + StaggerSeconds;
                        health = Mathf.Max(1, health);
                        recoveryBreatherPatternPending = true;
                        judgementText += "\n테스트 무적 - 피해를 무시했습니다.";
                        AppendRunStateResult("결과: 무적 보호 / 체력 " + health + "/" + maxHealthThisRun);
                        break;
                    }

                    staggerUntil = now + StaggerSeconds;
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
                            ? "무한모드 종료 - 체력 0\n기록 정산으로 재준비하거나 정산 후 로비로 돌아갈 수 있습니다."
                            : "탈출 실패 - 체력 0\n로비로 돌아가거나 다시 시작하세요.";
                    }
                    else
                    {
                        recoveryBreatherPatternPending = true;
                        AppendRunStateResult("결과: 피해 -1 / 체력 " + health + "/" + maxHealthThisRun);
                    }

                    AppendChainBreakText(comboBeforeResolve);
                    break;
            }

            if (routeBundleWasIntact && patternBundleBreakCount > 0)
            {
                AppendChoiceEffect(BuildRouteBundleBreakText());
            }

            if (!stopped)
            {
                AdvanceRunAfterPattern(now);
            }

            // Include chain and completed-bundle rewards in this judgement's actual gain.
            lastJudgementScoreGain = Mathf.Max(0, score - scoreBeforeResolve);
            lastJudgementHealthChange = health - healthBeforeResolve;
            lastJudgementFocusStateExtensionSeconds = focusStateWasActive
                ? Mathf.Max(0f, focusStateRemainingSeconds - focusSecondsBeforeResolve) : 0f;
            Publish(now);
        }

        private int AddScore(int amount, bool trackJudgementLimit = false)
        {
            // Keep the existing int save schema without wrapping a long infinite run below zero.
            int awarded = (int)Math.Min(Math.Max(0, amount), (long)int.MaxValue - score);
            if (trackJudgementLimit && awarded < amount) lastJudgementScoreLimited = true;
            score += awarded;
            return awarded;
        }

        private string BuildRouteBundleBreakText()
        {
            if (HasActiveRoute("안정 루트"))
            {
                return "안정 완주 중단 - 보정 생존, 다음 묶음에서 재도전";
            }

            if (HasActiveRoute("변주 루트"))
            {
                return "변주 완주 중단 - 엇박 흐름, 다음 묶음에서 재도전";
            }

            if (HasActiveRoute("압박 루트"))
            {
                return "압박 완주 중단 - 고점 도전, 다음 묶음에서 재도전";
            }

            return string.Empty;
        }

        private void RecordCurrentSegmentJudgement(GreedLastJudgementResult result)
        {
            if (infiniteMode || practiceMode)
            {
                return;
            }

            if (phase == GreedLastRunPhase.EscapeRunning)
            {
                switch (result)
                {
                    case GreedLastJudgementResult.Success:
                        escapeExactCount += 1;
                        break;
                    case GreedLastJudgementResult.Good:
                        escapeGoodCount += 1;
                        break;
                    case GreedLastJudgementResult.Miss:
                        escapeMissCount += 1;
                        break;
                }

                return;
            }

            int chapterSlot = Mathf.Clamp(chapterIndex, 1, 4) - 1;
            switch (result)
            {
                case GreedLastJudgementResult.Success:
                    chapterExactCounts[chapterSlot] += 1;
                    break;
                case GreedLastJudgementResult.Good:
                    chapterGoodCounts[chapterSlot] += 1;
                    break;
                case GreedLastJudgementResult.Miss:
                    chapterMissCounts[chapterSlot] += 1;
                    break;
            }
        }

        private void RecordAvoidanceJudgement(GreedLastAvoidanceType avoidanceType, GreedLastJudgementResult result)
        {
            int index = (int)avoidanceType;
            if (index <= 0 || index >= avoidanceExactCounts.Length)
            {
                return;
            }

            switch (result)
            {
                case GreedLastJudgementResult.Success:
                    avoidanceExactCounts[index] += 1;
                    currentBundleAvoidanceMask |= 1 << index;
                    break;
                case GreedLastJudgementResult.Good:
                    avoidanceGoodCounts[index] += 1;
                    break;
                case GreedLastJudgementResult.Miss:
                    avoidanceMissCounts[index] += 1;
                    break;
            }
        }

        private void RecordCurrentBundleJudgement(GreedLastJudgementResult result)
        {
            switch (result)
            {
                case GreedLastJudgementResult.Success:
                    currentBundleExactCount += 1;
                    break;
                case GreedLastJudgementResult.Good:
                    currentBundleGoodCount += 1;
                    break;
                case GreedLastJudgementResult.Miss:
                    currentBundleMissCount += 1;
                    break;
            }
        }

        private void CaptureCompletedSegmentPerformance()
        {
            if (phase == GreedLastRunPhase.EscapeRunning && coreRetrieved)
            {
                lastCompletedSegment = "탈출";
                lastCompletedSegmentExactCount = currentBundleExactCount;
                lastCompletedSegmentGoodCount = currentBundleGoodCount;
                lastCompletedSegmentMissCount = currentBundleMissCount;
                return;
            }

            int chapterSlot = Mathf.Clamp(chapterIndex, 1, 4) - 1;
            lastCompletedSegment = (chapterSlot + 1) + "장";
            lastCompletedSegmentExactCount = currentBundleExactCount;
            lastCompletedSegmentGoodCount = currentBundleGoodCount;
            lastCompletedSegmentMissCount = currentBundleMissCount;
        }

        private void CaptureCompletedInfiniteSectionPerformance()
        {
            lastCompletedSegment = "무한 " + infiniteSectionsCleared + "구간";
            lastCompletedSegmentExactCount = currentBundleExactCount;
            lastCompletedSegmentGoodCount = currentBundleGoodCount;
            lastCompletedSegmentMissCount = currentBundleMissCount;
        }

        private string BuildCompletedBundlePerformanceText()
        {
            string coaching = lastCompletedSegmentMissCount > 0
                ? "생존 재정비"
                : lastCompletedSegmentGoodCount > 0 ? "정확권 전환" : "완전 정확";
            return lastCompletedSegment
                + " 판정 정확 " + lastCompletedSegmentExactCount
                + " / 보정 " + lastCompletedSegmentGoodCount
                + " / 실수 " + lastCompletedSegmentMissCount
                + " / " + coaching;
        }

        private void ResetCurrentBundlePerformance()
        {
            currentBundleExactCount = 0;
            currentBundleGoodCount = 0;
            currentBundleMissCount = 0;
            currentBundleAvoidanceMask = 0;
        }

        private bool IsFocusStateActive => focusStateRemainingSeconds > 0.01f;

        private static int BuildFocusStateScoreBonus(int baseScore, bool focusStateActive)
        {
            if (!focusStateActive || baseScore <= 0)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.RoundToInt(baseScore * (FocusStateScoreMultiplier - 1f)));
        }

        private void GainFocus(
            int amount,
            bool extendActiveState,
            out bool stateActivated,
            out bool stateExtended)
        {
            stateActivated = false;
            stateExtended = false;
            if (IsFocusStateActive)
            {
                if (extendActiveState)
                {
                    float previous = focusStateRemainingSeconds;
                    focusStateRemainingSeconds = Mathf.Min(
                        FocusStateMaxDurationSeconds,
                        focusStateRemainingSeconds + FocusStateSuccessExtensionSeconds);
                    stateExtended = focusStateRemainingSeconds > previous + 0.001f;
                }

                return;
            }

            if (amount <= 0)
            {
                return;
            }

            focus = Mathf.Min(MaxFocus, focus + amount);
            if (focus < MaxFocus)
            {
                return;
            }

            focus = 0;
            focusStateRemainingSeconds = FocusStateDurationSeconds;
            focusStateActivationCount += 1;
            stateActivated = true;
        }

        private void RegisterInitialFocusState()
        {
            if (IsFocusStateActive)
            {
                focusStateActivationCount = Mathf.Max(1, focusStateActivationCount);
            }
        }

        private string BuildFocusStateResultText()
        {
            return IsFocusStateActive
                ? "집중 상태 " + focusStateRemainingSeconds.ToString("0.0") + "초"
                : "집중 " + focus + "/" + MaxFocus;
        }

        private void AppendRunStateResult(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            judgementText += "\n" + text;
        }

        private void BreakPrecisionStreak()
        {
            if (precisionStreak >= 2)
            {
                AppendChoiceEffect("정박 연속 " + precisionStreak + " 끊김");
            }

            precisionStreak = 0;
        }

        private int BuildCenterHitScoreBonus()
        {
            return BuildCenterHitScoreBonus(precisionStreak);
        }

        private int BuildCenterHitScoreBonus(int streak)
        {
            int streakBonus = Mathf.Clamp(streak, 1, 9) * 5;
            int threatBonus = infiniteMode ? GetInfiniteThreatLevel() * 4 : 0;
            return 25 + streakBonus + threatBonus;
        }

        private int BuildChoiceProfileSuccessScoreBonus(out string label)
        {
            return BuildChoiceProfileSuccessScoreBonus(lastCenterHit, combo, out label);
        }

        private int BuildChoiceProfileSuccessScoreBonus(bool centerHit, int awardedCombo, out string label)
        {
            switch (BuildActiveChoiceProfile())
            {
                case "안정형":
                    label = health >= Mathf.Max(2, maxHealthThisRun - 1) ? "안정형 유지" : "안정형 복구";
                    return health >= Mathf.Max(2, maxHealthThisRun - 1) ? 12 : 8;
                case "집중형":
                    label = centerHit ? "집중형 정박" : "집중형 유지";
                    return centerHit ? 18 : 8;
                case "연쇄형":
                    label = awardedCombo >= 3 ? "연쇄형 유지" : "연쇄형 시동";
                    return awardedCombo >= 3 ? 20 : 10;
                case "고점형":
                    label = centerHit ? "고점형 정박" : "고점형 성공";
                    return centerHit ? 28 : 18;
                case "균형형":
                    label = "균형형 보너스";
                    return 10;
                default:
                    label = string.Empty;
                    return 0;
            }
        }

        private int BuildChoiceProfileGoodScoreBonus(out string label)
        {
            switch (BuildActiveChoiceProfile())
            {
                case "안정형":
                    label = "안정형 보정";
                    return 12;
                case "집중형":
                    label = "집중형 보정";
                    return 8;
                case "연쇄형":
                    label = "연쇄형 재정비";
                    return 6;
                case "고점형":
                    label = "고점형 보정";
                    return 8;
                case "균형형":
                    label = "균형형 보정";
                    return 8;
                default:
                    label = string.Empty;
                    return 0;
            }
        }

        private static float BuildCenterHitWindow(GreedLastPatternModel pattern)
        {
            return Mathf.Max(0.018f, pattern.SuccessWindowSeconds * CenterHitWindowScale);
        }

        private int BuildSuccessComboGain(bool centerHit)
        {
            int stacks = GetActiveGiftStackCount("연쇄 축");
            int gain = 1 + Mathf.Min(2, stacks);
            if (stacks > 0 && HasChoice(relic, "파라오의 각인")) gain++;
            if (centerHit && IsRecoveryPattern(currentPattern)) gain += RecoveryPrecisionComboBonus;
            if (centerHit && currentPattern.TimingType == GreedLastTimingType.Offbeat) gain += OffbeatFlowComboBonus;
            return gain;
        }

        private int BuildJudgementScoreGainPreview(bool centerHit)
        {
            if (!activePattern || !currentPattern.IsValid || stopped || resumeCountdownActive || startCountdownActive) return 0;
            int gain;
            if (centerHit)
            {
                int nextCombo = combo + BuildSuccessComboGain(true);
                gain = 100 + nextCombo * 10;
                if (HasChoice(startGift, "고점 본능")) gain += 20;
                if (HasChoice(relic, "황금 박동")) gain += 40;
                if (HasChoice(relic, "파라오의 각인")) gain += 30;
                gain += BuildThreatPrecisionBonus(currentPattern.TimingType)
                    + BuildPatternVariationPrecisionBonus(currentPattern)
                    + BuildChoiceProfileSuccessScoreBonus(true, nextCombo, out _)
                    + BuildCenterHitScoreBonus(precisionStreak + 1);
                if (IsRecoveryPattern(currentPattern)) gain += RecoveryPrecisionScoreBonus;
                if (currentPattern.TimingType == GreedLastTimingType.Clutch)
                {
                    gain += ClutchCounterScoreBonus;
                    if (HasActiveRoute("압박 루트")) gain += PressureRouteClutchScoreBonus;
                }
                gain += BuildFocusStateScoreBonus(gain, IsFocusStateActive);
                int chainLevel = GetRhythmChainLevel(nextCombo);
                if (chainLevel > 0) gain += 30 * chainLevel + (infiniteMode ? 10 * GetInfiniteThreatLevel() : 0);
                if (!practiceMode && chapterProgress + 1 >= ChapterTargetPatterns
                    && patternBundleBreakCount <= 0 && HasActiveRoute("압박 루트"))
                    gain += PressureRouteBundleScoreBonus;
            }
            else
            {
                gain = 25 + BuildChoiceProfileGoodScoreBonus(out _);
                if (HasChoice(startGift, "고점 본능")) gain += 15;
                gain += BuildFocusStateScoreBonus(gain, IsFocusStateActive);
            }

            // Chain/route/section awards are added after the focus multiplier by Resolve.
            if (infiniteMode && chapterProgress + 1 >= ChapterTargetPatterns)
                gain += 50 * GetInfiniteThreatLevelForSections(infiniteSectionsCleared + 1);
            return Math.Min(Mathf.Max(0, gain), int.MaxValue - Mathf.Max(0, score));
        }

        private void CalculateGoodRecovery(out int survivalGain, out int stableGain)
        {
            int room = Mathf.Max(0, BuildBonusHealthCap() - health);
            int stacks = GetActiveGiftStackCount("생존 축");
            survivalGain = stacks > 0 ? Mathf.Min(1 + stacks / 2, room) : 0;
            stableGain = HasActiveRoute("안정 루트")
                ? Mathf.Min(StableRouteGoodHealthBonus, room - survivalGain) : 0;
        }

        private int BuildGoodHealthGainPreview()
        {
            if (stopped || (phase != GreedLastRunPhase.RunPlaying && phase != GreedLastRunPhase.EscapeRunning)) return 0;
            CalculateGoodRecovery(out int survivalGain, out int stableGain);
            return survivalGain + stableGain;
        }

        private int BuildThreatPrecisionBonus(GreedLastTimingType timingType)
        {
            return infiniteMode ? BuildThreatPrecisionBonusForLevel(GetInfiniteThreatLevel(), timingType) : 0;
        }

        private static int BuildThreatPrecisionBonusForLevel(int threatLevel, GreedLastTimingType timingType)
        {
            int safeThreatLevel = Mathf.Clamp(threatLevel, 1, InfiniteThreatMaxLevel);
            int bonus = Mathf.Max(0, safeThreatLevel - 1) * 12;
            if (timingType == GreedLastTimingType.Clutch)
            {
                bonus += safeThreatLevel >= 3 ? 10 : 0;
            }
            else if (timingType == GreedLastTimingType.Offbeat)
            {
                bonus += safeThreatLevel >= 3 ? 8 : 0;
            }

            return bonus;
        }

        private string BuildInfiniteThreatChangeSummary()
        {
            int threatLevel = GetInfiniteThreatLevel();
            return "위협 " + threatLevel + "/5 " + BuildInfiniteThreatPressureLabel(threatLevel)
                + " / 위험폭 " + BuildInfiniteThreatDangerWidthLabel(threatLevel)
                + " / " + BuildThreatPrecisionRewardRange(threatLevel);
        }

        private static string BuildInfiniteThreatPressureLabel(int threatLevel)
        {
            switch (Mathf.Clamp(threatLevel, 1, InfiniteThreatMaxLevel))
            {
                case 1:
                    return "안정";
                case 2:
                    return "상승";
                case 3:
                    return "압박";
                case 4:
                    return "고압";
                default:
                    return "최고";
            }
        }

        private static string BuildInfiniteThreatDangerWidthLabel(int threatLevel)
        {
            switch (Mathf.Clamp(threatLevel, 1, InfiniteThreatMaxLevel))
            {
                case 1:
                    return "좁음";
                case 2:
                    return "보통";
                case 3:
                    return "확대";
                case 4:
                    return "넓음";
                default:
                    return "최대";
            }
        }

        private static string BuildThreatPrecisionRewardRange(int threatLevel)
        {
            int perfectBonus = BuildThreatPrecisionBonusForLevel(threatLevel, GreedLastTimingType.Perfect);
            int clutchBonus = BuildThreatPrecisionBonusForLevel(threatLevel, GreedLastTimingType.Clutch);
            int offbeatBonus = BuildThreatPrecisionBonusForLevel(threatLevel, GreedLastTimingType.Offbeat);
            int minBonus = Mathf.Min(perfectBonus, Mathf.Min(clutchBonus, offbeatBonus));
            int maxBonus = Mathf.Max(perfectBonus, Mathf.Max(clutchBonus, offbeatBonus));
            return minBonus == maxBonus ? "정확+" + minBonus : "정확+" + minBonus + "~" + maxBonus;
        }

        private static int BuildPatternVariationPrecisionBonus(GreedLastPatternModel pattern)
        {
            string prompt = pattern.Prompt ?? string.Empty;
            if (prompt.Contains("[복구]", StringComparison.Ordinal)
                || prompt.Contains("[유예]", StringComparison.Ordinal))
            {
                return 0;
            }

            if (prompt.Contains("[위기]", StringComparison.Ordinal))
            {
                return 40;
            }

            if (prompt.Contains("[탈출]", StringComparison.Ordinal))
            {
                return 30;
            }

            if (prompt.Contains("[정밀]", StringComparison.Ordinal))
            {
                return 25;
            }

            if (prompt.Contains("[압박]", StringComparison.Ordinal))
            {
                return 22;
            }

            if (prompt.Contains("[급박]", StringComparison.Ordinal))
            {
                return 20;
            }

            if (prompt.Contains("[연쇄]", StringComparison.Ordinal))
            {
                return 15;
            }

            return 0;
        }

        public void ToggleDevInvincible()
        {
            devInvincible = !devInvincible;
            if (devInvincible && health <= 0)
            {
                health = BuildGrowthMaxHealth();
                maxHealthThisRun = Mathf.Max(maxHealthThisRun, health);
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

        public void DropHealthForDangerTest()
        {
            if (stopped)
            {
                judgementText = "종료 상태에서는 피해 테스트를 적용하지 않습니다.";
                Publish();
                return;
            }

            int healthCap = BuildBonusHealthCap();
            int previousHealth = health;
            health = Mathf.Clamp(health - 1, 1, healthCap);
            string result = health < previousHealth ? "테스트 피해 -1" : "테스트 체력 위험 유지";
            judgementText = result
                + " / 체력 " + health + "/" + healthCap
                + "\n체력 가장자리 위험 비네트와 박자 펄스를 확인합니다.";
            Publish();
        }

        public void JumpToDevShortcut(GreedLastDevShortcut shortcut, float now)
        {
            if (shortcut == GreedLastDevShortcut.ReviewTargetCheck && infiniteMode)
            {
                PrepareInfiniteReviewTargetDevCheck(now);
                Publish();
                return;
            }

            if (shortcut == GreedLastDevShortcut.RouteCycle)
            {
                string nextRoute = BuildNextRouteTestChoice(lastNode);
                PrepareDevJump(now);
                PrepareRouteTestDevCheck(nextRoute, now);
                Publish();
                return;
            }

            if (shortcut == GreedLastDevShortcut.AvoidanceCycle)
            {
                int cycleIndex = devAvoidanceCycleIndex % 5;
                devAvoidanceCycleIndex = (devAvoidanceCycleIndex + 1) % 5;
                PrepareDevJump(now);
                if (cycleIndex < 3)
                {
                    GreedLastAvoidanceType avoidanceType = (GreedLastAvoidanceType)(cycleIndex + 1);
                    PrepareAvoidanceTestDevCheck(avoidanceType, now);
                }
                else
                {
                    PrepareRotationCorridorTestDevCheck(
                        cycleIndex == 3 ? GreedLastRunChannel.Left : GreedLastRunChannel.Right,
                        now);
                }

                Publish();
                return;
            }

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
                    PrepareStartCountdown(now, "테스트 바로가기 - 탈출 직전\n카운트 뒤 마지막 함정이 내려옵니다.");
                    break;
                case GreedLastDevShortcut.ReviewTargetCheck:
                    PrepareReviewTargetDevCheck(now);
                    break;
            }

            Publish();
        }

        private static string BuildNextRouteTestChoice(string currentRoute)
        {
            if (HasChoice(currentRoute, "안정 루트"))
            {
                return "변주 루트";
            }

            return HasChoice(currentRoute, "변주 루트")
                ? "압박 루트"
                : "안정 루트";
        }

        private void PrepareRouteTestDevCheck(string route, float now)
        {
            score = 0;
            combo = 0;
            distance = 0f;
            ResetRunVitals();
            chapterIndex = 2;
            chapterProgress = 0;
            patternIndex = 0;
            phase = GreedLastRunPhase.RunPlaying;
            SeedDevChoice(GreedLastChoiceKind.Node, route);
            string contract = route == "안정 루트"
                ? "Good 체력 +" + StableRouteGoodHealthBonus + " / 넓은 판정"
                : route == "변주 루트"
                    ? "엇박 정박 집중 +" + VariationRouteOffbeatFocusBonus
                    : "Clutch 정박 +" + PressureRouteClutchScoreBonus + "점 / 좁은 판정";
            PrepareStartCountdown(
                now,
                "테스트 경로 체험 - " + route + "\n" + contract + "\n카운트 뒤 대표 함정이 내려옵니다.");
        }

        private void PrepareAvoidanceTestDevCheck(GreedLastAvoidanceType avoidanceType, float now)
        {
            score = 0;
            combo = 0;
            distance = 0f;
            ResetRunVitals();
            chapterIndex = 1;
            chapterProgress = Mathf.Clamp((int)avoidanceType - 1, 0, ChapterTargetPatterns - 1);
            patternIndex = chapterProgress;
            phase = GreedLastRunPhase.RunPlaying;
            SeedDevChoice(GreedLastChoiceKind.StartGift, "박자 감각");
            string action = avoidanceType == GreedLastAvoidanceType.Jump
                ? "점프"
                : avoidanceType == GreedLastAvoidanceType.Slide ? "숙이기" : "비틀기";
            PrepareStartCountdown(
                now,
                "테스트 동작 확인 - " + action
                    + "\n카운트 뒤 대표 함정의 궤적·자세·판정 피드백을 확인합니다.");
        }

        private void PrepareRotationCorridorTestDevCheck(GreedLastRunChannel channel, float now)
        {
            score = 0;
            combo = 0;
            distance = 0f;
            ResetRunVitals();
            chapterIndex = 1;
            chapterProgress = 2;
            patternIndex = chapterProgress;
            phase = GreedLastRunPhase.RunPlaying;
            SeedDevChoice(GreedLastChoiceKind.StartGift, "박자 감각");
            string patternId = channel == GreedLastRunChannel.Left
                ? "left_rotate_corridor_perfect"
                : "right_rotate_corridor_clutch";
            devNextPatternOverride = FindPatternById(patternId);
            string direction = channel == GreedLastRunChannel.Left ? "좌회전" : "우회전";
            string inputHint = channel == GreedLastRunChannel.Left
                ? "Q / 좌 기울임"
                : "E / 우 기울임";
            PrepareStartCountdown(
                now,
                "테스트 동작 확인 - " + direction + " 통로"
                    + "\n카운트 뒤 통로 기울기·주자 무게중심·" + inputHint + "을 확인합니다.");
        }

        private void PrepareReviewTargetDevCheck(float now)
        {
            string target = reviewTargetText ?? string.Empty;
            score = 0;
            combo = 0;
            ResetRunVitals();
            maxHealthThisRun = Mathf.Max(maxHealthThisRun, health);

            if (string.IsNullOrWhiteSpace(target))
            {
                chapterIndex = 1;
                chapterProgress = 0;
                SeedDevChoice(GreedLastChoiceKind.StartGift, "박자 감각");
                phase = GreedLastRunPhase.RunPlaying;
                PrepareStartCountdown(now, "테스트 바로가기 - 활성 복기 목표가 없습니다.");
                return;
            }

            if (target.Contains("무실수", StringComparison.Ordinal)
                || target.Contains("보정 0회", StringComparison.Ordinal))
            {
                chapterIndex = 4;
                chapterProgress = ChapterTargetPatterns - 1;
                coreRetrieved = true;
                SeedFullDevRoute();
                phase = GreedLastRunPhase.EscapeRunning;
                PrepareStartCountdown(
                    now,
                    "테스트 목표 직전 - " + target + "\n정확 입력 한 번 뒤 최종 정산을 확인합니다.");
                return;
            }

            chapterIndex = 1;
            chapterProgress = 0;
            SeedDevChoice(GreedLastChoiceKind.StartGift, "박자 감각");
            phase = GreedLastRunPhase.RunPlaying;
            SeedReviewTargetOneHitBefore(target);

            PrepareStartCountdown(
                now,
                "테스트 목표 직전 - " + target + "\n"
                    + (target.Contains("추천 보정 적용", StringComparison.Ordinal)
                        ? "가운데 추천 적용 버튼으로 달성 피드백을 확인합니다."
                        : "다음 정확 입력으로 달성 피드백을 확인합니다."));
        }

        private void PrepareInfiniteReviewTargetDevCheck(float now)
        {
            string target = reviewTargetText ?? string.Empty;
            if (health <= 0)
            {
                health = Mathf.Max(1, BuildGrowthMaxHealth());
                maxHealthThisRun = Mathf.Max(maxHealthThisRun, health);
            }

            currentPattern = GreedLastPatternModel.Empty;
            lastJudgedPattern = GreedLastPatternModel.Empty;
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
            saveEligible = false;
            escapeSucceeded = false;
            phase = GreedLastRunPhase.RunPlaying;
            ignoreReason = string.Empty;
            missReason = string.Empty;
            lastResult = GreedLastJudgementResult.None;

            if (string.IsNullOrWhiteSpace(target))
            {
                PrepareStartCountdown(now, "테스트 바로가기 - 활성 무한 복기 목표가 없습니다.");
                return;
            }

            if (target.Contains("무실수", StringComparison.Ordinal)
                || target.Contains("보정 0회", StringComparison.Ordinal))
            {
                chapterProgress = ChapterTargetPatterns - 1;
                goodCount = 0;
                missCount = 0;
                currentBundleExactCount = 0;
                currentBundleGoodCount = 0;
                currentBundleMissCount = 0;
                PrepareStartCountdown(
                    now,
                    "테스트 무한 목표 직전 - " + target + "\n정확 입력 한 번 뒤 End 정산 종료로 정산을 확인합니다.");
                return;
            }

            if (target.Contains("입력 없음", StringComparison.Ordinal))
            {
                int targetNoInput = ExtractTargetNumberAfterKeyword(target, "입력 없음");
                noInputMissCount = Mathf.Max(0, targetNoInput);
                chapterProgress = ChapterTargetPatterns - 1;
                ResetCurrentBundlePerformance();
                PrepareStartCountdown(
                    now,
                    "테스트 무한 목표 직전 - " + target + "\n정확 입력 뒤 End 정산 종료로 입력 누락을 정산합니다.");
                return;
            }

            if (target.Contains("구간 생존", StringComparison.Ordinal))
            {
                int targetSections = ExtractTargetNumberBeforeMarker(target, "구간");
                infiniteSectionsCleared = Mathf.Max(0, targetSections - 1);
                chapterProgress = ChapterTargetPatterns - 1;
                ResetCurrentBundlePerformance();
                PrepareStartCountdown(
                    now,
                    "테스트 무한 목표 직전 - " + target + "\n다음 정확 입력으로 목표 구간에 도달합니다.");
                return;
            }

            if (target.Contains("위협", StringComparison.Ordinal))
            {
                int targetThreat = Mathf.Clamp(ExtractTargetNumberAfterKeyword(target, "위협"), 1, InfiniteThreatMaxLevel);
                infiniteSectionsCleared = targetThreat <= 1 ? 0 : targetThreat * 2 - 3;
                infiniteThreatTestOffset = 0;
                maxInfiniteThreatLevel = Mathf.Max(1, GetInfiniteThreatLevel());
                chapterProgress = ChapterTargetPatterns - 1;
                ResetCurrentBundlePerformance();
                PrepareStartCountdown(
                    now,
                    "테스트 무한 목표 직전 - " + target + "\n다음 정확 입력으로 목표 위협에 진입합니다.");
                return;
            }

            if (target.Contains("실수", StringComparison.Ordinal)
                && target.Contains("이하", StringComparison.Ordinal))
            {
                int targetMisses = ExtractTargetNumberAfterKeyword(target, "실수");
                missCount = Mathf.Max(0, targetMisses);
                chapterProgress = ChapterTargetPatterns - 1;
                ResetCurrentBundlePerformance();
                PrepareStartCountdown(
                    now,
                    "테스트 무한 목표 직전 - " + target + "\n정확 입력 뒤 End 정산 종료로 허용 실수를 정산합니다.");
                return;
            }

            SeedReviewTargetOneHitBefore(target);
            PrepareStartCountdown(
                now,
                "테스트 무한 목표 직전 - " + target + "\n"
                    + (target.Contains("추천 보정 적용", StringComparison.Ordinal)
                        ? "가운데 추천 적용 버튼으로 달성 피드백을 확인합니다."
                        : "다음 정확 입력으로 달성 피드백을 확인합니다."));
        }

        private void SeedReviewTargetOneHitBefore(string target)
        {
            if (target.Contains("추천 보정 적용", StringComparison.Ordinal))
            {
                inputTimingOffsetSeconds = 0f;
                timingSampleCount = TimingRecommendationMinSamples;
                timingSignedErrorTotal = 0.32f;
                timingAbsErrorTotal = 0.32f;
                timingRecommendedOffsetTotal = -0.32f;
                earlyInputCount = 0;
                lateInputCount = TimingRecommendationMinSamples;
                timingRecommendationApplied = false;
                appliedTimingRecommendationMilliseconds = 0;
                return;
            }

            if (target.Contains("콤보", StringComparison.Ordinal))
            {
                int targetCombo = ExtractTargetNumberAfterKeyword(target, "콤보");
                combo = Mathf.Max(0, targetCombo - 1);
                maxCombo = Mathf.Max(maxCombo, combo);
                return;
            }

            if (target.Contains("점", StringComparison.Ordinal))
            {
                int targetScore = ExtractTargetNumberBeforeMarker(target, "점");
                score = Mathf.Max(0, targetScore - 1);
                return;
            }

            if (target.Contains("체력", StringComparison.Ordinal) || target.Contains("생존", StringComparison.Ordinal))
            {
                int targetHealth = target.Contains("체력", StringComparison.Ordinal)
                    ? ExtractTargetNumberAfterKeyword(target, "체력")
                    : ExtractTargetNumberAfterKeyword(target, "생존");
                maxHealthThisRun = Mathf.Max(maxHealthThisRun, targetHealth);
                health = Mathf.Clamp(targetHealth, 1, Mathf.Max(1, maxHealthThisRun));
                return;
            }

            if (target.Contains("집중", StringComparison.Ordinal))
            {
                int targetFocus = ExtractTargetNumberAfterKeyword(target, "집중");
                focusStateRemainingSeconds = 0f;
                if (target.Contains("발동", StringComparison.Ordinal))
                {
                    focusStateActivationCount = Mathf.Max(0, targetFocus - 1);
                    focus = Mathf.Max(0, MaxFocus - 1);
                }
                else
                {
                    focusStateActivationCount = 0;
                    focus = Mathf.Clamp(targetFocus - 1, 0, MaxFocus - 1);
                }

                return;
            }

            if (target.Contains("정박", StringComparison.Ordinal) || target.Contains("정확권", StringComparison.Ordinal))
            {
                int targetPrecision = ExtractTargetNumberBeforeMarker(target, "연속");
                if (targetPrecision <= 0)
                {
                    targetPrecision = target.Contains("정박", StringComparison.Ordinal)
                        ? ExtractTargetNumberAfterKeyword(target, "정박")
                        : ExtractTargetNumberAfterKeyword(target, "정확권");
                }

                precisionStreak = Mathf.Max(0, targetPrecision - 1);
                maxPrecisionStreak = Mathf.Max(maxPrecisionStreak, precisionStreak);
            }
        }

        private void PrepareDevJump(float now)
        {
            if (health <= 0)
            {
                health = BuildGrowthMaxHealth();
                maxHealthThisRun = Mathf.Max(maxHealthThisRun, health);
            }

            maxHealthThisRun = Mathf.Max(BuildGrowthMaxHealth(), health);

            ResetRunStats();
            infiniteMode = false;
            activeInfiniteLoadoutName = string.Empty;
            activeInfiniteLoadoutProfile = string.Empty;
            activeInfiniteLoadoutDisplayProfile = string.Empty;
            currentPattern = GreedLastPatternModel.Empty;
            lastJudgedPattern = GreedLastPatternModel.Empty;
            devNextPatternOverride = GreedLastPatternModel.Empty;
            activePattern = false;
            inputConsumed = false;
            stopped = false;
            paused = false;
            resumeCountdownActive = false;
            startCountdownActive = false;
            autoFlowBeforePause = false;
            autoFlow = false;
            staggerUntil = 0f;
            focusStateRemainingSeconds = 0f;
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

        private string BuildActiveChoiceProfile()
        {
            if (infiniteMode && !string.IsNullOrEmpty(activeInfiniteLoadoutProfile))
            {
                return activeInfiniteLoadoutProfile;
            }

            int survivalScore = 0;
            int focusScore = 0;
            int comboScore = 0;
            int scoreScore = 0;
            if (choiceHistoryCount > 0)
            {
                for (int i = 0; i < choiceHistoryCount; i += 1)
                {
                    AccumulateChoiceProfileScore(
                        choiceHistory[i],
                        ref survivalScore,
                        ref focusScore,
                        ref comboScore,
                        ref scoreScore);
                }
            }
            else
            {
                AccumulateChoiceProfileScore(startGift, ref survivalScore, ref focusScore, ref comboScore, ref scoreScore);
                AccumulateChoiceProfileScore(lastNode, ref survivalScore, ref focusScore, ref comboScore, ref scoreScore);
                AccumulateChoiceProfileScore(primaryGift, ref survivalScore, ref focusScore, ref comboScore, ref scoreScore);
                AccumulateChoiceProfileScore(relic, ref survivalScore, ref focusScore, ref comboScore, ref scoreScore);
            }

            if (survivalScore + focusScore + comboScore + scoreScore <= 0)
            {
                return infiniteMode ? "저장 조합" : "루트 미정";
            }

            int topScore = Mathf.Max(survivalScore, focusScore, comboScore, scoreScore);
            int topCount = 0;
            topCount += survivalScore == topScore ? 1 : 0;
            topCount += focusScore == topScore ? 1 : 0;
            topCount += comboScore == topScore ? 1 : 0;
            topCount += scoreScore == topScore ? 1 : 0;
            if (topCount > 1)
            {
                return "균형형";
            }

            if (survivalScore == topScore)
            {
                return "안정형";
            }

            if (focusScore == topScore)
            {
                return "집중형";
            }

            if (comboScore == topScore)
            {
                return "연쇄형";
            }

            return "고점형";
        }

        private static string BuildInfiniteLoadoutProfile(GreedLastRunRecord loadoutRecord)
        {
            if (!loadoutRecord.IsValid)
            {
                return string.Empty;
            }

            string explicitProfile = ExtractExplicitLoadoutProfile(loadoutRecord.ChoiceSummary);
            if (!string.IsNullOrEmpty(explicitProfile))
            {
                return explicitProfile;
            }

            int survivalScore = 0;
            int focusScore = 0;
            int comboScore = 0;
            int scoreScore = 0;
            if (!string.IsNullOrEmpty(loadoutRecord.ChoiceSummary)
                && loadoutRecord.ChoiceSummary != "선택 없음"
                && loadoutRecord.ChoiceSummary != "테스트 루트")
            {
                AccumulateChoiceProfileTextScore(loadoutRecord.ChoiceSummary, ref survivalScore, ref focusScore, ref comboScore, ref scoreScore);
            }
            else
            {
                AccumulateChoiceProfileScore(loadoutRecord.StartGift, ref survivalScore, ref focusScore, ref comboScore, ref scoreScore);
                AccumulateChoiceProfileScore(loadoutRecord.PrimaryGift, ref survivalScore, ref focusScore, ref comboScore, ref scoreScore);
                AccumulateChoiceProfileScore(loadoutRecord.Relic, ref survivalScore, ref focusScore, ref comboScore, ref scoreScore);
            }

            return BuildChoiceProfileFromScores(survivalScore, focusScore, comboScore, scoreScore);
        }

        private static string BuildInfiniteLoadoutDisplayProfile(GreedLastRunRecord loadoutRecord)
        {
            if (!loadoutRecord.IsValid)
            {
                return string.Empty;
            }

            string profile = BuildInfiniteLoadoutProfile(loadoutRecord);
            string route = BuildInfiniteLoadoutRouteProfile(loadoutRecord.ChoiceSummary);
            return JoinLoadoutProfileAndRoute(profile, route);
        }

        private static string BuildInfiniteLoadoutRouteProfile(string choiceSummary)
        {
            int safeCount = CountChoiceOccurrences(choiceSummary, "안정 루트");
            int syncopationCount = CountChoiceOccurrences(choiceSummary, "변주 루트");
            int pressureCount = CountChoiceOccurrences(choiceSummary, "압박 루트");
            int total = safeCount + syncopationCount + pressureCount;
            if (total <= 0)
            {
                return string.Empty;
            }

            int top = Mathf.Max(safeCount, syncopationCount, pressureCount);
            int topCount = 0;
            topCount += safeCount == top ? 1 : 0;
            topCount += syncopationCount == top ? 1 : 0;
            topCount += pressureCount == top ? 1 : 0;
            if (topCount > 1)
            {
                return "혼합 루트";
            }

            if (safeCount == top)
            {
                return "안정 루트";
            }

            return syncopationCount == top ? "변주 루트" : "압박 루트";
        }

        private static string JoinLoadoutProfileAndRoute(string profile, string route)
        {
            bool hasProfile = !string.IsNullOrEmpty(profile);
            bool hasRoute = !string.IsNullOrEmpty(route);
            if (hasProfile && hasRoute)
            {
                return profile + " / " + route;
            }

            return hasProfile ? profile : route;
        }

        private static string ExtractExplicitLoadoutProfile(string choiceSummary)
        {
            if (string.IsNullOrEmpty(choiceSummary))
            {
                return string.Empty;
            }

            string normalized = choiceSummary.Replace("\r", string.Empty);
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i += 1)
            {
                if (!lines[i].StartsWith("조합 성향:", StringComparison.Ordinal))
                {
                    continue;
                }

                string profile = lines[i].Substring("조합 성향:".Length).Trim();
                return IsKnownChoiceProfile(profile) ? profile : string.Empty;
            }

            return string.Empty;
        }

        private static bool IsKnownChoiceProfile(string profile)
        {
            return profile == "안정형"
                || profile == "집중형"
                || profile == "연쇄형"
                || profile == "고점형"
                || profile == "균형형";
        }

        private static string BuildChoiceProfileFromScores(int survivalScore, int focusScore, int comboScore, int scoreScore)
        {
            if (survivalScore + focusScore + comboScore + scoreScore <= 0)
            {
                return string.Empty;
            }

            int topScore = Mathf.Max(survivalScore, focusScore, comboScore, scoreScore);
            int topCount = 0;
            topCount += survivalScore == topScore ? 1 : 0;
            topCount += focusScore == topScore ? 1 : 0;
            topCount += comboScore == topScore ? 1 : 0;
            topCount += scoreScore == topScore ? 1 : 0;
            if (topCount > 1)
            {
                return "균형형";
            }

            if (survivalScore == topScore)
            {
                return "안정형";
            }

            if (focusScore == topScore)
            {
                return "집중형";
            }

            return comboScore == topScore ? "연쇄형" : "고점형";
        }

        private static void AccumulateChoiceProfileScore(
            string choice,
            ref int survivalScore,
            ref int focusScore,
            ref int comboScore,
            ref int scoreScore)
        {
            switch (choice)
            {
                case "안정 호흡":
                case "안정 루트":
                case "생존 축":
                case "사막의 잔상":
                    survivalScore += 2;
                    break;
                case "박자 감각":
                case "변주 루트":
                case "집중 축":
                    focusScore += 2;
                    break;
                case "압박 루트":
                case "연쇄 축":
                case "파라오의 각인":
                    comboScore += 2;
                    break;
                case "고점 본능":
                case "황금 박동":
                    scoreScore += 2;
                    break;
            }

            if (choice == "사막의 잔상")
            {
                focusScore += 1;
            }

            if (choice == "파라오의 각인")
            {
                focusScore += 1;
            }
        }

        private static void AccumulateChoiceProfileTextScore(
            string text,
            ref int survivalScore,
            ref int focusScore,
            ref int comboScore,
            ref int scoreScore)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            AddChoiceProfileTextScore(text, "안정 호흡", ref survivalScore, 2);
            AddChoiceProfileTextScore(text, "안정 루트", ref survivalScore, 2);
            AddChoiceProfileTextScore(text, "생존 축", ref survivalScore, 2);
            AddChoiceProfileTextScore(text, "사막의 잔상", ref survivalScore, 2);
            AddChoiceProfileTextScore(text, "박자 감각", ref focusScore, 2);
            AddChoiceProfileTextScore(text, "변주 루트", ref focusScore, 2);
            AddChoiceProfileTextScore(text, "집중 축", ref focusScore, 2);
            AddChoiceProfileTextScore(text, "압박 루트", ref comboScore, 2);
            AddChoiceProfileTextScore(text, "연쇄 축", ref comboScore, 2);
            AddChoiceProfileTextScore(text, "파라오의 각인", ref comboScore, 2);
            AddChoiceProfileTextScore(text, "고점 본능", ref scoreScore, 2);
            AddChoiceProfileTextScore(text, "황금 박동", ref scoreScore, 2);

            if (ContainsChoice(text, "사막의 잔상"))
            {
                focusScore += 1;
            }

            if (ContainsChoice(text, "파라오의 각인"))
            {
                focusScore += 1;
            }
        }

        private static void AddChoiceProfileTextScore(string text, string choice, ref int score, int amount)
        {
            if (ContainsChoice(text, choice))
            {
                score += amount;
            }
        }

        private void RestartAutoFlow(float now, string restartPrefix = null)
        {
            score = 0;
            combo = 0;
            ResetRunVitals();
            ResetRunStats();
            RegisterInitialFocusState();
            distance = 0f;
            currentPattern = GreedLastPatternModel.Empty;
            lastJudgedPattern = GreedLastPatternModel.Empty;
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
            activeInfiniteLoadoutName = string.Empty;
            activeInfiniteLoadoutProfile = string.Empty;
            activeInfiniteLoadoutDisplayProfile = string.Empty;
            ClearChoices();
            OpenChoice(GreedLastChoiceKind.StartGift);
            autoFlow = false;
            judgementText = (string.IsNullOrEmpty(restartPrefix) ? string.Empty : restartPrefix + "\n")
                + "재도전 시작 - 시작 기프트를 선택하세요." + BuildGrowthAppliedText();
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

                judgementText += BuildChoiceProfileResultLine();
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

                judgementText += BuildChoiceProfileResultLine();
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
            readyText += BuildChoiceProfileResultLine();
            PrepareStartCountdown(now, readyText + "\n카운트 뒤 첫 함정이 내려옵니다.");
            Publish(now);
        }

        private string BuildChoiceProfileResultLine()
        {
            string profile = BuildActiveChoiceProfile();
            if (!IsRecordableChoiceProfile(profile))
            {
                return string.Empty;
            }

            string displayProfile = infiniteMode
                ? BuildCurrentInfiniteLoadoutProfile()
                : JoinLoadoutProfileAndRoute(profile, BuildActiveRouteProfile());
            return "\n조합 성향: " + (string.IsNullOrEmpty(displayProfile) ? profile : displayProfile);
        }

        private string BuildActiveRouteProfile()
        {
            int safeCount = CountChoiceHistoryOccurrences("안정 루트");
            int syncopationCount = CountChoiceHistoryOccurrences("변주 루트");
            int pressureCount = CountChoiceHistoryOccurrences("압박 루트");
            int total = safeCount + syncopationCount + pressureCount;
            if (total <= 0)
            {
                return string.Empty;
            }

            int top = Mathf.Max(safeCount, syncopationCount, pressureCount);
            int topCount = 0;
            topCount += safeCount == top ? 1 : 0;
            topCount += syncopationCount == top ? 1 : 0;
            topCount += pressureCount == top ? 1 : 0;
            if (topCount > 1)
            {
                return "혼합 루트";
            }

            if (safeCount == top)
            {
                return "안정 루트";
            }

            return syncopationCount == top ? "변주 루트" : "압박 루트";
        }

        private int CountChoiceHistoryOccurrences(string choice)
        {
            int count = 0;
            for (int i = 0; i < choiceHistoryCount; i += 1)
            {
                if (string.Equals(choiceHistory[i], choice, StringComparison.Ordinal))
                {
                    count += 1;
                }
            }

            return count;
        }

        private int GetActiveGiftStackCount(string gift)
        {
            if (!infiniteMode)
            {
                return CountChoiceHistoryOccurrences(gift);
            }

            switch (gift)
            {
                case "집중 축":
                    return activeInfiniteFocusGiftStacks;
                case "생존 축":
                    return activeInfiniteSurvivalGiftStacks;
                case "연쇄 축":
                    return activeInfiniteChainGiftStacks;
                default:
                    return 0;
            }
        }

        private static int BuildStoredGiftStackCount(GreedLastRunRecord record, string gift)
        {
            if (!record.IsValid)
            {
                return 0;
            }

            int stacks = CountChoiceOccurrences(record.ChoiceSummary, gift);
            return stacks > 0 || !HasChoice(record.PrimaryGift, gift) ? stacks : 1;
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
            activeInfiniteFocusGiftStacks = 0;
            activeInfiniteSurvivalGiftStacks = 0;
            activeInfiniteChainGiftStacks = 0;
        }

        private string ApplyChoiceReward(GreedLastChoiceKind completedKind, string selected)
        {
            switch (selected)
            {
                case "박자 감각":
                    return ApplyChoiceFocusReward(1);
                case "안정 호흡":
                    return ApplyChoiceHealthReward();
                case "고점 본능":
                    AddScore(80);
                    return "점수 +80";
                case "안정 루트":
                    return ApplyChoiceHealthReward() + " / 넓은 판정 / Good 체력 +" + StableRouteGoodHealthBonus;
                case "변주 루트":
                    return ApplyChoiceFocusReward(1) + " / 엇박 우선 / 정박 집중 +" + VariationRouteOffbeatFocusBonus;
                case "압박 루트":
                    combo += 1;
                    maxCombo = Mathf.Max(maxCombo, combo);
                    AddScore(120);
                    return "콤보 +1 / 점수 +120 / 좁은 판정 / Clutch +" + PressureRouteClutchScoreBonus;
                case "집중 축":
                    return AppendGiftStackReward(selected, ApplyChoiceFocusReward(2));
                case "생존 축":
                    return AppendGiftStackReward(selected, ApplyChoiceHealthReward());
                case "연쇄 축":
                    combo += 2;
                    maxCombo = Mathf.Max(maxCombo, combo);
                    AddScore(60);
                    return AppendGiftStackReward(selected, "콤보 +2 / 점수 +60");
                case "황금 박동":
                    AddScore(200);
                    return "점수 +200";
                case "사막의 잔상":
                    return ApplyChoiceHealthReward() + " / " + ApplyChoiceFocusReward(1);
                case "파라오의 각인":
                    combo += 3;
                    maxCombo = Mathf.Max(maxCombo, combo);
                    return "콤보 +3 / " + ApplyChoiceFocusReward(MaxFocus);
                default:
                    return completedKind == GreedLastChoiceKind.None ? string.Empty : "기본 흐름 유지";
            }
        }

        private string ApplyChoiceHealthReward()
        {
            int previous = health;
            health = Mathf.Min(BuildBonusHealthCap(), health + 1);
            maxHealthThisRun = Mathf.Max(maxHealthThisRun, health);
            int gain = health - previous;
            return gain == 0 ? "체력 상한 유지" : "체력 " + (gain > 0 ? "+" : string.Empty) + gain;
        }

        private string AppendGiftStackReward(string gift, string rewardText)
        {
            int stacks = CountChoiceHistoryOccurrences(gift);
            return rewardText + " / " + gift + " 누적 " + Mathf.Max(1, stacks);
        }

        private string ApplyChoiceFocusReward(int amount)
        {
            string effect = BuildChoiceFocusRewardPreview(focus, focusStateRemainingSeconds, amount);
            GainFocus(
                Mathf.Max(0, amount),
                extendActiveState: true,
                out _,
                out _);
            return effect;
        }

        public static string BuildChoiceFocusRewardPreview(int currentFocus, float stateSeconds, int amount)
        {
            if (stateSeconds > 0.01f)
            {
                float extended = Mathf.Min(FocusStateMaxDurationSeconds, stateSeconds + FocusStateSuccessExtensionSeconds);
                return extended > stateSeconds + 0.001f ? "집중 상태 연장" : "집중 상태 상한 유지";
            }

            amount = Mathf.Max(0, amount);
            return amount > 0 && currentFocus + amount >= MaxFocus
                ? "집중 상태 발동" : "집중 +" + amount;
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

        public static GreedLastRunRecord CreatePreparedInfiniteLoadout(
            GreedLastRunRecord storedLoadout,
            string preparationGift,
            string preparationRelic)
        {
            if (!storedLoadout.IsValid)
            {
                return GreedLastRunRecord.Empty;
            }

            string gift = IsInfinitePreparationGift(preparationGift)
                ? preparationGift
                : string.Empty;
            string relicChoice = IsInfinitePreparationRelic(preparationRelic)
                ? preparationRelic
                : string.Empty;
            string summary = storedLoadout.ChoiceSummary ?? string.Empty;
            if (!string.IsNullOrEmpty(gift))
            {
                summary = string.IsNullOrEmpty(summary) ? gift : summary + " / " + gift;
            }

            return new GreedLastRunRecord(
                true,
                storedLoadout.Score,
                storedLoadout.Health,
                storedLoadout.Combo,
                storedLoadout.Focus,
                storedLoadout.MaxCombo,
                storedLoadout.SuccessCount,
                storedLoadout.GoodCount,
                storedLoadout.MissCount,
                storedLoadout.Distance,
                storedLoadout.LoadoutName,
                storedLoadout.StartGift,
                storedLoadout.PrimaryGift,
                relicChoice,
                summary,
                storedLoadout.TimingProfile);
        }

        private static bool IsInfinitePreparationGift(string gift)
        {
            return gift == "집중 축" || gift == "생존 축" || gift == "연쇄 축";
        }

        private static bool IsInfinitePreparationRelic(string relicChoice)
        {
            return relicChoice == "황금 박동"
                || relicChoice == "사막의 잔상"
                || relicChoice == "파라오의 각인";
        }

        public static string BuildInfiniteLoadoutBonusBreakdown(GreedLastRunRecord loadoutRecord)
        {
            GreedLastInfiniteLoadoutBonus bonus = CalculateInfiniteLoadoutBonus(loadoutRecord);
            if (!loadoutRecord.IsValid || !bonus.HasAnyBonus)
            {
                return "보너스 상세: 기본 체력 / 기본 집중 / 콤보 0 / 점수 0";
            }

            string text = "보너스 상세: 체력 +" + bonus.HealthBonus
                + " / 집중 +" + bonus.FocusBonus
                + " / 콤보 +" + bonus.ComboBonus
                + " / 점수 +" + bonus.ScoreBonus;
            string reason = BuildInfiniteLoadoutBonusReason(loadoutRecord);
            return string.IsNullOrEmpty(reason)
                ? text
                : text + "\n근거: " + reason;
        }

        public static string BuildInfiniteLoadoutBonusGrade(GreedLastRunRecord loadoutRecord)
        {
            GreedLastInfiniteLoadoutBonus bonus = CalculateInfiniteLoadoutBonus(loadoutRecord);
            if (!loadoutRecord.IsValid || !bonus.HasAnyBonus)
            {
                return "보너스 기본";
            }

            int value = bonus.HealthBonus * 2
                + bonus.FocusBonus
                + bonus.ComboBonus
                + Mathf.Clamp(bonus.ScoreBonus / 80, 0, 4);
            if (value >= 8)
            {
                return "보너스 S";
            }

            if (value >= 5)
            {
                return "보너스 A";
            }

            if (value >= 3)
            {
                return "보너스 B";
            }

            return "보너스 C";
        }

        public static string BuildInfiniteLoadoutStartStatePreview(
            GreedLastRunRecord loadoutRecord,
            int survivalLevel,
            int focusLevel,
            int timingLevel)
        {
            GreedLastInfiniteLoadoutBonus bonus = CalculateInfiniteLoadoutBonus(loadoutRecord);
            int safeSurvivalLevel = Mathf.Clamp(survivalLevel, 0, MaxGrowthLevel);
            int safeFocusLevel = Mathf.Clamp(focusLevel, 0, MaxGrowthLevel);
            int safeTimingLevel = Mathf.Clamp(timingLevel, 0, MaxGrowthLevel);
            int baseHealth = MaxHealth + safeSurvivalLevel;
            int healthCap = baseHealth + 2;
            int startHealth = Mathf.Clamp(baseHealth + bonus.HealthBonus, 1, healthCap);
            int startMaxHealth = Mathf.Max(baseHealth, startHealth);
            int totalStartFocus = Mathf.Clamp(safeFocusLevel, 0, MaxFocus) + bonus.FocusBonus;
            bool startsInFocusState = totalStartFocus >= MaxFocus;
            int startFocus = startsInFocusState ? 0 : Mathf.Clamp(totalStartFocus, 0, MaxFocus);
            float startFocusDuration = safeFocusLevel >= MaxFocus && bonus.HasAnyBonus
                ? Mathf.Min(FocusStateMaxDurationSeconds, FocusStateDurationSeconds + FocusStateSuccessExtensionSeconds)
                : FocusStateDurationSeconds;
            int startCombo = Mathf.Clamp(bonus.ComboBonus, 0, 9);
            string text = "시작 상태: 체력 " + startHealth + "/" + startMaxHealth
                + (startsInFocusState
                    ? " / 집중 상태 " + startFocusDuration.ToString("0.0") + "초"
                    : " / 집중 " + startFocus + "/" + MaxFocus)
                + " / 콤보 " + startCombo
                + " / 점수 " + bonus.ScoreBonus;
            string routePreview = BuildInfiniteLoadoutRouteStartPreview(loadoutRecord);
            if (!string.IsNullOrEmpty(routePreview))
            {
                text += "\n무한 루트: " + routePreview;
            }

            if (safeTimingLevel > 0)
            {
                text += "\n판정 보정: 정확 +" + (safeTimingLevel * 5)
                    + "ms / 보정 +" + (safeTimingLevel * 12) + "ms";
            }

            return text;
        }

        private static string BuildInfiniteLoadoutRouteStartPreview(GreedLastRunRecord loadoutRecord)
        {
            string route = BuildInfiniteLoadoutRouteProfile(loadoutRecord.ChoiceSummary);
            switch (route)
            {
                case "압박 루트":
                    return "압박 태그와 클러치 패턴이 가끔 강화";
                case "변주 루트":
                    return "엇박 패턴이 가끔 섞임";
                case "안정 루트":
                    return "정박 패턴으로 안정 진입";
                case "혼합 루트":
                    return "혼합 성향 / 기본 위협 중심";
                default:
                    return string.Empty;
            }
        }

        private string ApplyInfiniteLoadoutBonus(GreedLastRunRecord loadoutRecord)
        {
            GreedLastInfiniteLoadoutBonus bonus = CalculateInfiniteLoadoutBonus(loadoutRecord);
            if (!loadoutRecord.IsValid || !bonus.HasAnyBonus)
            {
                return "무한 보너스: 기본 진입";
            }

            health = Mathf.Clamp(health + bonus.HealthBonus, 1, BuildBonusHealthCap());
            maxHealthThisRun = Mathf.Max(maxHealthThisRun, health);
            GainFocus(bonus.FocusBonus, extendActiveState: true, out _, out _);
            combo = Mathf.Clamp(combo + bonus.ComboBonus, 0, 9);
            maxCombo = Mathf.Max(maxCombo, combo);
            AddScore(bonus.ScoreBonus);
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
            long judgementTotal = (long)loadoutRecord.SuccessCount + loadoutRecord.GoodCount + loadoutRecord.MissCount;

            AccumulateLoadoutKeywordBonus(loadoutRecord.StartGift, ref healthBonus, ref focusBonus, ref comboBonus, ref scoreBonus);
            int survivalGiftStacks = CountChoiceOccurrences(loadoutRecord.ChoiceSummary, "생존 축");
            int focusGiftStacks = CountChoiceOccurrences(loadoutRecord.ChoiceSummary, "집중 축");
            int chainGiftStacks = CountChoiceOccurrences(loadoutRecord.ChoiceSummary, "연쇄 축");
            if (survivalGiftStacks + focusGiftStacks + chainGiftStacks > 0)
            {
                healthBonus += Mathf.Min(2, survivalGiftStacks);
                focusBonus += focusGiftStacks > 0 ? Mathf.Min(MaxFocus, focusGiftStacks + 1) : 0;
                comboBonus += Mathf.Min(5, chainGiftStacks * 2);
                scoreBonus += Mathf.Min(180, chainGiftStacks * 60);
            }
            else
            {
                AccumulateLoadoutKeywordBonus(loadoutRecord.PrimaryGift, ref healthBonus, ref focusBonus, ref comboBonus, ref scoreBonus);
            }

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

            if (loadoutRecord.MaxCombo >= 6)
            {
                comboBonus += 1;
            }

            if (loadoutRecord.MaxCombo >= 10)
            {
                scoreBonus += 60;
            }

            if (judgementTotal > 0 && loadoutRecord.MissCount <= 1 && loadoutRecord.SuccessCount >= (long)loadoutRecord.GoodCount + loadoutRecord.MissCount)
            {
                focusBonus += 1;
                scoreBonus += 40;
            }

            int precisionStreak = ExtractPrecisionStreak(loadoutRecord.TimingProfile);
            if (precisionStreak >= 3)
            {
                focusBonus += 1;
                scoreBonus += (int)Math.Max(36L, Math.Min(80L, (long)precisionStreak * 12));
            }

            if (precisionStreak >= 5)
            {
                comboBonus += 1;
                scoreBonus += 40;
            }

            if (loadoutRecord.MissCount >= 3)
            {
                scoreBonus = Mathf.Max(0, scoreBonus - 40);
            }

            return new GreedLastInfiniteLoadoutBonus(
                Mathf.Clamp(healthBonus, 0, 2),
                Mathf.Clamp(focusBonus, 0, MaxFocus),
                Mathf.Clamp(comboBonus, 0, 5),
                Mathf.Clamp(scoreBonus, 0, 300));
        }

        private static string BuildInfiniteLoadoutBonusReason(GreedLastRunRecord loadoutRecord)
        {
            if (!loadoutRecord.IsValid)
            {
                return string.Empty;
            }

            string reason = string.Empty;
            int survivalGiftStacks = CountChoiceOccurrences(loadoutRecord.ChoiceSummary, "생존 축");
            int focusGiftStacks = CountChoiceOccurrences(loadoutRecord.ChoiceSummary, "집중 축");
            int chainGiftStacks = CountChoiceOccurrences(loadoutRecord.ChoiceSummary, "연쇄 축");
            AppendBonusReason(
                ref reason,
                loadoutRecord.Health >= 4 || survivalGiftStacks > 0 || ContainsChoice(loadoutRecord.PrimaryGift, "생존 축"),
                survivalGiftStacks > 1 ? "생존 x" + survivalGiftStacks : "생존");
            AppendBonusReason(
                ref reason,
                loadoutRecord.Focus >= MaxFocus || focusGiftStacks > 0 || ContainsChoice(loadoutRecord.PrimaryGift, "집중 축"),
                focusGiftStacks > 1 ? "집중 x" + focusGiftStacks : "집중");
            AppendBonusReason(
                ref reason,
                loadoutRecord.MaxCombo >= 6 || chainGiftStacks > 0 || ContainsChoice(loadoutRecord.PrimaryGift, "연쇄 축"),
                chainGiftStacks > 1 ? "연쇄 x" + chainGiftStacks : "연쇄");
            AppendBonusReason(ref reason, loadoutRecord.MissCount <= 1 && loadoutRecord.SuccessCount > 0, "정확도");
            AppendBonusReason(ref reason, ExtractPrecisionStreak(loadoutRecord.TimingProfile) >= 3, "정박");
            AppendBonusReason(ref reason, loadoutRecord.Score >= 700 || ContainsChoice(loadoutRecord.StartGift, "고점 본능"), "고점");
            return reason;
        }

        private static void AppendBonusReason(ref string reason, bool condition, string label)
        {
            if (!condition)
            {
                return;
            }

            reason = string.IsNullOrEmpty(reason) ? label : reason + " / " + label;
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

        private static int CountChoiceOccurrences(string text, string choice)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(choice))
            {
                return 0;
            }

            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int nextIndex = text.IndexOf(choice, index, StringComparison.Ordinal);
                if (nextIndex < 0)
                {
                    break;
                }

                count += 1;
                index = nextIndex + choice.Length;
            }

            return count;
        }

        private static int ExtractPrecisionStreak(string timingProfile)
        {
            if (string.IsNullOrWhiteSpace(timingProfile))
            {
                return 0;
            }

            string normalized = timingProfile.Replace('\r', ' ').Replace('\n', ' ').Trim();
            string prefix = "정박 최고 ";
            int start = normalized.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0)
            {
                return 0;
            }

            start += prefix.Length;
            int end = start;
            while (end < normalized.Length && char.IsDigit(normalized[end]))
            {
                end += 1;
            }

            if (end <= start)
            {
                return 0;
            }

            return int.TryParse(normalized.Substring(start, end - start), out int value)
                ? Mathf.Max(0, value)
                : 0;
        }

        private static bool HasChoice(string selected, string expected)
        {
            return string.Equals(selected, expected, StringComparison.Ordinal);
        }

        private bool HasActiveRoute(string routeName)
        {
            if (HasChoice(lastNode, routeName))
            {
                return true;
            }

            return infiniteMode
                && !string.IsNullOrEmpty(activeInfiniteLoadoutDisplayProfile)
                && activeInfiniteLoadoutDisplayProfile.Contains(routeName, StringComparison.Ordinal);
        }

        private static bool IsRecoveryPattern(GreedLastPatternModel pattern)
        {
            return !string.IsNullOrEmpty(pattern.Prompt)
                && pattern.Prompt.Contains("[복구]", StringComparison.Ordinal);
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

        private string BuildCurrentInfiniteLoadoutProfile()
        {
            if (!string.IsNullOrEmpty(activeInfiniteLoadoutDisplayProfile))
            {
                return activeInfiniteLoadoutDisplayProfile;
            }

            string profile = BuildActiveChoiceProfile();
            return string.IsNullOrEmpty(profile) || profile == "루트 미정" || profile == "저장 조합"
                ? string.Empty
                : profile;
        }

        private string BuildCurrentActiveRouteProfile()
        {
            if (!infiniteMode)
            {
                return lastNode;
            }

            if (activeInfiniteLoadoutDisplayProfile.Contains("안정 루트", StringComparison.Ordinal))
            {
                return "안정 루트";
            }

            if (activeInfiniteLoadoutDisplayProfile.Contains("변주 루트", StringComparison.Ordinal))
            {
                return "변주 루트";
            }

            return activeInfiniteLoadoutDisplayProfile.Contains("압박 루트", StringComparison.Ordinal)
                ? "압박 루트"
                : string.Empty;
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
            bool patternBundleCompleted = chapterProgress >= ChapterTargetPatterns;
            string patternBundleRewardText = patternBundleCompleted && !practiceMode
                ? ApplyCompletedPatternBundleReward()
                : string.Empty;
            string precisionPayoffText = patternBundleCompleted
                ? BuildCompletedPatternPrecisionPayoffText()
                : string.Empty;
            if (patternBundleCompleted && !practiceMode && !infiniteMode)
            {
                CaptureCompletedSegmentPerformance();
                ResetCurrentBundlePerformance();
            }

            if (practiceMode && patternBundleCompleted)
            {
                autoFlow = false;
                stopped = true;
                activePattern = false;
                inputConsumed = true;
                saveEligible = false;
                phase = GreedLastRunPhase.ClearResolving;
                judgementText = ResolvePracticeOpeningStep(lastPracticeOpeningContext) > 0
                    ? "복기 연습 완료 - 남은 대응 확인"
                    : "기초연습 완료 - 3축 대응 확인";
                judgementText += "\n정확 " + successCount + " / 보정 " + goodCount + " / 실수 " + missCount
                    + (string.IsNullOrEmpty(precisionPayoffText) ? string.Empty : "\n" + precisionPayoffText)
                    + (string.IsNullOrEmpty(patternBundleRewardText) ? string.Empty : "\n" + patternBundleRewardText)
                    + "\n" + BuildPracticeCompletionReviewText()
                    + "\n"
                    + (successCount >= ChapterTargetPatterns && goodCount == 0 && missCount == 0
                        ? "일반 런으로 바로 이어가거나 3축을 다시 확인하세요."
                        : "다시 연습하거나 연습을 마친 뒤 일반 런을 시작하세요.");
                ResetCurrentBundlePerformance();
                return;
            }

            if (infiniteMode)
            {
                bool threatRose = false;
                if (patternBundleCompleted)
                {
                    if (!string.IsNullOrEmpty(patternBundleRewardText))
                    {
                        judgementText += "\n" + patternBundleRewardText;
                    }

                    int previousThreatLevel = GetInfiniteThreatLevel();
                    chapterProgress = 0;
                    infiniteSectionsCleared += 1;
                    CaptureCompletedInfiniteSectionPerformance();
                    int currentThreatLevel = GetInfiniteThreatLevel();
                    maxInfiniteThreatLevel = Mathf.Max(maxInfiniteThreatLevel, currentThreatLevel);
                    threatRose = currentThreatLevel > previousThreatLevel;
                    int sectionBonus = AddScore(50 * currentThreatLevel, trackJudgementLimit: true);
                    judgementText += "\n무한 구간 유지 보너스 +" + sectionBonus
                        + " / 위협 " + currentThreatLevel
                        + "\n" + BuildCompletedBundlePerformanceText();
                    if (threatRose)
                    {
                        judgementText += "\n위협 상승 - " + BuildInfiniteThreatChangeSummary()
                            + "\n짧은 호흡 뒤 함정 속도와 판정 압박 증가";
                        infiniteThreatBreatherPatternPending = true;
                    }

                    ResetCurrentBundlePerformance();
                }

                phase = GreedLastRunPhase.RunPlaying;
                autoFlow = true;
                nextPatternAt = now + BuildNextPatternGapSeconds() + (threatRose ? InfiniteThreatRiseBreatherSeconds : 0f);
                return;
            }

            if (!patternBundleCompleted)
            {
                nextPatternAt = now + BuildNextPatternGapSeconds();
                return;
            }

            autoFlow = false;
            chapterProgress = 0;

            if (chapterIndex <= 3)
            {
                phase = GreedLastRunPhase.ChapterWrapUp;
                judgementText = $"챕터 {chapterIndex} 종료 - 선택으로 방향을 정리합니다."
                    + (string.IsNullOrEmpty(precisionPayoffText) ? string.Empty : "\n" + precisionPayoffText)
                    + (string.IsNullOrEmpty(patternBundleRewardText) ? string.Empty : "\n" + patternBundleRewardText);
                OpenChoice(GreedLastChoiceKind.Node);
                return;
            }

            if (!coreRetrieved)
            {
                phase = GreedLastRunPhase.CoreRetrieve;
                coreRetrieved = true;
                phase = GreedLastRunPhase.EscapeRunning;
                PrepareStartCountdown(
                    now,
                    "공명핵 회수 - 탈출 검증 준비\n카운트 뒤 마지막 구간이 시작됩니다."
                        + (string.IsNullOrEmpty(precisionPayoffText) ? string.Empty : "\n" + precisionPayoffText)
                        + (string.IsNullOrEmpty(patternBundleRewardText) ? string.Empty : "\n" + patternBundleRewardText));
                return;
            }

            escapeSucceeded = true;
            saveEligible = true;
            stopped = true;
            autoFlow = false;
            phase = GreedLastRunPhase.ClearResolving;
            judgementText = "탈출 성공 - 일반 런 클리어\n저장 가능한 기프트 방향이 만들어졌습니다."
                + (string.IsNullOrEmpty(precisionPayoffText) ? string.Empty : "\n" + precisionPayoffText)
                + (string.IsNullOrEmpty(patternBundleRewardText) ? string.Empty : "\n" + patternBundleRewardText);
        }

        private string BuildPracticeCompletionReviewText()
        {
            string review;
            if (missCount > 0)
            {
                review = "복기 우선: 실수 " + missCount + "회 / 같은 채널과 입력 순간 재확인";
            }
            else if (goodCount > 0)
            {
                review = "복기 우선: 보정 " + goodCount + "회 / 판정선 중앙 재확인";
            }
            else
            {
                review = ResolvePracticeOpeningStep(lastPracticeOpeningContext) > 0
                    ? "복기 확인: " + successCount + "회 정확 / 일반 런 진입 가능"
                    : "준비 완료: 3축 정확 / 일반 런 진입 가능";
            }

            return review
                + "\n" + BuildPracticeAvoidanceReviewText()
                + "\n" + BuildPracticeTimingCalibrationText();
        }

        private string BuildPracticeAvoidanceReviewText()
        {
            string[] labels = { string.Empty, "바닥→↑점프", "상단→↓숙이기", "측면→↔비틀기" };
            for (int i = 1; i < labels.Length; i += 1)
            {
                if (avoidanceMissCounts[i] > 0)
                {
                    return "회피 복기: " + labels[i] + " 실패 / " + i + "단계 다시";
                }
            }

            for (int i = 1; i < labels.Length; i += 1)
            {
                if (avoidanceGoodCounts[i] > 0)
                {
                    return "회피 복기: " + labels[i] + " 보정 / " + i + "단계 다시";
                }
            }

            string exactAxes = string.Empty;
            for (int i = 1; i < labels.Length; i += 1)
            {
                if (avoidanceExactCounts[i] <= 0) continue;
                if (exactAxes.Length > 0) exactAxes += " · ";
                exactAxes += labels[i];
            }
            return exactAxes.Length > 0
                ? "회피 동작: " + exactAxes + " 정확"
                : "회피 동작: 확인한 정확 대응 없음";
        }

        private string BuildPracticeTimingCalibrationText()
        {
            if (!HasTimingRecommendation)
            {
                return "입력 보정 샘플 " + timingSampleCount + "/" + TimingRecommendationMinSamples
                    + " / 정확·Good 입력으로 보완";
            }

            int currentMilliseconds = Mathf.RoundToInt(inputTimingOffsetSeconds * 1000f);
            int recommendationMilliseconds = TimingRecommendationMilliseconds;
            if (Mathf.Abs(currentMilliseconds - recommendationMilliseconds) < 25)
            {
                return "입력 보정: 현재 설정 안정 " + FormatOffset(inputTimingOffsetSeconds);
            }

            return "입력 보정 제안: " + FormatOffset(recommendationMilliseconds / 1000f)
                + " / 오른쪽 추천 적용";
        }

        private string BuildCompletedPatternPrecisionPayoffText()
        {
            if (lastResult != GreedLastJudgementResult.Success || !lastCenterHit || !currentPattern.IsValid)
            {
                return string.Empty;
            }

            string text = string.Empty;
            if (IsRecoveryPattern(currentPattern))
            {
                text = "복구 정박 - 흐름 재점화 / 보너스 +" + RecoveryPrecisionScoreBonus
                    + "점 / 콤보 +" + RecoveryPrecisionComboBonus;
            }

            string timingPayoff;
            switch (currentPattern.TimingType)
            {
                case GreedLastTimingType.Perfect:
                    timingPayoff = "정박 중앙 - 집중 충전 / " + (lastJudgementStartedInFocusState
                        ? "집중 상태 유지" : "집중 +" + PerfectFocusChargeBonus);
                    break;
                case GreedLastTimingType.Clutch:
                    timingPayoff = "압박 정박 - 반격 / 보너스 +" + ClutchCounterScoreBonus + "점";
                    break;
                case GreedLastTimingType.Offbeat:
                    timingPayoff = "엇박 정박 - 연쇄 가속 / 콤보 +" + OffbeatFlowComboBonus;
                    break;
                default:
                    timingPayoff = string.Empty;
                    break;
            }

            string routePayoff = string.Empty;
            if (currentPattern.TimingType == GreedLastTimingType.Offbeat
                && HasActiveRoute("변주 루트"))
            {
                routePayoff = "변주 루트 - 엇박 공명 / " + (lastJudgementStartedInFocusState
                    ? "집중 상태 유지" : "집중 +" + VariationRouteOffbeatFocusBonus);
            }
            else if (currentPattern.TimingType == GreedLastTimingType.Clutch
                && HasActiveRoute("압박 루트"))
            {
                routePayoff = "압박 루트 - 클러치 증폭 / 보너스 +" + PressureRouteClutchScoreBonus + "점";
            }

            if (string.IsNullOrEmpty(timingPayoff))
            {
                return string.IsNullOrEmpty(text) ? routePayoff : text + "\n" + routePayoff;
            }

            text = string.IsNullOrEmpty(text) ? timingPayoff : text + "\n" + timingPayoff;
            return string.IsNullOrEmpty(routePayoff) ? text : text + "\n" + routePayoff;
        }

        private float BuildNextPatternGapSeconds()
        {
            float gap;
            if (infiniteMode)
            {
                gap = Mathf.Lerp(
                    InfiniteBaseGapSeconds,
                    0.36f,
                    Mathf.Clamp01((GetInfiniteThreatLevel() - 1) / 4f));
            }
            else if (phase == GreedLastRunPhase.EscapeRunning)
            {
                gap = EscapeGapSeconds;
            }
            else
            {
                switch (Mathf.Clamp(chapterIndex, 1, 4))
                {
                    case 1:
                        gap = ChapterOneGapSeconds;
                        break;
                    case 2:
                        gap = ChapterTwoGapSeconds;
                        break;
                    case 3:
                        gap = ChapterThreeGapSeconds;
                        break;
                    default:
                        gap = ChapterFourGapSeconds;
                        break;
                }
            }

            if (HasChoice(lastNode, "안정 루트"))
            {
                gap += 0.12f;
            }
            else if (HasChoice(lastNode, "압박 루트"))
            {
                gap -= 0.08f;
            }
            else if (HasChoice(lastNode, "변주 루트"))
            {
                gap -= 0.03f;
            }

            if (recoveryBreatherPatternPending)
            {
                gap += 0.20f;
            }

            return Mathf.Clamp(gap, 0.28f, 1.05f);
        }

        private string ApplyCompletedPatternBundleReward()
        {
            bool cleanBundle = patternBundleBreakCount <= 0;
            patternBundleBreakCount = 0;
            if (!cleanBundle)
            {
                return string.Empty;
            }

            const int allAvoidanceMask = (1 << (int)GreedLastAvoidanceType.Jump)
                | (1 << (int)GreedLastAvoidanceType.Slide)
                | (1 << (int)GreedLastAvoidanceType.Twist);
            bool threeActionExact = (currentBundleAvoidanceMask & allAvoidanceMask) == allAvoidanceMask;
            if (threeActionExact)
            {
                threeActionExactBundleCount += 1;
            }

            if (HasActiveRoute("안정 루트"))
            {
                stableRouteBundleCount += 1;
                if (health < BuildBonusHealthCap())
                {
                    int healthGain = Mathf.Min(
                        StableRouteBundleHealthBonus,
                        BuildBonusHealthCap() - health);
                    health += healthGain;
                    maxHealthThisRun = Mathf.Max(maxHealthThisRun, health);
                    return AppendThreeActionExactText("패턴 묶음 완벽 - 안정 완주 / 체력 +" + healthGain, threeActionExact);
                }

                return AppendThreeActionExactText(
                    "패턴 묶음 완벽 - 안정 완주 / "
                        + ApplyRouteBundleFocusReward(1, FocusStateBundleExtensionSeconds),
                    threeActionExact);
            }

            if (HasActiveRoute("변주 루트"))
            {
                variationRouteBundleCount += 1;
                return AppendThreeActionExactText(
                    "패턴 묶음 완벽 - 변주 완주 / "
                        + ApplyRouteBundleFocusReward(
                            VariationRouteBundleFocusBonus,
                            VariationRouteBundleExtensionSeconds),
                    threeActionExact);
            }

            if (HasActiveRoute("압박 루트"))
            {
                pressureRouteBundleCount += 1;
                int awarded = AddScore(PressureRouteBundleScoreBonus, trackJudgementLimit: true);
                return AppendThreeActionExactText(
                    "패턴 묶음 완벽 - 압박 완주 / 점수 +" + awarded
                        + " / " + ApplyRouteBundleFocusReward(1, FocusStateBundleExtensionSeconds),
                    threeActionExact);
            }

            if (IsFocusStateActive)
            {
                float previous = focusStateRemainingSeconds;
                focusStateRemainingSeconds = Mathf.Min(
                    FocusStateMaxDurationSeconds,
                    focusStateRemainingSeconds + FocusStateBundleExtensionSeconds);
                float gained = Mathf.Max(0f, focusStateRemainingSeconds - previous);
                return AppendThreeActionExactText(
                    "패턴 묶음 완벽 - 집중 상태 +" + gained.ToString("0.0") + "초",
                    threeActionExact);
            }

            GainFocus(1, extendActiveState: false, out bool stateActivated, out _);
            string baseText = stateActivated
                ? "패턴 묶음 완벽 - 집중 상태 발동"
                : "패턴 묶음 완벽 - 집중 +1";
            return AppendThreeActionExactText(baseText, threeActionExact);
        }

        private static string AppendThreeActionExactText(string baseText, bool achieved)
        {
            return achieved ? baseText + " / 3동작 정확" : baseText;
        }

        private string ApplyRouteBundleFocusReward(int amount, float activeExtensionSeconds)
        {
            if (IsFocusStateActive)
            {
                float previous = focusStateRemainingSeconds;
                focusStateRemainingSeconds = Mathf.Min(
                    FocusStateMaxDurationSeconds,
                    focusStateRemainingSeconds + Mathf.Max(0f, activeExtensionSeconds));
                float gained = Mathf.Max(0f, focusStateRemainingSeconds - previous);
                return "집중 상태 +" + gained.ToString("0.0") + "초";
            }

            GainFocus(
                Mathf.Max(0, amount),
                extendActiveState: false,
                out bool stateActivated,
                out _);
            return stateActivated
                ? "집중 상태 발동"
                : "집중 +" + Mathf.Max(0, amount);
        }

        private void StartPattern(float now)
        {
            bool useInfiniteThreatBreather = infiniteThreatBreatherPatternPending;
            bool useRecoveryBreather = recoveryBreatherPatternPending;
            string previousMissReason = missReason;
            GreedLastPatternModel selectedPattern = SelectPatternFromDeck();
            currentPattern = BuildActivePattern(selectedPattern, useInfiniteThreatBreather, useRecoveryBreather);
            string recoveryCue = BuildRecoveryPatternCue(previousMissReason, currentPattern);
            string routeCue = BuildRoutePatternCue(currentPattern);
            RememberSelectedPattern(selectedPattern);
            infiniteThreatBreatherPatternPending = false;
            recoveryBreatherPatternPending = false;
            maxInfiniteThreatLevel = Mathf.Max(maxInfiniteThreatLevel, GetInfiniteThreatLevel());
            patternIndex += 1;
            patternStartedAt = now;
            activePattern = true;
            inputConsumed = false;
            ignoreReason = string.Empty;
            missReason = string.Empty;
            judgementText = IsRotationPattern(currentPattern)
                ? "회전 통로를 보고 Q/E 또는 같은 방향 채널을 누르세요."
                : "예고를 보고 같은 채널을 누르세요.";
            if (!string.IsNullOrEmpty(recoveryCue))
            {
                judgementText += "\n" + recoveryCue;
            }

            if (!string.IsNullOrEmpty(routeCue))
            {
                judgementText += "\n" + routeCue;
            }

            lastResult = GreedLastJudgementResult.None;
            Publish(now);
        }

        private GreedLastPatternModel SelectPatternFromDeck()
        {
            if (devNextPatternOverride.IsValid)
            {
                GreedLastPatternModel overridePattern = devNextPatternOverride;
                devNextPatternOverride = GreedLastPatternModel.Empty;
                return overridePattern;
            }

            int deckIndex = patternIndex;
            if (infiniteMode)
            {
                deckIndex += infiniteSectionsCleared + GetInfiniteThreatLevel();
                deckIndex = ApplyInfiniteLoadoutRoutePatternBias(deckIndex);
                deckIndex = ApplyRecoveryPatternBias(deckIndex);
            }
            else
            {
                deckIndex += Mathf.Max(0, chapterIndex - 1) * 2;
                if (phase == GreedLastRunPhase.EscapeRunning)
                {
                    deckIndex += 7;
                }

                deckIndex = ApplyRoutePatternBias(deckIndex);
                deckIndex = ApplyRecoveryPatternBias(deckIndex);
            }

            return patternDeck[FindReadablePatternIndex(deckIndex)];
        }

        private GreedLastPatternModel FindPatternById(string patternId)
        {
            if (string.IsNullOrWhiteSpace(patternId))
            {
                return GreedLastPatternModel.Empty;
            }

            for (int i = 0; i < patternDeck.Length; i += 1)
            {
                if (string.Equals(patternDeck[i].PatternId, patternId, StringComparison.Ordinal))
                {
                    return patternDeck[i];
                }
            }

            return GreedLastPatternModel.Empty;
        }

        private int ApplyRecoveryPatternBias(int deckIndex)
        {
            switch (missReason)
            {
                case "wrong_channel":
                    return FindPatternIndexAvoidingChannel(deckIndex + 1, currentPattern.Channel);
                case "too_early":
                    return FindPatternIndexByTiming(deckIndex + 1, GreedLastTimingType.Clutch);
                case "too_late":
                    return FindPatternIndexByTiming(deckIndex, GreedLastTimingType.Perfect);
                case "no_input":
                case "focus_guard":
                    return FindPatternIndexAvoidingTiming(deckIndex, GreedLastTimingType.Offbeat);
                default:
                    return deckIndex;
            }
        }

        private int ApplyInfiniteLoadoutRoutePatternBias(int deckIndex)
        {
            if (!infiniteMode || string.IsNullOrEmpty(activeInfiniteLoadoutDisplayProfile))
            {
                return deckIndex;
            }

            int seed = Mathf.Abs(patternIndex + infiniteSectionsCleared * 3 + combo + GetInfiniteThreatLevel());
            if (activeInfiniteLoadoutDisplayProfile.Contains("변주", StringComparison.Ordinal))
            {
                return seed % 3 == 0
                    ? FindPatternIndexByTiming(deckIndex + 1, GreedLastTimingType.Offbeat)
                    : deckIndex;
            }

            if (activeInfiniteLoadoutDisplayProfile.Contains("압박", StringComparison.Ordinal))
            {
                return seed % 3 == 0
                    ? FindPatternIndexByTiming(deckIndex + 1, GreedLastTimingType.Clutch)
                    : deckIndex;
            }

            if (activeInfiniteLoadoutDisplayProfile.Contains("안정", StringComparison.Ordinal))
            {
                return seed % 3 == 0
                    ? FindPatternIndexByTiming(deckIndex, GreedLastTimingType.Perfect)
                    : deckIndex;
            }

            return deckIndex;
        }

        private static string BuildRecoveryPatternCue(string previousMissReason, GreedLastPatternModel nextPattern)
        {
            string targetText = nextPattern.IsValid
                ? " / 다음 " + FormatChannel(nextPattern.Channel) + " " + FormatTimingType(nextPattern.TimingType)
                : string.Empty;
            switch (previousMissReason)
            {
                case "wrong_channel":
                    return "복구 예고: 직전 채널 오류 - 목표 레인 확인" + targetText;
                case "too_early":
                    return "복구 예고: 너무 빨랐음 - 판정선까지 대기" + targetText;
                case "too_late":
                    return "복구 예고: 너무 늦었음 - 예고 직후 빠르게 준비" + targetText;
                case "no_input":
                    return "복구 예고: 입력 없음 - 밝아진 레인 먼저 확인" + targetText;
                case "focus_guard":
                    return "복구 예고: 집중 보호 후 안정 패턴" + targetText;
                default:
                    return string.Empty;
            }
        }

        private string BuildRoutePatternCue(GreedLastPatternModel pattern)
        {
            string prompt = pattern.Prompt ?? string.Empty;
            if (prompt.Contains("[복구]", StringComparison.Ordinal))
            {
                return "루트 예고: 복구 - 판정 여유";
            }

            if (prompt.Contains("[압박]", StringComparison.Ordinal))
            {
                return "루트 예고: 압박 - Clutch 정박 +" + PressureRouteClutchScoreBonus + "점 / 판정 좁음";
            }

            if (prompt.Contains("[변주]", StringComparison.Ordinal))
            {
                return IsFocusStateActive
                    ? "루트 예고: 변주 - 엇박 정박 / " + BuildChoiceFocusRewardPreview(focus, focusStateRemainingSeconds, 1)
                    : "루트 예고: 변주 - 엇박 정박 집중 +" + VariationRouteOffbeatFocusBonus;
            }

            if (prompt.Contains("[안정]", StringComparison.Ordinal))
            {
                int gain = BuildGoodHealthGainPreview();
                return gain > 0 ? "루트 예고: 안정 - Good 체력 +" + gain + " / 판정 여유"
                    : "루트 예고: 안정 - 회복 상한 / 판정 여유";
            }

            if (prompt.Contains("[유예]", StringComparison.Ordinal))
            {
                return "루트 예고: 유예 - 조금 대기";
            }

            if (prompt.Contains("[탈출]", StringComparison.Ordinal))
            {
                return "루트 예고: 탈출 - 마지막 압박";
            }

            if (prompt.Contains("[위기]", StringComparison.Ordinal))
            {
                return "루트 예고: 위기 - 빠른 판단";
            }

            if (prompt.Contains("[급박]", StringComparison.Ordinal))
            {
                return "루트 예고: 급박 - 조금 빠르게 준비";
            }

            if (prompt.Contains("[정밀]", StringComparison.Ordinal))
            {
                return "루트 예고: 정밀 - 판정선 중앙";
            }

            if (prompt.Contains("[연쇄]", StringComparison.Ordinal))
            {
                return "루트 예고: 연쇄 - 콤보 유지";
            }

            if (prompt.Contains("[심층]", StringComparison.Ordinal))
            {
                return "루트 예고: 심층 - 판정 압박";
            }

            return string.Empty;
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
                int candidate = NormalizePatternIndex(startIndex + i);
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
                int candidate = NormalizePatternIndex(startIndex + i);
                if (patternDeck[candidate].TimingType != timingType)
                {
                    return candidate;
                }
            }

            return startIndex;
        }

        private int FindPatternIndexAvoidingChannel(int startIndex, GreedLastRunChannel channel)
        {
            if (channel == GreedLastRunChannel.None)
            {
                return startIndex;
            }

            for (int i = 0; i < patternDeck.Length; i += 1)
            {
                int candidate = NormalizePatternIndex(startIndex + i);
                if (patternDeck[candidate].Channel != channel)
                {
                    return candidate;
                }
            }

            return startIndex;
        }

        private int FindReadablePatternIndex(int startIndex)
        {
            int bestIndex = NormalizePatternIndex(startIndex);
            int bestPenalty = int.MaxValue;

            for (int i = 0; i < patternDeck.Length; i += 1)
            {
                int candidate = NormalizePatternIndex(startIndex + i);
                int penalty = BuildPatternRepeatPenalty(patternDeck[candidate]);
                if (penalty <= 0)
                {
                    return candidate;
                }

                if (penalty < bestPenalty)
                {
                    bestPenalty = penalty;
                    bestIndex = candidate;
                }
            }

            return bestIndex;
        }

        private int NormalizePatternIndex(int index)
        {
            return ((index % patternDeck.Length) + patternDeck.Length) % patternDeck.Length;
        }

        private int BuildPatternRepeatPenalty(GreedLastPatternModel candidate)
        {
            if (!candidate.IsValid)
            {
                return 0;
            }

            int penalty = 0;
            GreedLastTimingType preferredTiming = BuildPreferredChapterTiming();
            if (preferredTiming != GreedLastTimingType.None
                && candidate.TimingType != preferredTiming)
            {
                penalty += 180;
            }

            GreedLastAvoidanceType preferredAvoidance = BuildPreferredAvoidanceType();
            if (preferredAvoidance != GreedLastAvoidanceType.None
                && candidate.AvoidanceType != preferredAvoidance)
            {
                penalty += 90;
            }

            if (IsSamePatternFamily(candidate.PatternId, lastSelectedPatternId))
            {
                penalty += 100;
            }

            if ((infiniteMode || phase == GreedLastRunPhase.EscapeRunning)
                && IsSamePatternFamily(candidate.PatternId, previousSelectedPatternId))
            {
                penalty += 60;
            }

            if (currentPattern.IsValid && candidate.Channel == currentPattern.Channel)
            {
                penalty += 40;
            }

            if (infiniteMode
                && GetInfiniteThreatLevel() >= 4
                && currentPattern.IsValid
                && candidate.TimingType == currentPattern.TimingType)
            {
                penalty += 35;
            }

            string familyKey = BuildPatternFamilyKey(candidate.PatternId);
            int count = Mathf.Min(recentPatternCount, RecentPatternMemory);
            for (int i = 0; i < count; i += 1)
            {
                int freshness = RecentPatternMemory - i;
                if (!string.IsNullOrEmpty(familyKey) && familyKey == recentPatternFamilyKeys[i])
                {
                    penalty += 9 * freshness;
                }

                if (candidate.Channel == recentPatternChannels[i])
                {
                    penalty += 4 * freshness;
                }

                if (candidate.TimingType == recentPatternTimingTypes[i])
                {
                    penalty += (infiniteMode ? 4 : 2) * freshness;
                }

                if (candidate.AvoidanceType != GreedLastAvoidanceType.None
                    && candidate.AvoidanceType == recentPatternAvoidanceTypes[i])
                {
                    penalty += (infiniteMode ? 4 : 3) * freshness;
                }
            }

            return penalty;
        }

        private GreedLastTimingType BuildPreferredChapterTiming()
        {
            if (practiceMode)
            {
                switch (chapterProgress % ChapterTargetPatterns)
                {
                    case 0:
                        return GreedLastTimingType.Perfect;
                    case 1:
                        return GreedLastTimingType.Clutch;
                    default:
                        return GreedLastTimingType.Offbeat;
                }
            }

            if (infiniteMode)
            {
                return GreedLastTimingType.None;
            }

            if (phase == GreedLastRunPhase.EscapeRunning)
            {
                switch (chapterProgress % 3)
                {
                    case 0:
                        return GreedLastTimingType.Clutch;
                    case 1:
                        return GreedLastTimingType.Offbeat;
                    default:
                        return GreedLastTimingType.Perfect;
                }
            }

            if (chapterIndex == 1)
            {
                switch (chapterProgress % 3)
                {
                    case 0:
                        return GreedLastTimingType.Perfect;
                    case 1:
                        return GreedLastTimingType.Clutch;
                    default:
                        return GreedLastTimingType.Offbeat;
                }
            }

            if (chapterIndex == 3 && chapterProgress % 3 == 1)
            {
                GreedLastTimingType relicTiming = BuildPreferredRelicTiming();
                if (relicTiming != GreedLastTimingType.None)
                {
                    return relicTiming;
                }
            }

            if (chapterIndex == 2 || chapterIndex == 3)
            {
                GreedLastTimingType routeTiming = BuildPreferredRouteTiming();
                if (routeTiming != GreedLastTimingType.None)
                {
                    return routeTiming;
                }
            }

            if (chapterIndex >= 4)
            {
                switch (chapterProgress % 3)
                {
                    case 0:
                        return GreedLastTimingType.Clutch;
                    case 1:
                        return GreedLastTimingType.Offbeat;
                    default:
                        return GreedLastTimingType.Perfect;
                }
            }

            return GreedLastTimingType.None;
        }

        private GreedLastAvoidanceType BuildPreferredAvoidanceType()
        {
            return GetAuthoredAvoidanceType(
                chapterIndex,
                chapterProgress,
                practiceMode,
                infiniteMode,
                phase == GreedLastRunPhase.EscapeRunning || chapterIndex >= 4,
                BuildCurrentActiveRouteProfile());
        }

        public static GreedLastAvoidanceType GetAuthoredAvoidanceType(
            int chapterIndex,
            int chapterProgress,
            bool practiceMode,
            bool infiniteMode,
            bool finalValidation,
            string activeRouteProfile)
        {
            int step = Mathf.Max(0, chapterProgress) % ChapterTargetPatterns;
            if (infiniteMode)
            {
                return GreedLastAvoidanceType.None;
            }

            if (practiceMode || chapterIndex <= 1)
            {
                switch (step)
                {
                    case 0:
                        return GreedLastAvoidanceType.Jump;
                    case 1:
                        return GreedLastAvoidanceType.Slide;
                    default:
                        return GreedLastAvoidanceType.Twist;
                }
            }

            if (finalValidation || chapterIndex >= 4)
            {
                switch (step)
                {
                    case 0:
                        return GreedLastAvoidanceType.Slide;
                    case 1:
                        return GreedLastAvoidanceType.Twist;
                    default:
                        return GreedLastAvoidanceType.Jump;
                }
            }

            if (string.Equals(activeRouteProfile, "변주 루트", StringComparison.Ordinal))
            {
                switch (step)
                {
                    case 0:
                        return GreedLastAvoidanceType.Twist;
                    case 1:
                        return GreedLastAvoidanceType.Slide;
                    default:
                        return GreedLastAvoidanceType.Jump;
                }
            }

            if (string.Equals(activeRouteProfile, "압박 루트", StringComparison.Ordinal))
            {
                switch (step)
                {
                    case 0:
                        return GreedLastAvoidanceType.Slide;
                    case 1:
                        return GreedLastAvoidanceType.Twist;
                    default:
                        return GreedLastAvoidanceType.Jump;
                }
            }

            switch (step)
            {
                case 0:
                    return GreedLastAvoidanceType.Jump;
                case 1:
                    return GreedLastAvoidanceType.Slide;
                default:
                    return GreedLastAvoidanceType.Twist;
            }
        }

        public static bool IsRotationPattern(GreedLastPatternModel pattern)
        {
            if (!pattern.IsValid)
            {
                return false;
            }

            string patternId = pattern.PatternId ?? string.Empty;
            if (patternId.IndexOf("rotate", StringComparison.OrdinalIgnoreCase) >= 0
                || patternId.IndexOf("tilt", StringComparison.OrdinalIgnoreCase) >= 0
                || patternId.IndexOf("corridor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string prompt = pattern.Prompt ?? string.Empty;
            return prompt.Contains("좌회전", StringComparison.Ordinal)
                || prompt.Contains("우회전", StringComparison.Ordinal)
                || prompt.Contains("회전 통로", StringComparison.Ordinal);
        }

        private GreedLastTimingType BuildPreferredRouteTiming()
        {
            int step = Mathf.Clamp(chapterProgress, 0, ChapterTargetPatterns - 1);
            if (HasChoice(lastNode, "안정 루트"))
            {
                return step == 1
                    ? GreedLastTimingType.Clutch
                    : GreedLastTimingType.Perfect;
            }

            if (HasChoice(lastNode, "변주 루트"))
            {
                return step == 1
                    ? GreedLastTimingType.Perfect
                    : GreedLastTimingType.Offbeat;
            }

            if (HasChoice(lastNode, "압박 루트"))
            {
                return step == 1
                    ? GreedLastTimingType.Offbeat
                    : GreedLastTimingType.Clutch;
            }

            return GreedLastTimingType.None;
        }

        private GreedLastTimingType BuildPreferredRelicTiming()
        {
            if (HasChoice(relic, "황금 박동"))
            {
                return GreedLastTimingType.Perfect;
            }

            if (HasChoice(relic, "사막의 잔상"))
            {
                return GreedLastTimingType.Offbeat;
            }

            return HasChoice(relic, "파라오의 각인")
                ? GreedLastTimingType.Clutch
                : GreedLastTimingType.None;
        }

        private void RememberSelectedPattern(GreedLastPatternModel selectedPattern)
        {
            if (!selectedPattern.IsValid)
            {
                return;
            }

            previousSelectedPatternId = lastSelectedPatternId;
            lastSelectedPatternId = selectedPattern.PatternId;
            RememberRecentPattern(selectedPattern);
        }

        private void RememberRecentPattern(GreedLastPatternModel selectedPattern)
        {
            cachedRecentPatternFlowText = null;
            for (int i = RecentPatternMemory - 1; i > 0; i -= 1)
            {
                recentPatternFamilyKeys[i] = recentPatternFamilyKeys[i - 1];
                recentPatternChannels[i] = recentPatternChannels[i - 1];
                recentPatternTimingTypes[i] = recentPatternTimingTypes[i - 1];
                recentPatternAvoidanceTypes[i] = recentPatternAvoidanceTypes[i - 1];
            }

            recentPatternFamilyKeys[0] = BuildPatternFamilyKey(selectedPattern.PatternId);
            recentPatternChannels[0] = selectedPattern.Channel;
            recentPatternTimingTypes[0] = selectedPattern.TimingType;
            recentPatternAvoidanceTypes[0] = selectedPattern.AvoidanceType;
            recentPatternCount = Mathf.Min(recentPatternCount + 1, RecentPatternMemory);
        }

        private void ClearRecentPatternHistory()
        {
            cachedRecentPatternFlowText = null;
            for (int i = 0; i < RecentPatternMemory; i += 1)
            {
                recentPatternFamilyKeys[i] = string.Empty;
                recentPatternChannels[i] = GreedLastRunChannel.None;
                recentPatternTimingTypes[i] = GreedLastTimingType.None;
                recentPatternAvoidanceTypes[i] = GreedLastAvoidanceType.None;
            }

            recentPatternCount = 0;
        }

        private string BuildRecentPatternFlowText()
        {
            if (cachedRecentPatternFlowText != null) return cachedRecentPatternFlowText;
            int count = Mathf.Min(recentPatternCount, RecentPatternMemory);
            if (count <= 0)
            {
                return cachedRecentPatternFlowText = string.Empty;
            }

            string text = string.Empty;
            for (int i = count - 1; i >= 0; i -= 1)
            {
                if (recentPatternChannels[i] == GreedLastRunChannel.None
                    || recentPatternTimingTypes[i] == GreedLastTimingType.None)
                {
                    continue;
                }

                string hazard = FormatAvoidanceHazardShort(recentPatternAvoidanceTypes[i]);
                string action = FormatAvoidanceTypeShort(recentPatternAvoidanceTypes[i]);
                string avoidanceCue = string.IsNullOrEmpty(action)
                    ? string.Empty
                    : string.IsNullOrEmpty(hazard) ? action : hazard + "→" + action;
                string item = FormatChannel(recentPatternChannels[i])
                    + FormatTimingTypeShort(recentPatternTimingTypes[i])
                    + (string.IsNullOrEmpty(avoidanceCue) ? string.Empty : "·" + avoidanceCue);
                text = string.IsNullOrEmpty(text)
                    ? item
                    : text + " > " + item;
            }

            return cachedRecentPatternFlowText = text;
        }

        private static string FormatTimingTypeShort(GreedLastTimingType timingType)
        {
            switch (timingType)
            {
                case GreedLastTimingType.Perfect:
                    return "정";
                case GreedLastTimingType.Clutch:
                    return "압";
                case GreedLastTimingType.Offbeat:
                    return "엇";
                default:
                    return "-";
            }
        }

        private static string FormatAvoidanceTypeShort(GreedLastAvoidanceType avoidanceType)
        {
            switch (avoidanceType)
            {
                case GreedLastAvoidanceType.Jump:
                    return "점";
                case GreedLastAvoidanceType.Slide:
                    return "숙";
                case GreedLastAvoidanceType.Twist:
                    return "틀";
                default:
                    return string.Empty;
            }
        }

        private static string FormatAvoidanceHazardShort(GreedLastAvoidanceType avoidanceType)
        {
            switch (avoidanceType)
            {
                case GreedLastAvoidanceType.Jump:
                    return "바";
                case GreedLastAvoidanceType.Slide:
                    return "상";
                case GreedLastAvoidanceType.Twist:
                    return "측";
                default:
                    return string.Empty;
            }
        }

        private static bool IsSamePatternFamily(string candidateId, string storedId)
        {
            string candidateFamily = BuildPatternFamilyKey(candidateId);
            string storedFamily = BuildPatternFamilyKey(storedId);
            return !string.IsNullOrEmpty(candidateId)
                && !string.IsNullOrEmpty(storedId)
                && (candidateId == storedId
                    || candidateId.StartsWith(storedId, StringComparison.Ordinal)
                    || storedId.StartsWith(candidateId, StringComparison.Ordinal)
                    || (!string.IsNullOrEmpty(candidateFamily) && candidateFamily == storedFamily));
        }

        private static string BuildPatternFamilyKey(string patternId)
        {
            if (string.IsNullOrEmpty(patternId))
            {
                return string.Empty;
            }

            string[] parts = patternId.Split('_');
            if (parts.Length < 2)
            {
                return patternId;
            }

            string lanePart = parts[0];
            if (lanePart == "left" || lanePart == "center" || lanePart == "right")
            {
                return parts[1];
            }

            return parts[0];
        }

        private GreedLastPatternModel BuildActivePattern(
            GreedLastPatternModel source,
            bool useInfiniteThreatBreather,
            bool useRecoveryBreather)
        {
            if (!infiniteMode)
            {
                int pressureLevel = phase == GreedLastRunPhase.EscapeRunning
                    ? 4
                    : Mathf.Clamp(chapterIndex, 1, 4);
                bool finalValidation = phase == GreedLastRunPhase.EscapeRunning || chapterIndex >= 4;
                float normalSpeedScale = BuildNormalChapterTempoScale(pressureLevel, finalValidation);
                float normalWindowScale = BuildNormalChapterWindowScale(pressureLevel, finalValidation);
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

                string variationSuffix = BuildNormalPatternVariationSuffix(
                    pressureLevel,
                    out float variationSpeedScale,
                    out float variationWindowScale);
                normalSpeedScale = Mathf.Clamp(normalSpeedScale * variationSpeedScale, 0.78f, 1.08f);
                normalWindowScale = Mathf.Clamp(normalWindowScale * variationWindowScale, 0.82f, 1.14f);
                string recoverySuffix = string.Empty;
                if (useRecoveryBreather)
                {
                    normalSpeedScale = Mathf.Min(1.10f, normalSpeedScale * 1.04f);
                    normalWindowScale = Mathf.Min(1.16f, normalWindowScale * 1.06f);
                    recoverySuffix = " [복구]";
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
                    source.Prompt + suffix + routeSuffix + variationSuffix + recoverySuffix,
                    source.TelegraphSeconds * normalSpeedScale,
                    source.CoreSeconds * normalSpeedScale,
                    source.SuccessWindowSeconds * normalWindowScale + BuildGrowthSuccessWindowBonus(),
                    source.GoodWindowSeconds * normalWindowScale + BuildGrowthGoodWindowBonus(),
                    source.HitSeconds * normalSpeedScale,
                    source.AvoidanceType);
            }

            int threatLevel = GetInfiniteThreatLevel();
            float speedScale = Mathf.Clamp(1f - (threatLevel - 1) * 0.07f, 0.72f, 1f);
            float windowScale = Mathf.Clamp(1f - (threatLevel - 1) * 0.05f, 0.80f, 1f);
            string infiniteVariationSuffix;
            float infiniteSpeedScale;
            float infiniteWindowScale;
            string infiniteRouteSuffix = BuildInfiniteLoadoutRoutePatternSuffix();
            if (useRecoveryBreather)
            {
                infiniteSpeedScale = 1.05f;
                infiniteWindowScale = 1.07f;
                infiniteVariationSuffix = " [복구]";
            }
            else if (useInfiniteThreatBreather)
            {
                infiniteSpeedScale = 1.04f;
                infiniteWindowScale = 1.06f;
                infiniteVariationSuffix = " [유예]";
            }
            else
            {
                infiniteVariationSuffix = BuildInfinitePatternVariationSuffix(
                    threatLevel,
                    out infiniteSpeedScale,
                    out infiniteWindowScale);
            }

            speedScale = Mathf.Clamp(speedScale * infiniteSpeedScale, 0.68f, 1.05f);
            windowScale = Mathf.Clamp(windowScale * infiniteWindowScale, 0.76f, 1.08f);
            return new GreedLastPatternModel(
                source.PatternId + "_threat_" + threatLevel,
                source.Channel,
                source.TimingType,
                source.Prompt + " [위협 " + threatLevel + "]" + infiniteRouteSuffix + infiniteVariationSuffix,
                source.TelegraphSeconds * speedScale,
                source.CoreSeconds * speedScale,
                source.SuccessWindowSeconds * windowScale + BuildGrowthSuccessWindowBonus(),
                source.GoodWindowSeconds * windowScale + BuildGrowthGoodWindowBonus(),
                source.HitSeconds * speedScale,
                source.AvoidanceType);
        }

        private static float BuildNormalChapterTempoScale(int pressureLevel, bool finalValidation)
        {
            float scale = Mathf.Clamp(1f - (Mathf.Clamp(pressureLevel, 1, 4) - 1) * 0.035f, 0.88f, 1f);
            return finalValidation
                ? Mathf.Min(1.10f, scale * FinalValidationTempoEaseScale)
                : scale;
        }

        private static float BuildNormalChapterWindowScale(int pressureLevel, bool finalValidation)
        {
            float scale = Mathf.Clamp(1f - (Mathf.Clamp(pressureLevel, 1, 4) - 1) * 0.025f, 0.90f, 1f);
            return finalValidation
                ? Mathf.Min(1.16f, scale * FinalValidationWindowEaseScale)
                : scale;
        }

        private string BuildInfiniteLoadoutRoutePatternSuffix()
        {
            if (!infiniteMode || string.IsNullOrEmpty(activeInfiniteLoadoutDisplayProfile))
            {
                return string.Empty;
            }

            if (activeInfiniteLoadoutDisplayProfile.Contains("압박", StringComparison.Ordinal))
            {
                return " [압박]";
            }

            if (activeInfiniteLoadoutDisplayProfile.Contains("변주", StringComparison.Ordinal))
            {
                return " [변주]";
            }

            if (activeInfiniteLoadoutDisplayProfile.Contains("안정", StringComparison.Ordinal))
            {
                return " [안정]";
            }

            return string.Empty;
        }

        private string BuildNormalPatternVariationSuffix(int pressureLevel, out float speedScale, out float windowScale)
        {
            speedScale = 1f;
            windowScale = 1f;
            if (pressureLevel <= 1 && combo < 3)
            {
                return string.Empty;
            }

            int seed = Mathf.Abs(patternIndex + chapterIndex * 5 + chapterProgress * 3 + combo * 2);
            switch (seed % 4)
            {
                case 0:
                    speedScale = 0.97f;
                    windowScale = 0.99f;
                    return " [급박]";
                case 1:
                    speedScale = 1.03f;
                    windowScale = 1.02f;
                    return " [유예]";
                case 2:
                    if (pressureLevel >= 3 || combo >= 3)
                    {
                        windowScale = 0.97f;
                        return " [정밀]";
                    }

                    return string.Empty;
                default:
                    if (combo >= 6)
                    {
                        speedScale = 0.98f;
                        windowScale = 0.98f;
                        return " [연쇄]";
                    }

                    return string.Empty;
            }
        }

        private string BuildInfinitePatternVariationSuffix(int threatLevel, out float speedScale, out float windowScale)
        {
            speedScale = 1f;
            windowScale = 1f;
            int seed = Mathf.Abs(patternIndex + infiniteSectionsCleared * 7 + threatLevel * 11 + combo * 3);
            switch (seed % 5)
            {
                case 0:
                    speedScale = 0.97f;
                    windowScale = 0.99f;
                    return " [급박]";
                case 1:
                    speedScale = 1.02f;
                    windowScale = 1.01f;
                    return " [유예]";
                case 2:
                    if (threatLevel >= 3)
                    {
                        windowScale = 0.97f;
                        return " [정밀]";
                    }

                    return string.Empty;
                case 3:
                    if (combo >= 6)
                    {
                        speedScale = 0.98f;
                        windowScale = 0.98f;
                        return " [연쇄]";
                    }

                    return string.Empty;
                default:
                    if (threatLevel >= 4)
                    {
                        speedScale = 0.95f;
                        return " [위기]";
                    }

                    return string.Empty;
            }
        }

        private int GetInfiniteThreatLevel()
        {
            if (!infiniteMode)
            {
                return 1;
            }

            return GetInfiniteThreatLevelForSections(infiniteSectionsCleared);
        }

        private int GetInfiniteThreatLevelForSections(int sectionsCleared)
        {
            int baseThreat = Mathf.Clamp(1 + sectionsCleared / 2, 1, InfiniteThreatMaxLevel);
            return Mathf.Clamp(baseThreat + infiniteThreatTestOffset, 1, InfiniteThreatMaxLevel);
        }

        private void Ignore(string reason, string message, float now)
        {
            ignoreReason = reason;
            judgementText = message;
            lastResult = GreedLastJudgementResult.None;
            lastCenterHit = false;
            Publish(now);
        }

        private string BuildStoppedInputMessage()
        {
            if (infiniteMode)
            {
                return "무한모드 종료 - 기록 정산 또는 정산 후 로비를 선택하세요.";
            }

            return saveEligible
                ? "탈출 성공 - 저장하거나 로비로 돌아가세요."
                : "런 종료 - 로비로 돌아가 다시 시작하세요.";
        }

        private string BuildStaggerInputMessage(float now)
        {
            return "경직 중 입력은 무효입니다.\n다음 입력까지 "
                + Mathf.Max(0f, staggerUntil - now).ToString("0.0")
                + "s";
        }

        internal static string FormatTimingDelta(float signedDeltaSeconds)
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
            string focusPerformance = " / 집중 발동 " + focusStateActivationCount
                + "회 / 집중 성공 " + focusStateSuccessCount
                + "회 / 집중 유지 " + focusStateActiveSeconds.ToString("0.0") + "초"
                + (noInputMissCount > 0 ? " / 입력 없음 " + noInputMissCount + "회" : string.Empty)
                + (timingRecommendationApplied
                    ? " / 추천 보정 적용 " + appliedTimingRecommendationMilliseconds + "ms"
                    : string.Empty)
                + " / 정박 성공 " + perfectSuccessCount
                + "회 / 압박 성공 " + clutchSuccessCount
                + "회 / 엇박 성공 " + offbeatSuccessCount + "회"
                + " / 복구 정박 " + recoveryPrecisionCount + "회"
                + " / 정박 충전 " + perfectFocusChargeCount + "회"
                + " / 압박 반격 " + clutchCounterCount + "회"
                + " / 엇박 연쇄 " + offbeatFlowCount + "회"
                + " / 안정 복구 " + stableRouteRecoveryCount + "회"
                + " / 변주 공명 " + variationRouteFlowCount + "회"
                + " / 압박 증폭 " + pressureRouteCounterCount + "회"
                + " / 안정 완주 " + stableRouteBundleCount + "회"
                + " / 변주 완주 " + variationRouteBundleCount + "회"
                + " / 압박 완주 " + pressureRouteBundleCount + "회"
                + " / 3동작 정확 " + threeActionExactBundleCount + "회"
                + BuildAvoidancePerformanceText(GreedLastAvoidanceType.Jump, "점프")
                + BuildAvoidancePerformanceText(GreedLastAvoidanceType.Slide, "숙이기")
                + BuildAvoidancePerformanceText(GreedLastAvoidanceType.Twist, "비틀기")
                + BuildSegmentPerformanceText("1장", chapterExactCounts[0], chapterGoodCounts[0], chapterMissCounts[0])
                + BuildSegmentPerformanceText("2장", chapterExactCounts[1], chapterGoodCounts[1], chapterMissCounts[1])
                + BuildSegmentPerformanceText("3장", chapterExactCounts[2], chapterGoodCounts[2], chapterMissCounts[2])
                + BuildSegmentPerformanceText("4장", chapterExactCounts[3], chapterGoodCounts[3], chapterMissCounts[3])
                + BuildSegmentPerformanceText("탈출", escapeExactCount, escapeGoodCount, escapeMissCount)
                + BuildLastCompletedSegmentProfileText()
                + BuildInfiniteTerminalSegmentProfileText()
                + BuildReviewTargetProfileText();
            if (timingSampleCount <= 0)
            {
                return "타이밍 기록 없음 / 추천 샘플 0/" + TimingRecommendationMinSamples + focusPerformance;
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
                + " / 정박 최고 " + maxPrecisionStreak
                + " / " + recommendation
                + focusPerformance;
        }

        private string BuildAvoidancePerformanceText(GreedLastAvoidanceType avoidanceType, string label)
        {
            int index = (int)avoidanceType;
            return " / " + label + " 정확 " + avoidanceExactCounts[index]
                + "회 / " + label + " 보정 " + avoidanceGoodCounts[index]
                + "회 / " + label + " 실수 " + avoidanceMissCounts[index] + "회";
        }

        private string BuildReviewTargetProfileText()
        {
            if (string.IsNullOrWhiteSpace(reviewTargetText))
            {
                return string.Empty;
            }

            return " / 복기 목표 " + reviewTargetText.Trim()
                + " / 복기 결과 " + BuildReviewTargetResultText(reviewTargetText);
        }

        private string BuildReviewTargetResultText(string target)
        {
            if (target.Contains("무실수", StringComparison.Ordinal))
            {
                return missCount <= 0 ? "달성 무실수" : "실패 실수 " + missCount;
            }

            if (target.Contains("보정 0회", StringComparison.Ordinal))
            {
                if (goodCount > 0)
                {
                    return "실패 보정 " + goodCount;
                }

                return missCount > 0 ? "실패 실수 " + missCount : "달성 보정 0";
            }

            if (target.Contains("입력 없음", StringComparison.Ordinal))
            {
                int targetNoInput = ExtractTargetNumberAfterKeyword(target, "입력 없음");
                return noInputMissCount <= targetNoInput
                    ? "달성 입력 없음 " + noInputMissCount + "/" + targetNoInput
                    : "실패 입력 없음 " + noInputMissCount + "/" + targetNoInput;
            }

            if (target.Contains("추천 보정 적용", StringComparison.Ordinal))
            {
                return timingRecommendationApplied
                    ? "달성 추천 보정 " + appliedTimingRecommendationMilliseconds + "ms"
                    : "진행 추천 보정 미적용";
            }

            if (target.Contains("콤보", StringComparison.Ordinal))
            {
                int currentCombo = Mathf.Max(combo, maxCombo);
                int targetCombo = ExtractTargetNumberAfterKeyword(target, "콤보");
                return targetCombo > 0 && currentCombo >= targetCombo
                    ? "달성 콤보 " + currentCombo
                    : "진행 콤보 " + currentCombo;
            }

            if (target.Contains("점", StringComparison.Ordinal))
            {
                int targetScore = ExtractTargetNumberBeforeMarker(target, "점");
                return targetScore > 0 && score >= targetScore
                    ? "달성 " + score + "점"
                    : "진행 " + score + "점";
            }

            if (target.Contains("구간 생존", StringComparison.Ordinal))
            {
                int targetSections = ExtractTargetNumberBeforeMarker(target, "구간");
                return targetSections > 0 && infiniteSectionsCleared >= targetSections
                    ? "달성 " + infiniteSectionsCleared + "구간 생존"
                    : "진행 " + infiniteSectionsCleared + "/" + targetSections + "구간";
            }

            if (target.Contains("위협", StringComparison.Ordinal))
            {
                int targetThreat = ExtractTargetNumberAfterKeyword(target, "위협");
                return targetThreat > 0 && maxInfiniteThreatLevel >= targetThreat
                    ? "달성 위협 " + maxInfiniteThreatLevel
                    : "진행 위협 " + maxInfiniteThreatLevel;
            }

            if (target.Contains("실수", StringComparison.Ordinal)
                && target.Contains("이하", StringComparison.Ordinal))
            {
                int targetMisses = ExtractTargetNumberAfterKeyword(target, "실수");
                return missCount <= targetMisses
                    ? "달성 실수 " + missCount + "/" + targetMisses
                    : "실패 실수 " + missCount + "/" + targetMisses;
            }

            if (target.Contains("체력", StringComparison.Ordinal) || target.Contains("생존", StringComparison.Ordinal))
            {
                int targetHealth = target.Contains("체력", StringComparison.Ordinal)
                    ? ExtractTargetNumberAfterKeyword(target, "체력")
                    : ExtractTargetNumberAfterKeyword(target, "생존");
                return targetHealth > 0 && health >= targetHealth
                    ? "달성 체력 " + health
                    : "진행 체력 " + health;
            }

            if (target.Contains("집중", StringComparison.Ordinal))
            {
                bool activationTarget = target.Contains("발동", StringComparison.Ordinal);
                int currentFocus = activationTarget
                    ? focusStateActivationCount
                    : focusStateActivationCount > 0 ? Mathf.Max(focus, MaxFocus) : focus;
                int targetFocus = ExtractTargetNumberAfterKeyword(target, "집중");
                string focusText = activationTarget
                    ? "집중 발동 " + currentFocus + "회"
                    : "집중 " + currentFocus;
                return targetFocus > 0 && currentFocus >= targetFocus
                    ? "달성 " + focusText
                    : "진행 " + focusText;
            }

            if (target.Contains("정박", StringComparison.Ordinal) || target.Contains("정확권", StringComparison.Ordinal))
            {
                int targetPrecision = ExtractTargetNumberBeforeMarker(target, "연속");
                if (targetPrecision <= 0)
                {
                    targetPrecision = target.Contains("정박", StringComparison.Ordinal)
                        ? ExtractTargetNumberAfterKeyword(target, "정박")
                        : ExtractTargetNumberAfterKeyword(target, "정확권");
                }

                return targetPrecision > 0 && maxPrecisionStreak >= targetPrecision
                    ? "달성 정박 " + maxPrecisionStreak
                    : "진행 정박 " + maxPrecisionStreak;
            }

            return "기록됨";
        }

        private static int ExtractFirstTargetNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            for (int i = 0; i < text.Length; i += 1)
            {
                if (!char.IsDigit(text[i]))
                {
                    continue;
                }

                int start = i;
                while (i < text.Length && char.IsDigit(text[i]))
                {
                    i += 1;
                }

                string value = text.Substring(start, i - start);
                return int.TryParse(value, out int number) ? Mathf.Max(0, number) : 0;
            }

            return 0;
        }

        private static int ExtractTargetNumberAfterKeyword(string text, string keyword)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrEmpty(keyword))
            {
                return 0;
            }

            int keywordIndex = text.IndexOf(keyword, StringComparison.Ordinal);
            return keywordIndex < 0
                ? 0
                : ExtractFirstTargetNumber(text.Substring(keywordIndex + keyword.Length));
        }

        private static int ExtractTargetNumberBeforeMarker(string text, string marker)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrEmpty(marker))
            {
                return 0;
            }

            int markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex <= 0)
            {
                return 0;
            }

            int end = markerIndex - 1;
            while (end >= 0 && char.IsWhiteSpace(text[end]))
            {
                end -= 1;
            }

            int start = end;
            while (start >= 0 && char.IsDigit(text[start]))
            {
                start -= 1;
            }

            if (start == end)
            {
                return 0;
            }

            string value = text.Substring(start + 1, end - start);
            return int.TryParse(value, out int number) ? Mathf.Max(0, number) : 0;
        }

        private static string BuildSegmentPerformanceText(string segment, int exact, int good, int miss)
        {
            return " / " + segment + " 정확 " + exact
                + "회 / " + segment + " 보정 " + good
                + "회 / " + segment + " 실수 " + miss + "회";
        }

        private string BuildLastCompletedSegmentProfileText()
        {
            if (string.IsNullOrEmpty(lastCompletedSegment))
            {
                return string.Empty;
            }

            return " / 최근 구간 " + lastCompletedSegment
                + " / 최근 구간 정확 " + lastCompletedSegmentExactCount
                + "회 / 최근 구간 보정 " + lastCompletedSegmentGoodCount
                + "회 / 최근 구간 실수 " + lastCompletedSegmentMissCount + "회";
        }

        private string BuildInfiniteTerminalSegmentProfileText()
        {
            int resolvedPatternCount = currentBundleExactCount + currentBundleGoodCount + currentBundleMissCount;
            if (!infiniteMode
                || !stopped
                || missReason == "test_stop"
                || resolvedPatternCount <= 0)
            {
                return string.Empty;
            }

            return " / 종료 구간 무한 " + (infiniteSectionsCleared + 1) + "구간"
                + " / 종료 구간 진행 " + resolvedPatternCount + "/" + ChapterTargetPatterns
                + " / 종료 구간 정확 " + currentBundleExactCount
                + "회 / 종료 구간 보정 " + currentBundleGoodCount
                + "회 / 종료 구간 실수 " + currentBundleMissCount + "회";
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

        private static string FormatTimingType(GreedLastTimingType timingType)
        {
            switch (timingType)
            {
                case GreedLastTimingType.Perfect:
                    return "정박";
                case GreedLastTimingType.Clutch:
                    return "압박";
                case GreedLastTimingType.Offbeat:
                    return "엇박";
                default:
                    return "-";
            }
        }

        private void Publish()
        {
            Publish(Time.realtimeSinceStartup);
        }

        private void Publish(float realtimeNow)
        {
            Publish(realtimeNow, reuseTimingProfile: false);
        }

        private void Publish(float realtimeNow, bool reuseTimingProfile)
        {
            if (!reuseTimingProfile) cachedTimingProfileText = null;
            float now = paused ? pausedAt : realtimeNow;
            float elapsedSeconds = activePattern ? now - patternStartedAt : 0f;
            float hitSeconds = Mathf.Max(0.01f, currentPattern.HitSeconds);
            float beatProgress = activePattern ? Mathf.Clamp01(elapsedSeconds / hitSeconds) : 0f;
            float coreDelta = activePattern ? elapsedSeconds - currentPattern.CoreSeconds : 0f;
            float resumeCountdownRemaining = resumeCountdownActive
                ? Mathf.Max(0f, resumeCountdownEndsAt - realtimeNow)
                : 0f;
            float startCountdownReferenceNow = paused && startCountdownActive
                ? pausedAt
                : realtimeNow;
            float startCountdownRemaining = startCountdownActive
                ? Mathf.Max(0f, startCountdownEndsAt - startCountdownReferenceNow)
                : 0f;
            float staggerRemaining = Mathf.Max(0f, staggerUntil - now);
            SnapshotChanged?.Invoke(new GreedLastRunSnapshot(
                currentPattern,
                lastJudgedPattern,
                judgementText,
                ignoreReason,
                missReason,
                score,
                health,
                maxHealthThisRun,
                combo,
                focus,
                MaxFocus,
                IsFocusStateActive,
                focusStateRemainingSeconds,
                focusStateActivationCount,
                focusStateSuccessCount,
                focusStateActiveSeconds,
                maxCombo,
                precisionStreak,
                maxPrecisionStreak,
                recoveryPrecisionCount,
                perfectFocusChargeCount,
                clutchCounterCount,
                offbeatFlowCount,
                stableRouteRecoveryCount,
                variationRouteFlowCount,
                pressureRouteCounterCount,
                stableRouteBundleCount,
                variationRouteBundleCount,
                pressureRouteBundleCount,
                lastCenterHit,
                successCount,
                perfectSuccessCount,
                clutchSuccessCount,
                offbeatSuccessCount,
                goodCount,
                missCount,
                noInputMissCount,
                infiniteSectionsCleared,
                GetInfiniteThreatLevel(),
                maxInfiniteThreatLevel,
                distance,
                chapterIndex,
                chapterProgress,
                ChapterTargetPatterns,
                lastCompletedSegment,
                lastCompletedSegmentExactCount,
                lastCompletedSegmentGoodCount,
                lastCompletedSegmentMissCount,
                currentBundleExactCount,
                currentBundleGoodCount,
                currentBundleMissCount,
                currentBundleAvoidanceMask,
                threeActionExactBundleCount,
                patternBundleBreakCount <= 0,
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
                staggerRemaining,
                devInvincible,
                infiniteMode,
                refillRunMode,
                practiceMode,
                elapsedSeconds,
                beatProgress,
                coreDelta,
                resumeCountdownRemaining,
                startCountdownRemaining,
                inputTimingOffsetSeconds,
                HasTimingRecommendation,
                TimingRecommendationMilliseconds,
                timingSampleCount,
                TimingRecommendationMinSamples,
                timingRecommendationApplied,
                appliedTimingRecommendationMilliseconds,
                GetActiveGiftStackCount("집중 축"),
                GetActiveGiftStackCount("생존 축"),
                GetActiveGiftStackCount("연쇄 축"),
                relic,
                BuildActiveChoiceProfile(),
                BuildCurrentActiveRouteProfile(),
                activeInfiniteLoadoutName,
                BuildCurrentInfiniteLoadoutProfile(),
                reviewTargetText,
                BuildRecentPatternFlowText(),
                GetPublishedTimingProfileText(reuseTimingProfile),
                lastJudgementScoreGain,
                lastJudgementHealthChange,
                hasJudgedInputTiming,
                lastInputTimingDeltaSeconds,
                lastJudgedInputChannel,
                BuildJudgementScoreGainPreview(true),
                BuildJudgementScoreGainPreview(false),
                BuildGoodHealthGainPreview(),
                infiniteMode ? GetInfiniteThreatLevelForSections(infiniteSectionsCleared + 1) : 0,
                choiceKind != GreedLastChoiceKind.None && !stopped
                    ? Mathf.Max(0, Mathf.Min(BuildBonusHealthCap(), health + 1) - health) : 0,
                lastJudgementStartedInFocusState,
                lastJudgementFocusStateExtensionSeconds,
                lastJudgementProfileScoreBonus,
                lastJudgementThreatScoreBonus,
                lastJudgementScoreLimited));
        }

        private string GetPublishedTimingProfileText(bool reuse)
        {
            // Passive Tick only changes the displayed focus duration; commands rebuild all counters.
            if (reuse && cachedTimingProfileText != null
                && cachedTimingProfileFocusSeconds == focusStateActiveSeconds)
                return cachedTimingProfileText;
            string focusDuration = focusStateActiveSeconds.ToString("0.0");
            if (!reuse || cachedTimingProfileText == null || cachedTimingProfileFocusDuration != focusDuration)
            {
                cachedTimingProfileText = BuildTimingProfileText();
                cachedTimingProfileFocusDuration = focusDuration;
            }
            cachedTimingProfileFocusSeconds = focusStateActiveSeconds;
            return cachedTimingProfileText;
        }
    }
}
