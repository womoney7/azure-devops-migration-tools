using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Client;
using Microsoft.VisualStudio.Services.Common;
using MigrationTools._EngineV1.Configuration;
using MigrationTools.Endpoints;

namespace MigrationTools._EngineV1.Clients
{
    public class TfsMigrationClient : IMigrationClient
    {
        private TfsTeamProjectCollection _collection;
        private VssCredentials _vssCredentials;
        private NetworkCredential _credentials;

        private readonly ITelemetryLogger _Telemetry;
        private readonly ILogger<TfsMigrationClient> _logger;

        public TfsTeamProjectConfig TfsConfig { get; private set; }
        public IMigrationClientConfig Config { get { return TfsConfig; } }
        public IWorkItemMigrationClient WorkItems { get; }
        public ITestPlanMigrationClient TestPlans { get;  }
        
        // if you add Migration Engine in here you will have to fix the infinate loop
        public TfsMigrationClient(ITestPlanMigrationClient testPlanClient, IWorkItemMigrationClient workItemClient, ITelemetryLogger telemetry, ILogger<TfsMigrationClient> logger)
        {
            TestPlans = testPlanClient;
            WorkItems = workItemClient;
            _Telemetry = telemetry;
            _logger = logger;
        }

        public void Configure(IMigrationClientConfig config, NetworkCredential credentials = null)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            if (!(config is TfsTeamProjectConfig))
            {
                throw new ArgumentOutOfRangeException(string.Format("{0} needs to be of type {1}", nameof(config), nameof(TfsTeamProjectConfig)));
            }

            TfsConfig = (TfsTeamProjectConfig)config;
            _credentials = credentials;
            EnsureCollection();
            WorkItems.Configure(this);
            TestPlans.Configure(this);
        }

        public object InternalCollection
        {
            get
            {
                return _collection;
            }
        }

        private void EnsureCollection()
        {
            if (_collection == null)
            {
                _Telemetry.TrackEvent("TeamProjectContext.EnsureCollection",
                    new Dictionary<string, string> {
                          { "Name", TfsConfig.Project},
                          { "Target Project", TfsConfig.Project},
                          { "Target Collection",TfsConfig.Collection.ToString() },
                           { "ReflectedWorkItemID Field Name",TfsConfig.ReflectedWorkItemIDFieldName }
                    }, null);
                _collection = GetDependantTfsCollection(_credentials);
            }
        }

        private TfsTeamProjectCollection GetDependantTfsCollection(NetworkCredential credentials)
        {
            var startTime = DateTime.UtcNow;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            TfsTeamProjectCollection y;
            try
            {
                _logger.LogInformation("TfsMigrationClient::GetDependantTfsCollection:AuthenticationMode({0})", TfsConfig.AuthenticationMode.ToString());
                switch (TfsConfig.AuthenticationMode)
                {
                    case AuthenticationMode.AccessToken:
                        _logger.LogInformation("TfsMigrationClient::GetDependantTfsCollection: Connecting with AccessToken ");
                        _vssCredentials = new VssBasicCredential(string.Empty, TfsConfig.PersonalAccessToken);
                        y = new TfsTeamProjectCollection(TfsConfig.Collection, _vssCredentials);
                        break;

                    case AuthenticationMode.Windows:
                        _logger.LogInformation("TfsMigrationClient::GetDependantTfsCollection: Connecting with NetworkCredential passes on CommandLine ");
                        if (credentials is null)
                        {
                            throw new InvalidOperationException("If AuthenticationMode = Windows then you must pass credentails on the command line.");
                        }
                        _vssCredentials = new VssCredentials(new Microsoft.VisualStudio.Services.Common.WindowsCredential(credentials));
                        y = new TfsTeamProjectCollection(TfsConfig.Collection, _vssCredentials);
                        break;

                    case AuthenticationMode.Prompt:
                        _logger.LogInformation("TfsMigrationClient::GetDependantTfsCollection: Prompting for credentials ");
                        y = new TfsTeamProjectCollection(TfsConfig.Collection);
                        break;

                    default:
                        _logger.LogInformation("TfsMigrationClient::GetDependantTfsCollection: Setting _vssCredentials to Null ");
                        y = new TfsTeamProjectCollection(TfsConfig.Collection);
                        break;
                }
                _logger.LogInformation("MigrationClient: Connecting to {CollectionUrl} ", TfsConfig.Collection);
                _logger.LogInformation("MigrationClient: validating security for {@AuthorizedIdentity} ", y.AuthorizedIdentity);
                y.EnsureAuthenticated();
                timer.Stop();
                _logger.LogInformation("MigrationClient: Access granted to {CollectionUrl} for {Name} ({Account})", TfsConfig.Collection, y.AuthorizedIdentity.DisplayName, y.AuthorizedIdentity.UniqueName);
                _Telemetry.TrackDependency(new DependencyTelemetry("TfsObjectModel", TfsConfig.Collection.ToString(), "GetWorkItem", null, startTime, timer.Elapsed, "200", true));
            }
            catch (Exception ex)
            {
                timer.Stop();
                _Telemetry.TrackDependency(new DependencyTelemetry("TfsObjectModel", TfsConfig.Collection.ToString(), "GetWorkItem", null, startTime, timer.Elapsed, "500", false));
                _Telemetry.TrackException(ex,
                       new Dictionary<string, string> {
                            { "CollectionUrl", TfsConfig.Collection.ToString() },
                            { "TeamProjectName",  TfsConfig.Project}
                       },
                       new Dictionary<string, double> {
                            { "Time",timer.ElapsedMilliseconds }
                       });
                _logger.LogError(ex, "Unable to configure store");
                throw;
            }
            return y;
        }

        public T GetService<T>()
        {
            EnsureCollection();
            return _collection.GetService<T>();
        }
    }
}