using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace MigrationTools.DataContracts.Pipelines
{
    [ApiPath("projects", false)]
    [ApiName("Projects")]
    public class Projects : RestApiDefinition
    {
        // Summary: Project abbreviation.
        [DataMember(EmitDefaultValue = false)]
        public string Abbreviation
        {
            get;
            set;
        }

        //Summary: Url to default team identity image.
        [DataMember(EmitDefaultValue = false)]
        public string DefaultTeamImageUrl { get; set; }

        //Summary: The project's description (if any).
        [DataMember(EmitDefaultValue = false)]
        public string Description { get; set; }


        //Summary: Project last update time.
        [DataMember(EmitDefaultValue = false)]
        public string LastUpdateTime { get; set; }

        //Summary: Project revision.
        [DataMember(EmitDefaultValue = false)]
        public int Revision { get; set; }

        ////Summary: Project state.
        //[DataMember(EmitDefaultValue = false)]
        //public string State { get; set; }

        //Summary: Url to the full version of the object.
        [DataMember(EmitDefaultValue = false)]
        public string Url { get; set; }


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
