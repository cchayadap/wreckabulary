using System;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundLauncher
    {
        sealed class WorkshopInputs
        {
            public string Items, Rules, GeometryHash;
            public WordboundCameraProfile Camera;
            public WordboundVisualProfile Visual;
        }

        WorkshopInputs assignedWorkshopInputs;

        WordboundFlagshipContent ConfigureWorkshopSources(WordboundCourseAsset source)
        {
            var flagship = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(itemJson));
            workshop.ConfigureFlagship(flagship);
            BindAssignedWorkshopSource(source);
            return flagship;
        }

        void BindAssignedWorkshopSource(WordboundCourseAsset source)
        {
            if (!source) { assignedWorkshopInputs = null; return; }
            if (!source.Items || !source.RuleBook)
                throw new ArgumentException("Assign the course's item catalogue and rule book before opening the workshop.");
            assignedWorkshopInputs = new WorkshopInputs {
                Items = source.Items.text, Rules = source.RuleBook.text, GeometryHash = source.Catalogue().Hash,
                Camera = source.CameraProfile, Visual = source.VisualProfile
            };
        }

        WorkshopInputs InputsForWorkshop()
        {
            if (!workshop) throw new InvalidOperationException("Open the workshop before choosing playtest inputs.");
            if (!workshop.UsesAssignedCourse)
                return new WorkshopInputs { Items = itemJson, Rules = ruleJson, Camera = Assets.CameraProfile, Visual = Assets.VisualProfile };
            if (assignedWorkshopInputs == null || assignedWorkshopInputs.GeometryHash != workshop.Catalogue.Hash)
                throw new InvalidOperationException("The assigned draft does not match its captured course inputs.");
            return new WorkshopInputs {
                Items = assignedWorkshopInputs.Items, Rules = assignedWorkshopInputs.Rules,
                GeometryHash = assignedWorkshopInputs.GeometryHash,
                Camera = assignedWorkshopInputs.Camera ? assignedWorkshopInputs.Camera : Assets.CameraProfile,
                Visual = assignedWorkshopInputs.Visual ? assignedWorkshopInputs.Visual : Assets.VisualProfile
            };
        }
    }
}
