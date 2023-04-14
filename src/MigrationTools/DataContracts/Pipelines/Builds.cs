using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace MigrationTools.DataContracts.Pipelines
{
    [ApiPath("build/builds")]
    [ApiName("Builds Queue")]
    public class Builds : RestApiDefinition
    {
        public AgentSpecification AgentSpecification { get; set; }

        public override string Name
        {
            get => this.BuildNumber;
            set => this.BuildNumber = value;
        }

        public string BuildNumber { get; set; }

        public int BuildNumberRevision { get; set; }

        //public BuildController Controller { get; set; }

        public BuildDefinition Definition { get; set; }

        public OrchestrationPlan OrchestrationPlan { get; set; }

        public string Parameters { get; set; }

        public List<OrchestrationPlan> Plans { get; set; }

        public string Priority { get; set; }

        public Project Project { get; set; }

        public PropertiesCollection Properties { get; set; }

        public string Quality { get; set; }

        public AgentPoolQueue Queue { get; set; }

        public int? QueuePosition { get; set; }

        public DateTime? QueueTime { get; set; }

        public string Reason { get; set; }

        public Repository Repository { get; set; }

        // Summary: Gets or sets the identity reference for the user who created the Service endpoint.
        [DataMember(EmitDefaultValue = false)]
        public IdentityRef RequestedBy { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public IdentityRef RequestedFor { get; set; }

        public bool RetainedByRelease { get; set; }

        public DateTime? StartTime { get; set; }

        public string Status { get; set; }

        public List<string> Tags { get; set; }

        public object TemplateParameters { get; set; }

        public object TriggerInfo { get; set; }

        public override bool HasTaskGroups()
        {
            return false;
        }

        public override bool HasVariableGroups()
        {
            return false;
        }

        public override void ResetObject()
        {
            this.Id = null;
            this.RequestedBy = null;
            this.RequestedFor = null;
            this.Queue = null;
            this.OrchestrationPlan = null;
            this.Plans = null;
        }
    }

    public class OrchestrationPlan
    {
        public int OrchestrationType { get; set; }

        public string PlanId { get; set; }
    }


}
