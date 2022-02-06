using System;
using System.Collections.Generic;
using MigrationTools.DataContracts;
using MigrationTools.Endpoints;

namespace MigrationTools._EngineV1.Clients
{
    public abstract class WorkItemQueryBase : IWorkItemQuery
    {
        public WorkItemQueryBase(ITelemetryLogger telemetry)
        {
            Telemetry = telemetry;
        }

        public string Query { get; private set; }
        protected Dictionary<string, string> Parameters { get; private set; }
        protected IWorkItemMigrationClient MigrationClient { get; private set; }
        protected ITelemetryLogger Telemetry { get; }

        public void Configure(IWorkItemMigrationClient workItemMigrationClient, string query, Dictionary<string, string> parameters)
        {
            MigrationClient = workItemMigrationClient ?? throw new ArgumentNullException(nameof(workItemMigrationClient));
            Query = query ?? throw new ArgumentNullException(nameof(query));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        public abstract List<WorkItemData> GetWorkItems();
    }
}