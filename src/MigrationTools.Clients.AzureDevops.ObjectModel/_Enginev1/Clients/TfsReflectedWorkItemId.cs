using System;
using System.Text.RegularExpressions;
using MigrationTools.DataContracts;
using Serilog;

namespace MigrationTools._EngineV1.Clients
{
    public class TfsReflectedWorkItemId : ReflectedWorkItemId
    {
        private Uri _connection;
        private string _projectName;
        private string _workItemId;
        private static readonly Regex ReflectedIdRegex = new Regex(@"^(?<org>[\S ]+)\/(?<project>[\S ]+)\/_workitems\/edit\/(?<id>\d+)", RegexOptions.Compiled);

        public TfsReflectedWorkItemId(WorkItemData workItem)
            : base(workItem)
        {
            if (workItem is null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            _workItemId = workItem.Id;
            _projectName = workItem.ProjectName;
            _connection = workItem.ToWorkItem().Store.TeamProjectCollection.Uri;
        }

        public TfsReflectedWorkItemId(int workItemId, string tfsProject, Uri tfsTeamProjectCollection)
            : base(workItemId)
        {
            if (workItemId == 0)
            {
                throw new ArgumentNullException(nameof(workItemId));
            }

            _workItemId = workItemId.ToString();
            _projectName = tfsProject;
            _connection = tfsTeamProjectCollection;
        }

        public TfsReflectedWorkItemId(string ReflectedWorkItemId)
            : base(ReflectedWorkItemId)
        {
            if (ReflectedWorkItemId is null)
            {
                throw new ArgumentNullException(nameof(ReflectedWorkItemId));
            }

            var match = ReflectedIdRegex.Match(ReflectedWorkItemId);
            if (match.Success)
            {
                Log.Verbose("TfsReflectedWorkItemId: Match Sucess from {ReflectedWorkItemId}: {@ReflectedWorkItemIdObject}", ReflectedWorkItemId, this);
                _connection = new Uri(match.Groups[1].Value);
                _projectName = match.Groups[2].Value;
                _workItemId = match.Groups[3].Value;
            }
            else
            {
                Log.Error("TfsReflectedWorkItemId: Unable to match ReflectedWorkItemId({ReflectedWorkItemId}) as id! ", ReflectedWorkItemId);
                throw new ArgumentException("Unable to Parse ReflectedWorkItemId. Check Log...");
            }
        }

        public override string ToString()
        {
            if (_connection is null)
            {
                throw new ArgumentNullException(nameof(_connection));
            }
            if (_projectName is null)
            {
                throw new ArgumentNullException(nameof(_projectName));
            }
            if (_workItemId is null)
            {
                throw new ArgumentNullException(nameof(_workItemId));
            }
            return string.Format("{0}/{1}/_workitems/edit/{2}", _connection.ToString().TrimEnd('/'), _projectName, _workItemId);
        }
    }
}