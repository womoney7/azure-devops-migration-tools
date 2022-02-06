using MigrationTools._EngineV1.Configuration;

namespace MigrationTools.Tests
{
    public class FakeMigrationClientConfig : IMigrationClientConfig
    {
        public string Project { get { return "FakeProject"; } }
        public IMigrationClientConfig PopulateWithDefault()
        {
            return this;
        }

        public override string ToString()
        {
            return "FakeMigration";
        }
    }
}