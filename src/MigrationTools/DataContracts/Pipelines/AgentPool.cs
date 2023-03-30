using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace MigrationTools.DataContracts.Pipelines
{
    [ApiPath("distributedtask/pools", false)]
    [ApiName("Agent Pools")]
    public class AgentPool : RestApiDefinition
    {
        public int? AgentCloudId { get; set; }

        public bool? AutoProvision { get; set; }

        public bool? AutoSize { get; set; }

        public bool? AutoUpdate { get; set; }

        /// <summary>
        /// Creator of the pool. The creator of the pool is automatically added into the administrators group for the pool on creation.
        /// </summary>
        public IdentityRef CreatedBy { get; set; }

        /// <summary>
        /// The date/time of the pool creation.
        /// </summary>
        public DateTime? CreatedOn { get; set; }

        //public new int? Id { get; set; }

        public bool? IsHosted { get; set; }

        public bool? IsLegacy { get; set; }

        public TaskAgentPoolOptions Options { get; set; }

        public IdentityRef Owner { get; set; }

        public TaskAgentPoolType PoolType { get; set; }

        public string Scope { get; set; }

        public int? Size { get; set; }


        public override void ResetObject()
        {
            this.Id = null;
            this.CreatedBy = null;
            this.Owner = null;
        }

        public override bool HasTaskGroups()
        {
            return false;
        }

        public override bool HasVariableGroups()
        {
            return false;
        }
    }
}
