using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using MigrationTools._EngineV1.Configuration;
using MigrationTools._EngineV1.DataContracts;
using MigrationTools.DataContracts;

namespace MigrationTools._EngineV1.Clients
{
    public class TfsWorkItemMigrationClient : WorkItemMigrationClientBase
    {
        private readonly IWorkItemQueryBuilderFactory _workItemQueryBuilderFactory;
        private readonly ILogger<TfsWorkItemMigrationClient> _logger;
        private WorkItemStoreFlags _bypassRules;

        private ProjectData _project;
        private WorkItemStore _wistore;

        public TfsWorkItemMigrationClient(IWorkItemQueryBuilderFactory workItemQueryBuilderFactory, ITelemetryLogger telemetry, ILogger<TfsWorkItemMigrationClient> logger)
            : base(telemetry)
        {
            _workItemQueryBuilderFactory = workItemQueryBuilderFactory;
            _logger = logger;
        }

        internal TfsTeamProjectConfig Config { get; private set; }
        
        public override ProjectData Project { get { return _project; } }
        public WorkItemStore Store { get { return _wistore; } }

        public override ReflectedWorkItemId CreateReflectedWorkItemId(WorkItemData workItem)
        {
            return new TfsReflectedWorkItemId(workItem);
        }

        public List<WorkItemData> FilterExistingWorkItems(
            List<WorkItemData> sourceWorkItems,
            TfsWiqlDefinition wiqlDefinition,
            TfsWorkItemMigrationClient sourceWorkItemMigrationClient)
        {
            _logger.LogDebug("FilterExistingWorkItems: START | ");

            var targetQuery =
                string.Format(
                    @"SELECT [System.Id], [{0}] FROM WorkItems WHERE [System.TeamProject] = @TeamProject {1} ORDER BY {2}",
                     Config.AsTeamProjectConfig().ReflectedWorkItemIDFieldName,
                    wiqlDefinition.QueryBit,
                    wiqlDefinition.OrderBit);

            _logger.LogDebug("FilterByTarget: Query Execute...");
            var targetFoundItems = GetWorkItems(targetQuery);
            _logger.LogDebug("FilterByTarget: ... query complete.");
            _logger.LogDebug("FilterByTarget: Found {TargetWorkItemCount} based on the WIQLQueryBit in the target system.", targetFoundItems.Count);
            var targetFoundIds = targetFoundItems.Select(twi => GetReflectedWorkItemId(twi)).Where(x => x != null).ToList();

            //////////////////////////////////////////////////////////
            sourceWorkItems = sourceWorkItems.Where(p => targetFoundIds.All(p2 => p2.ToString() != sourceWorkItemMigrationClient.CreateReflectedWorkItemId(p).ToString())).ToList();
            _logger.LogDebug("FilterByTarget: After removing all found work items there are {SourceWorkItemCount} remaining to be migrated.", sourceWorkItems.Count);
            _logger.LogDebug("FilterByTarget: END");
            return sourceWorkItems;
        }

        public override WorkItemData FindReflectedWorkItem(WorkItemData workItemToReflect, bool cache)
        {
            TfsReflectedWorkItemId reflectedWorkItemId = new TfsReflectedWorkItemId(workItemToReflect);
            return FindReflectedWorkItemByReflectedWorkItemId(reflectedWorkItemId, cache);
        }

        public override WorkItemData FindReflectedWorkItemByReflectedWorkItemId(WorkItemData workItemToReflect)
        {
            return FindReflectedWorkItemByReflectedWorkItemId(CreateReflectedWorkItemId(workItemToReflect));
        }

