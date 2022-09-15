using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using MigrationTools._EngineV1.Clients;
using MigrationTools.DataContracts;
using MigrationTools.Exceptions;
using MigrationTools.Processors;

namespace MigrationTools.Enrichers
{
    public class TfsWorkItemLinkEnricher : WorkItemProcessorEnricher
    {
        private bool _save = true;
        private bool _filterWorkItemsThatAlreadyExistInTarget = true;
        private IMigrationEngine Engine;

        public TfsWorkItemLinkEnricher(IServiceProvider services, ILogger<TfsWorkItemLinkEnricher> logger)
            : base(services, logger)
        {
            Engine = services.GetRequiredService<IMigrationEngine>();
        }

        [Obsolete]
        public override void Configure(
            bool save = true,
            bool filterWorkItemsThatAlreadyExistInTarget = true)
        {
            _save = save;
            _filterWorkItemsThatAlreadyExistInTarget = filterWorkItemsThatAlreadyExistInTarget;
        }

        [Obsolete]
        public override int Enrich(WorkItemData sourceWorkItemLinkStart, WorkItemData targetWorkItemLinkStart)
        {
            if (sourceWorkItemLinkStart is null)
            {
                throw new ArgumentNullException(nameof(sourceWorkItemLinkStart));
            }
            if (targetWorkItemLinkStart is null)
            {
                throw new ArgumentNullException(nameof(targetWorkItemLinkStart));
            }
            if (targetWorkItemLinkStart.Id == "0")
            {
                Log.LogWarning("TfsWorkItemLinkEnricher::Enrich: Target work item must be saved before you can add a link: exiting Link Migration");
                return 0;
            }

            if (ShouldCopyLinks(sourceWorkItemLinkStart, targetWorkItemLinkStart))
            {
                Log.LogDebug("Links = '{@sourceWorkItemLinkStartLinks}", sourceWorkItemLinkStart.Links);
                foreach (Link item in sourceWorkItemLinkStart.ToWorkItem().Links)
                {
                    try
                    {
                        Log.LogInformation("Migrating link for {sourceWorkItemLinkStartId} of type {ItemGetTypeName}", sourceWorkItemLinkStart.Id, item.GetType().Name);
                        switch (item)
                        {
                            case Hyperlink hyperlink:
                                CreateHyperlink(hyperlink, targetWorkItemLinkStart);
                                break;
                            case RelatedLink relatedLink:
                                CreateRelatedLink(sourceWorkItemLinkStart, relatedLink, targetWorkItemLinkStart);
                                break;
                            case ExternalLink externalLink when IsBuildLink(externalLink) == false:
                                CreateExternalLink(externalLink, targetWorkItemLinkStart);
                                break;
                            default:
                                UnknownLinkTypeException ex = new UnknownLinkTypeException(string.Format("  [UnknownLinkType] Unable to {0}", item.GetType().Name));
                                Log.LogError(ex, "LinkMigrationContext");
                                throw ex;
                        }
                    }
                    catch (WorkItemLinkValidationException ex)
                    {
                        sourceWorkItemLinkStart.ToWorkItem().Reset();
                        targetWorkItemLinkStart.ToWorkItem().Reset();
                        Log.LogError(ex, "[WorkItemLinkValidationException] Adding link for wiSourceL={sourceWorkItemLinkStartId}", sourceWorkItemLinkStart.Id);
                    }
                    catch (FormatException ex)
                    {
                        sourceWorkItemLinkStart.ToWorkItem().Reset();
                        targetWorkItemLinkStart.ToWorkItem().Reset();
                        Log.LogError(ex, "[CREATE-FAIL] Adding Link for wiSourceL={sourceWorkItemLinkStartId}", sourceWorkItemLinkStart.Id);
                    }
                    catch (UnexpectedErrorException ex)
                    {
                        sourceWorkItemLinkStart.ToWorkItem().Reset();
                        targetWorkItemLinkStart.ToWorkItem().Reset();
                        Log.LogError(ex, "[UnexpectedErrorException] Adding Link for wiSourceL={sourceWorkItemLinkStartId}", sourceWorkItemLinkStart.Id);
                    }
                }
            }
            if (sourceWorkItemLinkStart.Type == "Test Case")
            {
                MigrateSharedSteps(sourceWorkItemLinkStart, targetWorkItemLinkStart);
            }
            return 0;
        }

