using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.TeamFoundation.Build.WebApi;

namespace MigrationTools.DataContracts.Pipelines
{
    [ApiPath("build/builds/{0}/artifacts")]
    [ApiName("Builds Artifacts")]
    public class BuildArtifacts : RestApiDefinition
    {
        public ArtifactResource Resource { get; set; }

        public string Source { get; set; }

        public override void ResetObject()
        {

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
