using System;

namespace WhisperOfTheForest.Vfx
{
    public static class VfxStates
    {
        public const string Windup = "Windup", Channel = "Channel", Release = "Release",
            Impact = "Impact", Sustain = "Sustain", Interrupt = "Interrupt", End = "End",
            Break = "Break", Expire = "Expire", Pulse = "Pulse";

        public static bool Contains(string name) => name == Windup || name == Channel || name == Release
            || name == Impact || name == Sustain || name == Interrupt || name == End
            || name == Break || name == Expire || name == Pulse;
    }

    public sealed class VfxHandle
    {
        private readonly VfxWorker worker;
        internal VfxHandle(VfxWorker worker) { this.worker = worker; }

        public void Trigger(string state)
        {
            if (Closed("Trigger(" + state + ")")) return;
            if (!VfxStates.Contains(state) || !worker.Def.SupportsState(state))
                throw new InvalidOperationException(worker.Def.defName + ": undeclared state '" + state + "'");
            worker.Trigger(state);
        }

        public void ReportShieldHit(in ShieldHitEvent hit)
        {
            if (!Closed("ReportShieldHit")) worker.OnShieldHit(hit);
        }

        public void ReportShieldPassThrough(in ShieldPassEvent pass)
        {
            if (!Closed("ReportShieldPassThrough")) worker.OnShieldPassThrough(pass);
        }

        public void End()
        {
            if (!Closed("End")) worker.FollowLost();
        }

        private bool Closed(string operation)
        {
            if (!worker.Finished) return false;
            VfxDiagnostics.Warning(worker.Def.defName + ": " + operation + " ignored: worker finished");
            return true;
        }
    }
}
