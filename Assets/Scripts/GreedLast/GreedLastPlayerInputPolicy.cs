namespace GreedLast
{
    // Player keyboard inputs are available on PC release builds too. This policy
    // controls presentation only; it never enables development commands.
    public static class GreedLastPlayerInputPolicy
    {
        public static bool ShowKeyboardHints(bool devToolsEnabled, bool isMobilePlatform)
            => devToolsEnabled || !isMobilePlatform;

        public static bool ShowHapticsSetting(bool devToolsEnabled, bool isMobilePlatform)
            => devToolsEnabled || isMobilePlatform;

        public static string ChannelKeyHint(int laneIndex)
        {
            switch (laneIndex)
            {
                case 0: return "A/←";
                case 1: return "S/Space";
                case 2: return "D/→";
                default: return string.Empty;
            }
        }

        public static string PauseButtonLabel(bool paused, bool resumeCountdown,
            bool devToolsEnabled, bool isMobilePlatform)
        {
            if (resumeCountdown) return "재개 준비";
            if (paused) return "재개";
            return ShowKeyboardHints(devToolsEnabled, isMobilePlatform) ? "ESC/P 메뉴" : "일시정지";
        }

        public static string ResumeHint(bool devToolsEnabled, bool isMobilePlatform)
            => ShowKeyboardHints(devToolsEnabled, isMobilePlatform)
                ? "ESC/P 또는 재개로 이어가기" : "재개 버튼으로 이어가기";

        public static string ResumeInstruction(bool devToolsEnabled, bool isMobilePlatform)
            => ShowKeyboardHints(devToolsEnabled, isMobilePlatform)
                ? "ESC/P 또는 재개를 누르면 카운트와 앞 박자 여유 뒤에 이어집니다."
                : "재개 버튼을 누르면 카운트와 앞 박자 여유 뒤에 이어집니다.";

        public static string PauseActionLabel(bool paused, bool devToolsEnabled, bool isMobilePlatform)
        {
            string action = paused ? "재개" : "일시정지";
            return ShowKeyboardHints(devToolsEnabled, isMobilePlatform) ? action + " (ESC/P)" : action;
        }
    }
}
