using MigrationTools.DataContracts.Pipelines;

namespace MigrationTools.DataContracts
{
    public class Mapping
    {
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public string Name { get; set; }
        public Version Version { get; set; }
    }
}