        private void MigrateSharedSteps(WorkItemData wiSourceL, WorkItemData wiTargetL)
        {
            const string microsoftVstsTcmSteps = "Microsoft.VSTS.TCM.Steps";
            var oldSteps = wiTargetL.ToWorkItem().Fields[microsoftVstsTcmSteps].Value.ToString();
            var newSteps = oldSteps;

            var sourceSharedStepLinks = wiSourceL.ToWorkItem().Links.OfType<RelatedLink>()
                .Where(x => x.LinkTypeEnd.Name == "Shared Steps").ToList();
            var sourceSharedSteps =
                sourceSharedStepLinks.Select(x => Engine.Source.WorkItems.GetWorkItem(x.RelatedWorkItemId.ToString()));

            foreach (WorkItemData sourceSharedStep in sourceSharedSteps)
            {
                WorkItemData matchingTargetSharedStep =
                    Engine.Target.WorkItems.FindReflectedWorkItemByReflectedWorkItemId(sourceSharedStep);

                if (matchingTargetSharedStep != null)
                {
                    newSteps = newSteps.Replace($"ref=\"{sourceSharedStep.Id}\"",
                        $"ref=\"{matchingTargetSharedStep.Id}\"");
                    wiTargetL.ToWorkItem().Fields[microsoftVstsTcmSteps].Value = newSteps;
                }
            }

            if (wiTargetL.ToWorkItem().IsDirty && _save)
            {
                wiTargetL.SaveToAzureDevOps();
            }
        }

        private void CreateExternalLink(ExternalLink sourceLink, WorkItemData target)
        {
            var exist = target.ToWorkItem().Links.OfType<ExternalLink>().SingleOrDefault(el => el.LinkedArtifactUri == sourceLink.LinkedArtifactUri);
            if (exist == null)
            {
                Log.LogInformation("Creating new {SourceLinkType} on {TargetId}", sourceLink.GetType().Name, target.Id);
                ExternalLink el = new ExternalLink(sourceLink.ArtifactLinkType, sourceLink.LinkedArtifactUri)
                {
                    Comment = sourceLink.Comment
                };
                target.ToWorkItem().Links.Add(el);
                if (_save)
                {
                    try
                    {
                        target.SaveToAzureDevOps();
                    }
                    catch (Exception ex)
                    {
                        // Ignore this link because the TFS server didn't recognize its type (There's no point in crashing the rest of the migration due to a link)
                        if (ex.Message.Contains("Unrecognized Resource link"))
                        {
                            Log.LogError(ex, "[{ExceptionType}] Failed to save link {SourceLinkType} on {TargetId}", ex.GetType().Name, sourceLink.GetType().Name, target.Id);
                            // Remove the link from the target so it doesn't cause problems downstream
                            target.ToWorkItem().Links.Remove(el);
                        }
                        else
                        {
                            //pass along the exception since we don't know what went wrong
                            throw;
                        }
                    }
                }
            }
            else
            {
                Log.LogInformation("Link {SourceLinkType} on {TargetId} already exists",
                                                  sourceLink.GetType().Name, target.Id);
            }
        }

        private bool IsBuildLink(ExternalLink link)
        {
            return link.LinkedArtifactUri != null &&
                   link.LinkedArtifactUri.StartsWith("vstfs:///Build/Build/", StringComparison.InvariantCultureIgnoreCase);
        }

        private void CreateRelatedLink(WorkItemData sourceLeft, RelatedLink item, WorkItemData targetLeft)
        {
            RelatedLink rl = item;
            //WorkItemData wiSourceR = null;
            //WorkItemData wiTargetR = null;
            int targetRightId = -1;
            Log.LogDebug("RelatedLink is of ArtifactLinkType='{ArtifactLinkType}':LinkTypeEnd='{LinkTypeEndImmutableName}' on WorkItemId s:{ids} t:{idt}", rl.ArtifactLinkType.Name, rl.LinkTypeEnd == null ? "null" : rl.LinkTypeEnd.ImmutableName, sourceLeft.Id, targetLeft.Id);

            if (rl.LinkTypeEnd != null) // On a registered link type these will for sure fail as target is not in the system.
            {
                try
                {
                    targetRightId = GetRightHandSideTargetWi(rl.RelatedWorkItemId, targetLeft);
                }
                catch (Exception ex)
                {
                    Log.LogError(ex, "  [FIND-FAIL] Adding Link of type {0} where wiSourceL={1}, wiTargetL={2} ", rl.LinkTypeEnd.ImmutableName, sourceLeft.Id, targetLeft.Id);
                    return;
                }
            }

            if (targetRightId == -1)
            {
                Log.LogWarning("[SKIP] [LINK_CAPTURE_RELATED] [{RegisteredLinkType}] target not found. wiSourceL={wiSourceL}, wiSourceR={wiSourceR}, wiTargetL={wiTargetL}", rl.ArtifactLinkType.GetType().Name, sourceLeft == null ? "null" : sourceLeft.Id, rl.RelatedWorkItemId.ToString(), targetLeft == null ? "null" : targetLeft.Id);
                return;
            }


            bool isExisting = false;
            try
            {
                var exist = targetLeft.ToWorkItem().Links.OfType<RelatedLink>()
                             .SingleOrDefault(l => l.RelatedWorkItemId == targetRightId && l.LinkTypeEnd.ImmutableName == item.LinkTypeEnd.ImmutableName);
                isExisting = exist != null;
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "  [SKIP] Unable to migrate links where wiSourceL={0}, wiSourceR={1}, wiTargetL={2}", ((sourceLeft != null) ? sourceLeft.Id.ToString() : "NotFound"), rl.RelatedWorkItemId.ToString(), (targetLeft != null) ? targetLeft.Id.ToString() : "NotFound");
                return;
            }

