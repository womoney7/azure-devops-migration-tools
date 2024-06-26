﻿using System;
using System.Collections.Generic;

using MigrationTools.Enrichers;

namespace MigrationTools.Processors
{
    public class TfsAreaAndIterationProcessorOptions : ProcessorOptions, ITfsNodeStructureOptions
    {

        public string[] NodeBasePaths { get; set; }
        public Dictionary<string, string> AreaMaps { get; set; }
        public Dictionary<string, string> IterationMaps { get; set; }


        /// <summary>
        /// When set to True the susyem will try to create any missing missing area or iteration paths from the revisions.
        /// </summary>
        public bool ShouldCreateMissingRevisionPaths { get; set; }

        public bool ReplicateAllExistingNodes { get; set; }

        public override Type ToConfigure => typeof(TfsAreaAndIterationProcessor);

        public override IProcessorOptions GetDefault()
        {
            return this;
        }

        public override void SetDefaults()
        {
            SourceName = "sourceName";
            TargetName = "targetName";
            AreaMaps = new Dictionary<string, string>();
            IterationMaps = new Dictionary<string, string>();
        }
    }
}