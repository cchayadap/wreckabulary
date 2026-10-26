using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundLauncher
    {
        readonly WordboundPadFocusGate failurePadGate = new WordboundPadFocusGate();
        WordboundInputBinding failureInput;
        int failureInputRevision;
        bool failureKeyboardArmed;
        string pendingFailureReturn;

        bool StandaloneStartupFailure => !destroying && !string.IsNullOrEmpty(failure) && !localClient &&
            !activeClient && activeLaunch == null && (!menu || menu.Coordinator == null);

        void Update()
        {
            if (ReadStartupFailureBack(Application.isFocused)) BackToLobby();
        }

        void QueueStartupFailureReturn()
        {
            if (StandaloneStartupFailure) pendingFailureReturn = failure;
        }

        bool ReadStartupFailureBack(bool focused)
        {
            if (!StandaloneStartupFailure || !focused || DesktopBinding.Typing || KeyBindings.GameplayBlocked)
            {
                SuspendFailureInput();
                return false;
            }
            bool clicked = pendingFailureReturn != null && pendingFailureReturn == failure;
            pendingFailureReturn = null;
            failureInput ??= new WordboundInputBinding();
            bool lost = failureInput.RefreshDevice();
            if (lost || failureInputRevision != failureInput.Revision)
            {
                failureInputRevision = failureInput.Revision;
                failureInput.SuspendGameplay(); failurePadGate.Suspend();
            }
            var escape = Keyboard.current?.escapeKey;
            bool back = escape != null && failureKeyboardArmed && escape.wasPressedThisFrame;
            if (escape == null || !escape.isPressed) failureKeyboardArmed = true;
            if (failurePadGate.CanRead(failureInput.ActiveGamepad, focused, false))
                back |= failureInput.ReadMenu(Time.unscaledTime).Back;
            if (!clicked && !back) return false;
            SuspendFailureInput();
            return true;
        }

        void SuspendFailureInput()
        {
            pendingFailureReturn = null;
            failureKeyboardArmed = false;
            failureInput?.SuspendGameplay();
            failurePadGate.Suspend();
        }

        void DisposeFailureInput()
        {
            SuspendFailureInput();
            failureInput?.Dispose(); failureInput = null;
        }
    }
}
