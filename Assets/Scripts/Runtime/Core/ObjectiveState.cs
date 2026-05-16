using System;
using System.Collections.Generic;

namespace GroceryQuotaHorror.Core
{
    [Serializable]
    public sealed class ObjectiveEntry
    {
        public int itemIndex;
        public string displayName;
        public int requiredCount;
        public int depositedCount;

        public bool Complete => depositedCount >= requiredCount;
    }

    [Serializable]
    public sealed class ObjectiveState
    {
        public List<ObjectiveEntry> entries = new();

        public bool IsComplete
        {
            get
            {
                if (entries.Count == 0)
                {
                    return false;
                }

                for (var i = 0; i < entries.Count; i++)
                {
                    if (!entries[i].Complete)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    public enum RunResult
    {
        InProgress,
        Success,
        FailedQuota,
        TeamWipe
    }
}

