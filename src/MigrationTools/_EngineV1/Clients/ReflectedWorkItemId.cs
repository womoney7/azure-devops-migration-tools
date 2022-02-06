using System;
using MigrationTools.DataContracts;

namespace MigrationTools._EngineV1.Clients
{
    public abstract class ReflectedWorkItemId
    {
        public ReflectedWorkItemId(WorkItemData workItem)
        {
            if (workItem is null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            WorkItemId = workItem.Id;
        }

        public ReflectedWorkItemId(string reflectedWorkItemId)
        {
            if (reflectedWorkItemId is null)
            {
                throw new ArgumentNullException(nameof(reflectedWorkItemId));
            }
            WorkItemId = reflectedWorkItemId;
        }

        public ReflectedWorkItemId(int reflectedWorkItemId)
        {
            if (reflectedWorkItemId == 0)
            {
                throw new ArgumentNullException(nameof(reflectedWorkItemId));
            }
            WorkItemId = reflectedWorkItemId.ToString();
        }

        public override string ToString()
        {
            return WorkItemId;
        }

        public string WorkItemId
        {
            get; private set;
        }
    }
}