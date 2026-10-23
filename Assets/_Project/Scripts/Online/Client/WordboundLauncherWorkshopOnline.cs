using System;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundLauncher
    {
        string authoredProfileBundleId;
        WordboundCameraProfile authoredOnlineCamera;
        WordboundVisualProfile authoredOnlineVisual;

        WordboundCameraProfile OnlineCameraFor(WordboundClientContent content) =>
            content?.WorkshopBundle?.Descriptor.BundleId == authoredProfileBundleId && authoredOnlineCamera
                ? authoredOnlineCamera : Assets.CameraProfile;

        WordboundVisualProfile OnlineVisualFor(WordboundClientContent content) =>
            content?.WorkshopBundle?.Descriptor.BundleId == authoredProfileBundleId && authoredOnlineVisual
                ? authoredOnlineVisual : Assets.VisualProfile;

        async void HostWorkshopOnline(WorkshopSnapshot draft, ResolvedCourse resolved, CoursePlayRequirements selected)
        {
            var sourceWorkshop = workshop;
            try
            {
                if (destroying || !sourceWorkshop || localClient || activeClient || activeLaunch != null ||
                    !menu || !menu.CanOpenAuthoredPrivate)
                    throw new InvalidOperationException("Return to the party screen, connect as a guest and leave the current party before hosting this map.");
                var inputs = InputsForWorkshop();
                var content = WordboundWorkshopOnlineContent.Create(draft, resolved, sourceWorkshop.Catalogue,
                    selected, inputs.Items, inputs.Rules);
                authoredProfileBundleId = content.WorkshopBundle.Descriptor.BundleId;
                authoredOnlineCamera = inputs.Camera;
                authoredOnlineVisual = inputs.Visual;
                await menu.OpenAuthoredPrivate(content, menu.SelectedRegion, menu.SelectedFill);
                if (destroying || sourceWorkshop != workshop || !sourceWorkshop) return;
                sourceWorkshop.Visible = false;
                menu.Visible = true;
            }
            catch (Exception error)
            {
                if (!destroying && sourceWorkshop && sourceWorkshop == workshop)
                    sourceWorkshop.ShowStatus("Online preparation could not finish: " + error.Message +
                        " Close the workshop to return to the party screen and retry or leave the pending party.");
            }
            finally { if (sourceWorkshop) sourceWorkshop.FinishOnlinePreparation(); }
        }
    }
}
