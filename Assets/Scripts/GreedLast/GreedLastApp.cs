using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreedLast
{
    public enum GreedLastScreenState
    {
        None = 0,
        BootInit = 1,
        ConnectChecking = 2,
        ConnectBlocked = 3,
        LobbyLoading = 4,
        LobbyReady = 5,
        ReSyncPending = 6,
        RunCoreTest = 7,
        SaveLoadoutDraft = 8,
        InfiniteRun = 9,
        InfiniteRecordBoard = 10,
        InfiniteStartReady = 11,
        InfiniteLoadoutSelect = 12,
    }

    public enum GreedLastRequestKind
    {
        BootInit = 0,
        ConnectCheck = 1,
        LobbySync = 2,
        ReSync = 3,
        LobbyAction = 4,
    }

    public enum GreedLastConnectBlockReason
    {
        None = 0,
        NetworkUnavailable = 1,
        Maintenance = 2,
        VersionMismatch = 3,
        SessionInvalid = 4,
    }

    public readonly struct GreedLastRequestToken
    {
        public GreedLastRequestToken(GreedLastRequestKind kind, int requestId)
        {
            Kind = kind;
            RequestId = requestId;
        }

        public GreedLastRequestKind Kind { get; }
        public int RequestId { get; }
        public bool IsValid => RequestId > 0;
    }

    public readonly struct GreedLastStateSnapshot
    {
        public GreedLastStateSnapshot(
            GreedLastScreenState state,
            int requestId,
            string headline,
            string detail,
            bool busy,
            bool coreActionsEnabled,
            bool firstSaveCompleted,
            bool saveLoadoutUnlocked,
            bool infiniteUnlocked,
            int selectedSaveSlotIndex,
            string[] saveSlotLabels,
            bool[] saveSlotUsable,
            bool[] saveSlotOccupied,
            bool saveSlotChoiceRequired,
            bool saveSlotOverwriteConfirmationPending,
            bool saveSlotDeleteConfirmationPending,
            bool saveSlotDetailViewActive,
            bool saveSlotRenameConfirmationPending,
            bool saveLoadoutCandidateAvailable,
            int recordBoardPageIndex,
            bool retryVisible,
            GreedLastConnectBlockReason blockReason)
        {
            State = state;
            RequestId = requestId;
            Headline = headline ?? string.Empty;
            Detail = detail ?? string.Empty;
            Busy = busy;
            CoreActionsEnabled = coreActionsEnabled;
            FirstSaveCompleted = firstSaveCompleted;
            SaveLoadoutUnlocked = saveLoadoutUnlocked;
            InfiniteUnlocked = infiniteUnlocked;
            SelectedSaveSlotIndex = selectedSaveSlotIndex;
            SaveSlotLabels = saveSlotLabels ?? Array.Empty<string>();
            SaveSlotUsable = saveSlotUsable ?? Array.Empty<bool>();
            SaveSlotOccupied = saveSlotOccupied ?? Array.Empty<bool>();
            SaveSlotChoiceRequired = saveSlotChoiceRequired;
            SaveSlotOverwriteConfirmationPending = saveSlotOverwriteConfirmationPending;
            SaveSlotDeleteConfirmationPending = saveSlotDeleteConfirmationPending;
            SaveSlotDetailViewActive = saveSlotDetailViewActive;
            SaveSlotRenameConfirmationPending = saveSlotRenameConfirmationPending;
            SaveLoadoutCandidateAvailable = saveLoadoutCandidateAvailable;
            RecordBoardPageIndex = recordBoardPageIndex;
            RetryVisible = retryVisible;
            BlockReason = blockReason;
        }

        public GreedLastScreenState State { get; }
        public int RequestId { get; }
        public string Headline { get; }
        public string Detail { get; }
        public bool Busy { get; }
        public bool CoreActionsEnabled { get; }
        public bool FirstSaveCompleted { get; }
        public bool SaveLoadoutUnlocked { get; }
        public bool InfiniteUnlocked { get; }
        public int SelectedSaveSlotIndex { get; }
        public string[] SaveSlotLabels { get; }
        public bool[] SaveSlotUsable { get; }
        public bool[] SaveSlotOccupied { get; }
        public bool SaveSlotChoiceRequired { get; }
        public bool SaveSlotOverwriteConfirmationPending { get; }
        public bool SaveSlotDeleteConfirmationPending { get; }
        public bool SaveSlotDetailViewActive { get; }
        public bool SaveSlotRenameConfirmationPending { get; }
        public bool SaveLoadoutCandidateAvailable { get; }
        public int RecordBoardPageIndex { get; }
        public bool RetryVisible { get; }
        public GreedLastConnectBlockReason BlockReason { get; }
    }

    public readonly struct GreedLastConnectResult
    {
        public GreedLastConnectResult(bool success, GreedLastConnectBlockReason reason, string message)
        {
            Success = success;
            Reason = reason;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public GreedLastConnectBlockReason Reason { get; }
        public string Message { get; }
    }

    public readonly struct GreedLastLobbySnapshot
    {
        public GreedLastLobbySnapshot(
            string periodId,
            bool firstSaveCompleted,
            bool saveLoadoutUnlocked,
            bool infiniteUnlocked,
            int selectedSaveSlotIndex,
            string[] saveSlotLabels,
            bool[] saveSlotUsable,
            bool[] saveSlotOccupied,
            bool auxiliaryWarning,
            string message)
        {
            PeriodId = periodId ?? string.Empty;
            FirstSaveCompleted = firstSaveCompleted;
            SaveLoadoutUnlocked = saveLoadoutUnlocked;
            InfiniteUnlocked = infiniteUnlocked;
            SelectedSaveSlotIndex = selectedSaveSlotIndex;
            SaveSlotLabels = saveSlotLabels ?? Array.Empty<string>();
            SaveSlotUsable = saveSlotUsable ?? Array.Empty<bool>();
            SaveSlotOccupied = saveSlotOccupied ?? Array.Empty<bool>();
            AuxiliaryWarning = auxiliaryWarning;
            Message = message ?? string.Empty;
        }

        public string PeriodId { get; }
        public bool FirstSaveCompleted { get; }
        public bool SaveLoadoutUnlocked { get; }
        public bool InfiniteUnlocked { get; }
        public int SelectedSaveSlotIndex { get; }
        public string[] SaveSlotLabels { get; }
        public bool[] SaveSlotUsable { get; }
        public bool[] SaveSlotOccupied { get; }
        public bool AuxiliaryWarning { get; }
        public string Message { get; }
        public bool CanShowInfiniteMode => InfiniteUnlocked;
    }

    public sealed class GreedLastRequestGate
    {
        private int nextRequestId;
        private GreedLastRequestToken activeToken;

        public int ActiveRequestId => activeToken.RequestId;
        public bool Busy => activeToken.IsValid;

        public bool TryBegin(GreedLastRequestKind kind, out GreedLastRequestToken token)
        {
            if (activeToken.IsValid)
            {
                token = default;
                return false;
            }

            nextRequestId += 1;
            activeToken = new GreedLastRequestToken(kind, nextRequestId);
            token = activeToken;
            return true;
        }

        public bool IsCurrent(GreedLastRequestToken token)
        {
            return token.IsValid
                && activeToken.IsValid
                && token.Kind == activeToken.Kind
                && token.RequestId == activeToken.RequestId;
        }

        public void Complete(GreedLastRequestToken token)
        {
            if (IsCurrent(token))
            {
                activeToken = default;
            }
        }
    }

    public sealed class GreedLastMockBackend
    {
        private const string CurrentPeriodId = "season-0001";
        private const int SaveSlotCount = 3;
        private const int NormalRankingCount = 5;
        private const int NormalAttemptHistoryCount = 5;
        private const int InfiniteRankingCount = 5;
        private const int RecordBoardPageCount = 4;
        private const int MaxSaveLoadoutUses = 2;
        private const int SaveDataVersion = 1;
        private const string PlayerPrefsKey = "GreedLast.MockBackend.SaveData.v1";
        private static readonly string[] SaveLoadoutNamePresets =
        {
            "균형 저장",
            "안정 저장",
            "고득점 저장",
            "장거리 저장",
            "집중 저장",
        };

        private bool online = true;
        private bool maintenance;
        private bool versionMatched = true;
        private bool sessionValid = true;
        private bool firstSaveCompleted;
        private bool saveLoadoutUnlocked;
        private bool savedLoadoutConfirmed;
        private bool saveLoadoutCandidateAvailable;
        private bool auxiliaryWarning;
        private int selectedSaveSlotIndex;
        private GreedLastRunRecord lastRunRecord = GreedLastRunRecord.Empty;
        private GreedLastRunAttemptRecord lastRunAttemptRecord = GreedLastRunAttemptRecord.Empty;
        private GreedLastRunRecord bestRunRecord = GreedLastRunRecord.Empty;
        private readonly GreedLastRunRecord[] normalRunRankings = new GreedLastRunRecord[NormalRankingCount];
        private readonly GreedLastRunAttemptRecord[] normalAttemptHistory = new GreedLastRunAttemptRecord[NormalAttemptHistoryCount];
        private readonly GreedLastRunRecord[] saveLoadoutSlots = new GreedLastRunRecord[SaveSlotCount];
        private readonly int[] saveLoadoutUsesRemaining = new int[SaveSlotCount];
        private readonly int[] saveLoadoutRenameSteps = new int[SaveSlotCount];
        private GreedLastInfiniteRunRecord lastInfiniteRunRecord = GreedLastInfiniteRunRecord.Empty;
        private GreedLastInfiniteRunRecord bestInfiniteRunRecord = GreedLastInfiniteRunRecord.Empty;
        private GreedLastInfiniteRunRecord previousBestBeforeLastInfiniteRun = GreedLastInfiniteRunRecord.Empty;
        private bool lastInfiniteRunWasNewBest;
        private readonly GreedLastInfiniteRunRecord[] infiniteRunRankings = new GreedLastInfiniteRunRecord[InfiniteRankingCount];
        private int normalClearCount;
        private int normalFailCount;
        private int normalAbandonCount;
        private int infiniteRunCount;

        public GreedLastMockBackend()
        {
            LoadPersistedData();
        }

        public int RecordBoardPages => RecordBoardPageCount;
        public bool HasSaveLoadoutCandidate => saveLoadoutCandidateAvailable && lastRunRecord.IsValid;

        public GreedLastConnectResult CheckConnect()
        {
            if (!online)
            {
                return new GreedLastConnectResult(false, GreedLastConnectBlockReason.NetworkUnavailable, "네트워크 연결을 확인할 수 없습니다.");
            }

            if (maintenance)
            {
                return new GreedLastConnectResult(false, GreedLastConnectBlockReason.Maintenance, "점검 상태입니다.");
            }

            if (!versionMatched)
            {
                return new GreedLastConnectResult(false, GreedLastConnectBlockReason.VersionMismatch, "클라이언트 버전 확인이 필요합니다.");
            }

            if (!sessionValid)
            {
                return new GreedLastConnectResult(false, GreedLastConnectBlockReason.SessionInvalid, "세션을 다시 확인해야 합니다.");
            }

            return new GreedLastConnectResult(true, GreedLastConnectBlockReason.None, "연결 확인 완료");
        }

        public GreedLastLobbySnapshot LoadLobby()
        {
            selectedSaveSlotIndex = Mathf.Clamp(selectedSaveSlotIndex, 0, SaveSlotCount - 1);

            string message = auxiliaryWarning
                ? "보조 데이터 일부가 늦습니다. 핵심 진입은 유지합니다."
                : "피라미드 입구 동기화 완료";
            bool hasUsableSaveSlot = HasAnyUsableSaveLoadoutSlot();

            return new GreedLastLobbySnapshot(
                CurrentPeriodId,
                firstSaveCompleted,
                saveLoadoutUnlocked,
                hasUsableSaveSlot,
                selectedSaveSlotIndex,
                BuildSaveSlotLabelSummaries(),
                BuildSaveSlotUsableFlags(),
                BuildSaveSlotOccupiedFlags(),
                auxiliaryWarning,
                message);
        }

        public GreedLastLobbySnapshot ReSyncLobby()
        {
            return LoadLobby();
        }

        public bool ValidateSaveLoadoutEntry(string periodId, GreedLastLobbySnapshot snapshot)
        {
            return snapshot.SaveLoadoutUnlocked
                && snapshot.PeriodId == CurrentPeriodId
                && periodId == CurrentPeriodId
                && saveLoadoutCandidateAvailable
                && lastRunRecord.IsValid;
        }

        public GreedLastLobbySnapshot RegisterClear(GreedLastRunRecord record)
        {
            if (record.IsValid)
            {
                firstSaveCompleted = true;
                saveLoadoutUnlocked = true;
                saveLoadoutCandidateAvailable = true;
                lastRunRecord = record;
                if (!bestRunRecord.IsValid || IsBetterRunRecord(record, bestRunRecord))
                {
                    bestRunRecord = record;
                }

                InsertNormalRanking(record);
                normalClearCount += 1;
                SavePersistedData();
            }

            return LoadLobby();
        }

        public bool RegisterNormalAttempt(GreedLastRunAttemptRecord record)
        {
            if (!record.IsValid)
            {
                return false;
            }

            lastRunAttemptRecord = record;
            InsertNormalAttemptHistory(record);
            if (record.Outcome == GreedLastRunAttemptOutcome.Failed)
            {
                normalFailCount += 1;
            }
            else if (record.Outcome == GreedLastRunAttemptOutcome.Abandoned)
            {
                normalAbandonCount += 1;
            }

            SavePersistedData();
            return true;
        }

        public string BuildSaveLoadoutDraftText(bool showSelectedSlot = true)
        {
            bool hasCandidate = HasSaveLoadoutCandidate;
            string text = (hasCandidate
                    ? "저장 모드\n후보: " + FormatSaveCandidateSummary(lastRunRecord)
                    : "관리 모드\n새 저장 후보 없음\n기존 슬롯만 확인 / 이름 변경 / 삭제할 수 있습니다.")
                + "\n\n슬롯\n"
                + BuildSaveSlotListText(showSelectedSlot, compact: true);

            if (!showSelectedSlot)
            {
                return hasCandidate
                    ? text
                        + "\n\n좌 / 중 / 우로 저장할 슬롯을 고르세요."
                        + "\n고른 뒤 저장 여부를 확정합니다."
                    : text
                        + "\n\n좌 / 중 / 우로 확인할 슬롯을 고르세요.";
            }

            GreedLastRunRecord selectedRecord = saveLoadoutSlots[selectedSaveSlotIndex];
            int selectedUses = saveLoadoutUsesRemaining[selectedSaveSlotIndex];
            string selectedText = text
                + "\n\n선택 슬롯: 슬롯 " + (selectedSaveSlotIndex + 1)
                + "\n" + FormatSaveSlotCompact(selectedRecord, selectedUses);

            return hasCandidate
                ? selectedText
                    + BuildOverwriteWarningText(selectedRecord, selectedUses)
                    + "\n\n상세 비교에서 후보와 기존 슬롯 차이를 볼 수 있습니다."
                : selectedText
                    + "\n\n상세 보기에서 기존 저장 조합 기록을 확인할 수 있습니다.";
        }

        public string BuildRunClearResultMessage(GreedLastRunRecord record)
        {
            if (!record.IsValid)
            {
                return "탈출 기록을 만들지 못했습니다.";
            }

            string nextAction = HasAnySaveLoadoutSlot()
                ? "다음: 저장 조합에서 후보 비교 / 슬롯 갱신"
                : "다음: 저장 조합에서 첫 슬롯 확정";

            return "일반 런 클리어\n"
                + $"등급 {BuildRunGrade(record)}  점수 {record.Score}  거리 {record.Distance:0.0}m  체력 {record.Health}  집중 {record.Focus}\n"
                + "성향: " + BuildLoadoutProfile(record) + "\n"
                + "저장 후보: " + record.LoadoutName
                + "\n최근 비교: " + BuildLatestNormalComparisonText()
                + "\n" + nextAction;
        }

        public string BuildRunAttemptResultMessage(GreedLastRunAttemptRecord record)
        {
            if (!record.IsValid)
            {
                return "피라미드 입구로 돌아왔습니다.";
            }

            return FormatRunAttemptOutcome(record.Outcome) + " 기록 저장\n"
                + FormatRunAttemptRecord(record)
                + "\n기록 보기에서 최근 실패/중단을 다시 확인할 수 있습니다.";
        }

        public GreedLastLobbySnapshot SelectSaveLoadoutSlot(int slotIndex)
        {
            selectedSaveSlotIndex = Mathf.Clamp(slotIndex, 0, SaveSlotCount - 1);
            SavePersistedData();
            return LoadLobby();
        }

        public GreedLastLobbySnapshot ConfirmSaveLoadout()
        {
            if (lastRunRecord.IsValid && saveLoadoutUnlocked && saveLoadoutCandidateAvailable)
            {
                saveLoadoutSlots[selectedSaveSlotIndex] = lastRunRecord;
                saveLoadoutUsesRemaining[selectedSaveSlotIndex] = MaxSaveLoadoutUses;
                saveLoadoutRenameSteps[selectedSaveSlotIndex] = 0;
                savedLoadoutConfirmed = true;
                saveLoadoutCandidateAvailable = false;
                SavePersistedData();
            }

            return LoadLobby();
        }

        public void DiscardSaveLoadoutCandidate()
        {
            if (!saveLoadoutCandidateAvailable)
            {
                return;
            }

            saveLoadoutCandidateAvailable = false;
            SavePersistedData();
        }

        public bool SelectedSaveSlotHasRecord()
        {
            selectedSaveSlotIndex = Mathf.Clamp(selectedSaveSlotIndex, 0, SaveSlotCount - 1);
            return saveLoadoutSlots[selectedSaveSlotIndex].IsValid;
        }

        public string BuildSaveConfirmMessage()
        {
            if (!savedLoadoutConfirmed || !saveLoadoutSlots[selectedSaveSlotIndex].IsValid)
            {
                return "저장 조합 확정에 실패했습니다.";
            }

            return "슬롯 " + (selectedSaveSlotIndex + 1) + " 저장 조합 확정 완료\n"
                + lastRunRecord.LoadoutName
                + "\n사용 가능 횟수 2회"
                + "\n무한모드가 열렸습니다.";
        }

        public string BuildSelectedSaveLoadoutComparisonText()
        {
            GreedLastRunRecord slotRecord = saveLoadoutSlots[selectedSaveSlotIndex];
            bool hasCandidate = HasSaveLoadoutCandidate;
            string slotHeader = "슬롯 " + (selectedSaveSlotIndex + 1)
                + (hasCandidate ? " 상세 비교" : " 상세 보기");
            if (!hasCandidate)
            {
                if (!slotRecord.IsValid)
                {
                    return slotHeader
                        + "\n비어 있음"
                        + "\n\n일반 런을 클리어하면 새 저장 후보를 만들 수 있습니다.";
                }

                return slotHeader
                    + "\n\n현재 슬롯\n" + FormatRunRecordDetail(slotRecord)
                    + "\n남은 사용 " + saveLoadoutUsesRemaining[selectedSaveSlotIndex] + "회";
            }

            string text = slotHeader
                + "\n\n후보\n" + FormatRunRecordDetail(lastRunRecord);

            if (!slotRecord.IsValid)
            {
                return text
                    + "\n\n현재 슬롯\n비어 있음"
                    + "\n\n이 슬롯에 저장하면 새 조합으로 2회 사용할 수 있습니다.";
            }

            text += "\n\n현재 슬롯\n" + FormatRunRecordDetail(slotRecord)
                + "\n남은 사용 " + saveLoadoutUsesRemaining[selectedSaveSlotIndex] + "회"
                + "\n\n차이"
                + $"\n점수 {FormatSigned(lastRunRecord.Score - slotRecord.Score)}"
                + $" / 거리 {FormatSigned(lastRunRecord.Distance - slotRecord.Distance)}m"
                + $" / 체력 {FormatSigned(lastRunRecord.Health - slotRecord.Health)}"
                + $" / 집중 {FormatSigned(lastRunRecord.Focus - slotRecord.Focus)}";
            return text;
        }

        public string RenameSelectedSaveLoadoutSlot()
        {
            selectedSaveSlotIndex = Mathf.Clamp(selectedSaveSlotIndex, 0, SaveSlotCount - 1);
            GreedLastRunRecord record = saveLoadoutSlots[selectedSaveSlotIndex];
            if (!record.IsValid)
            {
                return "선택한 슬롯이 비어 있어 이름을 바꿀 수 없습니다.";
            }

            saveLoadoutRenameSteps[selectedSaveSlotIndex] =
                (saveLoadoutRenameSteps[selectedSaveSlotIndex] + 1) % SaveLoadoutNamePresets.Length;
            string newName = BuildSaveLoadoutNameForStep(selectedSaveSlotIndex, saveLoadoutRenameSteps[selectedSaveSlotIndex]);
            saveLoadoutSlots[selectedSaveSlotIndex] = WithLoadoutName(record, newName);
            SavePersistedData();
            return "슬롯 " + (selectedSaveSlotIndex + 1) + " 이름 변경: " + newName;
        }

        public string BuildSelectedSaveLoadoutRenamePreviewText()
        {
            selectedSaveSlotIndex = Mathf.Clamp(selectedSaveSlotIndex, 0, SaveSlotCount - 1);
            GreedLastRunRecord record = saveLoadoutSlots[selectedSaveSlotIndex];
            if (!record.IsValid)
            {
                return "선택한 슬롯이 비어 있어 이름을 바꿀 수 없습니다.";
            }

            string nextName = BuildNextSaveLoadoutName(selectedSaveSlotIndex);
            return "이름 변경 후보"
                + "\n슬롯 " + (selectedSaveSlotIndex + 1)
                + "\n현재: " + record.LoadoutName
                + "\n변경: " + nextName
                + "\n다시 이름 변경을 누르면 적용됩니다.";
        }

        public string DeleteSelectedSaveLoadoutSlot()
        {
            selectedSaveSlotIndex = Mathf.Clamp(selectedSaveSlotIndex, 0, SaveSlotCount - 1);
            if (!saveLoadoutSlots[selectedSaveSlotIndex].IsValid)
            {
                return "선택한 슬롯은 이미 비어 있습니다.";
            }

            saveLoadoutSlots[selectedSaveSlotIndex] = GreedLastRunRecord.Empty;
            saveLoadoutUsesRemaining[selectedSaveSlotIndex] = 0;
            saveLoadoutRenameSteps[selectedSaveSlotIndex] = 0;
            savedLoadoutConfirmed = HasAnySaveLoadoutSlot();
            SavePersistedData();
            return "슬롯 " + (selectedSaveSlotIndex + 1) + " 저장 조합을 삭제했습니다.";
        }

        public string BuildActiveLoadoutText()
        {
            GreedLastRunRecord record = saveLoadoutSlots[selectedSaveSlotIndex];
            if (!record.IsValid || saveLoadoutUsesRemaining[selectedSaveSlotIndex] <= 0)
            {
                return "저장 조합을 다시 선택해야 합니다.";
            }

            return "슬롯 " + (selectedSaveSlotIndex + 1) + " 조합으로 진입\n"
                + record.LoadoutName
                + $"\n기록 기준 {record.Score}점 / {record.Distance:0.0}m"
                + "\n" + GreedLastRunCore.BuildInfiniteLoadoutBonusPreview(record)
                + "\n남은 사용 " + saveLoadoutUsesRemaining[selectedSaveSlotIndex] + "회";
        }

        public string BuildInfiniteStartReadyText()
        {
            if (!IsSaveSlotUsable(selectedSaveSlotIndex))
            {
                return "선택 슬롯: 슬롯 " + (selectedSaveSlotIndex + 1)
                    + "\n" + FormatSaveSlot(saveLoadoutSlots[selectedSaveSlotIndex], saveLoadoutUsesRemaining[selectedSaveSlotIndex])
                    + "\n\n무한 시작은 사용할 수 없습니다."
                    + "\n저장 조합 변경으로 사용 가능한 슬롯을 고르세요.";
            }

            return BuildActiveLoadoutText()
                + "\n\n무한 시작을 누르면 사용 횟수 1회가 차감됩니다."
                + "\n저장 조합 변경으로 다른 슬롯을 고를 수 있습니다.";
        }

        public string BuildInfiniteLoadoutSelectText()
        {
            bool usable = IsSaveSlotUsable(selectedSaveSlotIndex);
            return "무한모드에 사용할 저장 조합을 선택합니다."
                + "\n\n선택 슬롯: 슬롯 " + (selectedSaveSlotIndex + 1)
                + "\n" + BuildSaveSlotListText()
                + "\n\n선택 슬롯 기록: " + FormatSaveSlot(saveLoadoutSlots[selectedSaveSlotIndex], saveLoadoutUsesRemaining[selectedSaveSlotIndex])
                + "\n선택 상태: " + BuildSelectedInfiniteLoadoutStatusText()
                + "\n좌 / 중 / 우로 슬롯을 고릅니다."
                + (usable
                    ? "\n준비로 돌아가면 선택한 슬롯으로 무한모드를 시작할 수 있습니다."
                    : "\n사용 가능한 슬롯을 고르거나 로비로 돌아가세요.");
        }

        public string BuildSelectedInfiniteLoadoutDetailText()
        {
            GreedLastRunRecord record = saveLoadoutSlots[selectedSaveSlotIndex];
            string slotHeader = "슬롯 " + (selectedSaveSlotIndex + 1) + " 저장 조합 상세";
            if (!record.IsValid)
            {
                return slotHeader
                    + "\n비어 있음"
                    + "\n\n다른 슬롯을 선택하거나 로비로 돌아가세요.";
            }

            return slotHeader
                + "\n\n" + FormatRunRecordDetail(record)
                + "\n" + GreedLastRunCore.BuildInfiniteLoadoutBonusPreview(record)
                + "\n남은 사용 " + saveLoadoutUsesRemaining[selectedSaveSlotIndex] + "회"
                + (IsSaveSlotUsable(selectedSaveSlotIndex)
                    ? "\n\n준비로 돌아가면 이 슬롯으로 무한모드를 시작합니다."
                    : "\n\n사용 완료된 조합입니다. 새 일반 런 클리어 후 이 슬롯을 교체하세요.");
        }

        public bool TryConsumeSelectedSaveLoadout(
            out GreedLastRunRecord selectedRecord,
            out string detail,
            out string errorMessage)
        {
            selectedSaveSlotIndex = Mathf.Clamp(selectedSaveSlotIndex, 0, SaveSlotCount - 1);
            GreedLastRunRecord record = saveLoadoutSlots[selectedSaveSlotIndex];
            if (!record.IsValid)
            {
                selectedRecord = GreedLastRunRecord.Empty;
                detail = string.Empty;
                errorMessage = "선택한 슬롯에 저장 조합이 없습니다.";
                return false;
            }

            int remainingBeforeUse = saveLoadoutUsesRemaining[selectedSaveSlotIndex];
            if (remainingBeforeUse <= 0)
            {
                selectedRecord = GreedLastRunRecord.Empty;
                detail = string.Empty;
                errorMessage = "선택한 저장 조합은 이미 2회 사용했습니다.";
                return false;
            }

            saveLoadoutUsesRemaining[selectedSaveSlotIndex] = Mathf.Max(0, remainingBeforeUse - 1);
            SavePersistedData();
            detail = "슬롯 " + (selectedSaveSlotIndex + 1) + " 조합으로 진입\n"
                + record.LoadoutName
                + $"\n기록 기준 {record.Score}점 / {record.Distance:0.0}m"
                + "\n" + GreedLastRunCore.BuildInfiniteLoadoutBonusPreview(record)
                + "\n이번 입장으로 1회 사용"
                + "\n남은 사용 " + saveLoadoutUsesRemaining[selectedSaveSlotIndex] + "회";
            selectedRecord = record;
            errorMessage = string.Empty;
            return true;
        }

        public bool RegisterInfiniteRun(GreedLastInfiniteRunRecord record)
        {
            if (!record.IsValid)
            {
                return false;
            }

            lastInfiniteRunRecord = record;
            previousBestBeforeLastInfiniteRun = bestInfiniteRunRecord;
            lastInfiniteRunWasNewBest = !bestInfiniteRunRecord.IsValid || IsBetterInfiniteRecord(record, bestInfiniteRunRecord);
            if (lastInfiniteRunWasNewBest)
            {
                bestInfiniteRunRecord = record;
            }

            InsertInfiniteRanking(record);
            infiniteRunCount += 1;
            SavePersistedData();
            return true;
        }

        public string BuildInfiniteRunResultMessage(GreedLastInfiniteRunRecord record)
        {
            if (!record.IsValid)
            {
                return "무한모드 기록이 없습니다.";
            }

            string bestText = bestInfiniteRunRecord.IsValid
                ? FormatInfiniteRecordCompact(bestInfiniteRunRecord)
                : FormatInfiniteRecordCompact(record);
            return "무한모드 기록 저장"
                + "\n이번 " + FormatInfiniteRecordCompact(record)
                + "\n최고 " + bestText
                + "\n" + BuildInfiniteRunResultComparisonText(record);
        }

        public string BuildInfiniteRecordBoardText()
        {
            return BuildRecordBoardText(0);
        }

        public string BuildRecordBoardText(int pageIndex)
        {
            int page = NormalizeRecordBoardPage(pageIndex);
            string latestNormalText = lastRunRecord.IsValid
                ? FormatRunRecordCompact(lastRunRecord)
                : "기록 없음";
            string bestNormalText = bestRunRecord.IsValid
                ? FormatRunRecordCompact(bestRunRecord)
                : "기록 없음";
            string lastAttemptText = lastRunAttemptRecord.IsValid
                ? FormatRunAttemptRecord(lastRunAttemptRecord)
                : "기록 없음";
            string latestText = lastInfiniteRunRecord.IsValid
                ? FormatInfiniteRecord(lastInfiniteRunRecord)
                : "기록 없음";
            string bestText = bestInfiniteRunRecord.IsValid
                ? FormatInfiniteRecord(bestInfiniteRunRecord)
                : "기록 없음";

            switch (page)
            {
                case 1:
                    return "기록 보드 2/4 - 일반 런\n"
                        + "\n상위 기록\n" + BuildNormalRankingText()
                        + "\n\n최근 실패/중단\n" + lastAttemptText
                        + "\n\n실패/중단 기록\n" + BuildNormalAttemptHistoryText();
                case 2:
                    return "기록 보드 3/4 - 무한 기록\n"
                        + "\n최근 기록\n" + latestText
                        + "\n\n최고 기록\n" + bestText;
                case 3:
                    return "기록 보드 4/4 - 무한 랭킹\n"
                        + "\n상위 기록\n" + BuildInfiniteRankingText();
                default:
                    return "기록 보드 1/4 - 요약\n"
                        + "\n일반 런 최근\n" + latestNormalText
                        + "\n\n일반 런 최고\n" + bestNormalText
                        + "\n\n무한모드 최근\n" + latestText
                        + "\n\n무한모드 최고\n" + bestText;
            }
        }

        private static int NormalizeRecordBoardPage(int page)
        {
            return ((page % RecordBoardPageCount) + RecordBoardPageCount) % RecordBoardPageCount;
        }

        public bool ToggleOfflineForEditorCheck()
        {
            online = !online;
            return online;
        }

        private static bool IsBetterRunRecord(GreedLastRunRecord candidate, GreedLastRunRecord currentBest)
        {
            if (candidate.Score != currentBest.Score)
            {
                return candidate.Score > currentBest.Score;
            }

            if (!Mathf.Approximately(candidate.Distance, currentBest.Distance))
            {
                return candidate.Distance > currentBest.Distance;
            }

            if (candidate.Health != currentBest.Health)
            {
                return candidate.Health > currentBest.Health;
            }

            return candidate.Focus > currentBest.Focus;
        }

        private void InsertNormalRanking(GreedLastRunRecord record)
        {
            int insertIndex = NormalRankingCount;
            for (int i = 0; i < normalRunRankings.Length; i += 1)
            {
                if (!normalRunRankings[i].IsValid || IsBetterRunRecord(record, normalRunRankings[i]))
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex >= NormalRankingCount)
            {
                return;
            }

            for (int i = normalRunRankings.Length - 1; i > insertIndex; i -= 1)
            {
                normalRunRankings[i] = normalRunRankings[i - 1];
            }

            normalRunRankings[insertIndex] = record;
        }

        private void InsertNormalAttemptHistory(GreedLastRunAttemptRecord record)
        {
            if (!record.IsValid)
            {
                return;
            }

            for (int i = normalAttemptHistory.Length - 1; i > 0; i -= 1)
            {
                normalAttemptHistory[i] = normalAttemptHistory[i - 1];
            }

            normalAttemptHistory[0] = record;
        }

        private static bool IsBetterInfiniteRecord(GreedLastInfiniteRunRecord candidate, GreedLastInfiniteRunRecord currentBest)
        {
            if (candidate.Score != currentBest.Score)
            {
                return candidate.Score > currentBest.Score;
            }

            if (!Mathf.Approximately(candidate.Distance, currentBest.Distance))
            {
                return candidate.Distance > currentBest.Distance;
            }

            if (candidate.MaxThreatLevel != currentBest.MaxThreatLevel)
            {
                return candidate.MaxThreatLevel > currentBest.MaxThreatLevel;
            }

            return candidate.SectionsCleared > currentBest.SectionsCleared;
        }

        private string BuildInfiniteRunResultComparisonText(GreedLastInfiniteRunRecord record)
        {
            if (!record.IsValid)
            {
                return "비교 기록 없음";
            }

            if (lastInfiniteRunWasNewBest)
            {
                if (previousBestBeforeLastInfiniteRun.IsValid)
                {
                    return "신기록 / 이전 최고 대비 "
                        + FormatInfiniteRecordDelta(record, previousBestBeforeLastInfiniteRun);
                }

                return "첫 무한모드 최고 기록입니다.";
            }

            if (bestInfiniteRunRecord.IsValid)
            {
                return "최고 대비 " + FormatInfiniteRecordDelta(record, bestInfiniteRunRecord);
            }

            return "비교할 최고 기록이 아직 없습니다.";
        }

        private string BuildLatestInfiniteComparisonText()
        {
            if (!lastInfiniteRunRecord.IsValid)
            {
                return "기록 없음";
            }

            if (lastInfiniteRunWasNewBest && previousBestBeforeLastInfiniteRun.IsValid)
            {
                return "최근 기록이 신기록입니다.\n이전 최고 대비 "
                    + FormatInfiniteRecordDelta(lastInfiniteRunRecord, previousBestBeforeLastInfiniteRun);
            }

            if (IsSameInfiniteRecord(lastInfiniteRunRecord, bestInfiniteRunRecord))
            {
                return "최근 기록이 현재 최고 기록입니다.";
            }

            if (bestInfiniteRunRecord.IsValid)
            {
                return "현재 최고 대비 "
                    + FormatInfiniteRecordDelta(lastInfiniteRunRecord, bestInfiniteRunRecord);
            }

            return "비교할 최고 기록이 아직 없습니다.";
        }

        private string BuildLatestNormalComparisonText()
        {
            if (!lastRunRecord.IsValid)
            {
                return "기록 없음";
            }

            if (IsSameRunRecord(lastRunRecord, bestRunRecord))
            {
                return "최근 기록이 현재 최고 기록입니다.";
            }

            if (bestRunRecord.IsValid)
            {
                return "현재 최고 대비 "
                    + FormatRunRecordDelta(lastRunRecord, bestRunRecord);
            }

            return "비교할 최고 기록이 아직 없습니다.";
        }

        private static string FormatRunRecordDelta(GreedLastRunRecord candidate, GreedLastRunRecord baseline)
        {
            if (!candidate.IsValid || !baseline.IsValid)
            {
                return "비교 기준 없음";
            }

            return $"점수 {FormatSigned(candidate.Score - baseline.Score)}"
                + $" / 거리 {FormatSigned(candidate.Distance - baseline.Distance)}m"
                + $" / 체력 {FormatSigned(candidate.Health - baseline.Health)}"
                + $" / 집중 {FormatSigned(candidate.Focus - baseline.Focus)}";
        }

        private static bool IsSameRunRecord(GreedLastRunRecord left, GreedLastRunRecord right)
        {
            return left.IsValid
                && right.IsValid
                && left.Score == right.Score
                && Mathf.Approximately(left.Distance, right.Distance)
                && left.Health == right.Health
                && left.Focus == right.Focus;
        }

        private static string FormatInfiniteRecordDelta(GreedLastInfiniteRunRecord candidate, GreedLastInfiniteRunRecord baseline)
        {
            if (!candidate.IsValid || !baseline.IsValid)
            {
                return "비교 기준 없음";
            }

            int candidateThreat = Mathf.Max(1, candidate.MaxThreatLevel);
            int baselineThreat = Mathf.Max(1, baseline.MaxThreatLevel);
            return $"점수 {FormatSigned(candidate.Score - baseline.Score)}"
                + $" / 거리 {FormatSigned(candidate.Distance - baseline.Distance)}m"
                + $" / 구간 {FormatSigned(candidate.SectionsCleared - baseline.SectionsCleared)}"
                + $" / 위협 {FormatSigned(candidateThreat - baselineThreat)}"
                + $" / 콤보 {FormatSigned(candidate.MaxCombo - baseline.MaxCombo)}"
                + $" / Miss {FormatSigned(candidate.MissCount - baseline.MissCount)}";
        }

        private static bool IsSameInfiniteRecord(GreedLastInfiniteRunRecord left, GreedLastInfiniteRunRecord right)
        {
            return left.IsValid
                && right.IsValid
                && left.Score == right.Score
                && Mathf.Approximately(left.Distance, right.Distance)
                && left.SectionsCleared == right.SectionsCleared
                && left.MaxThreatLevel == right.MaxThreatLevel
                && left.MaxCombo == right.MaxCombo
                && left.MissCount == right.MissCount;
        }

        private void InsertInfiniteRanking(GreedLastInfiniteRunRecord record)
        {
            int insertIndex = InfiniteRankingCount;
            for (int i = 0; i < infiniteRunRankings.Length; i += 1)
            {
                if (!infiniteRunRankings[i].IsValid || IsBetterInfiniteRecord(record, infiniteRunRankings[i]))
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex >= InfiniteRankingCount)
            {
                return;
            }

            for (int i = infiniteRunRankings.Length - 1; i > insertIndex; i -= 1)
            {
                infiniteRunRankings[i] = infiniteRunRankings[i - 1];
            }

            infiniteRunRankings[insertIndex] = record;
        }

        private string BuildInfiniteRankingText()
        {
            string text = string.Empty;
            bool hasRecord = false;
            for (int i = 0; i < infiniteRunRankings.Length; i += 1)
            {
                if (!infiniteRunRankings[i].IsValid)
                {
                    continue;
                }

                if (hasRecord)
                {
                    text += "\n";
                }

                hasRecord = true;
                text += (i + 1) + ". " + FormatInfiniteRankingLine(infiniteRunRankings[i]);
            }

            return hasRecord ? text : "기록 없음";
        }

        private string BuildNormalRankingText()
        {
            string text = string.Empty;
            bool hasRecord = false;
            for (int i = 0; i < normalRunRankings.Length; i += 1)
            {
                if (!normalRunRankings[i].IsValid)
                {
                    continue;
                }

                if (hasRecord)
                {
                    text += "\n";
                }

                hasRecord = true;
                text += (i + 1) + ". " + FormatRunRecordListLine(normalRunRankings[i]);
            }

            return hasRecord ? text : "기록 없음";
        }

        private string BuildNormalAttemptHistoryText()
        {
            string text = string.Empty;
            bool hasRecord = false;
            for (int i = 0; i < normalAttemptHistory.Length; i += 1)
            {
                if (!normalAttemptHistory[i].IsValid)
                {
                    continue;
                }

                if (hasRecord)
                {
                    text += "\n";
                }

                hasRecord = true;
                text += (i + 1) + ". " + FormatRunAttemptListLine(normalAttemptHistory[i]);
            }

            return hasRecord ? text : "기록 없음";
        }

        private string BuildRunProgressSummaryText()
        {
            int normalAttemptCount = normalClearCount + normalFailCount + normalAbandonCount;
            int survivalAttempts = normalClearCount + normalFailCount;
            int survivalRate = survivalAttempts <= 0
                ? 0
                : Mathf.RoundToInt(normalClearCount / (float)survivalAttempts * 100f);

            return $"일반 {normalAttemptCount}회  클리어 {normalClearCount} / 실패 {normalFailCount} / 중단 {normalAbandonCount}"
                + $"\n클리어율 {survivalRate}%  무한 기록 {infiniteRunCount}회";
        }

        private static string FormatInfiniteRecord(GreedLastInfiniteRunRecord record)
        {
            string loadoutText = string.IsNullOrEmpty(record.LoadoutName) ? "조합 -" : record.LoadoutName;
            int threatLevel = Mathf.Max(1, record.MaxThreatLevel);
            return $"[{BuildInfiniteGrade(record)}] {record.Score}점 / {record.Distance:0.0}m / {record.SectionsCleared}구간 / 위협 {threatLevel} / 콤보 {record.MaxCombo}"
                + "\n조합: " + loadoutText
                + "\n타이밍: " + FormatTimingProfile(record.TimingProfile);
        }

        private static string FormatInfiniteRecordCompact(GreedLastInfiniteRunRecord record)
        {
            int threatLevel = Mathf.Max(1, record.MaxThreatLevel);
            return $"[{BuildInfiniteGrade(record)}] {record.Score}점 / {record.Distance:0.0}m / {record.SectionsCleared}구간 / 위협 {threatLevel}";
        }

        private static string FormatInfiniteRecordListLine(GreedLastInfiniteRunRecord record)
        {
            string loadoutText = string.IsNullOrEmpty(record.LoadoutName) ? "조합 -" : record.LoadoutName;
            int threatLevel = Mathf.Max(1, record.MaxThreatLevel);
            return $"[{BuildInfiniteGrade(record)}] {record.Score}점 / {record.Distance:0.0}m / {record.SectionsCleared}구간 / 위협 {threatLevel} / {FormatTimingProfile(record.TimingProfile)} / {loadoutText}";
        }

        private static string FormatInfiniteRankingLine(GreedLastInfiniteRunRecord record)
        {
            if (!record.IsValid)
            {
                return "기록 없음";
            }

            int threatLevel = Mathf.Max(1, record.MaxThreatLevel);
            return $"[{BuildInfiniteGrade(record)}] {record.Score}점 / {record.Distance:0.0}m / {record.SectionsCleared}구간 / 위협 {threatLevel} / 콤보 {record.MaxCombo}";
        }

        private static string FormatRunRecordDetail(GreedLastRunRecord record)
        {
            if (!record.IsValid)
            {
                return "비어 있음";
            }

            return record.LoadoutName
                + "\n등급 " + BuildRunGrade(record)
                + "\n성향 " + BuildLoadoutProfile(record)
                + "\n시작 " + record.StartGift
                + " / 기프트 " + record.PrimaryGift
                + " / 유물 " + record.Relic
                + $"\n점수 {record.Score}  거리 {record.Distance:0.0}m  체력 {record.Health}  집중 {record.Focus}"
                + "\n타이밍 " + FormatTimingProfile(record.TimingProfile)
                + "\n선택 " + record.ChoiceSummary;
        }

        private static string FormatRunRecordCompact(GreedLastRunRecord record)
        {
            if (!record.IsValid)
            {
                return "기록 없음";
            }

            string loadoutText = string.IsNullOrEmpty(record.LoadoutName) ? "조합 -" : record.LoadoutName;
            return $"[{BuildRunGrade(record)}] {BuildLoadoutProfile(record)} / {record.Score}점 / {record.Distance:0.0}m"
                + "\n조합: " + loadoutText
                + "\n타이밍: " + FormatTimingProfile(record.TimingProfile);
        }

        private static string FormatRunRecordListLine(GreedLastRunRecord record)
        {
            if (!record.IsValid)
            {
                return "기록 없음";
            }

            string loadoutText = string.IsNullOrEmpty(record.LoadoutName) ? "조합 -" : record.LoadoutName;
            return $"[{BuildRunGrade(record)}] {BuildLoadoutProfile(record)} / {record.Score}점 / {record.Distance:0.0}m / {FormatTimingProfile(record.TimingProfile)} / {loadoutText}";
        }

        private static string FormatRunAttemptRecord(GreedLastRunAttemptRecord record)
        {
            if (!record.IsValid)
            {
                return "기록 없음";
            }

            string missText = string.IsNullOrEmpty(record.MissReason)
                ? "-"
                : record.MissReason;
            return FormatRunAttemptOutcome(record.Outcome)
                + $" / {record.Score}점 / {record.Distance:0.0}m / 챕터 {record.ChapterIndex} {record.ChapterProgress}/{record.ChapterTarget}"
                + $"\n체력 {record.Health}  집중 {record.Focus}  최대 콤보 {record.MaxCombo}"
                + $"\n판정 성공 {record.SuccessCount}  Good {record.GoodCount}  Miss {record.MissCount}"
                + "\n타이밍 " + FormatTimingProfile(record.TimingProfile)
                + "\n공명핵:" + FormatBool(record.CoreRetrieved) + "  마지막 실수:" + missText
                + "\n선택 " + record.ChoiceSummary;
        }

        private static string FormatRunAttemptListLine(GreedLastRunAttemptRecord record)
        {
            if (!record.IsValid)
            {
                return "기록 없음";
            }

            string missText = string.IsNullOrEmpty(record.MissReason)
                ? "-"
                : record.MissReason;
            return FormatRunAttemptOutcome(record.Outcome)
                + $" / {record.Score}점 / {record.Distance:0.0}m / 챕터 {record.ChapterIndex} {record.ChapterProgress}/{record.ChapterTarget}"
                + $" / Miss {record.MissCount} / {FormatTimingProfile(record.TimingProfile)} / {missText}";
        }

        private static string FormatTimingProfile(string timingProfile)
        {
            return string.IsNullOrEmpty(timingProfile) ? "타이밍 기록 없음" : timingProfile;
        }

        private static string FormatRunAttemptOutcome(GreedLastRunAttemptOutcome outcome)
        {
            switch (outcome)
            {
                case GreedLastRunAttemptOutcome.Failed:
                    return "탈출 실패";
                case GreedLastRunAttemptOutcome.Abandoned:
                    return "탈출 중단";
                default:
                    return "기록 없음";
            }
        }

        private static string BuildLoadoutProfile(GreedLastRunRecord record)
        {
            if (!record.IsValid)
            {
                return "-";
            }

            if (record.Health >= 4
                || ContainsChoice(record.ChoiceSummary, "안정 루트")
                || ContainsChoice(record.StartGift, "안정 호흡")
                || ContainsChoice(record.PrimaryGift, "생존 축")
                || ContainsChoice(record.Relic, "사막의 잔상"))
            {
                return "안정형";
            }

            if (record.Focus >= 3
                || ContainsChoice(record.ChoiceSummary, "변주 루트")
                || ContainsChoice(record.StartGift, "박자 감각")
                || ContainsChoice(record.PrimaryGift, "집중 축"))
            {
                return "집중형";
            }

            if (record.Combo >= 3
                || ContainsChoice(record.PrimaryGift, "연쇄 축")
                || ContainsChoice(record.Relic, "파라오의 각인"))
            {
                return "연쇄형";
            }

            if (ContainsChoice(record.ChoiceSummary, "압박 루트")
                || ContainsChoice(record.StartGift, "고점 본능")
                || ContainsChoice(record.Relic, "황금 박동"))
            {
                return "고점형";
            }

            return "균형형";
        }

        private static bool ContainsChoice(string text, string choice)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(choice, StringComparison.Ordinal) >= 0;
        }

        private static string BuildRunGrade(GreedLastRunRecord record)
        {
            if (!record.IsValid)
            {
                return "-";
            }

            int gradeScore = record.Score + Mathf.RoundToInt(record.Distance * 2f) + record.Health * 80 + record.Focus * 40;
            if (gradeScore >= 1100)
            {
                return "S";
            }

            if (gradeScore >= 820)
            {
                return "A";
            }

            if (gradeScore >= 560)
            {
                return "B";
            }

            return "C";
        }

        private static string BuildInfiniteGrade(GreedLastInfiniteRunRecord record)
        {
            if (!record.IsValid)
            {
                return "-";
            }

            int gradeScore = record.Score
                + record.SectionsCleared * 120
                + Mathf.Max(1, record.MaxThreatLevel) * 60
                + record.MaxCombo * 30
                - record.MissCount * 90;
            if (gradeScore >= 2000)
            {
                return "S";
            }

            if (gradeScore >= 1300)
            {
                return "A";
            }

            if (gradeScore >= 700)
            {
                return "B";
            }

            return "C";
        }

        private static string FormatSigned(int value)
        {
            return value >= 0 ? "+" + value : value.ToString();
        }

        private static string FormatSigned(float value)
        {
            return value >= 0f ? "+" + value.ToString("0.0") : value.ToString("0.0");
        }

        private static string FormatBool(bool value)
        {
            return value ? "Y" : "N";
        }

        private static GreedLastRunRecord WithLoadoutName(GreedLastRunRecord record, string loadoutName)
        {
            return new GreedLastRunRecord(
                record.IsValid,
                record.Score,
                record.Health,
                record.Combo,
                record.Focus,
                record.Distance,
                loadoutName,
                record.StartGift,
                record.PrimaryGift,
                record.Relic,
                record.ChoiceSummary,
                record.TimingProfile);
        }

        private string BuildSaveSlotListText(bool showSelectedSlot = true, bool compact = false)
        {
            string text = string.Empty;
            for (int i = 0; i < saveLoadoutSlots.Length; i += 1)
            {
                if (i > 0)
                {
                    text += "\n";
                }

                string marker = showSelectedSlot && i == selectedSaveSlotIndex ? "> " : "  ";
                string slotText = compact
                    ? FormatSaveSlotCompact(saveLoadoutSlots[i], saveLoadoutUsesRemaining[i])
                    : FormatSaveSlot(saveLoadoutSlots[i], saveLoadoutUsesRemaining[i]);
                text += marker + "슬롯 " + (i + 1) + ": " + slotText;
            }

            return text;
        }

        private string[] BuildSaveSlotLabelSummaries()
        {
            string[] labels = new string[SaveSlotCount];
            for (int i = 0; i < labels.Length; i += 1)
            {
                labels[i] = FormatSaveSlotLabel(saveLoadoutSlots[i], saveLoadoutUsesRemaining[i]);
            }

            return labels;
        }

        private bool[] BuildSaveSlotUsableFlags()
        {
            bool[] flags = new bool[SaveSlotCount];
            for (int i = 0; i < flags.Length; i += 1)
            {
                flags[i] = IsSaveSlotUsable(i);
            }

            return flags;
        }

        private bool[] BuildSaveSlotOccupiedFlags()
        {
            bool[] flags = new bool[SaveSlotCount];
            for (int i = 0; i < flags.Length; i += 1)
            {
                flags[i] = saveLoadoutSlots[i].IsValid;
            }

            return flags;
        }

        private bool IsSaveSlotUsable(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= saveLoadoutSlots.Length)
            {
                return false;
            }

            return saveLoadoutSlots[slotIndex].IsValid
                && saveLoadoutUsesRemaining[slotIndex] > 0;
        }

        private bool HasAnySaveLoadoutSlot()
        {
            for (int i = 0; i < saveLoadoutSlots.Length; i += 1)
            {
                if (saveLoadoutSlots[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAnyUsableSaveLoadoutSlot()
        {
            for (int i = 0; i < saveLoadoutSlots.Length; i += 1)
            {
                if (IsSaveSlotUsable(i))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatSaveSlot(GreedLastRunRecord record, int usesRemaining)
        {
            if (!record.IsValid)
            {
                return "비어 있음";
            }

            string usesText = usesRemaining > 0
                ? usesRemaining + "회 남음"
                : "사용 완료";
            return record.LoadoutName + $" / [{BuildRunGrade(record)}] {BuildLoadoutProfile(record)} / {record.Score}점 / {record.Distance:0.0}m / {usesText}";
        }

        private static string FormatSaveSlotCompact(GreedLastRunRecord record, int usesRemaining)
        {
            if (!record.IsValid)
            {
                return "비어 있음";
            }

            string usesText = usesRemaining > 0
                ? usesRemaining + "회 남음"
                : "사용 완료";
            return record.LoadoutName + $" / {BuildRunGrade(record)} / {record.Score}점 / {usesText}";
        }

        private static string FormatSaveCandidateSummary(GreedLastRunRecord record)
        {
            if (!record.IsValid)
            {
                return "후보 없음";
            }

            return record.LoadoutName
                + $" / {BuildRunGrade(record)} / {record.Score}점 / {record.Distance:0.0}m"
                + "\n" + record.StartGift + " + " + record.PrimaryGift + " + " + record.Relic;
        }

        private static string BuildOverwriteWarningText(GreedLastRunRecord record, int usesRemaining)
        {
            if (!record.IsValid)
            {
                return "\n빈 슬롯이라 바로 저장됩니다.";
            }

            if (usesRemaining <= 0)
            {
                return "\n사용 완료된 조합입니다. 저장하면 새 후보로 교체됩니다.";
            }

            return "\n기존 저장 조합이 있습니다. 저장하려면 한 번 더 확정해야 합니다.";
        }

        private static string FormatSaveSlotLabel(GreedLastRunRecord record, int usesRemaining)
        {
            if (!record.IsValid)
            {
                return "빈 슬롯";
            }

            return usesRemaining > 0
                ? usesRemaining + "회"
                : "사용 완료";
        }

        private string BuildSelectedInfiniteLoadoutStatusText()
        {
            GreedLastRunRecord record = saveLoadoutSlots[selectedSaveSlotIndex];
            int usesRemaining = saveLoadoutUsesRemaining[selectedSaveSlotIndex];
            if (!record.IsValid)
            {
                return "비어 있음 - 무한모드 시작 불가";
            }

            if (usesRemaining <= 0)
            {
                return "사용 완료 - 새 저장 조합으로 교체 필요";
            }

            return "시작 가능 - 남은 사용 " + usesRemaining + "회";
        }

        private static string BuildSaveLoadoutNameForStep(int slotIndex, int step)
        {
            int safeStep = Mathf.Clamp(step, 0, SaveLoadoutNamePresets.Length - 1);
            return SaveLoadoutNamePresets[safeStep] + " S" + (slotIndex + 1);
        }

        private string BuildNextSaveLoadoutName(int slotIndex)
        {
            int safeSlotIndex = Mathf.Clamp(slotIndex, 0, SaveSlotCount - 1);
            int nextStep = (saveLoadoutRenameSteps[safeSlotIndex] + 1) % SaveLoadoutNamePresets.Length;
            return BuildSaveLoadoutNameForStep(safeSlotIndex, nextStep);
        }

        private void LoadPersistedData()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                return;
            }

            string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            BackendSaveData data;
            try
            {
                data = JsonUtility.FromJson<BackendSaveData>(json);
            }
            catch (ArgumentException)
            {
                return;
            }

            if (data == null || data.version != SaveDataVersion)
            {
                return;
            }

            firstSaveCompleted = data.firstSaveCompleted;
            saveLoadoutUnlocked = data.saveLoadoutUnlocked;
            savedLoadoutConfirmed = data.savedLoadoutConfirmed;
            saveLoadoutCandidateAvailable = data.saveLoadoutCandidateAvailable && data.lastRunRecord != null && data.lastRunRecord.isValid;
            selectedSaveSlotIndex = Mathf.Clamp(data.selectedSaveSlotIndex, 0, SaveSlotCount - 1);
            lastRunRecord = FromData(data.lastRunRecord);
            lastRunAttemptRecord = FromData(data.lastRunAttemptRecord);
            bestRunRecord = FromData(data.bestRunRecord);
            lastInfiniteRunRecord = FromData(data.lastInfiniteRunRecord);
            bestInfiniteRunRecord = FromData(data.bestInfiniteRunRecord);
            normalClearCount = Mathf.Max(0, data.normalClearCount);
            normalFailCount = Mathf.Max(0, data.normalFailCount);
            normalAbandonCount = Mathf.Max(0, data.normalAbandonCount);
            infiniteRunCount = Mathf.Max(0, data.infiniteRunCount);
            for (int i = 0; i < normalRunRankings.Length; i += 1)
            {
                normalRunRankings[i] = GreedLastRunRecord.Empty;
            }

            for (int i = 0; i < normalAttemptHistory.Length; i += 1)
            {
                normalAttemptHistory[i] = GreedLastRunAttemptRecord.Empty;
            }

            for (int i = 0; i < infiniteRunRankings.Length; i += 1)
            {
                infiniteRunRankings[i] = GreedLastInfiniteRunRecord.Empty;
            }

            for (int i = 0; i < saveLoadoutSlots.Length; i += 1)
            {
                saveLoadoutSlots[i] = GreedLastRunRecord.Empty;
                saveLoadoutUsesRemaining[i] = 0;
                saveLoadoutRenameSteps[i] = 0;
            }

            if (data.saveLoadoutSlots != null)
            {
                int count = Mathf.Min(saveLoadoutSlots.Length, data.saveLoadoutSlots.Length);
                for (int i = 0; i < count; i += 1)
                {
                    saveLoadoutSlots[i] = FromData(data.saveLoadoutSlots[i]);
                    saveLoadoutUsesRemaining[i] = saveLoadoutSlots[i].IsValid ? MaxSaveLoadoutUses : 0;
                }
            }

            if (data.saveLoadoutUsesRemaining != null)
            {
                int count = Mathf.Min(saveLoadoutUsesRemaining.Length, data.saveLoadoutUsesRemaining.Length);
                for (int i = 0; i < count; i += 1)
                {
                    saveLoadoutUsesRemaining[i] = saveLoadoutSlots[i].IsValid
                        ? Mathf.Clamp(data.saveLoadoutUsesRemaining[i], 0, MaxSaveLoadoutUses)
                        : 0;
                }
            }

            if (data.saveLoadoutRenameSteps != null)
            {
                int count = Mathf.Min(saveLoadoutRenameSteps.Length, data.saveLoadoutRenameSteps.Length);
                for (int i = 0; i < count; i += 1)
                {
                    saveLoadoutRenameSteps[i] = Mathf.Clamp(data.saveLoadoutRenameSteps[i], 0, SaveLoadoutNamePresets.Length - 1);
                }
            }

            savedLoadoutConfirmed = savedLoadoutConfirmed && HasAnySaveLoadoutSlot();

            if (data.normalRunRankings != null)
            {
                int count = Mathf.Min(normalRunRankings.Length, data.normalRunRankings.Length);
                for (int i = 0; i < count; i += 1)
                {
                    normalRunRankings[i] = FromData(data.normalRunRankings[i]);
                }
            }
            else if (lastRunRecord.IsValid)
            {
                normalRunRankings[0] = lastRunRecord;
            }

            if (!bestRunRecord.IsValid && lastRunRecord.IsValid)
            {
                bestRunRecord = lastRunRecord;
            }

            if (data.normalAttemptHistory != null)
            {
                int count = Mathf.Min(normalAttemptHistory.Length, data.normalAttemptHistory.Length);
                for (int i = 0; i < count; i += 1)
                {
                    normalAttemptHistory[i] = FromData(data.normalAttemptHistory[i]);
                }
            }
            else if (lastRunAttemptRecord.IsValid)
            {
                normalAttemptHistory[0] = lastRunAttemptRecord;
            }

            if (normalClearCount == 0 && lastRunRecord.IsValid)
            {
                normalClearCount = 1;
            }

            if (normalFailCount == 0
                && normalAbandonCount == 0
                && lastRunAttemptRecord.IsValid)
            {
                if (lastRunAttemptRecord.Outcome == GreedLastRunAttemptOutcome.Failed)
                {
                    normalFailCount = 1;
                }
                else if (lastRunAttemptRecord.Outcome == GreedLastRunAttemptOutcome.Abandoned)
                {
                    normalAbandonCount = 1;
                }
            }

            if (data.infiniteRunRankings != null)
            {
                int count = Mathf.Min(infiniteRunRankings.Length, data.infiniteRunRankings.Length);
                for (int i = 0; i < count; i += 1)
                {
                    infiniteRunRankings[i] = FromData(data.infiniteRunRankings[i]);
                }
            }
            else if (bestInfiniteRunRecord.IsValid)
            {
                infiniteRunRankings[0] = bestInfiniteRunRecord;
            }
        }

        private void SavePersistedData()
        {
            var data = new BackendSaveData
            {
                version = SaveDataVersion,
                firstSaveCompleted = firstSaveCompleted,
                saveLoadoutUnlocked = saveLoadoutUnlocked,
                savedLoadoutConfirmed = savedLoadoutConfirmed,
                saveLoadoutCandidateAvailable = saveLoadoutCandidateAvailable,
                selectedSaveSlotIndex = Mathf.Clamp(selectedSaveSlotIndex, 0, SaveSlotCount - 1),
                lastRunRecord = ToData(lastRunRecord),
                lastRunAttemptRecord = ToData(lastRunAttemptRecord),
                bestRunRecord = ToData(bestRunRecord),
                normalRunRankings = new RunRecordSaveData[NormalRankingCount],
                normalAttemptHistory = new RunAttemptRecordSaveData[NormalAttemptHistoryCount],
                normalClearCount = normalClearCount,
                normalFailCount = normalFailCount,
                normalAbandonCount = normalAbandonCount,
                saveLoadoutSlots = new RunRecordSaveData[SaveSlotCount],
                saveLoadoutUsesRemaining = new int[SaveSlotCount],
                saveLoadoutRenameSteps = new int[SaveSlotCount],
                lastInfiniteRunRecord = ToData(lastInfiniteRunRecord),
                bestInfiniteRunRecord = ToData(bestInfiniteRunRecord),
                infiniteRunRankings = new InfiniteRecordSaveData[InfiniteRankingCount],
                infiniteRunCount = infiniteRunCount,
            };

            for (int i = 0; i < data.normalRunRankings.Length; i += 1)
            {
                data.normalRunRankings[i] = ToData(normalRunRankings[i]);
            }

            for (int i = 0; i < data.normalAttemptHistory.Length; i += 1)
            {
                data.normalAttemptHistory[i] = ToData(normalAttemptHistory[i]);
            }

            for (int i = 0; i < data.saveLoadoutSlots.Length; i += 1)
            {
                data.saveLoadoutSlots[i] = ToData(saveLoadoutSlots[i]);
                data.saveLoadoutUsesRemaining[i] = Mathf.Clamp(saveLoadoutUsesRemaining[i], 0, MaxSaveLoadoutUses);
                data.saveLoadoutRenameSteps[i] = Mathf.Clamp(saveLoadoutRenameSteps[i], 0, SaveLoadoutNamePresets.Length - 1);
            }

            for (int i = 0; i < data.infiniteRunRankings.Length; i += 1)
            {
                data.infiniteRunRankings[i] = ToData(infiniteRunRankings[i]);
            }

            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private static RunRecordSaveData ToData(GreedLastRunRecord record)
        {
            return new RunRecordSaveData
            {
                isValid = record.IsValid,
                score = record.Score,
                health = record.Health,
                combo = record.Combo,
                focus = record.Focus,
                distance = record.Distance,
                loadoutName = record.LoadoutName,
                startGift = record.StartGift,
                primaryGift = record.PrimaryGift,
                relic = record.Relic,
                choiceSummary = record.ChoiceSummary,
                timingProfile = record.TimingProfile,
            };
        }

        private static GreedLastRunRecord FromData(RunRecordSaveData data)
        {
            if (data == null || !data.isValid)
            {
                return GreedLastRunRecord.Empty;
            }

            return new GreedLastRunRecord(
                true,
                data.score,
                data.health,
                data.combo,
                data.focus,
                data.distance,
                data.loadoutName,
                data.startGift,
                data.primaryGift,
                data.relic,
                data.choiceSummary,
                data.timingProfile);
        }

        private static RunAttemptRecordSaveData ToData(GreedLastRunAttemptRecord record)
        {
            return new RunAttemptRecordSaveData
            {
                isValid = record.IsValid,
                outcome = (int)record.Outcome,
                score = record.Score,
                health = record.Health,
                combo = record.Combo,
                focus = record.Focus,
                maxCombo = record.MaxCombo,
                successCount = record.SuccessCount,
                goodCount = record.GoodCount,
                missCount = record.MissCount,
                distance = record.Distance,
                chapterIndex = record.ChapterIndex,
                chapterProgress = record.ChapterProgress,
                chapterTarget = record.ChapterTarget,
                coreRetrieved = record.CoreRetrieved,
                missReason = record.MissReason,
                choiceSummary = record.ChoiceSummary,
                timingProfile = record.TimingProfile,
            };
        }

        private static GreedLastRunAttemptRecord FromData(RunAttemptRecordSaveData data)
        {
            if (data == null || !data.isValid)
            {
                return GreedLastRunAttemptRecord.Empty;
            }

            GreedLastRunAttemptOutcome outcome = Enum.IsDefined(typeof(GreedLastRunAttemptOutcome), data.outcome)
                ? (GreedLastRunAttemptOutcome)data.outcome
                : GreedLastRunAttemptOutcome.None;
            if (outcome == GreedLastRunAttemptOutcome.None)
            {
                return GreedLastRunAttemptRecord.Empty;
            }

            return new GreedLastRunAttemptRecord(
                true,
                outcome,
                data.score,
                data.health,
                data.combo,
                data.focus,
                data.maxCombo,
                data.successCount,
                data.goodCount,
                data.missCount,
                data.distance,
                Mathf.Max(1, data.chapterIndex),
                Mathf.Max(0, data.chapterProgress),
                Mathf.Max(1, data.chapterTarget),
                data.coreRetrieved,
                data.missReason,
                data.choiceSummary,
                data.timingProfile);
        }

        private static InfiniteRecordSaveData ToData(GreedLastInfiniteRunRecord record)
        {
            return new InfiniteRecordSaveData
            {
                isValid = record.IsValid,
                score = record.Score,
                distance = record.Distance,
                sectionsCleared = record.SectionsCleared,
                maxThreatLevel = record.MaxThreatLevel,
                loadoutName = record.LoadoutName,
                successCount = record.SuccessCount,
                goodCount = record.GoodCount,
                missCount = record.MissCount,
                maxCombo = record.MaxCombo,
                timingProfile = record.TimingProfile,
            };
        }

        private static GreedLastInfiniteRunRecord FromData(InfiniteRecordSaveData data)
        {
            if (data == null || !data.isValid)
            {
                return GreedLastInfiniteRunRecord.Empty;
            }

            return new GreedLastInfiniteRunRecord(
                true,
                data.score,
                data.distance,
                data.sectionsCleared,
                Mathf.Max(1, data.maxThreatLevel),
                string.IsNullOrEmpty(data.loadoutName) ? "기록 조합" : data.loadoutName,
                data.successCount,
                data.goodCount,
                data.missCount,
                data.maxCombo,
                data.timingProfile);
        }

        [Serializable]
        private sealed class BackendSaveData
        {
            public int version;
            public bool firstSaveCompleted;
            public bool saveLoadoutUnlocked;
            public bool savedLoadoutConfirmed;
            public bool saveLoadoutCandidateAvailable;
            public int selectedSaveSlotIndex;
            public RunRecordSaveData lastRunRecord;
            public RunAttemptRecordSaveData lastRunAttemptRecord;
            public RunRecordSaveData bestRunRecord;
            public RunRecordSaveData[] normalRunRankings;
            public RunAttemptRecordSaveData[] normalAttemptHistory;
            public int normalClearCount;
            public int normalFailCount;
            public int normalAbandonCount;
            public RunRecordSaveData[] saveLoadoutSlots;
            public int[] saveLoadoutUsesRemaining;
            public int[] saveLoadoutRenameSteps;
            public InfiniteRecordSaveData lastInfiniteRunRecord;
            public InfiniteRecordSaveData bestInfiniteRunRecord;
            public InfiniteRecordSaveData[] infiniteRunRankings;
            public int infiniteRunCount;
        }

        [Serializable]
        private sealed class RunRecordSaveData
        {
            public bool isValid;
            public int score;
            public int health;
            public int combo;
            public int focus;
            public float distance;
            public string loadoutName;
            public string startGift;
            public string primaryGift;
            public string relic;
            public string choiceSummary;
            public string timingProfile;
        }

        [Serializable]
        private sealed class RunAttemptRecordSaveData
        {
            public bool isValid;
            public int outcome;
            public int score;
            public int health;
            public int combo;
            public int focus;
            public int maxCombo;
            public int successCount;
            public int goodCount;
            public int missCount;
            public float distance;
            public int chapterIndex;
            public int chapterProgress;
            public int chapterTarget;
            public bool coreRetrieved;
            public string missReason;
            public string choiceSummary;
            public string timingProfile;
        }

        [Serializable]
        private sealed class InfiniteRecordSaveData
        {
            public bool isValid;
            public int score;
            public float distance;
            public int sectionsCleared;
            public int maxThreatLevel;
            public string loadoutName;
            public int successCount;
            public int goodCount;
            public int missCount;
            public int maxCombo;
            public string timingProfile;
        }
    }

    public sealed class GreedLastStateMachine
    {
        private readonly GreedLastRequestGate requestGate;
        private GreedLastLobbySnapshot lobbySnapshot;
        private int recordBoardPageIndex = -1;

        public GreedLastStateMachine(GreedLastRequestGate requestGate)
        {
            this.requestGate = requestGate;
        }

        public event Action<GreedLastStateSnapshot> StateChanged;

        public GreedLastScreenState CurrentState { get; private set; }
        public GreedLastConnectBlockReason BlockReason { get; private set; }

        public GreedLastLobbySnapshot LobbySnapshot => lobbySnapshot;

        public void SetBoot(string detail)
        {
            CurrentState = GreedLastScreenState.BootInit;
            BlockReason = GreedLastConnectBlockReason.None;
            Publish("GREED LAST", detail, coreActionsEnabled: false, retryVisible: false);
        }

        public void SetConnectChecking()
        {
            CurrentState = GreedLastScreenState.ConnectChecking;
            BlockReason = GreedLastConnectBlockReason.None;
            Publish("연결 확인", "세션과 버전을 확인하는 중입니다.", coreActionsEnabled: false, retryVisible: false);
        }

        public void SetConnectBlocked(GreedLastConnectBlockReason reason, string message)
        {
            CurrentState = GreedLastScreenState.ConnectBlocked;
            BlockReason = reason;
            Publish("진입 보류", message, coreActionsEnabled: false, retryVisible: true);
        }

        public void SetLobbyLoading()
        {
            CurrentState = GreedLastScreenState.LobbyLoading;
            BlockReason = GreedLastConnectBlockReason.None;
            Publish("로비 동기화", "진입 가능한 로비 데이터를 준비하고 있습니다.", coreActionsEnabled: false, retryVisible: false);
        }

        public void SetLobbyReady(GreedLastLobbySnapshot snapshot)
        {
            lobbySnapshot = snapshot;
            CurrentState = GreedLastScreenState.LobbyReady;
            BlockReason = GreedLastConnectBlockReason.None;
            Publish("피라미드 입구", snapshot.Message, coreActionsEnabled: true, retryVisible: false);
        }

        public void SetReSyncPending(string message, bool coreActionsEnabled)
        {
            CurrentState = GreedLastScreenState.ReSyncPending;
            Publish("재동기화 필요", message, coreActionsEnabled, retryVisible: true);
        }

        public void ShowLocalNotice(string message)
        {
            Publish("피라미드 입구", message, CurrentState == GreedLastScreenState.LobbyReady, retryVisible: false);
        }

        public void SetRunCoreTest()
        {
            CurrentState = GreedLastScreenState.RunCoreTest;
            BlockReason = GreedLastConnectBlockReason.None;
            Publish("함정 대응 검증", "좌 / 중 / 우 채널과 타이밍 판정을 확인합니다.", coreActionsEnabled: true, retryVisible: false);
        }

        public void SetInfiniteRun(string detail)
        {
            CurrentState = GreedLastScreenState.InfiniteRun;
            BlockReason = GreedLastConnectBlockReason.None;
            Publish("무한모드", detail, coreActionsEnabled: true, retryVisible: false);
        }

        public void SetInfiniteStartReady(GreedLastLobbySnapshot snapshot, string detail)
        {
            lobbySnapshot = snapshot;
            CurrentState = GreedLastScreenState.InfiniteStartReady;
            BlockReason = GreedLastConnectBlockReason.None;
            Publish("무한모드 준비", detail, coreActionsEnabled: true, retryVisible: false);
        }

        public void SetInfiniteLoadoutSelect(
            GreedLastLobbySnapshot snapshot,
            string detail,
            bool saveSlotDeleteConfirmationPending = false,
            bool saveSlotDetailViewActive = false,
            bool saveSlotRenameConfirmationPending = false)
        {
            lobbySnapshot = snapshot;
            CurrentState = GreedLastScreenState.InfiniteLoadoutSelect;
            BlockReason = GreedLastConnectBlockReason.None;
            Publish(
                "저장 조합 선택",
                detail,
                coreActionsEnabled: true,
                retryVisible: false,
                saveSlotDeleteConfirmationPending: saveSlotDeleteConfirmationPending,
                saveSlotDetailViewActive: saveSlotDetailViewActive,
                saveSlotRenameConfirmationPending: saveSlotRenameConfirmationPending);
        }

        public void SetSaveLoadoutDraft(
            GreedLastLobbySnapshot snapshot,
            string detail,
            bool saveSlotChoiceRequired = false,
            bool saveSlotOverwriteConfirmationPending = false,
            bool saveSlotDeleteConfirmationPending = false,
            bool saveSlotDetailViewActive = false,
            bool saveSlotRenameConfirmationPending = false,
            bool saveLoadoutCandidateAvailable = false)
        {
            lobbySnapshot = snapshot;
            CurrentState = GreedLastScreenState.SaveLoadoutDraft;
            BlockReason = GreedLastConnectBlockReason.None;
            Publish(
                "저장 조합 후보",
                detail,
                coreActionsEnabled: true,
                retryVisible: false,
                saveSlotChoiceRequired: saveSlotChoiceRequired,
                saveSlotOverwriteConfirmationPending: saveSlotOverwriteConfirmationPending,
                saveSlotDeleteConfirmationPending: saveSlotDeleteConfirmationPending,
                saveSlotDetailViewActive: saveSlotDetailViewActive,
                saveSlotRenameConfirmationPending: saveSlotRenameConfirmationPending,
                saveLoadoutCandidateAvailable: saveLoadoutCandidateAvailable);
        }

        public void SetInfiniteRecordBoard(GreedLastLobbySnapshot snapshot, string detail, int pageIndex)
        {
            lobbySnapshot = snapshot;
            recordBoardPageIndex = pageIndex;
            CurrentState = GreedLastScreenState.InfiniteRecordBoard;
            BlockReason = GreedLastConnectBlockReason.None;
            Publish("기록 보드", detail, coreActionsEnabled: true, retryVisible: false);
        }

        private void Publish(
            string headline,
            string detail,
            bool coreActionsEnabled,
            bool retryVisible,
            bool saveSlotChoiceRequired = false,
            bool saveSlotOverwriteConfirmationPending = false,
            bool saveSlotDeleteConfirmationPending = false,
            bool saveSlotDetailViewActive = false,
            bool saveSlotRenameConfirmationPending = false,
            bool saveLoadoutCandidateAvailable = false)
        {
            StateChanged?.Invoke(new GreedLastStateSnapshot(
                CurrentState,
                requestGate.ActiveRequestId,
                headline,
                detail,
                requestGate.Busy,
                coreActionsEnabled && !requestGate.Busy,
                lobbySnapshot.FirstSaveCompleted,
                lobbySnapshot.SaveLoadoutUnlocked,
                lobbySnapshot.InfiniteUnlocked,
                lobbySnapshot.SelectedSaveSlotIndex,
                lobbySnapshot.SaveSlotLabels,
                lobbySnapshot.SaveSlotUsable,
                lobbySnapshot.SaveSlotOccupied,
                saveSlotChoiceRequired,
                saveSlotOverwriteConfirmationPending,
                saveSlotDeleteConfirmationPending,
                saveSlotDetailViewActive,
                saveSlotRenameConfirmationPending,
                saveLoadoutCandidateAvailable,
                CurrentState == GreedLastScreenState.InfiniteRecordBoard ? recordBoardPageIndex : -1,
                retryVisible && !requestGate.Busy,
                BlockReason));
        }
    }

    public sealed class GreedLastApp : MonoBehaviour
    {
        private GreedLastRequestGate requestGate;
        private GreedLastMockBackend backend;
        private GreedLastStateMachine stateMachine;
        private GreedLastRunCore runCore;
        private GreedLastRuntimeUi runtimeUi;
        private int recordBoardPageIndex;
        private int preferredRecordBoardPageIndex;
        private bool saveDraftRequiresExplicitSlotChoice;
        private bool saveDraftOverwriteConfirmPending;
        private bool saveSlotDeleteConfirmPending;
        private bool saveSlotDetailViewActive;
        private bool saveSlotRenameConfirmPending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var root = new GameObject(nameof(GreedLastApp));
            root.AddComponent<GreedLastApp>();
            DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;

            requestGate = new GreedLastRequestGate();
            backend = new GreedLastMockBackend();
            stateMachine = new GreedLastStateMachine(requestGate);
            runCore = new GreedLastRunCore();

            runtimeUi = gameObject.AddComponent<GreedLastRuntimeUi>();
            runtimeUi.Initialize(
                RequestNormalRun,
                RequestSaveLoadout,
                RequestInfiniteRun,
                RequestInfiniteRecordBoard,
                RequestRetrySync,
                ToggleOfflineForEditorCheck,
                RequestNextRunPattern,
                RequestToggleRunPause,
                RequestReturnToLobby,
                RequestShowSaveLoadoutDetail,
                RequestRenameSaveLoadoutSlot,
                RequestDeleteSaveLoadoutSlot,
                ToggleRunInvincible,
                RequestInfiniteTestStop,
                RequestFocusMaxForTest,
                RequestThreatUpForTest,
                RequestThreatDownForTest,
                RequestTimingOffsetAdjust,
                RequestRunDevShortcut,
                HandleRunChannelInput);

            stateMachine.StateChanged += runtimeUi.Render;
            runCore.SnapshotChanged += runtimeUi.RenderRun;
        }

        private void Start()
        {
            StartCoroutine(BootThenConnect());
        }

        private void OnDestroy()
        {
            if (stateMachine != null && runtimeUi != null)
            {
                stateMachine.StateChanged -= runtimeUi.Render;
            }

            if (runCore != null && runtimeUi != null)
            {
                runCore.SnapshotChanged -= runtimeUi.RenderRun;
            }
        }

        private void Update()
        {
            if (stateMachine == null || runCore == null)
            {
                return;
            }

            if (IsRunState())
            {
                runCore.Tick(Time.realtimeSinceStartup);
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    RequestToggleRunPause();
                    return;
                }
            }
            else if (Input.GetKeyDown(KeyCode.Escape) && TryHandleBackKeyboardInput())
            {
                return;
            }

            if (TryHandleLaneKeyboardInput())
            {
                return;
            }
        }

        private bool TryHandleLaneKeyboardInput()
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                HandleRunChannelInput(GreedLastRunChannel.Left);
                return true;
            }
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                HandleRunChannelInput(GreedLastRunChannel.Center);
                return true;
            }
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                HandleRunChannelInput(GreedLastRunChannel.Right);
                return true;
            }

            return false;
        }

        private bool TryHandleBackKeyboardInput()
        {
            if (stateMachine.CurrentState == GreedLastScreenState.SaveLoadoutDraft
                || stateMachine.CurrentState == GreedLastScreenState.InfiniteLoadoutSelect
                || stateMachine.CurrentState == GreedLastScreenState.InfiniteRecordBoard
                || stateMachine.CurrentState == GreedLastScreenState.InfiniteStartReady)
            {
                RequestReturnToLobby();
                return true;
            }

            return false;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && stateMachine != null && runCore != null && IsRunState())
            {
                runCore.PauseForAppBackground(Time.realtimeSinceStartup);
                return;
            }

            if (!pauseStatus
                && stateMachine != null
                && stateMachine.CurrentState == GreedLastScreenState.LobbyReady)
            {
                stateMachine.SetReSyncPending("앱 복귀 후 최신 로비 상태를 다시 확인해야 합니다.", coreActionsEnabled: false);
            }
        }

        private IEnumerator BootThenConnect()
        {
            if (!requestGate.TryBegin(GreedLastRequestKind.BootInit, out GreedLastRequestToken token))
            {
                yield break;
            }

            stateMachine.SetBoot("로컬 설정을 준비하는 중입니다.");
            yield return new WaitForSecondsRealtime(0.35f);
            requestGate.Complete(token);
            yield return ConnectThenLoadLobby();
        }

        private IEnumerator ConnectThenLoadLobby()
        {
            if (!requestGate.TryBegin(GreedLastRequestKind.ConnectCheck, out GreedLastRequestToken connectToken))
            {
                yield break;
            }

            stateMachine.SetConnectChecking();
            yield return new WaitForSecondsRealtime(0.45f);

            GreedLastConnectResult result = backend.CheckConnect();
            if (!requestGate.IsCurrent(connectToken))
            {
                yield break;
            }

            requestGate.Complete(connectToken);

            if (!result.Success)
            {
                stateMachine.SetConnectBlocked(result.Reason, result.Message);
                yield break;
            }

            yield return LoadLobby();
        }

        private IEnumerator LoadLobby()
        {
            if (!requestGate.TryBegin(GreedLastRequestKind.LobbySync, out GreedLastRequestToken token))
            {
                yield break;
            }

            stateMachine.SetLobbyLoading();
            yield return new WaitForSecondsRealtime(0.45f);

            GreedLastLobbySnapshot snapshot = backend.LoadLobby();
            if (!requestGate.IsCurrent(token))
            {
                yield break;
            }

            requestGate.Complete(token);

            if (snapshot.AuxiliaryWarning)
            {
                stateMachine.SetReSyncPending(snapshot.Message, coreActionsEnabled: true);
            }
            else
            {
                stateMachine.SetLobbyReady(snapshot);
            }
        }

        private void RequestRetrySync()
        {
            if (requestGate.Busy)
            {
                return;
            }

            StartCoroutine(RetrySyncRoutine());
        }

        private IEnumerator RetrySyncRoutine()
        {
            if (!requestGate.TryBegin(GreedLastRequestKind.ReSync, out GreedLastRequestToken token))
            {
                yield break;
            }

            stateMachine.SetReSyncPending("최신 로비 상태를 다시 확인하고 있습니다.", coreActionsEnabled: false);
            yield return new WaitForSecondsRealtime(0.4f);

            GreedLastConnectResult connectResult = backend.CheckConnect();
            if (!requestGate.IsCurrent(token))
            {
                yield break;
            }

            if (!connectResult.Success)
            {
                requestGate.Complete(token);

                if (stateMachine.CurrentState == GreedLastScreenState.LobbyReady
                    || stateMachine.CurrentState == GreedLastScreenState.ReSyncPending)
                {
                    stateMachine.SetReSyncPending(connectResult.Message, coreActionsEnabled: false);
                }
                else
                {
                    stateMachine.SetConnectBlocked(connectResult.Reason, connectResult.Message);
                }

                yield break;
            }

            GreedLastLobbySnapshot snapshot = backend.ReSyncLobby();
            requestGate.Complete(token);
            stateMachine.SetLobbyReady(snapshot);
        }

        private void RequestNormalRun()
        {
            if (!TryBeginLobbyAction(out GreedLastRequestToken token))
            {
                return;
            }

            requestGate.Complete(token);
            stateMachine.SetRunCoreTest();
            runCore.Enter();
        }

        private void RequestSaveLoadout()
        {
            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteStartReady)
            {
                saveSlotDeleteConfirmPending = false;
                saveSlotDetailViewActive = false;
                saveSlotRenameConfirmPending = false;
                stateMachine.SetInfiniteLoadoutSelect(backend.LoadLobby(), backend.BuildInfiniteLoadoutSelectText());
                return;
            }

            if (!TryBeginLobbyAction(out GreedLastRequestToken token))
            {
                return;
            }

            GreedLastLobbySnapshot snapshot = stateMachine.LobbySnapshot;
            requestGate.Complete(token);

            if (snapshot.SaveLoadoutUnlocked)
            {
                OpenSaveLoadoutDraft(
                    snapshot,
                    requireExplicitSlotChoice: false,
                    backend.HasSaveLoadoutCandidate
                        ? null
                        : "저장할 새 클리어 후보는 없습니다.\n기존 저장 슬롯은 여기서 관리할 수 있습니다.");
            }
            else
            {
                stateMachine.ShowLocalNotice("저장할 새 클리어 후보가 없습니다.\n일반 런을 다시 클리어하면 새 저장 기회가 생깁니다.");
            }
        }

        private void RequestInfiniteRun()
        {
            if (!TryBeginLobbyAction(out GreedLastRequestToken token))
            {
                return;
            }

            GreedLastLobbySnapshot snapshot = stateMachine.LobbySnapshot;
            requestGate.Complete(token);

            if (!snapshot.InfiniteUnlocked)
            {
                stateMachine.ShowLocalNotice(snapshot.SaveLoadoutUnlocked
                    ? "사용 가능한 저장 조합이 없습니다. 일반 런 클리어 뒤 저장 조합을 다시 확정하세요."
                    : "무한모드는 저장 조합 확정 뒤 열립니다.");
                return;
            }

            stateMachine.SetInfiniteStartReady(backend.LoadLobby(), backend.BuildInfiniteStartReadyText());
        }

        private void StartInfiniteRunFromSelectedLoadout()
        {
            if (!backend.TryConsumeSelectedSaveLoadout(out GreedLastRunRecord selectedLoadout, out string runDetail, out string errorMessage))
            {
                runCore.Exit();
                stateMachine.SetInfiniteStartReady(
                    backend.LoadLobby(),
                    errorMessage + "\n\n" + backend.BuildInfiniteStartReadyText());
                return;
            }

            stateMachine.SetInfiniteRun(runDetail);
            runCore.EnterInfiniteRun(selectedLoadout);
        }

        private void RequestInfiniteRecordBoard()
        {
            if (!TryBeginLobbyAction(out GreedLastRequestToken token))
            {
                return;
            }

            requestGate.Complete(token);
            OpenRecordBoard(preferredRecordBoardPageIndex);
        }

        private bool TryBeginLobbyAction(out GreedLastRequestToken token)
        {
            if (stateMachine.CurrentState != GreedLastScreenState.LobbyReady
                && stateMachine.CurrentState != GreedLastScreenState.ReSyncPending)
            {
                token = default;
                return false;
            }

            return requestGate.TryBegin(GreedLastRequestKind.LobbyAction, out token);
        }

        private void ToggleOfflineForEditorCheck()
        {
            bool online = backend.ToggleOfflineForEditorCheck();
            if (online)
            {
                StartCoroutine(RetrySyncRoutine());
            }
            else
            {
                stateMachine.SetReSyncPending("개발 확인용으로 네트워크 OFF 상태가 되었습니다.", coreActionsEnabled: false);
            }
        }

        private void RequestNextRunPattern()
        {
            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteRecordBoard)
            {
                MoveRecordBoardPage(1);
                return;
            }

            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteStartReady)
            {
                if (!IsSelectedSaveSlotUsable(stateMachine.LobbySnapshot))
                {
                    stateMachine.SetInfiniteStartReady(
                        backend.LoadLobby(),
                        "무한모드에 사용할 슬롯을 먼저 선택하세요.\n\n" + backend.BuildInfiniteStartReadyText());
                    return;
                }

                StartInfiniteRunFromSelectedLoadout();
                return;
            }

            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteLoadoutSelect)
            {
                saveSlotDeleteConfirmPending = false;
                saveSlotDetailViewActive = false;
                saveSlotRenameConfirmPending = false;
                stateMachine.SetInfiniteStartReady(backend.LoadLobby(), backend.BuildInfiniteStartReadyText());
                return;
            }

            if (stateMachine.CurrentState == GreedLastScreenState.SaveLoadoutDraft)
            {
                ConfirmSaveLoadoutAndReturn();
                return;
            }

            if (IsRunState())
            {
                if (runCore.IsPaused)
                {
                    runCore.TogglePause(Time.realtimeSinceStartup);
                    return;
                }

                if (runCore.CanExitAfterClear)
                {
                    CompleteClearAndOpenSaveDraft();
                    return;
                }

                if (runCore.IsStoppedInfiniteRun)
                {
                    backend.RegisterInfiniteRun(runCore.CreateInfiniteRunRecord());
                    StartInfiniteRunFromSelectedLoadout();
                    return;
                }

                if (runCore.IsStoppedNormalRun)
                {
                    RegisterNormalAttempt(GreedLastRunAttemptOutcome.Failed);
                }

                runCore.ToggleAutoFlow(Time.realtimeSinceStartup);
            }
        }

        private void HandleRunChannelInput(GreedLastRunChannel channel)
        {
            if (stateMachine.CurrentState == GreedLastScreenState.SaveLoadoutDraft
                || stateMachine.CurrentState == GreedLastScreenState.InfiniteLoadoutSelect)
            {
                SelectSaveSlot(channel);
                return;
            }

            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteRecordBoard)
            {
                SelectRecordBoardPage(channel);
                return;
            }

            if (IsRunState())
            {
                runtimeUi.RecordLaneInput(channel);
                runCore.HandleInput(channel, Time.realtimeSinceStartup);
            }
        }

        private void SelectSaveSlot(GreedLastRunChannel channel)
        {
            int slotIndex = channel == GreedLastRunChannel.Left
                ? 0
                : channel == GreedLastRunChannel.Center
                    ? 1
                    : 2;
            GreedLastLobbySnapshot snapshot = backend.SelectSaveLoadoutSlot(slotIndex);
            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteLoadoutSelect)
            {
                saveSlotDeleteConfirmPending = false;
                saveSlotDetailViewActive = false;
                saveSlotRenameConfirmPending = false;
                stateMachine.SetInfiniteLoadoutSelect(snapshot, backend.BuildInfiniteLoadoutSelectText());
                return;
            }

            OpenSaveLoadoutDraft(snapshot, requireExplicitSlotChoice: false);
        }

        private void OpenSaveLoadoutDraft(
            GreedLastLobbySnapshot snapshot,
            bool requireExplicitSlotChoice,
            string notice = null)
        {
            saveDraftRequiresExplicitSlotChoice = requireExplicitSlotChoice;
            saveDraftOverwriteConfirmPending = false;
            saveSlotDeleteConfirmPending = false;
            saveSlotDetailViewActive = false;
            saveSlotRenameConfirmPending = false;
            string detail = backend.BuildSaveLoadoutDraftText(!requireExplicitSlotChoice);
            if (requireExplicitSlotChoice)
            {
                detail += "\n\n저장할 슬롯을 좌 / 중 / 우 중에서 먼저 고르세요.";
                detail += "\n선택 전에는 저장 확정이 잠겨 있습니다.";
            }

            if (!string.IsNullOrEmpty(notice))
            {
                detail = notice + "\n\n" + detail;
            }

            stateMachine.SetSaveLoadoutDraft(
                snapshot,
                detail,
                requireExplicitSlotChoice,
                saveLoadoutCandidateAvailable: backend.HasSaveLoadoutCandidate);
        }

        private void OpenSaveLoadoutOverwriteConfirmation(string notice)
        {
            saveDraftRequiresExplicitSlotChoice = false;
            saveDraftOverwriteConfirmPending = true;
            saveSlotDeleteConfirmPending = false;
            saveSlotDetailViewActive = false;
            saveSlotRenameConfirmPending = false;
            string detail = backend.BuildSaveLoadoutDraftText(showSelectedSlot: true);
            if (!string.IsNullOrEmpty(notice))
            {
                detail = notice + "\n\n" + detail;
            }

            stateMachine.SetSaveLoadoutDraft(
                backend.LoadLobby(),
                detail,
                saveSlotChoiceRequired: false,
                saveSlotOverwriteConfirmationPending: true,
                saveLoadoutCandidateAvailable: backend.HasSaveLoadoutCandidate);
        }

        private void OpenSaveLoadoutDeleteConfirmation(string notice)
        {
            saveDraftRequiresExplicitSlotChoice = false;
            saveDraftOverwriteConfirmPending = false;
            saveSlotDeleteConfirmPending = true;
            saveSlotDetailViewActive = false;
            saveSlotRenameConfirmPending = false;
            string detail = backend.BuildSaveLoadoutDraftText(showSelectedSlot: true);
            if (!string.IsNullOrEmpty(notice))
            {
                detail = notice + "\n\n" + detail;
            }

            stateMachine.SetSaveLoadoutDraft(
                backend.LoadLobby(),
                detail,
                saveSlotChoiceRequired: false,
                saveSlotDeleteConfirmationPending: true,
                saveLoadoutCandidateAvailable: backend.HasSaveLoadoutCandidate);
        }

        private void OpenInfiniteLoadoutDeleteConfirmation(string notice)
        {
            saveSlotDeleteConfirmPending = true;
            saveSlotDetailViewActive = false;
            saveSlotRenameConfirmPending = false;
            string detail = backend.BuildInfiniteLoadoutSelectText();
            if (!string.IsNullOrEmpty(notice))
            {
                detail = notice + "\n\n" + detail;
            }

            stateMachine.SetInfiniteLoadoutSelect(
                backend.LoadLobby(),
                detail,
                saveSlotDeleteConfirmationPending: true);
        }

        private void OpenSaveLoadoutRenameConfirmation(string notice)
        {
            saveDraftRequiresExplicitSlotChoice = false;
            saveDraftOverwriteConfirmPending = false;
            saveSlotDeleteConfirmPending = false;
            saveSlotDetailViewActive = false;
            saveSlotRenameConfirmPending = true;
            string detail = backend.BuildSelectedSaveLoadoutRenamePreviewText()
                + "\n\n" + backend.BuildSaveLoadoutDraftText(showSelectedSlot: true);
            if (!string.IsNullOrEmpty(notice))
            {
                detail = notice + "\n\n" + detail;
            }

            stateMachine.SetSaveLoadoutDraft(
                backend.LoadLobby(),
                detail,
                saveSlotChoiceRequired: false,
                saveSlotRenameConfirmationPending: true,
                saveLoadoutCandidateAvailable: backend.HasSaveLoadoutCandidate);
        }

        private void OpenInfiniteLoadoutRenameConfirmation(string notice)
        {
            saveSlotDeleteConfirmPending = false;
            saveSlotDetailViewActive = false;
            saveSlotRenameConfirmPending = true;
            string detail = backend.BuildSelectedSaveLoadoutRenamePreviewText()
                + "\n\n" + backend.BuildInfiniteLoadoutSelectText();
            if (!string.IsNullOrEmpty(notice))
            {
                detail = notice + "\n\n" + detail;
            }

            stateMachine.SetInfiniteLoadoutSelect(
                backend.LoadLobby(),
                detail,
                saveSlotRenameConfirmationPending: true);
        }

        private void SelectRecordBoardPage(GreedLastRunChannel channel)
        {
            runtimeUi.RecordLaneInput(channel);
            if (channel == GreedLastRunChannel.Left)
            {
                MoveRecordBoardPage(-1);
                return;
            }

            if (channel == GreedLastRunChannel.Center)
            {
                OpenRecordBoard(0);
                return;
            }

            MoveRecordBoardPage(1);
        }

        private void MoveRecordBoardPage(int delta)
        {
            int pageCount = Mathf.Max(1, backend.RecordBoardPages);
            recordBoardPageIndex = (recordBoardPageIndex + delta) % pageCount;
            if (recordBoardPageIndex < 0)
            {
                recordBoardPageIndex += pageCount;
            }

            stateMachine.SetInfiniteRecordBoard(
                backend.LoadLobby(),
                backend.BuildRecordBoardText(recordBoardPageIndex),
                recordBoardPageIndex);
        }

        private void OpenRecordBoard(int pageIndex)
        {
            int pageCount = Mathf.Max(1, backend.RecordBoardPages);
            recordBoardPageIndex = pageIndex % pageCount;
            if (recordBoardPageIndex < 0)
            {
                recordBoardPageIndex += pageCount;
            }

            stateMachine.SetInfiniteRecordBoard(
                backend.LoadLobby(),
                backend.BuildRecordBoardText(recordBoardPageIndex),
                recordBoardPageIndex);
        }

        private void RequestShowSaveLoadoutDetail()
        {
            saveSlotDeleteConfirmPending = false;
            saveSlotRenameConfirmPending = false;
            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteLoadoutSelect)
            {
                if (saveSlotDetailViewActive)
                {
                    saveSlotDetailViewActive = false;
                    stateMachine.SetInfiniteLoadoutSelect(backend.LoadLobby(), backend.BuildInfiniteLoadoutSelectText());
                    return;
                }

                saveSlotDetailViewActive = true;
                stateMachine.SetInfiniteLoadoutSelect(
                    backend.LoadLobby(),
                    backend.BuildSelectedInfiniteLoadoutDetailText(),
                    saveSlotDetailViewActive: true);
                return;
            }

            if (stateMachine.CurrentState != GreedLastScreenState.SaveLoadoutDraft)
            {
                return;
            }

            if (saveDraftRequiresExplicitSlotChoice)
            {
                RefreshSaveLoadoutDraft("상세 비교 전에 저장할 슬롯을 먼저 선택하세요.");
                return;
            }

            if (saveSlotDetailViewActive)
            {
                saveSlotDetailViewActive = false;
                RefreshSaveLoadoutDraft(null);
                return;
            }

            saveSlotDetailViewActive = true;
            stateMachine.SetSaveLoadoutDraft(
                backend.LoadLobby(),
                backend.BuildSelectedSaveLoadoutComparisonText(),
                saveSlotOverwriteConfirmationPending: saveDraftOverwriteConfirmPending,
                saveSlotDetailViewActive: true,
                saveLoadoutCandidateAvailable: backend.HasSaveLoadoutCandidate);
        }

        private void RequestRenameSaveLoadoutSlot()
        {
            saveSlotDeleteConfirmPending = false;
            saveSlotDetailViewActive = false;
            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteLoadoutSelect)
            {
                if (!backend.SelectedSaveSlotHasRecord())
                {
                    saveSlotRenameConfirmPending = false;
                    RefreshInfiniteLoadoutSelect("선택한 슬롯이 비어 있어 이름을 바꿀 수 없습니다.");
                    return;
                }

                if (!saveSlotRenameConfirmPending)
                {
                    OpenInfiniteLoadoutRenameConfirmation(null);
                    return;
                }

                string infiniteNotice = backend.RenameSelectedSaveLoadoutSlot();
                saveSlotRenameConfirmPending = false;
                RefreshInfiniteLoadoutSelect(infiniteNotice);
                return;
            }

            if (stateMachine.CurrentState != GreedLastScreenState.SaveLoadoutDraft)
            {
                return;
            }

            if (saveDraftRequiresExplicitSlotChoice)
            {
                saveSlotRenameConfirmPending = false;
                RefreshSaveLoadoutDraft("이름 변경 전에 저장할 슬롯을 먼저 선택하세요.");
                return;
            }

            if (!backend.SelectedSaveSlotHasRecord())
            {
                saveSlotRenameConfirmPending = false;
                RefreshSaveLoadoutDraft("선택한 슬롯이 비어 있어 이름을 바꿀 수 없습니다.");
                return;
            }

            if (!saveSlotRenameConfirmPending)
            {
                OpenSaveLoadoutRenameConfirmation(null);
                return;
            }

            string notice = backend.RenameSelectedSaveLoadoutSlot();
            saveSlotRenameConfirmPending = false;
            RefreshSaveLoadoutDraft(notice);
        }

        private void RequestDeleteSaveLoadoutSlot()
        {
            saveSlotDetailViewActive = false;
            saveSlotRenameConfirmPending = false;
            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteLoadoutSelect)
            {
                if (!backend.SelectedSaveSlotHasRecord())
                {
                    saveSlotDeleteConfirmPending = false;
                    RefreshInfiniteLoadoutSelect("선택한 슬롯은 이미 비어 있습니다.");
                    return;
                }

                if (!saveSlotDeleteConfirmPending)
                {
                    OpenInfiniteLoadoutDeleteConfirmation(
                        "선택한 저장 조합을 삭제합니다.\n다시 삭제를 누르면 슬롯이 비워집니다.");
                    return;
                }

                string infiniteNotice = backend.DeleteSelectedSaveLoadoutSlot();
                saveSlotDeleteConfirmPending = false;
                RefreshInfiniteLoadoutSelect(infiniteNotice);
                return;
            }

            if (stateMachine.CurrentState != GreedLastScreenState.SaveLoadoutDraft)
            {
                return;
            }

            if (saveDraftRequiresExplicitSlotChoice)
            {
                saveSlotDeleteConfirmPending = false;
                RefreshSaveLoadoutDraft("삭제 전에 저장할 슬롯을 먼저 선택하세요.");
                return;
            }

            if (!backend.SelectedSaveSlotHasRecord())
            {
                saveSlotDeleteConfirmPending = false;
                RefreshSaveLoadoutDraft("선택한 슬롯은 이미 비어 있습니다.");
                return;
            }

            if (!saveSlotDeleteConfirmPending)
            {
                OpenSaveLoadoutDeleteConfirmation(
                    "선택한 저장 조합을 삭제합니다.\n다시 삭제를 누르면 슬롯이 비워집니다.");
                return;
            }

            string notice = backend.DeleteSelectedSaveLoadoutSlot();
            saveSlotDeleteConfirmPending = false;
            RefreshSaveLoadoutDraft(notice);
        }

        private void RefreshSaveLoadoutDraft(string notice)
        {
            OpenSaveLoadoutDraft(backend.LoadLobby(), saveDraftRequiresExplicitSlotChoice, notice);
        }

        private void RefreshInfiniteLoadoutSelect(string notice)
        {
            string detail = backend.BuildInfiniteLoadoutSelectText();
            if (!string.IsNullOrEmpty(notice))
            {
                detail += "\n\n" + notice;
            }

            stateMachine.SetInfiniteLoadoutSelect(
                backend.LoadLobby(),
                detail,
                saveSlotDeleteConfirmationPending: saveSlotDeleteConfirmPending,
                saveSlotRenameConfirmationPending: saveSlotRenameConfirmPending);
        }

        private void ToggleRunInvincible()
        {
            if (IsRunState())
            {
                runCore.ToggleDevInvincible();
            }
        }

        private void RequestToggleRunPause()
        {
            if (IsRunState())
            {
                runCore.TogglePause(Time.realtimeSinceStartup);
            }
        }

        private void RequestInfiniteTestStop()
        {
            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteRun)
            {
                runCore.ForceStopInfiniteRunForTest();
            }
        }

        private void RequestFocusMaxForTest()
        {
            if (IsRunState())
            {
                runCore.FillFocusForTest();
            }
        }

        private void RequestThreatUpForTest()
        {
            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteRun)
            {
                runCore.RaiseInfiniteThreatForTest();
            }
        }

        private void RequestThreatDownForTest()
        {
            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteRun)
            {
                runCore.LowerInfiniteThreatForTest();
            }
        }

        private void RequestTimingOffsetAdjust(int milliseconds)
        {
            if (!IsRunState())
            {
                return;
            }

            if (milliseconds == 0)
            {
                runCore.ApplyRecommendedInputTimingOffset();
                return;
            }

            runCore.AdjustInputTimingOffset(milliseconds / 1000f);
        }

        private void RequestRunDevShortcut(GreedLastDevShortcut shortcut)
        {
            if (stateMachine.CurrentState == GreedLastScreenState.RunCoreTest)
            {
                runCore.JumpToDevShortcut(shortcut, Time.realtimeSinceStartup);
            }
        }

        private void RequestReturnToLobby()
        {
            if (stateMachine.CurrentState == GreedLastScreenState.SaveLoadoutDraft)
            {
                bool skippedClearSave = saveDraftRequiresExplicitSlotChoice;
                saveDraftRequiresExplicitSlotChoice = false;
                saveDraftOverwriteConfirmPending = false;
                saveSlotDeleteConfirmPending = false;
                saveSlotDetailViewActive = false;
                saveSlotRenameConfirmPending = false;
                if (skippedClearSave)
                {
                    backend.DiscardSaveLoadoutCandidate();
                }

                stateMachine.SetLobbyReady(backend.LoadLobby());
                if (skippedClearSave)
                {
                    stateMachine.ShowLocalNotice("저장 조합 저장은 건너뛰었습니다.\n일반 런 클리어 기록은 기록 보드에 남아 있습니다.");
                }

                return;
            }

            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteLoadoutSelect)
            {
                saveSlotDeleteConfirmPending = false;
                saveSlotDetailViewActive = false;
                saveSlotRenameConfirmPending = false;
                stateMachine.SetLobbyReady(backend.LoadLobby());
                return;
            }

            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteRecordBoard)
            {
                recordBoardPageIndex = 0;
                stateMachine.SetLobbyReady(backend.LoadLobby());
                return;
            }

            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteStartReady)
            {
                stateMachine.SetLobbyReady(backend.LoadLobby());
                return;
            }

            if (!IsRunState())
            {
                return;
            }

            if (runCore.CanExitAfterClear)
            {
                CompleteClearAndReturnToLobby();
                return;
            }

            if (stateMachine.CurrentState == GreedLastScreenState.InfiniteRun)
            {
                GreedLastInfiniteRunRecord record = runCore.CreateInfiniteRunRecord();
                bool registered = backend.RegisterInfiniteRun(record);
                if (registered)
                {
                    preferredRecordBoardPageIndex = 2;
                }

                string message = registered
                    ? backend.BuildInfiniteRunResultMessage(record)
                    : "피라미드 입구로 돌아왔습니다.";
                ReturnToLobby(message);
                return;
            }

            GreedLastRunAttemptOutcome outcome = runCore.IsStoppedNormalRun
                ? GreedLastRunAttemptOutcome.Failed
                : GreedLastRunAttemptOutcome.Abandoned;
            string normalMessage = RegisterNormalAttempt(outcome);
            ReturnToLobby(normalMessage);
        }

        private string RegisterNormalAttempt(GreedLastRunAttemptOutcome outcome)
        {
            GreedLastRunAttemptRecord record = runCore.CreateNormalAttemptRecord(outcome);
            bool registered = backend.RegisterNormalAttempt(record);
            if (registered)
            {
                preferredRecordBoardPageIndex = 1;
            }

            return registered ? backend.BuildRunAttemptResultMessage(record) : "피라미드 입구로 돌아왔습니다.";
        }

        private void CompleteClearAndReturnToLobby()
        {
            GreedLastRunRecord record = runCore.CreateRunRecord();
            GreedLastLobbySnapshot snapshot = backend.RegisterClear(record);
            if (record.IsValid)
            {
                preferredRecordBoardPageIndex = 0;
            }

            runCore.Exit();
            stateMachine.SetLobbyReady(snapshot);
            stateMachine.ShowLocalNotice(backend.BuildRunClearResultMessage(record));
        }

        private void CompleteClearAndOpenSaveDraft()
        {
            GreedLastRunRecord record = runCore.CreateRunRecord();
            GreedLastLobbySnapshot snapshot = backend.RegisterClear(record);
            if (!record.IsValid)
            {
                runCore.Exit();
                stateMachine.SetLobbyReady(snapshot);
                stateMachine.ShowLocalNotice(backend.BuildRunClearResultMessage(record));
                return;
            }

            preferredRecordBoardPageIndex = 0;
            runCore.Exit();
            OpenSaveLoadoutDraft(
                snapshot,
                requireExplicitSlotChoice: true,
                notice: "일반 런 클리어. 저장할 슬롯을 고른 뒤 확정하세요.");
        }

        private void ConfirmSaveLoadoutAndReturn()
        {
            saveSlotRenameConfirmPending = false;
            if (!backend.HasSaveLoadoutCandidate)
            {
                RefreshSaveLoadoutDraft("저장할 새 클리어 후보가 없습니다.\n기존 저장 슬롯만 관리할 수 있습니다.");
                return;
            }

            if (saveDraftRequiresExplicitSlotChoice)
            {
                RefreshSaveLoadoutDraft("저장할 슬롯을 먼저 선택하세요.");
                return;
            }

            if (backend.SelectedSaveSlotHasRecord() && !saveDraftOverwriteConfirmPending)
            {
                OpenSaveLoadoutOverwriteConfirmation(
                    "선택한 슬롯에 기존 저장 조합이 있습니다.\n다시 저장을 누르면 이 후보로 덮어씁니다.");
                return;
            }

            GreedLastLobbySnapshot snapshot = backend.ConfirmSaveLoadout();
            saveDraftRequiresExplicitSlotChoice = false;
            saveDraftOverwriteConfirmPending = false;
            saveSlotDeleteConfirmPending = false;
            saveSlotDetailViewActive = false;
            saveSlotRenameConfirmPending = false;
            stateMachine.SetLobbyReady(snapshot);
            stateMachine.ShowLocalNotice(backend.BuildSaveConfirmMessage());
        }

        private void ReturnToLobby(string message)
        {
            runCore.Exit();
            stateMachine.SetLobbyReady(backend.LoadLobby());
            stateMachine.ShowLocalNotice(message);
        }

        private static bool IsSelectedSaveSlotUsable(GreedLastLobbySnapshot snapshot)
        {
            return snapshot.SaveSlotUsable != null
                && snapshot.SelectedSaveSlotIndex >= 0
                && snapshot.SelectedSaveSlotIndex < snapshot.SaveSlotUsable.Length
                && snapshot.SaveSlotUsable[snapshot.SelectedSaveSlotIndex];
        }

        private bool IsRunState()
        {
            return stateMachine.CurrentState == GreedLastScreenState.RunCoreTest
                || stateMachine.CurrentState == GreedLastScreenState.InfiniteRun;
        }
    }

    public sealed class GreedLastRuntimeUi : MonoBehaviour
    {
        private const float DesignWidth = 1080f;
        private const float DesignHeight = 1920f;
        private const int RecordBoardPageCount = 4;
        private const float TargetCueLeadSeconds = 0.14f;
        private const float ActiveBeatVolume = 0.075f;
        private const float IdleBeatVolume = 0.14f;
        private const float LaneCueVolume = 0.24f;
        private const float TargetCueVolume = 0.25f;
        private const float MinSfxVolume = 0f;
        private const float MaxSfxVolume = 1f;
        private const float DefaultSfxVolume = 1f;
        private const string SfxVolumePrefsKey = "GreedLast.RuntimeUi.SfxVolume";
        private const string HapticsEnabledPrefsKey = "GreedLast.RuntimeUi.HapticsEnabled";
        private static readonly Color32 ButtonNormalColor = new Color32(205, 166, 70, 255);
        private static readonly Color32 ButtonHighlightedColor = new Color32(238, 204, 114, 255);
        private static readonly Color32 ButtonPressedColor = new Color32(165, 124, 48, 255);
        private static readonly Color32 ButtonDisabledColor = new Color32(66, 71, 72, 180);
        private static readonly Color32 ButtonConfirmColor = new Color32(238, 204, 114, 255);
        private static readonly Color32 ButtonConfirmPressedColor = new Color32(185, 132, 42, 255);
        private static readonly Color32 ButtonDangerColor = new Color32(207, 87, 65, 255);
        private static readonly Color32 ButtonDangerHighlightedColor = new Color32(240, 126, 91, 255);
        private static readonly Color32 ButtonDangerPressedColor = new Color32(148, 54, 44, 255);

        private static bool DevToolsEnabled => Application.isEditor || Debug.isDebugBuild;

        private GameObject rootPanel;
        private GameObject lanePreviewRoot;
        private Text titleText;
        private Text subtitleText;
        private Text detailText;
        private Text runHudText;
        private Text debugText;
        private GameObject runGaugeRoot;
        private Image healthGaugeFill;
        private Image focusGaugeFill;
        private Text healthGaugeText;
        private Text focusGaugeText;
        private GameObject runProgressRoot;
        private Image runProgressFill;
        private Text runProgressText;
        private GameObject comboBadgeRoot;
        private Image comboBadgeBack;
        private Text comboBadgeText;
        private GameObject pauseMenuOverlay;
        private Image pauseMenuBackdrop;
        private Image pauseMenuPanel;
        private Text pauseMenuTitleText;
        private Text pauseMenuBodyText;
        private Button normalRunButton;
        private Button saveLoadoutButton;
        private Button infiniteButton;
        private Button infiniteRecordButton;
        private Button retryButton;
        private Button debugConnectionButton;
        private Button nextPatternButton;
        private Button returnLobbyButton;
        private Button saveSlotDetailButton;
        private Button saveSlotRenameButton;
        private Button saveSlotDeleteButton;
        private Button pauseButton;
        private Button devInvincibleButton;
        private Button infiniteTestStopButton;
        private Button focusMaxButton;
        private Button threatUpButton;
        private Button threatDownButton;
        private Button hapticsButton;
        private Button[] devShortcutButtons;
        private Button[] timingOffsetButtons;
        private Button[] sfxVolumeButtons;
        private Button[] laneButtons;
        private Text[] laneLabelTexts;
        private Image[] laneImages;
        private float[] lanePressPulseUntil;
        private Image[] runMotionLines;
        private Image beatMarker;
        private Image goodTimingBand;
        private Image successTimingBand;
        private Image judgementLine;
        private Image laneDangerBand;
        private Image trapGlow;
        private Image trapMarker;
        private Image trapStrike;
        private Text timingGuideText;
        private Text judgementFeedbackText;
        private Image feedbackFlash;
        private AudioSource rhythmAudio;
        private AudioClip beatClip;
        private AudioClip perfectCueClip;
        private AudioClip clutchCueClip;
        private AudioClip offbeatCueClip;
        private AudioClip[] laneCueClips;
        private AudioClip successClip;
        private AudioClip goodClip;
        private AudioClip missClip;
        private AudioClip chainClip;
        private Action normalRunRequested;
        private Action saveLoadoutRequested;
        private Action infiniteRunRequested;
        private Action infiniteRecordRequested;
        private Action retryRequested;
        private Action debugConnectionRequested;
        private Action nextPatternRequested;
        private Action returnLobbyRequested;
        private Action saveSlotDetailRequested;
        private Action saveSlotRenameRequested;
        private Action saveSlotDeleteRequested;
        private Action pauseRequested;
        private Action invincibleRequested;
        private Action infiniteTestStopRequested;
        private Action focusMaxRequested;
        private Action threatUpRequested;
        private Action threatDownRequested;
        private Action<int> timingOffsetRequested;
        private Action<GreedLastDevShortcut> devShortcutRequested;
        private Action<GreedLastRunChannel> runChannelRequested;
        private float beatPhase;
        private float nextBeatSoundAt;
        private float feedbackFlashUntil;
        private float judgementFeedbackUntil;
        private float comboBadgePulseUntil;
        private float sfxVolume = DefaultSfxVolume;
        private bool hapticsEnabled = true;
        private int lastFeedbackScore;
        private int lastFeedbackHealth = -1;
        private string lastJudgementText = string.Empty;
        private bool lastActivePatternVisual;
        private bool targetCuePlayed;
        private bool runModeActive;
        private bool saveSlotModeActive;
        private GreedLastRunSnapshot latestRunSnapshot;
        private bool hasRunSnapshot;

        public void Initialize(
            Action onNormalRunRequested,
            Action onSaveLoadoutRequested,
            Action onInfiniteRunRequested,
            Action onInfiniteRecordRequested,
            Action onRetryRequested,
            Action onDebugConnectionRequested,
            Action onNextPatternRequested,
            Action onPauseRequested,
            Action onReturnLobbyRequested,
            Action onSaveSlotDetailRequested,
            Action onSaveSlotRenameRequested,
            Action onSaveSlotDeleteRequested,
            Action onInvincibleRequested,
            Action onInfiniteTestStopRequested,
            Action onFocusMaxRequested,
            Action onThreatUpRequested,
            Action onThreatDownRequested,
            Action<int> onTimingOffsetRequested,
            Action<GreedLastDevShortcut> onDevShortcutRequested,
            Action<GreedLastRunChannel> onRunChannelRequested)
        {
            normalRunRequested = onNormalRunRequested;
            saveLoadoutRequested = onSaveLoadoutRequested;
            infiniteRunRequested = onInfiniteRunRequested;
            infiniteRecordRequested = onInfiniteRecordRequested;
            retryRequested = onRetryRequested;
            debugConnectionRequested = onDebugConnectionRequested;
            nextPatternRequested = onNextPatternRequested;
            pauseRequested = onPauseRequested;
            returnLobbyRequested = onReturnLobbyRequested;
            saveSlotDetailRequested = onSaveSlotDetailRequested;
            saveSlotRenameRequested = onSaveSlotRenameRequested;
            saveSlotDeleteRequested = onSaveSlotDeleteRequested;
            invincibleRequested = onInvincibleRequested;
            infiniteTestStopRequested = onInfiniteTestStopRequested;
            focusMaxRequested = onFocusMaxRequested;
            threatUpRequested = onThreatUpRequested;
            threatDownRequested = onThreatDownRequested;
            timingOffsetRequested = onTimingOffsetRequested;
            devShortcutRequested = onDevShortcutRequested;
            runChannelRequested = onRunChannelRequested;
            EnsureUi();
        }

        private void Update()
        {
            if (beatMarker == null)
            {
                return;
            }

            float beatSpeed = BuildBeatVisualSpeed(latestRunSnapshot);
            beatPhase += Time.unscaledDeltaTime * beatSpeed;
            float pulse = 0.5f + Mathf.Sin(beatPhase * Mathf.PI * 2f) * 0.5f;
            beatMarker.color = Color32.Lerp(new Color32(240, 197, 89, 140), new Color32(255, 238, 181, 255), pulse);
            beatMarker.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.86f, 1.18f, pulse);

            if (runModeActive
                && (latestRunSnapshot.ResumeCountdownActive || (latestRunSnapshot.StartCountdownActive && !latestRunSnapshot.Paused))
                && Time.unscaledTime >= nextBeatSoundAt)
            {
                nextBeatSoundAt = Time.unscaledTime + BuildBeatInterval(latestRunSnapshot);
                PlayClip(beatClip, IdleBeatVolume * 1.35f);
            }
            else if (runModeActive && !latestRunSnapshot.Paused && Time.unscaledTime >= nextBeatSoundAt)
            {
                nextBeatSoundAt = Time.unscaledTime + BuildBeatInterval(latestRunSnapshot);
                PlayClip(beatClip, latestRunSnapshot.ActivePattern ? ActiveBeatVolume : IdleBeatVolume);
            }

            if (!saveSlotModeActive)
            {
                for (int i = 0; i < laneImages.Length; i += 1)
                {
                    float lanePulse = Mathf.Repeat(beatPhase + i * 0.2f, 1f);
                    Color32 baseColor = Color32.Lerp(
                        new Color32(32, 42, 49, 255),
                        new Color32(69, 88, 92, 255),
                        lanePulse * 0.35f);
                    GreedLastRunChannel laneChannel = (GreedLastRunChannel)(i + 1);
                    if (hasRunSnapshot
                        && latestRunSnapshot.ActivePattern
                        && latestRunSnapshot.Pattern.Channel == laneChannel)
                    {
                        baseColor = Color32.Lerp(baseColor, new Color32(154, 84, 63, 255), 0.72f);
                    }

                    float pressPulse = BuildLanePressPulse(i);
                    if (pressPulse > 0f)
                    {
                        baseColor = Color32.Lerp(baseColor, new Color32(255, 238, 181, 255), pressPulse * 0.72f);
                    }

                    laneImages[i].color = baseColor;
                    laneImages[i].rectTransform.localScale = Vector3.one * (1f + pressPulse * 0.055f);
                    if (laneLabelTexts != null && i < laneLabelTexts.Length && laneLabelTexts[i] != null && runModeActive)
                    {
                        laneLabelTexts[i].color = Color32.Lerp(new Color32(218, 226, 218, 230), new Color32(255, 238, 181, 255), pressPulse);
                    }
                }
            }

            UpdateForwardMotionLines();
            UpdateRunTimingVisuals();
            UpdateFeedbackFlash();
        }

        public void RecordLaneInput(GreedLastRunChannel channel)
        {
            if (lanePressPulseUntil == null)
            {
                return;
            }

            int index = (int)channel - 1;
            if (index < 0 || index >= lanePressPulseUntil.Length)
            {
                return;
            }

            lanePressPulseUntil[index] = Time.unscaledTime + 0.16f;
        }

        public void Render(GreedLastStateSnapshot snapshot)
        {
            EnsureUi();
            if (pauseMenuOverlay != null)
            {
                pauseMenuOverlay.SetActive(false);
            }

            SetRunMenuButtonLayout(false);

            titleText.text = snapshot.Headline;
            subtitleText.text = "모바일 리듬 탈출 / 좌 · 중 · 우 대응";
            string detail = BuildDetail(snapshot);
            detailText.text = detail;
            debugText.text = $"state: {snapshot.State}\nrequestId: {snapshot.RequestId}\nblock: {snapshot.BlockReason}";

            bool lobbyLike = snapshot.State == GreedLastScreenState.LobbyReady
                || snapshot.State == GreedLastScreenState.ReSyncPending;
            bool runLike = snapshot.State == GreedLastScreenState.RunCoreTest
                || snapshot.State == GreedLastScreenState.InfiniteRun;
            bool infiniteRunLike = snapshot.State == GreedLastScreenState.InfiniteRun;
            bool normalRunLike = snapshot.State == GreedLastScreenState.RunCoreTest;
            bool saveDraftConfirmLike = snapshot.State == GreedLastScreenState.SaveLoadoutDraft;
            bool infiniteLoadoutSelectLike = snapshot.State == GreedLastScreenState.InfiniteLoadoutSelect;
            bool saveDraftLike = saveDraftConfirmLike || infiniteLoadoutSelectLike;
            bool recordBoardLike = snapshot.State == GreedLastScreenState.InfiniteRecordBoard;
            bool infinitePrepLike = snapshot.State == GreedLastScreenState.InfiniteStartReady;
            runModeActive = runLike;
            saveSlotModeActive = saveDraftLike;
            bool lobbyNoticeLike = snapshot.State == GreedLastScreenState.LobbyReady
                && !string.IsNullOrEmpty(detail);
            if (runGaugeRoot != null)
            {
                runGaugeRoot.SetActive(runLike);
            }

            if (runProgressRoot != null)
            {
                runProgressRoot.SetActive(runLike);
            }

            if (comboBadgeRoot != null)
            {
                comboBadgeRoot.SetActive(false);
            }

            detailText.fontSize = recordBoardLike ? 26 : lobbyNoticeLike ? 31 : saveDraftLike || infinitePrepLike ? 27 : 33;
            if (recordBoardLike)
            {
                SetAnchored(detailText.rectTransform, 0.5f, 0.55f, 920f, 640f);
            }
            else if (lobbyNoticeLike)
            {
                SetAnchored(detailText.rectTransform, 0.5f, 0.585f, 900f, 190f);
            }
            else
            {
                SetAnchored(detailText.rectTransform, 0.5f, 0.59f, 850f, 260f);
            }

            if (lanePreviewRoot != null)
            {
                lanePreviewRoot.SetActive(true);
            }

            normalRunButton.gameObject.SetActive(lobbyLike);
            saveLoadoutButton.gameObject.SetActive(lobbyLike || infinitePrepLike);
            infiniteButton.gameObject.SetActive(lobbyLike && snapshot.FirstSaveCompleted);
            infiniteRecordButton.gameObject.SetActive(lobbyLike);
            retryButton.gameObject.SetActive(snapshot.RetryVisible || snapshot.State == GreedLastScreenState.ConnectBlocked);
            nextPatternButton.gameObject.SetActive(runLike || saveDraftLike || recordBoardLike || infinitePrepLike);
            returnLobbyButton.gameObject.SetActive(runLike || saveDraftLike || recordBoardLike || infinitePrepLike);
            saveSlotDetailButton.gameObject.SetActive(saveDraftLike);
            saveSlotRenameButton.gameObject.SetActive(saveDraftLike);
            saveSlotDeleteButton.gameObject.SetActive(saveDraftLike);
            runHudText.gameObject.SetActive(runLike);
            pauseButton.gameObject.SetActive(runLike);
            bool showDevTools = DevToolsEnabled;
            debugConnectionButton.gameObject.SetActive(showDevTools && !runLike && !saveDraftLike && !recordBoardLike && !infinitePrepLike);
            devInvincibleButton.gameObject.SetActive(showDevTools && runLike);
            infiniteTestStopButton.gameObject.SetActive(showDevTools && infiniteRunLike);
            focusMaxButton.gameObject.SetActive(showDevTools && runLike);
            threatUpButton.gameObject.SetActive(showDevTools && infiniteRunLike);
            threatDownButton.gameObject.SetActive(showDevTools && infiniteRunLike);
            hapticsButton.gameObject.SetActive(showDevTools && runLike);
            for (int i = 0; i < devShortcutButtons.Length; i += 1)
            {
                devShortcutButtons[i].gameObject.SetActive(showDevTools && normalRunLike);
            }

            for (int i = 0; i < timingOffsetButtons.Length; i += 1)
            {
                timingOffsetButtons[i].gameObject.SetActive(runLike);
            }

            for (int i = 0; i < sfxVolumeButtons.Length; i += 1)
            {
                sfxVolumeButtons[i].gameObject.SetActive(showDevTools && runLike);
            }

            normalRunButton.interactable = snapshot.CoreActionsEnabled;
            saveLoadoutButton.interactable = lobbyLike
                ? snapshot.CoreActionsEnabled && snapshot.SaveLoadoutUnlocked
                : infinitePrepLike && snapshot.SaveLoadoutUnlocked;
            infiniteButton.interactable = snapshot.CoreActionsEnabled && snapshot.InfiniteUnlocked;
            infiniteRecordButton.interactable = snapshot.CoreActionsEnabled;
            retryButton.interactable = !snapshot.Busy;
            debugConnectionButton.interactable = showDevTools && !snapshot.Busy;
            bool selectedSaveSlotOccupied = IsSelectedSaveSlotOccupied(snapshot);
            nextPatternButton.interactable = runLike || saveDraftLike || recordBoardLike || infinitePrepLike;
            returnLobbyButton.interactable = runLike || saveDraftLike || recordBoardLike || infinitePrepLike;
            saveSlotDetailButton.interactable = saveDraftLike && !snapshot.SaveSlotChoiceRequired;
            saveSlotRenameButton.interactable = saveDraftLike && !snapshot.SaveSlotChoiceRequired && selectedSaveSlotOccupied;
            saveSlotDeleteButton.interactable = saveDraftLike && !snapshot.SaveSlotChoiceRequired && selectedSaveSlotOccupied;
            pauseButton.interactable = runLike;
            devInvincibleButton.interactable = showDevTools && runLike;
            infiniteTestStopButton.interactable = showDevTools && infiniteRunLike;
            focusMaxButton.interactable = showDevTools && runLike;
            threatUpButton.interactable = showDevTools && infiniteRunLike;
            threatDownButton.interactable = showDevTools && infiniteRunLike;
            hapticsButton.interactable = showDevTools && runLike;
            for (int i = 0; i < devShortcutButtons.Length; i += 1)
            {
                devShortcutButtons[i].interactable = showDevTools && normalRunLike;
            }

            for (int i = 0; i < timingOffsetButtons.Length; i += 1)
            {
                timingOffsetButtons[i].interactable = runLike;
            }

            for (int i = 0; i < sfxVolumeButtons.Length; i += 1)
            {
                sfxVolumeButtons[i].interactable = showDevTools && runLike;
            }

            if (saveDraftLike)
            {
                bool canSaveCandidate = !infiniteLoadoutSelectLike && snapshot.SaveLoadoutCandidateAvailable;
                UpdateSaveSlotLabels(
                    snapshot.SaveSlotChoiceRequired ? -1 : snapshot.SelectedSaveSlotIndex,
                    snapshot.SaveSlotLabels,
                    snapshot.SaveSlotUsable,
                    snapshot.SaveSlotOccupied,
                    infiniteLoadoutSelectLike);
                SetButtonLabel(nextPatternButton, infiniteLoadoutSelectLike
                    ? "준비로 돌아가기"
                    : snapshot.SaveSlotChoiceRequired
                        ? "슬롯 선택 필요"
                        : !canSaveCandidate
                            ? "저장 후보 없음"
                            : snapshot.SaveSlotOverwriteConfirmationPending
                                ? "덮어쓰기 확정"
                                : "슬롯 " + (snapshot.SelectedSaveSlotIndex + 1) + "에 저장");
                nextPatternButton.interactable = infiniteLoadoutSelectLike
                    || (!snapshot.SaveSlotChoiceRequired && canSaveCandidate);
                SetButtonLabel(returnLobbyButton, infiniteLoadoutSelectLike
                    ? "로비로"
                    : snapshot.SaveSlotChoiceRequired
                        ? "저장 안 함"
                        : canSaveCandidate ? "선택만 하고 로비로" : "로비로");
                SetButtonLabel(saveSlotDetailButton, snapshot.SaveSlotDetailViewActive
                    ? "목록 보기"
                    : infiniteLoadoutSelectLike || !canSaveCandidate ? "상세 보기" : "상세 비교");
                SetButtonLabel(saveSlotRenameButton, snapshot.SaveSlotRenameConfirmationPending ? "이름 확정" : "이름 변경");
                SetButtonLabel(saveSlotDeleteButton, snapshot.SaveSlotDeleteConfirmationPending ? "삭제 확정" : "슬롯 삭제");
                if (!selectedSaveSlotOccupied)
                {
                    SetButtonLabel(saveSlotRenameButton, "이름 없음");
                    SetButtonLabel(saveSlotDeleteButton, "삭제 없음");
                }

                for (int i = 0; i < laneButtons.Length; i += 1)
                {
                    laneButtons[i].interactable = true;
                }
            }
            else if (recordBoardLike)
            {
                UpdateRecordBoardLaneLabels(snapshot.RecordBoardPageIndex);
            }
            else if (!runLike)
            {
                UpdateDefaultLaneLabels();
            }

            if (lobbyLike)
            {
                string selectedSlotSummary = BuildSelectedSaveSlotButtonSummary(snapshot);
                SetButtonLabel(normalRunButton, "일반 런");
                SetButtonLabel(saveLoadoutButton, snapshot.SaveLoadoutUnlocked
                    ? "저장 조합 " + selectedSlotSummary
                    : "저장 조합");
                SetButtonLabel(infiniteButton, snapshot.InfiniteUnlocked
                    ? "무한 " + selectedSlotSummary
                    : "무한모드");
                SetButtonLabel(infiniteRecordButton, IsRecordRelevantLobbyNotice(detail) ? "방금 기록" : "기록 보기");
            }
            else if (recordBoardLike)
            {
                SetButtonLabel(nextPatternButton, "다음 기록");
                SetButtonLabel(returnLobbyButton, "로비로");
            }
            else if (infinitePrepLike)
            {
                bool selectedSlotUsable = IsSelectedSaveSlotUsable(snapshot);
                SetButtonLabel(saveLoadoutButton, "저장 조합 변경");
                SetButtonLabel(nextPatternButton, selectedSlotUsable ? "무한 시작" : "슬롯 선택 필요");
                SetButtonLabel(returnLobbyButton, "로비로");
                nextPatternButton.interactable = selectedSlotUsable;
            }

            ApplyButtonTones(snapshot, saveDraftLike);
        }

        public void RenderRun(GreedLastRunSnapshot snapshot)
        {
            EnsureUi();
            latestRunSnapshot = snapshot;
            hasRunSnapshot = true;
            RenderRunHeader(snapshot);
            bool showDevTools = DevToolsEnabled;

            string runState = snapshot.Stopped
                ? snapshot.InfiniteMode ? "상태: 무한 종료" : snapshot.SaveEligible ? "상태: 탈출 성공" : "상태: 탈출 실패"
                : snapshot.ResumeCountdownActive
                    ? "상태: 재개 준비"
                    : snapshot.Paused
                        ? "상태: 일시정지"
                        : snapshot.StartCountdownActive
                            ? "상태: 시작 준비"
                            : snapshot.ChoiceActive
                                ? "상태: 선택 중"
                                : snapshot.AutoFlow
                                    ? "상태: 자동 진행"
                                    : "상태: 대기";

            string chapterText = snapshot.Phase == GreedLastRunPhase.EscapeRunning
                ? "챕터 4 탈출"
                : snapshot.Phase == GreedLastRunPhase.ClearResolving
                    ? "결과 준비"
                    : $"챕터 {snapshot.ChapterIndex} {snapshot.ChapterProgress}/{snapshot.ChapterTarget}";

            string choiceText = snapshot.ChoiceActive
                ? "\n선택: " + snapshot.ChoicePrompt
                : string.Empty;

            runHudText.text = snapshot.InfiniteMode
                ? BuildInfiniteRunHud(snapshot, runState)
                : BuildNormalRunHud(snapshot, runState, chapterText, choiceText);
            runHudText.fontSize = snapshot.InfiniteMode && snapshot.Stopped ? 30 : 27;
            UpdateRunGauges(snapshot);
            UpdateRunProgress(snapshot);
            UpdateComboBadge(snapshot);
            if (snapshot.ResumeCountdownActive || (snapshot.StartCountdownActive && !snapshot.Paused))
            {
                ShowResumeCountdownFeedback(snapshot);
            }

            UpdatePauseMenuOverlay(snapshot);
            UpdateRunSideToolsForPause(snapshot, showDevTools);
            SetRunMenuButtonLayout(snapshot.Paused);

            bool normalClearReady = snapshot.Stopped && snapshot.SaveEligible && !snapshot.InfiniteMode;
            string nextButtonLabel = snapshot.Stopped
                ? normalClearReady ? "저장하기" : snapshot.InfiniteMode ? "무한 재시작" : "다시 시작"
                : snapshot.Paused
                    ? "재개"
                    : snapshot.ChoiceActive
                        ? "선택 대기"
                        : snapshot.AutoFlow
                            ? "자동 정지"
                            : "자동 시작";
            SetButtonLabel(nextPatternButton, nextButtonLabel);
            SetButtonLabel(returnLobbyButton, normalClearReady ? "저장 안 함" : snapshot.InfiniteMode || snapshot.Paused ? "로비로" : snapshot.Stopped ? "로비로" : "탈출 포기");
            SetButtonLabel(pauseButton, snapshot.ResumeCountdownActive ? "재개 준비" : snapshot.StartCountdownActive ? "시작 준비" : snapshot.Paused ? "재개" : "일시정지");
            SetButtonLabel(devInvincibleButton, snapshot.DevInvincible ? "무적 ON" : "무적 OFF");
            SetButtonLabel(infiniteTestStopButton, "종료 테스트");
            SetButtonLabel(focusMaxButton, "집중 MAX");
            SetButtonLabel(threatUpButton, "위협 +1");
            SetButtonLabel(threatDownButton, "위협 -1");
            UpdateHapticsButtonLabel();
            SetButtonLabel(timingOffsetButtons[0], "빠름 +20");
            SetButtonLabel(timingOffsetButtons[1], "추천/0");
            SetButtonLabel(timingOffsetButtons[2], "늦음 -20");
            SetButtonLabel(sfxVolumeButtons[0], "소리 -");
            UpdateSfxVolumeButtonLabel();
            SetButtonLabel(sfxVolumeButtons[2], "소리 +");
            nextPatternButton.interactable = snapshot.Paused || (!snapshot.CountdownActive && (!snapshot.ChoiceActive || snapshot.Stopped));
            pauseButton.interactable = !snapshot.Stopped && !snapshot.ChoiceActive && !snapshot.CountdownActive;
            infiniteTestStopButton.interactable = showDevTools && snapshot.InfiniteMode && !snapshot.Stopped && !snapshot.Paused && !snapshot.CountdownActive;
            focusMaxButton.interactable = showDevTools && !snapshot.Stopped && !snapshot.Paused && !snapshot.CountdownActive;
            threatUpButton.interactable = showDevTools && snapshot.InfiniteMode && !snapshot.Stopped && !snapshot.Paused && !snapshot.CountdownActive;
            threatDownButton.interactable = showDevTools && snapshot.InfiniteMode && !snapshot.Stopped && !snapshot.Paused && !snapshot.CountdownActive;
            hapticsButton.interactable = showDevTools;

            for (int i = 0; i < laneButtons.Length; i += 1)
            {
                laneButtons[i].interactable = snapshot.ChoiceActive || (!snapshot.Stopped && !snapshot.Paused && !snapshot.CountdownActive);
            }

            UpdateLaneLabels(snapshot);

            PlayJudgementFeedback(snapshot);
        }

        private void UpdatePauseMenuOverlay(GreedLastRunSnapshot snapshot)
        {
            if (pauseMenuOverlay == null)
            {
                return;
            }

            pauseMenuOverlay.SetActive(snapshot.Paused);
            if (!snapshot.Paused)
            {
                return;
            }

            if (pauseMenuTitleText != null)
            {
                pauseMenuTitleText.text = snapshot.InfiniteMode ? "무한모드 메뉴" : "일시정지 메뉴";
            }

            if (pauseMenuBodyText != null)
            {
                pauseMenuBodyText.text = "ESC 또는 재개로 이어가기\n로비로 돌아가면 현재 런은 중단됩니다.";
            }
        }

        private void UpdateRunSideToolsForPause(GreedLastRunSnapshot snapshot, bool showDevTools)
        {
            bool showSideTools = !snapshot.Paused;
            pauseButton.gameObject.SetActive(showSideTools);
            devInvincibleButton.gameObject.SetActive(showDevTools && showSideTools);
            infiniteTestStopButton.gameObject.SetActive(showDevTools && showSideTools && snapshot.InfiniteMode);
            focusMaxButton.gameObject.SetActive(showDevTools && showSideTools);
            threatUpButton.gameObject.SetActive(showDevTools && showSideTools && snapshot.InfiniteMode);
            threatDownButton.gameObject.SetActive(showDevTools && showSideTools && snapshot.InfiniteMode);
            hapticsButton.gameObject.SetActive(showDevTools && showSideTools);

            for (int i = 0; i < devShortcutButtons.Length; i += 1)
            {
                devShortcutButtons[i].gameObject.SetActive(showDevTools && showSideTools && !snapshot.InfiniteMode);
            }

            for (int i = 0; i < timingOffsetButtons.Length; i += 1)
            {
                timingOffsetButtons[i].gameObject.SetActive(showSideTools);
            }

            for (int i = 0; i < sfxVolumeButtons.Length; i += 1)
            {
                sfxVolumeButtons[i].gameObject.SetActive(showDevTools && showSideTools);
            }
        }

        private void SetRunMenuButtonLayout(bool pauseMenu)
        {
            if (pauseMenu)
            {
                SetAnchored(nextPatternButton.GetComponent<RectTransform>(), 0.5f, 0.42f, 560f, 78f);
                SetAnchored(returnLobbyButton.GetComponent<RectTransform>(), 0.5f, 0.355f, 420f, 70f);
                return;
            }

            SetAnchored(nextPatternButton.GetComponent<RectTransform>(), 0.5f, 0.155f, 690f, 86f);
            SetAnchored(returnLobbyButton.GetComponent<RectTransform>(), 0.5f, 0.09f, 420f, 72f);
        }

        private void UpdateRunGauges(GreedLastRunSnapshot snapshot)
        {
            if (runGaugeRoot == null)
            {
                return;
            }

            runGaugeRoot.SetActive(true);
            int healthMax = Mathf.Max(3, snapshot.Health);
            float healthRatio = healthMax <= 0 ? 0f : Mathf.Clamp01(snapshot.Health / (float)healthMax);
            float focusRatio = snapshot.MaxFocus <= 0 ? 0f : Mathf.Clamp01(snapshot.Focus / (float)snapshot.MaxFocus);
            SetGaugeFill(healthGaugeFill, healthRatio);
            SetGaugeFill(focusGaugeFill, focusRatio);

            if (healthGaugeFill != null)
            {
                healthGaugeFill.color = snapshot.Health <= 1
                    ? new Color32(207, 87, 65, 235)
                    : new Color32(218, 86, 68, 220);
            }

            if (focusGaugeFill != null)
            {
                focusGaugeFill.color = snapshot.Focus >= snapshot.MaxFocus
                    ? new Color32(255, 238, 181, 230)
                    : new Color32(86, 158, 184, 220);
            }

            if (healthGaugeText != null)
            {
                healthGaugeText.text = "체력 " + snapshot.Health;
            }

            if (focusGaugeText != null)
            {
                focusGaugeText.text = "집중 " + snapshot.Focus + "/" + snapshot.MaxFocus;
            }
        }

        private void UpdateRunProgress(GreedLastRunSnapshot snapshot)
        {
            if (runProgressRoot == null)
            {
                return;
            }

            runProgressRoot.SetActive(true);
            int target = Mathf.Max(1, snapshot.ChapterTarget);
            float ratio = Mathf.Clamp01(snapshot.ChapterProgress / (float)target);
            string label;

            if (snapshot.Stopped)
            {
                ratio = snapshot.SaveEligible || snapshot.InfiniteMode ? 1f : ratio;
                label = snapshot.InfiniteMode
                    ? $"무한 종료  {snapshot.InfiniteSectionsCleared}구간 / 위협 {snapshot.InfiniteThreatLevel}"
                    : snapshot.SaveEligible
                        ? "탈출 완료"
                        : "런 중단";
            }
            else if (snapshot.InfiniteMode)
            {
                label = $"무한 구간 {snapshot.ChapterProgress}/{target}  위협 {snapshot.InfiniteThreatLevel}";
            }
            else if (snapshot.Phase == GreedLastRunPhase.EscapeRunning)
            {
                label = $"탈출 검증 {snapshot.ChapterProgress}/{target}";
            }
            else if (snapshot.Phase == GreedLastRunPhase.ClearResolving)
            {
                ratio = 1f;
                label = "탈출 완료";
            }
            else
            {
                label = $"챕터 {snapshot.ChapterIndex}  {snapshot.ChapterProgress}/{target}";
            }

            SetGaugeFill(runProgressFill, ratio);
            if (runProgressFill != null)
            {
                runProgressFill.color = snapshot.InfiniteMode
                    ? new Color32(205, 166, 70, 225)
                    : snapshot.Phase == GreedLastRunPhase.EscapeRunning || snapshot.Phase == GreedLastRunPhase.ClearResolving
                        ? new Color32(255, 238, 181, 230)
                        : new Color32(104, 151, 142, 220);
            }

            if (runProgressText != null)
            {
                runProgressText.text = label;
            }
        }

        private void UpdateComboBadge(GreedLastRunSnapshot snapshot)
        {
            if (comboBadgeRoot == null)
            {
                return;
            }

            bool showBadge = snapshot.Combo > 0 || snapshot.Stopped && snapshot.MaxCombo > 0;
            comboBadgeRoot.SetActive(showBadge);
            if (!showBadge)
            {
                return;
            }

            bool chainActive = snapshot.Combo >= 3;
            string text = snapshot.Stopped
                ? "최대 콤보 " + snapshot.MaxCombo
                : chainActive
                    ? "콤보 " + snapshot.Combo + "  연쇄 " + BuildRhythmChainLabel(snapshot)
                    : "콤보 " + snapshot.Combo;

            if (comboBadgeText != null)
            {
                comboBadgeText.text = text;
            }

            if (comboBadgeBack != null)
            {
                comboBadgeBack.color = snapshot.Stopped
                    ? new Color32(24, 33, 36, 218)
                    : chainActive
                        ? new Color32(178, 123, 36, 228)
                        : new Color32(35, 65, 75, 218);
            }

            float pulse = Time.unscaledTime < comboBadgePulseUntil
                ? 1.06f + Mathf.Sin(Time.unscaledTime * 48f) * 0.035f
                : 1f;
            comboBadgeRoot.transform.localScale = Vector3.one * pulse;
        }

        private static string BuildNormalRunHud(
            GreedLastRunSnapshot snapshot,
            string runState,
            string chapterText,
            string choiceText)
        {
            return $"{runState}\n"
                + $"{chapterText}  공명핵:{FormatBool(snapshot.CoreRetrieved)}  저장가능:{FormatBool(snapshot.SaveEligible)}\n"
                + $"함정: {snapshot.Pattern.Prompt}\n"
                + $"채널: {FormatChannel(snapshot.Pattern.Channel)} / 타이밍: {snapshot.Pattern.TimingType}\n"
                + $"판정: {snapshot.JudgementText}\n"
                + $"거리 {snapshot.Distance:0.0}m  점수 {snapshot.Score}\n"
                + $"체력 {snapshot.Health}  콤보 {snapshot.Combo}  연쇄:{BuildRhythmChainLabel(snapshot)}  집중 {snapshot.Focus}/{snapshot.MaxFocus}  보호:{BuildFocusGuardLabel(snapshot)}\n"
                + $"타이밍 진단: {snapshot.TimingProfile}\n"
                + $"가이드: {BuildTimingGuideLabel(snapshot)}  보정:{FormatTimingOffset(snapshot.InputTimingOffsetSeconds)}  테스트무적:{FormatBool(snapshot.DevInvincible)}   miss: {FormatEmpty(snapshot.MissReason)}"
                + choiceText;
        }

        private static string BuildInfiniteRunHud(GreedLastRunSnapshot snapshot, string runState)
        {
            if (snapshot.Stopped)
            {
                string lastMissText = string.IsNullOrEmpty(snapshot.MissReason)
                    ? "마지막 실수 -"
                    : "마지막 실수 " + snapshot.MissReason;
                return $"{runState}\n"
                    + $"이번 기록: [{BuildInfiniteSnapshotGrade(snapshot)}] {snapshot.Score}점 / {snapshot.Distance:0.0}m / {snapshot.InfiniteSectionsCleared}구간 / 위협 {snapshot.InfiniteThreatLevel}\n"
                    + $"판정: 성공 {snapshot.SuccessCount}  Good {snapshot.GoodCount}  Miss {snapshot.MissCount}\n"
                    + $"타이밍 진단: {snapshot.TimingProfile}\n"
                    + $"최대 콤보 {snapshot.MaxCombo}  {lastMissText}\n"
                    + "무한 재시작 또는 로비로 돌아갈 수 있습니다.";
            }

            string sectionText = snapshot.Stopped
                ? $"이번 기록: {snapshot.InfiniteSectionsCleared}구간 / {snapshot.Distance:0.0}m / {snapshot.Score}점"
                : $"무한 구간 {snapshot.ChapterProgress}/{snapshot.ChapterTarget}  위협 {snapshot.InfiniteThreatLevel}";
            string resultText = $"\n누적 {snapshot.InfiniteSectionsCleared}구간  성공 {snapshot.SuccessCount}  Good {snapshot.GoodCount}  Miss {snapshot.MissCount}";

            return $"{runState}\n"
                + $"{sectionText}\n"
                + $"함정: {snapshot.Pattern.Prompt}\n"
                + $"채널: {FormatChannel(snapshot.Pattern.Channel)} / 타이밍: {snapshot.Pattern.TimingType}\n"
                + $"판정: {snapshot.JudgementText}\n"
                + $"거리 {snapshot.Distance:0.0}m  점수 {snapshot.Score}\n"
                + $"체력 {snapshot.Health}  콤보 {snapshot.Combo}  연쇄:{BuildRhythmChainLabel(snapshot)}  집중 {snapshot.Focus}/{snapshot.MaxFocus}  보호:{BuildFocusGuardLabel(snapshot)}\n"
                + $"타이밍 진단: {snapshot.TimingProfile}\n"
                + $"가이드: {BuildTimingGuideLabel(snapshot)}  보정:{FormatTimingOffset(snapshot.InputTimingOffsetSeconds)}  테스트무적:{FormatBool(snapshot.DevInvincible)}   miss: {FormatEmpty(snapshot.MissReason)}"
                + resultText;
        }

        private void RenderRunHeader(GreedLastRunSnapshot snapshot)
        {
            if (snapshot.ResumeCountdownActive)
            {
                titleText.text = "재개 준비";
                detailText.text = "카운트 " + Mathf.CeilToInt(snapshot.ResumeCountdownRemainingSeconds)
                    + "\n앞 박자 여유 뒤 함정이 다시 내려옵니다.";
                return;
            }

            if (snapshot.Paused)
            {
                titleText.text = snapshot.InfiniteMode ? "무한모드 메뉴" : "일시정지 메뉴";
                detailText.text = "ESC 또는 재개를 누르면 카운트 뒤에 이어집니다.\n로비로 돌아가면 현재 런은 중단됩니다.";
                return;
            }

            if (snapshot.StartCountdownActive)
            {
                titleText.text = "박자 준비";
                detailText.text = "카운트 " + Mathf.CeilToInt(snapshot.StartCountdownRemainingSeconds)
                    + "\n카운트 뒤 함정이 내려옵니다.";
                return;
            }

            if (snapshot.InfiniteMode)
            {
                titleText.text = "무한모드";
                detailText.text = snapshot.Stopped
                    ? "무한모드 종료\n"
                        + $"등급 {BuildInfiniteSnapshotGrade(snapshot)}  점수 {snapshot.Score}  구간 {snapshot.InfiniteSectionsCleared}  거리 {snapshot.Distance:0.0}m\n"
                        + "무한 재시작 또는 로비로 돌아갈 수 있습니다."
                    : "저장 조합으로 함정을 계속 돌파합니다.";
                return;
            }

            if (snapshot.Phase == GreedLastRunPhase.ClearResolving)
            {
                titleText.text = "피라미드 탈출";
                detailText.text = "공명핵 회수 완료\n저장하기를 누르면 슬롯 선택 화면으로 이동합니다.\n저장하지 않으면 바로 로비로 돌아갑니다.";
                return;
            }

            if (snapshot.Phase == GreedLastRunPhase.FailResolving)
            {
                titleText.text = "탈출 실패";
                detailText.text = "체력이 0이 되어 탈출이 중단되었습니다.\n다시 시작하거나 로비로 돌아갈 수 있습니다.";
                return;
            }

            if (snapshot.ChoiceActive)
            {
                titleText.text = "방향 선택";
                detailText.text = snapshot.ChoicePrompt;
                return;
            }

            if (snapshot.Phase == GreedLastRunPhase.EscapeRunning)
            {
                titleText.text = "탈출 검증";
                detailText.text = "공명핵을 들고 마지막 함정 구간을 빠져나갑니다.";
                return;
            }

            titleText.text = "함정 대응 검증";
            detailText.text = "좌 / 중 / 우 채널과 타이밍 판정을 확인합니다.";
        }

        private static string BuildDetail(GreedLastStateSnapshot snapshot)
        {
            if (snapshot.State == GreedLastScreenState.LobbyReady)
            {
                return ShouldHideLobbyDetail(snapshot.Detail)
                    ? string.Empty
                    : snapshot.Detail;
            }

            if (snapshot.State == GreedLastScreenState.ReSyncPending)
            {
                return snapshot.Detail
                    + "\n\n핵심 버튼은 최신 상태 확인 뒤 다시 열립니다.";
            }

            if (snapshot.State == GreedLastScreenState.SaveLoadoutDraft)
            {
                return snapshot.Detail;
            }

            return snapshot.Detail;
        }

        private static bool ShouldHideLobbyDetail(string detail)
        {
            return string.IsNullOrEmpty(detail)
                || detail == "피라미드 입구 동기화 완료";
        }

        private static bool IsRecordRelevantLobbyNotice(string detail)
        {
            return !string.IsNullOrEmpty(detail)
                && (detail.StartsWith("일반 런 클리어", StringComparison.Ordinal)
                    || detail.StartsWith("탈출 실패 기록 저장", StringComparison.Ordinal)
                    || detail.StartsWith("탈출 중단 기록 저장", StringComparison.Ordinal)
                    || detail.StartsWith("무한모드 기록 저장", StringComparison.Ordinal));
        }

        private static string BuildSelectedSaveSlotButtonSummary(GreedLastStateSnapshot snapshot)
        {
            int slotNumber = snapshot.SelectedSaveSlotIndex + 1;
            string label = snapshot.SaveSlotLabels != null
                && snapshot.SelectedSaveSlotIndex >= 0
                && snapshot.SelectedSaveSlotIndex < snapshot.SaveSlotLabels.Length
                ? snapshot.SaveSlotLabels[snapshot.SelectedSaveSlotIndex]
                : string.Empty;
            if (string.IsNullOrEmpty(label) || label == "빈 슬롯")
            {
                label = "빈칸";
            }
            else if (label == "사용 완료")
            {
                label = "0회";
            }

            return "S" + slotNumber + " " + label;
        }

        private static bool IsSelectedSaveSlotUsable(GreedLastLobbySnapshot snapshot)
        {
            return snapshot.SaveSlotUsable != null
                && snapshot.SelectedSaveSlotIndex >= 0
                && snapshot.SelectedSaveSlotIndex < snapshot.SaveSlotUsable.Length
                && snapshot.SaveSlotUsable[snapshot.SelectedSaveSlotIndex];
        }

        private static bool IsSelectedSaveSlotUsable(GreedLastStateSnapshot snapshot)
        {
            return snapshot.SaveSlotUsable != null
                && snapshot.SelectedSaveSlotIndex >= 0
                && snapshot.SelectedSaveSlotIndex < snapshot.SaveSlotUsable.Length
                && snapshot.SaveSlotUsable[snapshot.SelectedSaveSlotIndex];
        }

        private static bool IsSelectedSaveSlotOccupied(GreedLastStateSnapshot snapshot)
        {
            return snapshot.SaveSlotOccupied != null
                && snapshot.SelectedSaveSlotIndex >= 0
                && snapshot.SelectedSaveSlotIndex < snapshot.SaveSlotOccupied.Length
                && snapshot.SaveSlotOccupied[snapshot.SelectedSaveSlotIndex];
        }

        private void ApplyButtonTones(GreedLastStateSnapshot snapshot, bool saveDraftLike)
        {
            ResetButtonTones();
            if (!saveDraftLike)
            {
                return;
            }

            if (snapshot.SaveSlotOverwriteConfirmationPending)
            {
                SetButtonTone(nextPatternButton, ButtonConfirmColor, ButtonHighlightedColor, ButtonConfirmPressedColor);
            }

            if (snapshot.SaveSlotRenameConfirmationPending)
            {
                SetButtonTone(saveSlotRenameButton, ButtonConfirmColor, ButtonHighlightedColor, ButtonConfirmPressedColor);
            }

            if (snapshot.SaveSlotDeleteConfirmationPending)
            {
                SetButtonTone(saveSlotDeleteButton, ButtonDangerColor, ButtonDangerHighlightedColor, ButtonDangerPressedColor);
            }
        }

        private void ResetButtonTones()
        {
            SetButtonTone(normalRunButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(saveLoadoutButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(infiniteButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(infiniteRecordButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(retryButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(debugConnectionButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(nextPatternButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(returnLobbyButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(saveSlotDetailButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(saveSlotRenameButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(saveSlotDeleteButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(pauseButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(devInvincibleButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(infiniteTestStopButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(focusMaxButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(threatUpButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(threatDownButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonTone(hapticsButton, ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            SetButtonToneArray(devShortcutButtons);
            SetButtonToneArray(timingOffsetButtons);
            SetButtonToneArray(sfxVolumeButtons);
        }

        private static void SetButtonToneArray(Button[] buttons)
        {
            if (buttons == null)
            {
                return;
            }

            for (int i = 0; i < buttons.Length; i += 1)
            {
                SetButtonTone(buttons[i], ButtonNormalColor, ButtonHighlightedColor, ButtonPressedColor);
            }
        }

        private static void SetButtonTone(Button button, Color32 normal, Color32 highlighted, Color32 pressed)
        {
            if (button == null)
            {
                return;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = pressed;
            colors.selectedColor = highlighted;
            colors.disabledColor = ButtonDisabledColor;
            button.colors = colors;

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = button.interactable ? normal : ButtonDisabledColor;
            }
        }

        private void EnsureUi()
        {
            if (rootPanel != null)
            {
                return;
            }

            EnsureCamera();
            EnsureEventSystem();

            GameObject canvasObject = new GameObject("GreedLastCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasObject);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
            scaler.matchWidthOrHeight = 1f;

            rootPanel = CreateRect("Root", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image background = rootPanel.AddComponent<Image>();
            background.color = new Color32(11, 16, 18, 255);

            feedbackFlash = CreateRect("FeedbackFlash", rootPanel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).AddComponent<Image>();
            feedbackFlash.color = new Color32(255, 255, 255, 0);
            feedbackFlash.raycastTarget = false;

            CreateTemplePreview(rootPanel.transform);
            CreateAudio();

            titleText = CreateText(rootPanel.transform, "Title", "GREED LAST", 72, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetAnchored(titleText.rectTransform, 0.5f, 0.82f, 900f, 110f);

            subtitleText = CreateText(rootPanel.transform, "Subtitle", string.Empty, 30, TextAnchor.MiddleCenter, FontStyle.Normal);
            SetAnchored(subtitleText.rectTransform, 0.5f, 0.765f, 900f, 70f);

            detailText = CreateText(rootPanel.transform, "Detail", string.Empty, 33, TextAnchor.UpperCenter, FontStyle.Normal);
            SetAnchored(detailText.rectTransform, 0.5f, 0.59f, 850f, 260f);

            runHudText = CreateText(rootPanel.transform, "RunHud", string.Empty, 27, TextAnchor.UpperCenter, FontStyle.Bold);
            SetAnchored(runHudText.rectTransform, 0.5f, 0.255f, 900f, 245f);

            judgementFeedbackText = CreateText(rootPanel.transform, "JudgementFeedback", string.Empty, 48, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetAnchored(judgementFeedbackText.rectTransform, 0.5f, 0.385f, 850f, 96f);
            judgementFeedbackText.color = new Color32(255, 238, 181, 0);
            judgementFeedbackText.raycastTarget = false;

            CreateRunProgress(rootPanel.transform);
            CreateRunGauges(rootPanel.transform);
            CreateComboBadge(rootPanel.transform);
            CreatePauseMenuOverlay(rootPanel.transform);

            normalRunButton = CreateButton(rootPanel.transform, "NormalRunButton", "일반 런");
            saveLoadoutButton = CreateButton(rootPanel.transform, "SaveLoadoutButton", "저장 조합");
            infiniteButton = CreateButton(rootPanel.transform, "InfiniteButton", "무한모드");
            infiniteRecordButton = CreateButton(rootPanel.transform, "InfiniteRecordButton", "무한 기록");
            retryButton = CreateButton(rootPanel.transform, "RetryButton", "Retry Sync");
            debugConnectionButton = CreateButton(rootPanel.transform, "DebugConnectionButton", "연결 전환");
            nextPatternButton = CreateButton(rootPanel.transform, "NextPatternButton", "자동 시작");
            returnLobbyButton = CreateButton(rootPanel.transform, "ReturnLobbyButton", "로비로");
            saveSlotDetailButton = CreateButton(rootPanel.transform, "SaveSlotDetailButton", "상세 비교");
            saveSlotRenameButton = CreateButton(rootPanel.transform, "SaveSlotRenameButton", "이름 변경");
            saveSlotDeleteButton = CreateButton(rootPanel.transform, "SaveSlotDeleteButton", "슬롯 삭제");
            pauseButton = CreateButton(rootPanel.transform, "PauseButton", "일시정지");
            devInvincibleButton = CreateButton(rootPanel.transform, "DevInvincibleButton", "무적 OFF");
            infiniteTestStopButton = CreateButton(rootPanel.transform, "InfiniteTestStopButton", "종료 테스트");
            focusMaxButton = CreateButton(rootPanel.transform, "FocusMaxButton", "집중 MAX");
            threatUpButton = CreateButton(rootPanel.transform, "ThreatUpButton", "위협 +1");
            threatDownButton = CreateButton(rootPanel.transform, "ThreatDownButton", "위협 -1");
            hapticsButton = CreateButton(rootPanel.transform, "HapticsButton", "진동 ON");
            devShortcutButtons = new Button[4];
            devShortcutButtons[0] = CreateButton(rootPanel.transform, "DevGiftButton", "기프트");
            devShortcutButtons[1] = CreateButton(rootPanel.transform, "DevRelicButton", "유물");
            devShortcutButtons[2] = CreateButton(rootPanel.transform, "DevAfterCoreButton", "핵 직후");
            devShortcutButtons[3] = CreateButton(rootPanel.transform, "DevBeforeClearButton", "끝 직전");
            timingOffsetButtons = new Button[3];
            timingOffsetButtons[0] = CreateButton(rootPanel.transform, "TimingOffsetPlusButton", "빠름 +20");
            timingOffsetButtons[1] = CreateButton(rootPanel.transform, "TimingOffsetRecommendationButton", "추천/0");
            timingOffsetButtons[2] = CreateButton(rootPanel.transform, "TimingOffsetMinusButton", "늦음 -20");
            sfxVolumeButtons = new Button[3];
            sfxVolumeButtons[0] = CreateButton(rootPanel.transform, "SfxVolumeDownButton", "소리 -");
            sfxVolumeButtons[1] = CreateButton(rootPanel.transform, "SfxVolumeResetButton", "소리 100");
            sfxVolumeButtons[2] = CreateButton(rootPanel.transform, "SfxVolumeUpButton", "소리 +");

            SetAnchored(normalRunButton.GetComponent<RectTransform>(), 0.5f, 0.35f, 690f, 92f);
            SetAnchored(saveLoadoutButton.GetComponent<RectTransform>(), 0.5f, 0.29f, 690f, 92f);
            SetAnchored(infiniteButton.GetComponent<RectTransform>(), 0.5f, 0.23f, 690f, 92f);
            SetAnchored(infiniteRecordButton.GetComponent<RectTransform>(), 0.91f, 0.47f, 180f, 54f);
            SetAnchored(retryButton.GetComponent<RectTransform>(), 0.5f, 0.17f, 690f, 92f);
            SetAnchored(debugConnectionButton.GetComponent<RectTransform>(), 0.5f, 0.09f, 420f, 72f);
            SetAnchored(nextPatternButton.GetComponent<RectTransform>(), 0.5f, 0.155f, 690f, 86f);
            SetAnchored(returnLobbyButton.GetComponent<RectTransform>(), 0.5f, 0.09f, 420f, 72f);
            SetAnchored(saveSlotDetailButton.GetComponent<RectTransform>(), 0.91f, 0.47f, 180f, 54f);
            SetAnchored(saveSlotRenameButton.GetComponent<RectTransform>(), 0.91f, 0.425f, 180f, 54f);
            SetAnchored(saveSlotDeleteButton.GetComponent<RectTransform>(), 0.91f, 0.38f, 180f, 54f);
            SetAnchored(pauseButton.GetComponent<RectTransform>(), 0.91f, 0.735f, 180f, 54f);
            SetAnchored(devInvincibleButton.GetComponent<RectTransform>(), 0.91f, 0.69f, 180f, 54f);
            SetAnchored(infiniteTestStopButton.GetComponent<RectTransform>(), 0.91f, 0.645f, 180f, 54f);
            SetAnchored(focusMaxButton.GetComponent<RectTransform>(), 0.91f, 0.33f, 180f, 54f);
            SetAnchored(threatUpButton.GetComponent<RectTransform>(), 0.91f, 0.285f, 180f, 54f);
            SetAnchored(threatDownButton.GetComponent<RectTransform>(), 0.91f, 0.24f, 180f, 54f);
            SetAnchored(hapticsButton.GetComponent<RectTransform>(), 0.91f, 0.06f, 180f, 54f);
            SetAnchored(devShortcutButtons[0].GetComponent<RectTransform>(), 0.91f, 0.645f, 180f, 54f);
            SetAnchored(devShortcutButtons[1].GetComponent<RectTransform>(), 0.91f, 0.60f, 180f, 54f);
            SetAnchored(devShortcutButtons[2].GetComponent<RectTransform>(), 0.91f, 0.555f, 180f, 54f);
            SetAnchored(devShortcutButtons[3].GetComponent<RectTransform>(), 0.91f, 0.51f, 180f, 54f);
            SetAnchored(timingOffsetButtons[0].GetComponent<RectTransform>(), 0.91f, 0.465f, 180f, 54f);
            SetAnchored(timingOffsetButtons[1].GetComponent<RectTransform>(), 0.91f, 0.42f, 180f, 54f);
            SetAnchored(timingOffsetButtons[2].GetComponent<RectTransform>(), 0.91f, 0.375f, 180f, 54f);
            SetAnchored(sfxVolumeButtons[0].GetComponent<RectTransform>(), 0.91f, 0.195f, 180f, 54f);
            SetAnchored(sfxVolumeButtons[1].GetComponent<RectTransform>(), 0.91f, 0.15f, 180f, 54f);
            SetAnchored(sfxVolumeButtons[2].GetComponent<RectTransform>(), 0.91f, 0.105f, 180f, 54f);
            SetButtonFontSize(infiniteRecordButton, 24);
            SetButtonFontSize(saveSlotDetailButton, 24);
            SetButtonFontSize(saveSlotRenameButton, 24);
            SetButtonFontSize(saveSlotDeleteButton, 24);
            SetButtonFontSize(pauseButton, 24);
            SetButtonFontSize(devInvincibleButton, 24);
            SetButtonFontSize(infiniteTestStopButton, 24);
            SetButtonFontSize(focusMaxButton, 24);
            SetButtonFontSize(threatUpButton, 24);
            SetButtonFontSize(threatDownButton, 24);
            SetButtonFontSize(hapticsButton, 24);
            for (int i = 0; i < devShortcutButtons.Length; i += 1)
            {
                SetButtonFontSize(devShortcutButtons[i], 24);
            }

            for (int i = 0; i < timingOffsetButtons.Length; i += 1)
            {
                SetButtonFontSize(timingOffsetButtons[i], 22);
            }

            for (int i = 0; i < sfxVolumeButtons.Length; i += 1)
            {
                SetButtonFontSize(sfxVolumeButtons[i], 22);
            }

            normalRunButton.onClick.AddListener(() => normalRunRequested?.Invoke());
            saveLoadoutButton.onClick.AddListener(() => saveLoadoutRequested?.Invoke());
            infiniteButton.onClick.AddListener(() => infiniteRunRequested?.Invoke());
            infiniteRecordButton.onClick.AddListener(() => infiniteRecordRequested?.Invoke());
            retryButton.onClick.AddListener(() => retryRequested?.Invoke());
            debugConnectionButton.onClick.AddListener(() => debugConnectionRequested?.Invoke());
            nextPatternButton.onClick.AddListener(() => nextPatternRequested?.Invoke());
            returnLobbyButton.onClick.AddListener(() => returnLobbyRequested?.Invoke());
            saveSlotDetailButton.onClick.AddListener(() => saveSlotDetailRequested?.Invoke());
            saveSlotRenameButton.onClick.AddListener(() => saveSlotRenameRequested?.Invoke());
            saveSlotDeleteButton.onClick.AddListener(() => saveSlotDeleteRequested?.Invoke());
            pauseButton.onClick.AddListener(() => pauseRequested?.Invoke());
            devInvincibleButton.onClick.AddListener(() => invincibleRequested?.Invoke());
            infiniteTestStopButton.onClick.AddListener(() => infiniteTestStopRequested?.Invoke());
            focusMaxButton.onClick.AddListener(() => focusMaxRequested?.Invoke());
            threatUpButton.onClick.AddListener(() => threatUpRequested?.Invoke());
            threatDownButton.onClick.AddListener(() => threatDownRequested?.Invoke());
            hapticsButton.onClick.AddListener(() => ToggleHaptics());
            devShortcutButtons[0].onClick.AddListener(() => devShortcutRequested?.Invoke(GreedLastDevShortcut.GiftChoice));
            devShortcutButtons[1].onClick.AddListener(() => devShortcutRequested?.Invoke(GreedLastDevShortcut.RelicChoice));
            devShortcutButtons[2].onClick.AddListener(() => devShortcutRequested?.Invoke(GreedLastDevShortcut.AfterCore));
            devShortcutButtons[3].onClick.AddListener(() => devShortcutRequested?.Invoke(GreedLastDevShortcut.BeforeClear));
            timingOffsetButtons[0].onClick.AddListener(() => timingOffsetRequested?.Invoke(20));
            timingOffsetButtons[1].onClick.AddListener(() => timingOffsetRequested?.Invoke(0));
            timingOffsetButtons[2].onClick.AddListener(() => timingOffsetRequested?.Invoke(-20));
            sfxVolumeButtons[0].onClick.AddListener(() => AdjustSfxVolume(-0.1f));
            sfxVolumeButtons[1].onClick.AddListener(() => ResetSfxVolume());
            sfxVolumeButtons[2].onClick.AddListener(() => AdjustSfxVolume(0.1f));

            debugText = CreateText(rootPanel.transform, "Debug", string.Empty, 22, TextAnchor.LowerLeft, FontStyle.Normal);
            debugText.color = new Color32(125, 139, 143, 255);
            SetAnchored(debugText.rectTransform, 0.24f, 0.04f, 450f, 110f);
        }

        private void CreatePauseMenuOverlay(Transform parent)
        {
            pauseMenuOverlay = CreateRect("PauseMenuOverlay", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            pauseMenuOverlay.SetActive(false);

            pauseMenuBackdrop = CreateRect("PauseMenuBackdrop", pauseMenuOverlay.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).AddComponent<Image>();
            pauseMenuBackdrop.color = new Color32(5, 8, 9, 178);
            pauseMenuBackdrop.raycastTarget = false;

            GameObject panel = CreateRect("PauseMenuPanel", pauseMenuOverlay.transform, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.5f), new Vector2(760f, 820f));
            pauseMenuPanel = panel.AddComponent<Image>();
            pauseMenuPanel.color = new Color32(18, 27, 30, 236);
            pauseMenuPanel.raycastTarget = false;

            Image panelGlow = CreateRect("PauseMenuPanelGlow", panel.transform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero).AddComponent<Image>();
            panelGlow.color = new Color32(255, 238, 181, 22);
            panelGlow.raycastTarget = false;

            pauseMenuTitleText = CreateText(panel.transform, "PauseMenuTitle", "일시정지 메뉴", 52, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetAnchored(pauseMenuTitleText.rectTransform, 0.5f, 0.72f, 650f, 90f);
            pauseMenuTitleText.color = new Color32(237, 239, 231, 255);

            pauseMenuBodyText = CreateText(panel.transform, "PauseMenuBody", string.Empty, 29, TextAnchor.MiddleCenter, FontStyle.Normal);
            SetAnchored(pauseMenuBodyText.rectTransform, 0.5f, 0.58f, 650f, 130f);
            pauseMenuBodyText.color = new Color32(190, 204, 204, 235);
        }

        private void CreateRunProgress(Transform parent)
        {
            runProgressRoot = CreateRect("RunProgressRoot", parent, new Vector2(0.5f, 0.715f), new Vector2(0.5f, 0.715f), new Vector2(0.5f, 0.5f), new Vector2(760f, 64f));
            runProgressRoot.SetActive(false);

            Image back = CreateRect("RunProgressBack", runProgressRoot.transform, new Vector2(0f, 0.18f), new Vector2(1f, 0.58f), Vector2.zero, Vector2.zero).AddComponent<Image>();
            back.color = new Color32(19, 27, 30, 190);
            back.raycastTarget = false;

            runProgressFill = CreateRect("RunProgressFill", back.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).AddComponent<Image>();
            runProgressFill.color = new Color32(104, 151, 142, 220);
            runProgressFill.raycastTarget = false;

            runProgressText = CreateText(runProgressRoot.transform, "RunProgressText", string.Empty, 24, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetAnchored(runProgressText.rectTransform, 0.5f, 0.72f, 720f, 36f);
            runProgressText.color = new Color32(218, 226, 218, 230);
            runProgressText.raycastTarget = false;
        }

        private void CreateComboBadge(Transform parent)
        {
            comboBadgeRoot = CreateRect("ComboBadgeRoot", parent, new Vector2(0.5f, 0.672f), new Vector2(0.5f, 0.672f), new Vector2(0.5f, 0.5f), new Vector2(430f, 58f));
            comboBadgeRoot.SetActive(false);

            comboBadgeBack = CreateRect("ComboBadgeBack", comboBadgeRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).AddComponent<Image>();
            comboBadgeBack.color = new Color32(35, 65, 75, 218);
            comboBadgeBack.raycastTarget = false;

            Image accent = CreateRect("ComboBadgeAccent", comboBadgeRoot.transform, new Vector2(0f, 0f), new Vector2(0.025f, 1f), Vector2.zero, Vector2.zero).AddComponent<Image>();
            accent.color = new Color32(255, 238, 181, 235);
            accent.raycastTarget = false;

            comboBadgeText = CreateText(comboBadgeRoot.transform, "ComboBadgeText", string.Empty, 29, TextAnchor.MiddleCenter, FontStyle.Bold);
            comboBadgeText.rectTransform.anchorMin = Vector2.zero;
            comboBadgeText.rectTransform.anchorMax = Vector2.one;
            comboBadgeText.rectTransform.offsetMin = Vector2.zero;
            comboBadgeText.rectTransform.offsetMax = Vector2.zero;
            comboBadgeText.color = new Color32(237, 239, 231, 245);
            comboBadgeText.raycastTarget = false;
        }

        private void CreateForwardMotionLines(Transform parent)
        {
            const int lineCount = 7;
            runMotionLines = new Image[lineCount];
            for (int i = 0; i < lineCount; i += 1)
            {
                GameObject line = CreateRect("ForwardMotionLine_" + i, parent, new Vector2(0.48f, 0.82f), new Vector2(0.52f, 0.82f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f));
                Image image = line.AddComponent<Image>();
                image.color = new Color32(104, 151, 142, 0);
                image.raycastTarget = false;
                runMotionLines[i] = image;
            }
        }

        private void CreateRunGauges(Transform parent)
        {
            runGaugeRoot = CreateRect("RunGaugeRoot", parent, new Vector2(0.5f, 0.345f), new Vector2(0.5f, 0.345f), new Vector2(0.5f, 0.5f), new Vector2(860f, 96f));
            runGaugeRoot.SetActive(false);

            CreateRunGauge(runGaugeRoot.transform, "HealthGauge", "체력", 0.25f, new Color32(218, 86, 68, 220), out healthGaugeFill, out healthGaugeText);
            CreateRunGauge(runGaugeRoot.transform, "FocusGauge", "집중", 0.75f, new Color32(86, 158, 184, 220), out focusGaugeFill, out focusGaugeText);
        }

        private void CreateRunGauge(Transform parent, string name, string label, float x, Color32 fillColor, out Image fillImage, out Text valueText)
        {
            GameObject gaugeRoot = CreateRect(name, parent, new Vector2(x, 0.5f), new Vector2(x, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 76f));

            Image back = CreateRect(name + "Back", gaugeRoot.transform, new Vector2(0f, 0.24f), new Vector2(1f, 0.70f), Vector2.zero, Vector2.zero).AddComponent<Image>();
            back.color = new Color32(19, 27, 30, 210);
            back.raycastTarget = false;

            fillImage = CreateRect(name + "Fill", back.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).AddComponent<Image>();
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;

            Text labelText = CreateText(gaugeRoot.transform, name + "Label", label, 21, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetAnchored(labelText.rectTransform, 0.18f, 0.83f, 120f, 34f);
            labelText.color = new Color32(190, 204, 204, 220);
            labelText.raycastTarget = false;

            valueText = CreateText(gaugeRoot.transform, name + "Value", string.Empty, 24, TextAnchor.MiddleRight, FontStyle.Bold);
            SetAnchored(valueText.rectTransform, 0.76f, 0.83f, 170f, 34f);
            valueText.color = new Color32(237, 239, 231, 240);
            valueText.raycastTarget = false;
        }

        private void CreateTemplePreview(Transform parent)
        {
            GameObject laneRoot = CreateRect("LanePreview", parent, new Vector2(0.14f, 0.44f), new Vector2(0.86f, 0.74f), Vector2.zero, Vector2.zero);
            lanePreviewRoot = laneRoot;
            laneRoot.AddComponent<CanvasGroup>().alpha = 0.95f;

            laneImages = new Image[3];
            lanePressPulseUntil = new float[3];
            laneButtons = new Button[3];
            laneLabelTexts = new Text[3];
            for (int i = 0; i < laneImages.Length; i += 1)
            {
                float x = 0.2f + i * 0.3f;
                GameObject lane = CreateRect("Lane_" + i, laneRoot.transform, new Vector2(x, 0.5f), new Vector2(x, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(195f, 520f));
                Image laneImage = lane.AddComponent<Image>();
                laneImage.color = new Color32(32, 42, 49, 255);
                laneImages[i] = laneImage;
                Button laneButton = lane.AddComponent<Button>();
                ColorBlock laneColors = laneButton.colors;
                laneColors.normalColor = Color.white;
                laneColors.highlightedColor = new Color32(255, 238, 181, 255);
                laneColors.pressedColor = new Color32(205, 166, 70, 255);
                laneColors.disabledColor = new Color32(66, 71, 72, 180);
                laneButton.colors = laneColors;
                int capturedIndex = i;
                laneButton.onClick.AddListener(() => runChannelRequested?.Invoke((GreedLastRunChannel)(capturedIndex + 1)));
                laneButtons[i] = laneButton;

                Text laneLabel = CreateText(lane.transform, "Label", i == 0 ? "좌" : i == 1 ? "중" : "우", 56, TextAnchor.MiddleCenter, FontStyle.Bold);
                laneLabel.color = new Color32(218, 226, 218, 230);
                laneLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                laneLabel.verticalOverflow = VerticalWrapMode.Overflow;
                laneLabel.rectTransform.anchorMin = Vector2.zero;
                laneLabel.rectTransform.anchorMax = Vector2.one;
                laneLabel.rectTransform.offsetMin = Vector2.zero;
                laneLabel.rectTransform.offsetMax = Vector2.zero;
                laneLabelTexts[i] = laneLabel;
            }

            CreateForwardMotionLines(laneRoot.transform);

            GameObject goodBand = CreateRect("GoodTimingBand", laneRoot.transform, new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.24f), Vector2.zero, Vector2.zero);
            goodTimingBand = goodBand.AddComponent<Image>();
            goodTimingBand.color = new Color32(105, 168, 190, 0);
            goodTimingBand.raycastTarget = false;

            GameObject successBand = CreateRect("SuccessTimingBand", laneRoot.transform, new Vector2(0.08f, 0.21f), new Vector2(0.92f, 0.23f), Vector2.zero, Vector2.zero);
            successTimingBand = successBand.AddComponent<Image>();
            successTimingBand.color = new Color32(255, 238, 181, 0);
            successTimingBand.raycastTarget = false;

            GameObject line = CreateRect("JudgementLine", laneRoot.transform, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.22f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f));
            judgementLine = line.AddComponent<Image>();
            judgementLine.color = new Color32(255, 238, 181, 205);

            GameObject dangerBand = CreateRect("LaneDangerBand", laneRoot.transform, new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.88f), Vector2.zero, Vector2.zero);
            laneDangerBand = dangerBand.AddComponent<Image>();
            laneDangerBand.color = new Color32(207, 87, 65, 0);
            laneDangerBand.raycastTarget = false;
            dangerBand.transform.SetSiblingIndex(3);

            GameObject marker = CreateRect("BeatMarker", laneRoot.transform, new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.5f), new Vector2(112f, 112f));
            beatMarker = marker.AddComponent<Image>();
            beatMarker.color = new Color32(255, 238, 181, 255);

            GameObject glow = CreateRect("TrapGlow", laneRoot.transform, new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.5f), new Vector2(190f, 190f));
            trapGlow = glow.AddComponent<Image>();
            trapGlow.color = new Color32(207, 87, 65, 0);
            trapGlow.raycastTarget = false;

            GameObject trap = CreateRect("TrapMarker", laneRoot.transform, new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.5f), new Vector2(128f, 128f));
            trapMarker = trap.AddComponent<Image>();
            trapMarker.color = new Color32(207, 87, 65, 0);
            trapMarker.raycastTarget = false;

            GameObject strike = CreateRect("TrapStrike", laneRoot.transform, new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.5f), new Vector2(34f, 190f));
            trapStrike = strike.AddComponent<Image>();
            trapStrike.color = new Color32(255, 238, 181, 0);
            trapStrike.raycastTarget = false;

            timingGuideText = CreateText(laneRoot.transform, "TimingGuide", string.Empty, 28, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetAnchored(timingGuideText.rectTransform, 0.5f, 0.04f, 760f, 64f);
            timingGuideText.color = new Color32(218, 226, 218, 220);
        }

        private void CreateAudio()
        {
            sfxVolume = Mathf.Clamp(
                PlayerPrefs.GetFloat(SfxVolumePrefsKey, DefaultSfxVolume),
                MinSfxVolume,
                MaxSfxVolume);
            hapticsEnabled = PlayerPrefs.GetInt(HapticsEnabledPrefsKey, 1) == 1;
            rhythmAudio = gameObject.AddComponent<AudioSource>();
            rhythmAudio.playOnAwake = false;
            rhythmAudio.loop = false;
            rhythmAudio.spatialBlend = 0f;
            rhythmAudio.volume = sfxVolume;

            beatClip = CreateTone("beat_tick", 760f, 0.045f, 0.18f);
            perfectCueClip = CreateTone("perfect_cue", 1240f, 0.055f, 0.26f);
            clutchCueClip = CreateSweepTone("clutch_cue", 920f, 420f, 0.085f, 0.28f);
            offbeatCueClip = CreateDoubleTone("offbeat_cue", 620f, 960f, 0.105f, 0.22f);
            laneCueClips = new AudioClip[3];
            laneCueClips[0] = CreateTone("left_cue", 440f, 0.08f, 0.24f);
            laneCueClips[1] = CreateTone("center_cue", 660f, 0.08f, 0.24f);
            laneCueClips[2] = CreateTone("right_cue", 880f, 0.08f, 0.24f);
            successClip = CreateTone("success_tone", 1040f, 0.12f, 0.35f);
            goodClip = CreateTone("good_tone", 620f, 0.09f, 0.24f);
            missClip = CreateTone("miss_tone", 180f, 0.15f, 0.35f);
            chainClip = CreateDoubleTone("chain_tone", 880f, 1320f, 0.12f, 0.28f);
        }

        private static void EnsureCamera()
        {
            if (FindAnyObjectByType<Camera>() != null)
            {
                return;
            }

            GameObject cameraObject = new GameObject("GreedLastCamera", typeof(Camera), typeof(AudioListener));
            DontDestroyOnLoad(cameraObject);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(11, 16, 18, 255);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("GreedLastEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            GameObject buttonObject = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620f, 90f));
            Image image = buttonObject.AddComponent<Image>();
            image.color = ButtonNormalColor;

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonNormalColor;
            colors.highlightedColor = ButtonHighlightedColor;
            colors.pressedColor = ButtonPressedColor;
            colors.selectedColor = ButtonHighlightedColor;
            colors.disabledColor = ButtonDisabledColor;
            button.colors = colors;

            Text text = CreateText(buttonObject.transform, "Label", label, 33, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.color = new Color32(12, 16, 17, 255);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, TextAnchor alignment, FontStyle style)
        {
            GameObject textObject = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(800f, 160f));
            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color32(237, 239, 231, 255);
            return text;
        }

        private static GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot == Vector2.zero ? new Vector2(0.5f, 0.5f) : pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return obj;
        }

        private static void SetAnchored(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(x, y);
            rect.anchorMax = new Vector2(x, y);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void SetGaugeFill(Image fillImage, float amount)
        {
            if (fillImage == null)
            {
                return;
            }

            RectTransform rect = fillImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private float BuildLanePressPulse(int index)
        {
            if (lanePressPulseUntil == null || index < 0 || index >= lanePressPulseUntil.Length)
            {
                return 0f;
            }

            float remaining = lanePressPulseUntil[index] - Time.unscaledTime;
            if (remaining <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(remaining / 0.16f);
        }

        private void UpdateForwardMotionLines()
        {
            if (runMotionLines == null)
            {
                return;
            }

            bool motionActive = runModeActive
                && hasRunSnapshot
                && !latestRunSnapshot.Stopped
                && !latestRunSnapshot.Paused
                && !latestRunSnapshot.ChoiceActive;

            if (!motionActive)
            {
                for (int i = 0; i < runMotionLines.Length; i += 1)
                {
                    if (runMotionLines[i] != null)
                    {
                        runMotionLines[i].color = new Color32(104, 151, 142, 0);
                    }
                }

                return;
            }

            float threatStrength = latestRunSnapshot.InfiniteMode
                ? Mathf.Clamp01((latestRunSnapshot.InfiniteThreatLevel - 1) / 4f)
                : 0f;
            float speedBias = latestRunSnapshot.CountdownActive ? 0.55f : latestRunSnapshot.ActivePattern ? 1.18f : 0.82f;
            Color32 baseColor = latestRunSnapshot.InfiniteMode
                ? new Color32(205, 166, 70, 255)
                : new Color32(104, 151, 142, 255);

            for (int i = 0; i < runMotionLines.Length; i += 1)
            {
                Image line = runMotionLines[i];
                if (line == null)
                {
                    continue;
                }

                float phase = Mathf.Repeat(beatPhase * (0.46f + threatStrength * 0.2f) * speedBias + i / (float)runMotionLines.Length, 1f);
                float y = Mathf.Lerp(0.86f, 0.20f, phase);
                float halfWidth = Mathf.Lerp(0.035f, 0.42f, phase);
                RectTransform rect = line.rectTransform;
                rect.anchorMin = new Vector2(Mathf.Clamp01(0.5f - halfWidth), y);
                rect.anchorMax = new Vector2(Mathf.Clamp01(0.5f + halfWidth), y);
                rect.sizeDelta = new Vector2(0f, Mathf.Lerp(3f, 15f, phase));
                rect.anchoredPosition = Vector2.zero;

                int alpha = Mathf.RoundToInt(Mathf.Lerp(18f, 82f + threatStrength * 32f, phase) * Mathf.Lerp(0.72f, 1.08f, Mathf.Sin((beatPhase + phase) * Mathf.PI * 2f) * 0.5f + 0.5f));
                line.color = WithAlpha(baseColor, alpha);
            }
        }

        private void UpdateRunTimingVisuals()
        {
            if (trapMarker == null || judgementLine == null || !hasRunSnapshot)
            {
                HideTrapDangerVisuals();
                return;
            }

            if (!runModeActive)
            {
                HideTrapDangerVisuals();
                SetJudgementLineY(0.22f);
                judgementLine.color = new Color32(255, 238, 181, 120);
                HideTimingBands();
                if (timingGuideText != null)
                {
                    timingGuideText.text = string.Empty;
                }

                lastActivePatternVisual = false;
                targetCuePlayed = false;
                return;
            }

            if (!latestRunSnapshot.ActivePattern)
            {
                HideTrapDangerVisuals();
                SetJudgementLineY(0.22f);
                judgementLine.color = new Color32(255, 238, 181, 120);
                HideTimingBands();
                if (timingGuideText != null)
                {
                    timingGuideText.text = runModeActive ? "다음 함정 대기" : string.Empty;
                    timingGuideText.color = new Color32(125, 139, 143, 180);
                }

                lastActivePatternVisual = false;
                targetCuePlayed = false;
                return;
            }

            if (!lastActivePatternVisual)
            {
                PlayLaneCue(latestRunSnapshot.Pattern.Channel);
                targetCuePlayed = false;
            }

            if (!latestRunSnapshot.Paused && !targetCuePlayed && latestRunSnapshot.CoreDeltaSeconds >= -TargetCueLeadSeconds)
            {
                PlayClip(GetTimingCueClip(latestRunSnapshot.Pattern.TimingType), BuildTimingCueVolume(latestRunSnapshot));
                targetCuePlayed = true;
            }

            lastActivePatternVisual = true;

            float channelX = 0.2f;
            if (latestRunSnapshot.Pattern.Channel == GreedLastRunChannel.Center)
            {
                channelX = 0.5f;
            }
            else if (latestRunSnapshot.Pattern.Channel == GreedLastRunChannel.Right)
            {
                channelX = 0.8f;
            }

            float progress = latestRunSnapshot.BeatProgress;
            float coreProgress = Mathf.Clamp01(latestRunSnapshot.Pattern.CoreSeconds / Mathf.Max(0.01f, latestRunSnapshot.Pattern.HitSeconds));
            float y = Mathf.Lerp(0.88f, 0.18f, progress);
            float coreY = Mathf.Lerp(0.88f, 0.18f, coreProgress);
            RectTransform rect = trapMarker.rectTransform;
            rect.anchorMin = new Vector2(channelX, y);
            rect.anchorMax = new Vector2(channelX, y);
            rect.anchoredPosition = Vector2.zero;

            float coreNear = Mathf.Clamp01(1f - Mathf.Abs(latestRunSnapshot.CoreDeltaSeconds) / 0.32f);
            UpdateTrapDangerVisuals(channelX, y, coreNear);
            SetTimingBand(goodTimingBand, latestRunSnapshot.Pattern.GoodWindowSeconds, channelX, false);
            SetTimingBand(successTimingBand, latestRunSnapshot.Pattern.SuccessWindowSeconds, channelX, true);

            SetJudgementLineY(coreY);

            trapMarker.color = Color32.Lerp(new Color32(207, 87, 65, 170), new Color32(255, 238, 181, 255), coreNear);
            trapMarker.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.88f, 1.22f, coreNear);
            judgementLine.color = Color32.Lerp(new Color32(255, 238, 181, 150), new Color32(255, 255, 255, 255), coreNear);
            if (timingGuideText != null)
            {
                timingGuideText.text = BuildTimingGuideLabel(latestRunSnapshot);
                timingGuideText.color = GetTimingGuideColor(latestRunSnapshot);
            }
        }

        private void UpdateTrapDangerVisuals(float channelX, float y, float coreNear)
        {
            float threatStrength = latestRunSnapshot.InfiniteMode
                ? Mathf.Clamp01((latestRunSnapshot.InfiniteThreatLevel - 1) / 4f)
                : 0f;
            float warningPulse = 0.5f + Mathf.Sin((beatPhase * 1.6f + coreNear) * Mathf.PI * 2f) * 0.5f;
            Color32 baseColor = GetTrapBaseColor(latestRunSnapshot.Pattern.TimingType);
            Color32 accentColor = GetTrapAccentColor(latestRunSnapshot.Pattern.TimingType);
            int dangerAlpha = Mathf.RoundToInt(Mathf.Lerp(28f, 116f, Mathf.Max(coreNear, threatStrength * 0.7f)) * Mathf.Lerp(0.78f, 1.18f, warningPulse));
            int glowAlpha = Mathf.RoundToInt(Mathf.Lerp(35f, 154f, coreNear) + threatStrength * 32f);
            int strikeAlpha = Mathf.RoundToInt(Mathf.Lerp(70f, 240f, coreNear));

            if (laneDangerBand != null)
            {
                float halfWidth = Mathf.Lerp(0.095f, 0.13f, threatStrength);
                RectTransform dangerRect = laneDangerBand.rectTransform;
                dangerRect.anchorMin = new Vector2(Mathf.Clamp01(channelX - halfWidth), 0.18f);
                dangerRect.anchorMax = new Vector2(Mathf.Clamp01(channelX + halfWidth), 0.88f);
                dangerRect.offsetMin = Vector2.zero;
                dangerRect.offsetMax = Vector2.zero;
                laneDangerBand.color = WithAlpha(baseColor, dangerAlpha);
            }

            if (trapGlow != null)
            {
                RectTransform glowRect = trapGlow.rectTransform;
                glowRect.anchorMin = new Vector2(channelX, y);
                glowRect.anchorMax = new Vector2(channelX, y);
                glowRect.anchoredPosition = Vector2.zero;
                glowRect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.44f + threatStrength * 0.22f, Mathf.Max(coreNear, warningPulse * 0.42f));
                trapGlow.color = WithAlpha(baseColor, glowAlpha);
            }

            if (trapStrike != null)
            {
                RectTransform strikeRect = trapStrike.rectTransform;
                strikeRect.anchorMin = new Vector2(channelX, y);
                strikeRect.anchorMax = new Vector2(channelX, y);
                strikeRect.anchoredPosition = Vector2.zero;
                strikeRect.sizeDelta = new Vector2(Mathf.Lerp(24f, 44f, coreNear), Mathf.Lerp(150f, 230f, coreNear + threatStrength * 0.25f));
                strikeRect.localScale = Vector3.one * Mathf.Lerp(0.82f, 1.18f, warningPulse);
                strikeRect.localEulerAngles = new Vector3(0f, 0f, GetTrapStrikeAngle(latestRunSnapshot.Pattern.TimingType));
                trapStrike.color = WithAlpha(accentColor, strikeAlpha);
            }
        }

        private void HideTrapDangerVisuals()
        {
            if (trapMarker != null)
            {
                trapMarker.color = new Color32(207, 87, 65, 0);
            }

            if (laneDangerBand != null)
            {
                laneDangerBand.color = new Color32(207, 87, 65, 0);
            }

            if (trapGlow != null)
            {
                trapGlow.color = new Color32(207, 87, 65, 0);
            }

            if (trapStrike != null)
            {
                trapStrike.color = new Color32(255, 238, 181, 0);
            }
        }

        private void HideTimingBands()
        {
            if (goodTimingBand != null)
            {
                goodTimingBand.color = new Color32(105, 168, 190, 0);
            }

            if (successTimingBand != null)
            {
                successTimingBand.color = new Color32(255, 238, 181, 0);
            }
        }

        private void SetJudgementLineY(float y)
        {
            RectTransform lineRect = judgementLine.rectTransform;
            lineRect.anchorMin = new Vector2(0.08f, y);
            lineRect.anchorMax = new Vector2(0.92f, y);
            lineRect.anchoredPosition = Vector2.zero;
        }

        private void SetTimingBand(Image band, float windowSeconds, float channelX, bool focusedLaneOnly)
        {
            if (band == null)
            {
                return;
            }

            GreedLastPatternModel pattern = latestRunSnapshot.Pattern;
            float hitSeconds = Mathf.Max(0.01f, pattern.HitSeconds);
            float startProgress = Mathf.Clamp01((pattern.CoreSeconds - windowSeconds) / hitSeconds);
            float endProgress = Mathf.Clamp01((pattern.CoreSeconds + windowSeconds) / hitSeconds);
            float yA = Mathf.Lerp(0.88f, 0.18f, startProgress);
            float yB = Mathf.Lerp(0.88f, 0.18f, endProgress);
            RectTransform rect = band.rectTransform;
            float minX = focusedLaneOnly ? Mathf.Clamp01(channelX - 0.095f) : 0.08f;
            float maxX = focusedLaneOnly ? Mathf.Clamp01(channelX + 0.095f) : 0.92f;
            rect.anchorMin = new Vector2(minX, Mathf.Min(yA, yB));
            rect.anchorMax = new Vector2(maxX, Mathf.Max(yA, yB));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            band.color = band == successTimingBand
                ? new Color32(255, 238, 181, 116)
                : new Color32(105, 168, 190, 28);
        }

        private void PlayLaneCue(GreedLastRunChannel channel)
        {
            if (laneCueClips == null)
            {
                return;
            }

            int index = (int)channel - 1;
            if (index >= 0 && index < laneCueClips.Length)
            {
                PlayClip(laneCueClips[index], LaneCueVolume);
            }
        }

        private AudioClip GetTimingCueClip(GreedLastTimingType timingType)
        {
            switch (timingType)
            {
                case GreedLastTimingType.Clutch:
                    return clutchCueClip;
                case GreedLastTimingType.Offbeat:
                    return offbeatCueClip;
                case GreedLastTimingType.Perfect:
                    return perfectCueClip;
                default:
                    return perfectCueClip;
            }
        }

        private static float BuildTimingCueVolume(GreedLastRunSnapshot snapshot)
        {
            if (!snapshot.InfiniteMode)
            {
                return TargetCueVolume;
            }

            return Mathf.Lerp(TargetCueVolume, 0.36f, Mathf.Clamp01((snapshot.InfiniteThreatLevel - 1) / 4f));
        }

        private void PlayJudgementFeedback(GreedLastRunSnapshot snapshot)
        {
            bool newResult = snapshot.LastResult != GreedLastJudgementResult.None
                && (snapshot.Score != lastFeedbackScore
                    || snapshot.Health != lastFeedbackHealth
                    || snapshot.JudgementText != lastJudgementText);

            lastFeedbackScore = snapshot.Score;
            lastFeedbackHealth = snapshot.Health;
            lastJudgementText = snapshot.JudgementText;

            if (!newResult)
            {
                return;
            }

            switch (snapshot.LastResult)
            {
                case GreedLastJudgementResult.Success:
                    PlayClip(successClip, 0.55f);
                    comboBadgePulseUntil = Time.unscaledTime + 0.22f;
                    if (snapshot.Combo >= 3)
                    {
                        PlayClip(chainClip, Mathf.Lerp(0.24f, 0.40f, Mathf.Clamp01(snapshot.Combo / 15f)));
                        PlayHaptic(0.035f);
                    }
                    else
                    {
                        PlayHaptic(0.018f);
                    }

                    feedbackFlash.color = new Color32(255, 238, 181, 90);
                    feedbackFlashUntil = Time.unscaledTime + 0.12f;
                    break;
                case GreedLastJudgementResult.Good:
                    PlayClip(goodClip, 0.38f);
                    PlayHaptic(0.012f);
                    feedbackFlash.color = new Color32(105, 168, 190, 75);
                    feedbackFlashUntil = Time.unscaledTime + 0.10f;
                    break;
                case GreedLastJudgementResult.Miss:
                    PlayClip(missClip, 0.48f);
                    PlayHaptic(0.075f);
                    feedbackFlash.color = new Color32(180, 42, 36, 95);
                    feedbackFlashUntil = Time.unscaledTime + 0.16f;
                    break;
            }

            ShowJudgementFeedback(snapshot);
        }

        private void ShowJudgementFeedback(GreedLastRunSnapshot snapshot)
        {
            if (judgementFeedbackText == null)
            {
                return;
            }

            judgementFeedbackText.fontSize = 48;
            judgementFeedbackText.text = BuildJudgementFeedbackText(snapshot);
            judgementFeedbackText.color = GetJudgementFeedbackColor(snapshot.LastResult);
            judgementFeedbackUntil = Time.unscaledTime + 0.82f;
        }

        private void ShowResumeCountdownFeedback(GreedLastRunSnapshot snapshot)
        {
            if (judgementFeedbackText == null)
            {
                return;
            }

            judgementFeedbackText.fontSize = 104;
            judgementFeedbackText.text = BuildResumeCountdownFeedbackText(snapshot);
            judgementFeedbackText.color = new Color32(255, 238, 181, 255);
            judgementFeedbackUntil = Time.unscaledTime + 0.12f;
        }

        private static string BuildJudgementFeedbackText(GreedLastRunSnapshot snapshot)
        {
            string[] lines = snapshot.JudgementText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string timingLine = lines.Length > 1 ? lines[1] : string.Empty;
            switch (snapshot.LastResult)
            {
                case GreedLastJudgementResult.Success:
                    string chainText = snapshot.Combo >= 3 ? "  연쇄 " + BuildRhythmChainLabel(snapshot) : string.Empty;
                    return string.IsNullOrEmpty(timingLine) ? "SUCCESS" + chainText : "SUCCESS" + chainText + "  " + timingLine;
                case GreedLastJudgementResult.Good:
                    return string.IsNullOrEmpty(timingLine) ? "GOOD" : "GOOD  " + timingLine;
                case GreedLastJudgementResult.Miss:
                    if (snapshot.MissReason == "wrong_channel")
                    {
                        return string.IsNullOrEmpty(timingLine) ? "MISS  채널 오류" : "MISS  " + timingLine;
                    }

                    if (snapshot.MissReason == "no_input")
                    {
                        return "MISS  입력 없음";
                    }

                    return string.IsNullOrEmpty(timingLine) ? "MISS" : "MISS  " + timingLine;
                default:
                    return string.Empty;
            }
        }

        private static string BuildResumeCountdownFeedbackText(GreedLastRunSnapshot snapshot)
        {
            if (snapshot.CountdownRemainingSeconds > 0.9f)
            {
                return "2";
            }

            if (snapshot.CountdownRemainingSeconds > 0.25f)
            {
                return "1";
            }

            return "GO";
        }

        private static Color32 GetJudgementFeedbackColor(GreedLastJudgementResult result)
        {
            switch (result)
            {
                case GreedLastJudgementResult.Success:
                    return new Color32(255, 238, 181, 255);
                case GreedLastJudgementResult.Good:
                    return new Color32(132, 190, 208, 245);
                case GreedLastJudgementResult.Miss:
                    return new Color32(231, 91, 77, 245);
                default:
                    return new Color32(255, 255, 255, 0);
            }
        }

        private void UpdateFeedbackFlash()
        {
            if (feedbackFlash == null || feedbackFlash.color.a <= 0f)
            {
                UpdateJudgementFeedbackText();
                return;
            }

            if (Time.unscaledTime >= feedbackFlashUntil)
            {
                feedbackFlash.color = new Color32(255, 255, 255, 0);
            }

            UpdateJudgementFeedbackText();
        }

        private void UpdateJudgementFeedbackText()
        {
            if (judgementFeedbackText == null)
            {
                return;
            }

            if (runModeActive
                && hasRunSnapshot
                && (latestRunSnapshot.ResumeCountdownActive || (latestRunSnapshot.StartCountdownActive && !latestRunSnapshot.Paused)))
            {
                ShowResumeCountdownFeedback(latestRunSnapshot);
                return;
            }

            if (!runModeActive || Time.unscaledTime >= judgementFeedbackUntil)
            {
                judgementFeedbackText.fontSize = 48;
                judgementFeedbackText.color = new Color32(255, 238, 181, 0);
            }
        }

        private void PlayClip(AudioClip clip, float volume)
        {
            if (rhythmAudio != null && clip != null)
            {
                rhythmAudio.PlayOneShot(clip, volume);
            }
        }

        private void PlayHaptic(float seconds)
        {
            if (!hapticsEnabled)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    long milliseconds = Mathf.Clamp(Mathf.RoundToInt(seconds * 1000f), 1, 120);
                    vibrator.Call("vibrate", milliseconds);
                }
            }
            catch
            {
                Handheld.Vibrate();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        private void ToggleHaptics()
        {
            hapticsEnabled = !hapticsEnabled;
            PlayerPrefs.SetInt(HapticsEnabledPrefsKey, hapticsEnabled ? 1 : 0);
            PlayerPrefs.Save();
            UpdateHapticsButtonLabel();
            if (hapticsEnabled)
            {
                PlayHaptic(0.03f);
            }
        }

        private void UpdateHapticsButtonLabel()
        {
            if (hapticsButton == null)
            {
                return;
            }

            SetButtonLabel(hapticsButton, hapticsEnabled ? "진동 ON" : "진동 OFF");
        }

        private void AdjustSfxVolume(float delta)
        {
            SetSfxVolume(sfxVolume + delta);
            PlayClip(successClip, 0.35f);
        }

        private void ResetSfxVolume()
        {
            SetSfxVolume(DefaultSfxVolume);
            PlayClip(successClip, 0.35f);
        }

        private void SetSfxVolume(float value)
        {
            sfxVolume = Mathf.Clamp(value, MinSfxVolume, MaxSfxVolume);
            if (rhythmAudio != null)
            {
                rhythmAudio.volume = sfxVolume;
            }

            PlayerPrefs.SetFloat(SfxVolumePrefsKey, sfxVolume);
            PlayerPrefs.Save();
            UpdateSfxVolumeButtonLabel();
        }

        private void UpdateSfxVolumeButtonLabel()
        {
            if (sfxVolumeButtons == null || sfxVolumeButtons.Length < 2 || sfxVolumeButtons[1] == null)
            {
                return;
            }

            SetButtonLabel(sfxVolumeButtons[1], "소리 " + Mathf.RoundToInt(sfxVolume * 100f));
        }

        private static AudioClip CreateTone(string name, float frequency, float duration, float gain)
        {
            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i += 1)
            {
                float t = i / (float)sampleRate;
                float envelope = 1f - (i / (float)samples);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * gain * envelope;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSweepTone(string name, float startFrequency, float endFrequency, float duration, float gain)
        {
            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i += 1)
            {
                float t = i / (float)Mathf.Max(1, samples - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += 2f * Mathf.PI * frequency / sampleRate;
                float envelope = 1f - t;
                data[i] = Mathf.Sin(phase) * gain * envelope;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateDoubleTone(string name, float firstFrequency, float secondFrequency, float duration, float gain)
        {
            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i += 1)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)Mathf.Max(1, samples - 1);
                float envelope = 1f - normalized;
                float split = normalized < 0.48f ? 1f : 0f;
                float first = Mathf.Sin(2f * Mathf.PI * firstFrequency * t) * split;
                float second = Mathf.Sin(2f * Mathf.PI * secondFrequency * t) * (1f - split);
                data[i] = (first + second) * gain * envelope;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
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

        private static string FormatEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }

        private static string FormatBool(bool value)
        {
            return value ? "Y" : "N";
        }

        private static string FormatTimingOffset(float seconds)
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

        private static string BuildFocusGuardLabel(GreedLastRunSnapshot snapshot)
        {
            return snapshot.Focus >= snapshot.MaxFocus
                ? "준비"
                : "충전 " + snapshot.Focus + "/" + snapshot.MaxFocus;
        }

        private static string BuildRhythmChainLabel(GreedLastRunSnapshot snapshot)
        {
            int level = Mathf.Clamp(snapshot.Combo / 3, 0, 5);
            return level <= 0 ? "-" : "x" + level;
        }

        private static string BuildInfiniteSnapshotGrade(GreedLastRunSnapshot snapshot)
        {
            int gradeScore = snapshot.Score
                + snapshot.InfiniteSectionsCleared * 120
                + snapshot.MaxCombo * 30
                - snapshot.MissCount * 90;
            if (gradeScore >= 2000)
            {
                return "S";
            }

            if (gradeScore >= 1300)
            {
                return "A";
            }

            if (gradeScore >= 700)
            {
                return "B";
            }

            return "C";
        }

        private static float BuildBeatVisualSpeed(GreedLastRunSnapshot snapshot)
        {
            if (snapshot.ResumeCountdownActive || (snapshot.StartCountdownActive && !snapshot.Paused))
            {
                return 1.6f;
            }

            if (snapshot.Paused)
            {
                return 0f;
            }

            if (!snapshot.InfiniteMode)
            {
                return 1.6f;
            }

            return Mathf.Lerp(1.6f, 2.18f, Mathf.Clamp01((snapshot.InfiniteThreatLevel - 1) / 4f));
        }

        private static float BuildBeatInterval(GreedLastRunSnapshot snapshot)
        {
            if (!snapshot.InfiniteMode)
            {
                return 0.5f;
            }

            return Mathf.Lerp(0.5f, 0.36f, Mathf.Clamp01((snapshot.InfiniteThreatLevel - 1) / 4f));
        }

        private static string BuildTimingGuideLabel(GreedLastRunSnapshot snapshot)
        {
            if (snapshot.ResumeCountdownActive)
            {
                return "재개 " + snapshot.ResumeCountdownRemainingSeconds.ToString("0.0") + "s";
            }

            if (snapshot.Paused)
            {
                return "일시정지";
            }

            if (snapshot.StartCountdownActive)
            {
                return "시작 " + snapshot.StartCountdownRemainingSeconds.ToString("0.0") + "s";
            }

            if (!snapshot.ActivePattern)
            {
                return "대기";
            }

            float secondsToCore = -snapshot.CoreDeltaSeconds;
            string timingWord = Mathf.Abs(snapshot.CoreDeltaSeconds) <= snapshot.Pattern.SuccessWindowSeconds
                ? "지금"
                : secondsToCore > 0f
                    ? "준비 " + secondsToCore.ToString("0.00") + "s"
                    : "늦음 " + Mathf.Abs(secondsToCore).ToString("0.00") + "s";
            return FormatChannel(snapshot.Pattern.Channel)
                + " / " + timingWord
                + $" / 성공 ±{snapshot.Pattern.SuccessWindowSeconds:0.00}s";
        }

        private static Color32 GetTimingGuideColor(GreedLastRunSnapshot snapshot)
        {
            if (snapshot.ResumeCountdownActive || (snapshot.StartCountdownActive && !snapshot.Paused))
            {
                return new Color32(255, 238, 181, 245);
            }

            if (snapshot.Paused)
            {
                return new Color32(125, 139, 143, 220);
            }

            if (!snapshot.ActivePattern)
            {
                return new Color32(125, 139, 143, 180);
            }

            if (Mathf.Abs(snapshot.CoreDeltaSeconds) <= snapshot.Pattern.SuccessWindowSeconds)
            {
                return new Color32(255, 238, 181, 255);
            }

            if (snapshot.CoreDeltaSeconds > snapshot.Pattern.GoodWindowSeconds)
            {
                return new Color32(207, 87, 65, 235);
            }

            return new Color32(218, 226, 218, 220);
        }

        private static Color32 GetTrapBaseColor(GreedLastTimingType timingType)
        {
            switch (timingType)
            {
                case GreedLastTimingType.Perfect:
                    return new Color32(211, 174, 76, 255);
                case GreedLastTimingType.Clutch:
                    return new Color32(207, 87, 65, 255);
                case GreedLastTimingType.Offbeat:
                    return new Color32(95, 153, 171, 255);
                default:
                    return new Color32(207, 87, 65, 255);
            }
        }

        private static Color32 GetTrapAccentColor(GreedLastTimingType timingType)
        {
            switch (timingType)
            {
                case GreedLastTimingType.Perfect:
                    return new Color32(255, 238, 181, 255);
                case GreedLastTimingType.Clutch:
                    return new Color32(255, 169, 118, 255);
                case GreedLastTimingType.Offbeat:
                    return new Color32(168, 220, 226, 255);
                default:
                    return new Color32(255, 238, 181, 255);
            }
        }

        private static float GetTrapStrikeAngle(GreedLastTimingType timingType)
        {
            switch (timingType)
            {
                case GreedLastTimingType.Clutch:
                    return -18f;
                case GreedLastTimingType.Offbeat:
                    return 22f;
                default:
                    return 0f;
            }
        }

        private static Color32 WithAlpha(Color32 color, int alpha)
        {
            color.a = (byte)Mathf.Clamp(alpha, 0, 255);
            return color;
        }

        private void UpdateLaneLabels(GreedLastRunSnapshot snapshot)
        {
            if (laneLabelTexts == null || laneLabelTexts.Length < 3)
            {
                return;
            }

            if (snapshot.ChoiceActive)
            {
                SetLaneLabelMode(34, true);
                laneLabelTexts[0].text = snapshot.ChoiceLeft;
                laneLabelTexts[1].text = snapshot.ChoiceCenter;
                laneLabelTexts[2].text = snapshot.ChoiceRight;
                return;
            }

            SetLaneLabelMode(56, false);
            laneLabelTexts[0].text = "좌";
            laneLabelTexts[1].text = "중";
            laneLabelTexts[2].text = "우";
        }

        private void UpdateSaveSlotLabels(
            int selectedSlotIndex,
            string[] slotLabels,
            bool[] slotUsable,
            bool[] slotOccupied,
            bool showUsability)
        {
            if (laneLabelTexts == null || laneLabelTexts.Length < 3)
            {
                return;
            }

            SetLaneLabelMode(34, true);
            for (int i = 0; i < laneLabelTexts.Length; i += 1)
            {
                bool selected = i == selectedSlotIndex;
                bool usable = slotUsable != null && i < slotUsable.Length && slotUsable[i];
                bool occupied = slotOccupied != null && i < slotOccupied.Length && slotOccupied[i];
                string slotState = slotLabels != null
                    && i < slotLabels.Length
                    && !string.IsNullOrEmpty(slotLabels[i])
                    ? slotLabels[i]
                    : "슬롯 " + (i + 1);
                string actionHint = showUsability
                    ? usable ? "시작 가능" : occupied ? "사용 완료" : "사용 불가"
                    : occupied ? "교체 대상" : "바로 저장";

                laneLabelTexts[i].text = selected
                    ? "선택\nS" + (i + 1) + "\n" + slotState + "\n" + actionHint
                    : "S" + (i + 1) + "\n" + slotState + "\n" + actionHint;

                if (laneImages != null && i < laneImages.Length && laneImages[i] != null)
                {
                    if (showUsability)
                    {
                        laneImages[i].color = selected
                            ? usable ? new Color32(154, 112, 48, 255) : new Color32(108, 54, 48, 255)
                            : usable ? new Color32(32, 52, 55, 225) : new Color32(24, 29, 31, 205);
                    }
                    else
                    {
                        laneImages[i].color = selected
                            ? occupied ? new Color32(154, 112, 48, 255) : new Color32(74, 123, 118, 255)
                            : occupied ? new Color32(32, 42, 49, 225) : new Color32(24, 50, 49, 215);
                    }
                }
            }
        }

        private void UpdateRecordBoardLaneLabels(int pageIndex)
        {
            if (laneLabelTexts == null || laneLabelTexts.Length < 3)
            {
                return;
            }

            int page = NormalizeRecordBoardPage(pageIndex);
            int previousPage = (page + RecordBoardPageCount - 1) % RecordBoardPageCount;
            int nextPage = (page + 1) % RecordBoardPageCount;
            SetLaneLabelMode(34, true);
            laneLabelTexts[0].text = "좌\n" + GetRecordBoardPageLabel(previousPage);
            laneLabelTexts[1].text = "중\n요약";
            laneLabelTexts[2].text = "우\n" + GetRecordBoardPageLabel(nextPage);
            if (laneButtons != null)
            {
                for (int i = 0; i < laneButtons.Length; i += 1)
                {
                    laneButtons[i].interactable = true;
                }
            }
        }

        private static string GetRecordBoardPageLabel(int page)
        {
            switch (NormalizeRecordBoardPage(page))
            {
                case 1:
                    return "일반\n런";
                case 2:
                    return "무한\n요약";
                case 3:
                    return "무한\n랭킹";
                default:
                    return "요약";
            }
        }

        private static int NormalizeRecordBoardPage(int page)
        {
            return ((page % RecordBoardPageCount) + RecordBoardPageCount) % RecordBoardPageCount;
        }

        private void UpdateDefaultLaneLabels()
        {
            if (laneLabelTexts == null || laneLabelTexts.Length < 3)
            {
                return;
            }

            SetLaneLabelMode(56, false);
            laneLabelTexts[0].text = "좌";
            laneLabelTexts[1].text = "중";
            laneLabelTexts[2].text = "우";
            if (laneButtons != null)
            {
                for (int i = 0; i < laneButtons.Length; i += 1)
                {
                    laneButtons[i].interactable = false;
                }
            }
        }

        private void SetLaneLabelMode(int fontSize, bool bestFit)
        {
            for (int i = 0; i < laneLabelTexts.Length; i += 1)
            {
                Text label = laneLabelTexts[i];
                if (label == null)
                {
                    continue;
                }

                label.fontSize = fontSize;
                label.resizeTextForBestFit = bestFit;
                label.resizeTextMinSize = 20;
                label.resizeTextMaxSize = fontSize;
            }
        }

        private static void SetButtonFontSize(Button button, int fontSize)
        {
            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = fontSize;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 18;
                text.resizeTextMaxSize = fontSize;
            }
        }

        private static void SetButtonLabel(Button button, string label)
        {
            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}
