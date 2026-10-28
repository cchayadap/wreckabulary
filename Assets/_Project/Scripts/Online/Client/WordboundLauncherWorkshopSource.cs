using System;
using UnityEngine;
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
        WordboundVisualResolver workshopVisuals;
        string workshopVisualItems;
        WordboundVisualProfile workshopVisualProfile;
        bool workshopWordboundArtwork;

        WordboundFlagshipContent ConfigureWorkshopSources(WordboundCourseAsset source)
        {
            var flagship = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(itemJson));
            workshop.ConfigureFlagship(flagship);
            BindAssignedWorkshopSource(source);
            workshop.SpawnAssetVisual ??= CreateWorkshopVisual;
            return flagship;
        }

        GameObject CreateWorkshopVisual(string id, Transform parent)
        {
            var inputs = InputsForWorkshop();
            bool wordbound = inputs.Visual && inputs.Visual.BaseArtwork == WordboundBaseArtwork.WordboundEquipment;
            if (workshopVisuals == null || workshopVisualItems != inputs.Items || workshopVisualProfile != inputs.Visual || workshopWordboundArtwork != wordbound)
            {
                workshopVisualItems = inputs.Items; workshopVisualProfile = inputs.Visual; workshopWordboundArtwork = wordbound;
                workshopVisuals = new WordboundVisualResolver(ItemCatalogue.FromJson(inputs.Items),
                    useWordboundCollection: wordbound,
                    profile: inputs.Visual);
            }
            if (!workshop.Catalogue.Assets.TryGetValue(id, out var asset)) return null;
            var size = new Vector3(asset.WidthMm, asset.HeightMm, asset.DepthMm) / 1000f;
            return workshopVisuals.Create(id, size, parent).Root;
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
