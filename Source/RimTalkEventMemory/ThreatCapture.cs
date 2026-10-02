using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimTalkEventPlus
{
    // A transaction-like buffer for one IncidentWorker.TryExecute invocation.
    // Raid letters can arrive before their Lords, while SightstealerSwarm does
    // the reverse, so routing happens only after a successful worker returns.
    internal sealed class ThreatCaptureContext
    {
        private readonly List<Lord> capturedLords = new List<Lord>();
        private readonly List<Letter> capturedLetters = new List<Letter>();

        public readonly IncidentWorker worker;
        public readonly IncidentParms parms;
        public readonly string incidentDefName;
        public readonly string incidentLabel;

        public List<Lord> CapturedLords => capturedLords;
        public List<Letter> CapturedLetters => capturedLetters;

        public ThreatCaptureContext(IncidentWorker worker, IncidentParms parms)
        {
            this.worker = worker;
            this.parms = parms;
            IncidentDef incidentDef = worker?.def;
            incidentDefName = incidentDef?.defName;
            incidentLabel = incidentDef?.label;
        }

        public void CaptureLord(Lord lord)
        {
            if (lord == null || capturedLords.Contains(lord))
                return;

            Map targetMap = parms?.target as Map;
            if (targetMap == null || lord.Map != targetMap)
                return;

            // Native raids have a resolved faction by this point. Do not let a
            // nested helper Lord be attributed to the raid.
            if (worker is IncidentWorker_Raid && parms?.faction != null && lord.faction != parms.faction)
                return;

            capturedLords.Add(lord);
        }

        public void CaptureLetter(Letter letter)
        {
            if (letter == null || letter.ID <= 0 ||
                !ThreatUtility.IsThreatLetter(letter) || capturedLetters.Contains(letter))
                return;

            capturedLetters.Add(letter);
        }
    }

    internal static class ThreatCaptureScope
    {
        [ThreadStatic]
        private static Stack<ThreatCaptureContext> captureStack;

        private static ThreatCaptureContext Current
        {
            get
            {
                return captureStack != null && captureStack.Count > 0 ? captureStack.Peek() : null;
            }
        }

        public static ThreatCaptureContext Push(IncidentWorker worker, IncidentParms parms)
        {
            if (captureStack == null)
                captureStack = new Stack<ThreatCaptureContext>();

            var context = new ThreatCaptureContext(worker, parms);
            captureStack.Push(context);
            return context;
        }

        public static void Commit(ThreatCaptureContext context)
        {
            if (context == null || context.CapturedLords.Count == 0)
                return;

            Verse.Current.Game?.GetComponent<ThreatTrackerComponent>()?.RegisterCapturedThreat(context);
        }

        public static void Pop(ThreatCaptureContext context)
        {
            if (context == null || captureStack == null || captureStack.Count == 0)
                return;

            if (ReferenceEquals(captureStack.Peek(), context))
            {
                captureStack.Pop();
                return;
            }

            // Avoid attributing later activity to a stale scope if another
            // patch broke the expected nested-finalizer ordering.
            captureStack.Clear();
        }

        // A true return means a scope existed even if it rejected this Lord;
        // LordMaker must not make an unscoped fallback record in that case.
        public static bool CaptureLord(Lord lord)
        {
            ThreatCaptureContext context = Current;
            if (context == null)
                return false;

            context.CaptureLord(lord);
            return true;
        }

        public static void CaptureReceivedLetter(Letter letter, int delayTicks)
        {
            ThreatCaptureContext context = Current;
            if (context == null || letter == null || delayTicks != 0 || Find.Archive == null)
                return;

            // Only immediate, accepted letters belong to the incident bundle.
            if (Find.Archive.Contains(letter))
                context.CaptureLetter(letter);
        }
    }

    // Shared incident wrapper: it captures a complete bundle before dispatch,
    // regardless of whether a worker emits letters or Lords first.
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
    internal static class IncidentWorker_TryExecute_ThreatCapturePatch
    {
        private static void Prefix(IncidentWorker __instance, IncidentParms parms, out ThreatCaptureContext __state)
        {
            __state = ThreatCaptureScope.Push(__instance, parms);
        }

        private static void Postfix(ThreatCaptureContext __state, bool __result)
        {
            if (__result)
                ThreatCaptureScope.Commit(__state);
        }

        private static Exception Finalizer(Exception __exception, ThreatCaptureContext __state)
        {
            ThreatCaptureScope.Pop(__state);
            return __exception;
        }
    }

    // Capture the concrete, archived Letter with its stable ID. Direct calls
    // to LetterStack are included, not only SendStandardLetter callers.
    [HarmonyPatch]
    internal static class LetterStack_ReceiveLetter_ThreatCapturePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(LetterStack),
                nameof(LetterStack.ReceiveLetter),
                new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) });
        }

        private static void Postfix(Letter let, int delayTicks)
        {
            ThreatCaptureScope.CaptureReceivedLetter(let, delayTicks);
        }
    }

    // LordMaker is the canonical post-initialization capture point for normal
    // vanilla and modded Lord creation.
    [HarmonyPatch(typeof(LordMaker), nameof(LordMaker.MakeNewLord))]
    internal static class LordMaker_MakeNewLord_ThreatCapturePatch
    {
        private static void Postfix(Lord __result)
        {
            if (!ThreatCaptureScope.CaptureLord(__result))
                Current.Game?.GetComponent<ThreatTrackerComponent>()?.RegisterUnscopedLord(__result);
        }
    }

    // Scoped-only support for incidents that construct a Lord directly. It is
    // unsafe to create a fallback record here because AddLord can run before
    // pawn ownership and LordJob initialization are complete.
    [HarmonyPatch(typeof(LordManager), nameof(LordManager.AddLord))]
    internal static class LordManager_AddLord_ThreatCapturePatch
    {
        private static void Postfix(Lord newLord)
        {
            ThreatCaptureScope.CaptureLord(newLord);
        }
    }

    [HarmonyPatch(typeof(LordManager), nameof(LordManager.RemoveLord))]
    internal static class LordManager_RemoveLord_ThreatLifecyclePatch
    {
        private static void Postfix(Lord oldLord)
        {
            Current.Game?.GetComponent<ThreatTrackerComponent>()?.NotifyLordRemoved(oldLord);
        }
    }
}
