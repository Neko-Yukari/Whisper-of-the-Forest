using System;
using Verse;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// The only entry point a spell may use: hand over a <see cref="VfxRequest"/> and forget it. The
    /// service finds the Vfx Def, creates its worker and gives the worker to the request's map.
    ///
    /// A malformed request or a missing Def is a programming error, not a gameplay outcome: it throws
    /// instead of quietly skipping the effect, so the defect shows up in the first test rather than as
    /// a spell that "sometimes does not glow".
    /// </summary>
    public static class VfxService
    {
        public static VfxHandle Play(VfxRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.map == null)
            {
                throw new ArgumentException("a visual request needs a map", nameof(request));
            }

            if (request.vfxDefName.NullOrEmpty())
            {
                throw new ArgumentException("a visual request needs a vfxDefName", nameof(request));
            }

            VfxDef def = DefDatabase<VfxDef>.GetNamed(request.vfxDefName, false);
            if (def == null)
            {
                throw new InvalidOperationException("vfx def " + request.vfxDefName + " is not loaded");
            }

            if (def.lifetime == VfxLifetime.FollowThing && request.follow == null)
            {
                throw new ArgumentException(
                    "vfx def " + def.defName + " follows a thing, but the request has no follow", nameof(request));
            }

            VfxWorker worker = (VfxWorker)Activator.CreateInstance(def.workerClass);
            worker.Start(request, def);
            MapComponent_Vfx.For(request.map).Add(worker);
            return new VfxHandle(worker);
        }
    }

    /// <summary>Visual-layer logging: the shared leveled entry with the vfx module tag.</summary>
    public static class VfxDiagnostics
    {
        public static void Error(string message)
        {
            WotfLog.Error(WotfLog.ModuleVfx, message);
        }

        public static void Warning(string message)
        {
            WotfLog.Warning(WotfLog.ModuleVfx, message);
        }

        public static void Info(string message)
        {
            WotfLog.Info(WotfLog.ModuleVfx, message);
        }

        public static void Debug(string message)
        {
            WotfLog.Debug(WotfLog.ModuleVfx, message);
        }
    }
}