        public override WorkItemData FindReflectedWorkItemByReflectedWorkItemId(string refId)
        {
            var workItemQueryBuilder = _workItemQueryBuilderFactory.Create();
            StringBuilder queryBuilder = FindReflectedWorkItemQueryBase(workItemQueryBuilder);
            queryBuilder.AppendFormat("[{0}] = @idToFind", Config.ReflectedWorkItemIDFieldName);
            workItemQueryBuilder.AddParameter("idToFind", refId.ToString());
            workItemQueryBuilder.Query = queryBuilder.ToString();
            return FindWorkItemByQuery(workItemQueryBuilder);
        }

        public override ProjectData GetProject()
        {
            var startTime = DateTime.UtcNow;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var projectName = Config.Project;
                var y = (from Project x in Store.Projects where string.Equals(x.Name, projectName, StringComparison.OrdinalIgnoreCase) select x).Single(); // Use Single instead of SingleOrDefault to force an exception here
                timer.Stop();
                Telemetry.TrackDependency(new DependencyTelemetry("TfsObjectModel", Config.Collection.ToString(), "GetProject", null, startTime, timer.Elapsed, "200", true));
                return y.ToProjectData(); // With SingleOrDefault earlier this would result in a NullReferenceException which is hard to debug
            }
            catch (Exception ex)
            {
                timer.Stop();
                Telemetry.TrackDependency(new DependencyTelemetry("TfsObjectModel", Config.Collection.ToString(), "GetProject", null, startTime, timer.Elapsed, "500", false));
                Telemetry.TrackException(ex,
                       new Dictionary<string, string> {
                            { "CollectionUrl", Config.Collection.ToString() }
                       },
                       new Dictionary<string, double> {
                            { "Time",timer.ElapsedMilliseconds }
                       });
                _logger.LogError(ex, "Unable to get project with name {ConfiguredProjectName}", Config.Project);
                throw;
            }
        }

        public override ReflectedWorkItemId GetReflectedWorkItemId(WorkItemData workItem)
        {
            _logger.LogDebug("GetReflectedWorkItemId: START");
            var local = workItem.ToWorkItem();
            if (!local.Fields.Contains(Config.ReflectedWorkItemIDFieldName))
            {
                _logger.LogDebug("GetReflectedWorkItemId: END - no reflected work item id on work item");
                return null;
            }
            string rwiid = local.Fields[Config.ReflectedWorkItemIDFieldName].Value.ToString();
            if (!string.IsNullOrEmpty(rwiid))
            {
                _logger.LogDebug("GetReflectedWorkItemId: END - Has ReflectedWorkItemIdField and has value");
                return new TfsReflectedWorkItemId(rwiid);
            }
            _logger.LogDebug("GetReflectedWorkItemId: END - Has ReflectedWorkItemIdField but has no value");
            return null;
        }

        public override WorkItemData GetRevision(WorkItemData workItem, int revision)
        {
            throw new NotImplementedException("GetRevision in combination with WorkItemData is buggy");
        }

        public override WorkItemData GetWorkItem(string id)
        {
            return GetWorkItem(int.Parse(id));
        }