            if (isExisting)
            {
                Log.LogInformation("  [SKIP] Already Exists a Link of type {0} where wiSourceL={1}, wiSourceR={2}, wiTargetL={3}, wiTargetR={4} ", rl.LinkTypeEnd.ImmutableName, sourceLeft.Id, rl.RelatedWorkItemId.ToString(), targetLeft.Id, targetRightId);
                return;
            }

            //if (wiTargetR.ToWorkItem().IsAccessDenied)
            //{
            //    Log.LogInformation("  [AccessDenied] The Target  work item is inaccessable to create a Link of type {0} where wiSourceL={1}, wiSourceR={2}, wiTargetL={3}, wiTargetR={4} ", rl.LinkTypeEnd.ImmutableName, sourceLeft.Id, wiSourceR.Id, targetLeft.Id, wiTargetR.Id);
            //    return;
            //}

            if (targetRightId == -1) // cant this by freak chance be the same even though they are not the same item
            {
                Log.LogInformation(
                          "  [SKIP] Unable to migrate link where Link of type {0} where wiSourceL={1}, wiSourceR={2}, wiTargetL={3} as target WI has not been migrated",
                          rl.LinkTypeEnd.ImmutableName, sourceLeft.Id, rl.RelatedWorkItemId.ToString(), targetLeft.Id);
                return;
            }

            Log.LogInformation("  [CREATE-START] Adding Link of type {0} where wiSourceL={1}, wiSourceR={2}, wiTargetL={3}, wiTargetR={4} ", rl.LinkTypeEnd.ImmutableName, sourceLeft.Id, rl.RelatedWorkItemId.ToString(), targetLeft.Id, targetRightId);
            var client = (TfsWorkItemMigrationClient)Engine.Target.WorkItems;
            if (!client.Store.WorkItemLinkTypes.LinkTypeEnds.Contains(rl.LinkTypeEnd.ImmutableName))
            {
                Log.LogError($"  [SKIP] Unable to migrate Link because type {rl.LinkTypeEnd.ImmutableName} does not exist in the target project.");
                return;
            }

            var linkTypeEnd = client.Store.WorkItemLinkTypes.LinkTypeEnds[rl.LinkTypeEnd.ImmutableName];
            if (linkTypeEnd.ImmutableName == "System.LinkTypes.Hierarchy-Forward")
            {
                var targetRightWorkItem = Engine.Target.WorkItems.GetWorkItem(targetRightId);
                // TF201036: You cannot add a Child link between work items xxx and xxx because a work item can have only one Parent link.
                var potentialParentConflictLink = targetRightWorkItem.ToWorkItem().Links.OfType<RelatedLink>().SingleOrDefault(l => l.LinkTypeEnd.ImmutableName == "System.LinkTypes.Hierarchy-Reverse");
                if (potentialParentConflictLink != null)
                {
                    targetRightWorkItem.ToWorkItem().Links.Remove(potentialParentConflictLink);
                }
                linkTypeEnd = ((TfsWorkItemMigrationClient)Engine.Target.WorkItems).Store.WorkItemLinkTypes.LinkTypeEnds["System.LinkTypes.Hierarchy-Reverse"];
                var newLl = new RelatedLink(linkTypeEnd, int.Parse(targetLeft.Id));
                targetRightWorkItem.ToWorkItem().Links.Add(newLl);
                targetRightWorkItem.ToWorkItem().Fields["System.ChangedBy"].Value = "Migration";
                targetRightWorkItem.SaveToAzureDevOps();
            }
            else
            {
                if (linkTypeEnd.ImmutableName == "System.LinkTypes.Hierarchy-Reverse")
                {
                    // TF201065: You can not add a Parent link to this work item because a work item can have only one link of this type.
                    var potentialParentConflictLink = targetLeft.ToWorkItem().Links.OfType<RelatedLink>().SingleOrDefault(l => l.LinkTypeEnd.ImmutableName == "System.LinkTypes.Hierarchy-Reverse");
                    if (potentialParentConflictLink != null)
                    {
                        targetLeft.ToWorkItem().Links.Remove(potentialParentConflictLink);
                    }
                }

                var newRl = new RelatedLink(linkTypeEnd, targetRightId);
                targetLeft.ToWorkItem().Links.Add(newRl);
                if (_save)
                {
                    targetLeft.SaveToAzureDevOps();
                }
            }
            Log.LogInformation(
                    "  [CREATE-SUCCESS] Adding Link of type {0} where wiSourceL={1}, wiSourceR={2}, wiTargetL={3}, wiTargetR={4} ",
                    rl.LinkTypeEnd.ImmutableName, sourceLeft.Id, rl.RelatedWorkItemId.ToString(), targetLeft.Id, targetRightId);
        }

