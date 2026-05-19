using System;
using UnityEngine;

namespace GroceryQuotaHorror.Data
{
    public static class GameRuntime
    {
        private const string DefaultProfilesResourcePath = "BalanceProfiles";
        private const string SharedBalanceResourcePath = "BalanceProfiles/Prototype";
        private const string DefaultContentResourcePath = "GameContentDatabase";

        private static GameBalanceProfile currentBalance;
        private static GameContentDatabase currentContent;

        public static GameBalanceProfile Balance
        {
            get
            {
                if (currentBalance == null)
                {
                    currentBalance = Resources.Load<GameBalanceProfile>(SharedBalanceResourcePath);
                    if (currentBalance == null)
                    {
                        var profiles = Resources.LoadAll<GameBalanceProfile>(DefaultProfilesResourcePath);
                        if (profiles.Length > 0)
                        {
                            currentBalance = profiles[0];
                        }
                    }
                }

                return currentBalance;
            }
        }

        public static GameContentDatabase Content
        {
            get
            {
                if (currentContent == null)
                {
                    currentContent = Resources.Load<GameContentDatabase>(DefaultContentResourcePath);
                }

                return currentContent;
            }
        }

        public static GameBalanceProfile[] AllProfiles
        {
            get
            {
                var sharedProfile = Resources.Load<GameBalanceProfile>(SharedBalanceResourcePath);
                return sharedProfile != null
                    ? new[] { sharedProfile }
                    : Resources.LoadAll<GameBalanceProfile>(DefaultProfilesResourcePath);
            }
        }

        public static event Action RuntimeSettingsChanged;

        public static void SetRuntimeAssets(GameBalanceProfile balance, GameContentDatabase content)
        {
            if (balance != null)
            {
                currentBalance = balance;
            }

            if (content != null)
            {
                currentContent = content;
            }

            ApplyGlobalPhysics();
            RuntimeSettingsChanged?.Invoke();
        }

        public static bool TrySetBalanceProfileByName(string profileName)
        {
            var profiles = AllProfiles;
            for (var i = 0; i < profiles.Length; i++)
            {
                if (string.Equals(profiles[i].name, profileName, StringComparison.OrdinalIgnoreCase))
                {
                    currentBalance = profiles[i];
                    ApplyGlobalPhysics();
                    RuntimeSettingsChanged?.Invoke();
                    return true;
                }
            }

            return false;
        }

        public static void NotifySettingsChanged()
        {
            ApplyGlobalPhysics();
            RuntimeSettingsChanged?.Invoke();
        }

        public static void ApplyGlobalPhysics()
        {
            if (Balance != null)
            {
                UnityEngine.Physics.gravity = Balance.globalPhysics.worldGravity;
            }
        }
    }
}
