using System;

namespace GreedLast.Editor
{
    internal static class GreedLastPrototypePracticePreference
    {
        internal interface IStore
        {
            bool Exists { get; }
            int Value { get; set; }
            bool Owned { get; set; }
            void Delete();
            void Save();
            void Backup(bool exists, int value);
        }

        internal static void Prepare(IStore store, bool existed, int value)
        {
            Require(!store.Owned, "A previous onboarding fixture still requires restoration; keep its baseline intact.");
            Validate(existed, value);
            Require(Matches(store, existed, value), "The onboarding baseline changed before fixture acquisition.");
            // The durable baseline must exist before claiming ownership or changing the preference.
            store.Backup(existed, value);
            store.Owned = true;
            store.Value = 1;
            store.Save();
            Require(Matches(store, true, 1), "The onboarding fixture was not applied; restoration is still required.");
        }

        internal static bool Restore(IStore store, bool existed, int value)
        {
            if (!store.Owned) return false;
            Validate(existed, value);
            if (existed) store.Value = value;
            else store.Delete();
            store.Save();
            Require(Matches(store, existed, value), "The onboarding fixture did not restore its captured baseline.");
            store.Owned = false;
            return true;
        }

        private static bool Matches(IStore store, bool existed, int value)
            => store.Exists == existed && (!existed || store.Value == value);

        private static void Validate(bool existed, int value)
            => Require(!existed || value == 0 || value == 1,
                "An onboarding fixture must not overwrite or restore an unknown personal value.");

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
