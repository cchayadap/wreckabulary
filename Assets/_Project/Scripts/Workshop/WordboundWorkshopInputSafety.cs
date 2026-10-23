using System;
using UnityEngine;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        readonly WordboundPadFocusGate workshopPadGate = new WordboundPadFocusGate();
        bool workshopTyping;

        void SuspendWorkshopInput()
        {
            recoveryDecision = 0;
            workshopPadGate.Suspend();
            controllerTools = false;
            CancelGesture();
        }

        void CaptureWorkshopTextFocus() => workshopTyping =
            (GUI.GetNameOfFocusedControl() ?? "").StartsWith("workshop-text:", StringComparison.Ordinal);

        void OnApplicationFocus(bool focused) { if (!focused) SuspendWorkshopInput(); }
    }
}