        public override WorkItemData GetWorkItem(int id)
        {
            var startTime = DateTime.UtcNow;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var y = Store.GetWorkItem(id);
                timer.Stop();
                Telemetry.TrackDependency(new DependencyTelemetry("TfsObjectModel", Config.Collection.ToString(), "GetWorkItem", null, startTime, timer.Elapsed, "200", true));
                return y?.AsWorkItemData();
            }
            catch (Exception ex)
            {
                timer.Stop();
                Telemetry.TrackDependency(new DependencyTelemetry("TfsObjectModel", Config.Collection.ToString(), "GetWorkItem", null, startTime, timer.Elapsed, "500", false));
                Telemetry.TrackException(ex,
                       new Dictionary<string, string> {
                            { "CollectionUrl", Config.Collection.ToString() }
                       },
                       new Dictionary<string, double> {
                            { "Time", timer.ElapsedMilliseconds }
                       });
                _logger.LogError(ex, "Unable to get workitem with {workitemId} from store", id);
                throw;
            }
        }

        public override List<WorkItemData> GetWorkItems()
        {
            throw new NotImplementedException();
        }

        public override List<WorkItemData> GetWorkItems(string WIQLQuery)
        {
            var wiqb = _workItemQueryBuilderFactory.Create();
            wiqb.Query = WIQLQuery;
            return GetWorkItems(wiqb);
        }

        public override List<WorkItemData> GetWorkItems(IWorkItemQueryBuilder queryBuilder)
        {
            queryBuilder.AddParameter("TeamProject", Config.Project);
            return queryBuilder.BuildWIQLQuery(this).GetWorkItems();
        }

        protected override void InnerConfigure(IMigrationClientConfig config, bool bypassRules = true)
        {
            Config = config.AsTeamProjectConfig();
            _bypassRules = bypassRules ? WorkItemStoreFlags.BypassRules : WorkItemStoreFlags.None;
            _wistore = GetWorkItemStore();
            _project = GetProject();
        }

        public override WorkItemData PersistWorkItem(WorkItemData workItem)
        {
            throw new NotImplementedException();
        }

        protected WorkItemData FindReflectedWorkItemByReflectedWorkItemId(ReflectedWorkItemId refId, bool cache = true)
        {
            var foundWorkItem = GetFromCache(refId);
            if (foundWorkItem is null)
            {
                var wiqb = _workItemQueryBuilderFactory.Create();
                wiqb.Query = string.Format(@"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject]=@TeamProject AND [{0}] = '@idToFind'", Config.ReflectedWorkItemIDFieldName);
                wiqb.AddParameter("idToFind", refId.ToString());
                wiqb.AddParameter("TeamProject", Config.Project);
                var items = wiqb.BuildWIQLQuery(this).GetWorkItems();

                foundWorkItem = items.FirstOrDefault(wi => wi.ToWorkItem().Fields[Config.ReflectedWorkItemIDFieldName].Value.ToString() == refId.ToString());
                if (cache && foundWorkItem != null)
                {
                    AddToCache(foundWorkItem);
                }
            }
            return foundWorkItem;
        }

        private StringBuilder FindReflectedWorkItemQueryBase(IWorkItemQueryBuilder query)
        {
            StringBuilder s = new StringBuilder();
            s.Append("SELECT [System.Id] FROM WorkItems");
            s.Append(" WHERE ");
            if (!Config.AllowCrossProjectLinking)
            {
                s.Append("[System.TeamProject]=@TeamProject AND ");
                query.AddParameter("TeamProject", Config.Project);
            }
            return s;
        }

        private WorkItemData FindWorkItemByQuery(IWorkItemQueryBuilder query)
        {
            List<WorkItemData> newFound;
            newFound = query.BuildWIQLQuery(this).GetWorkItems();
            if (newFound.Count == 0)
            {
                return null;
            }
            return newFound[0];
        }

        private WorkItemStore GetWorkItemStore()
        {
            var startTime = DateTime.UtcNow;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var store = new WorkItemStore((TfsTeamProjectCollection)MigrationClient.InternalCollection, _bypassRules);
                timer.Stop();
                Telemetry.TrackDependency(new DependencyTelemetry("TfsObjectModel", Config.Collection.ToString(), "GetWorkItemStore", null, startTime, timer.Elapsed, "200", true));
                return store;
            }
            catch (Exception ex)
            {
                timer.Stop();
                Telemetry.TrackDependency(new DependencyTelemetry("TfsObjectModel", Config.Collection.ToString(), "GetWorkItemStore", null, startTime, timer.Elapsed, "500", false));
                Telemetry.TrackException(ex,
                       new Dictionary<string, string> {
                            { "CollectionUrl", Config.Collection.ToString() }
                       },
                       new Dictionary<string, double> {
                            { "Time",timer.ElapsedMilliseconds }
                       });
                _logger.LogError(ex, "Unable to configure store");
                throw;
            }
        }
    }
}