        private int GetRightHandSideTargetWi(int sourceRightId, WorkItemData wiTargetL)
        {
            int targetRightId;
            //WorkItemData wiTargetR;
            if (!(wiTargetL == null)
                && Engine.Source.Config.AsTeamProjectConfig().Project == wiTargetL.ToWorkItem().Project.Name
                && Engine.Source.Config.AsTeamProjectConfig().Collection.AbsoluteUri.Replace("/", "") == wiTargetL.ToWorkItem().Project.Store.TeamProjectCollection.Uri.ToString().Replace("/", ""))
            {
                // Moving to same team project as SourceR
                targetRightId = sourceRightId;
            }
            else
            {
                // Moving to Other Team Project from Source
                var reflectedWorkItemId = new TfsReflectedWorkItemId(sourceRightId, Engine.Source.Config.AsTeamProjectConfig().Project, Engine.Source.Config.AsTeamProjectConfig().Collection);
                targetRightId = Engine.Target.WorkItems.FindReflectedWorkItemId(reflectedWorkItemId);
                //if (targetRightId == -1) // Assume source only (other team project)
                //{
                //    targetRightId = sourceRightId;
                //    if (wiTargetR.ToWorkItem().Project.Store.TeamProjectCollection.Uri.ToString().Replace("/", "") != wiSourceR.ToWorkItem().Project.Store.TeamProjectCollection.Uri.ToString().Replace("/", ""))
                //    {
                //        targetRightId = -1; // Totally bogus break! as not same team collection
                //    }
                //}
            }
            return targetRightId;
        }

        private void CreateHyperlink(Hyperlink sourceLink, WorkItemData target)
        {
            var sourceLinkAbsoluteUri = GetAbsoluteUri(sourceLink);
            if (string.IsNullOrEmpty(sourceLinkAbsoluteUri))
            {
                Log.LogWarning("  [SKIP] Unable to create a hyperlink to [{0}]", sourceLink.Location);
                return;
            }

            var targetItem = target.ToWorkItem();

            var exist = targetItem.Links.OfType<Hyperlink>().Any(hl => string.Equals(sourceLinkAbsoluteUri, GetAbsoluteUri(hl), StringComparison.OrdinalIgnoreCase));

            if (exist)
            {
                return;
            }

            var hl = new Hyperlink(sourceLinkAbsoluteUri) // Use AbsoluteUri here as a possible \\UNC\Path\Link will be converted to file://UNC/Path/Link this way
            {
                Comment = sourceLink.Comment
            };

            targetItem.Links.Add(hl);
            if (_save)
            {
                target.SaveToAzureDevOps();
            }
        }

        private string GetAbsoluteUri(Hyperlink hyperlink)
        {
            try
            {
                return new Uri(hyperlink.Location.Trim('"')).AbsoluteUri;
            }
            catch (UriFormatException e)
            {
                Log.LogError("Unable to get AbsoluteUri of [{0}]: {1}", hyperlink.Location, e.Message);
                return null;
            }
        }

        private bool ShouldCopyLinks(WorkItemData sourceWorkItemLinkStart, WorkItemData targetWorkItemLinkStart)
        {
            if (_filterWorkItemsThatAlreadyExistInTarget)
            {
                if (targetWorkItemLinkStart.ToWorkItem().Links.Count == sourceWorkItemLinkStart.ToWorkItem().Links.Count) // we should never have this as the target should not have existed in this path
                {
                    Log.LogInformation("[SKIP] Source and Target have same number of links  {sourceWorkItemLinkStartId} - {sourceWorkItemLinkStartType}", sourceWorkItemLinkStart.Id, sourceWorkItemLinkStart.Type.ToString());
                    return false;
                }
            }
            return true;
        }

        [Obsolete("v2 Archtecture: use Configure(bool save = true, bool filter = true) instead", true)]
        public override void Configure(IProcessorEnricherOptions options)
        {
            throw new NotImplementedException();
        }

        protected override void RefreshForProcessorType(IProcessor processor)
        {
            throw new NotImplementedException();
        }

        protected override void EntryForProcessorType(IProcessor processor)
        {
            throw new NotImplementedException();
        }
    }
}