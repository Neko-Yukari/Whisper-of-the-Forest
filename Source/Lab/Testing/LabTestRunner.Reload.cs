using System;
using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Testing
{
    internal static partial class LabTestRunner
    {
        private static void ParkForReload()
        {
            state.Outcome.Phase = "reload";
            TestArena.Reserve(state.Arena.Rect);
            state.PendingReloads.Add(new PendingReload
            {
                Entry = state.Current,
                Instance = state.Instance,
                Outcome = state.Outcome,
                Context = state.Context,
                Arena = state.Arena
            });

            state.Current = null;
            state.Instance = null;
            state.Outcome = null;
            state.Context = null;
            state.Arena = null;
            state.Index++;
        }

        private static void BeginMergedReload()
        {
            string save = "wotf-l2-" + state.RunId;
            state.ReloadSaveName = save;
            try
            {
                GameDataSaveLoader.SaveGame(save);
            }
            catch (Exception exception)
            {
                FailPendingReloads("save failed: " + exception.Message);
                return;
            }

            state.ResumeIndex = state.Index;
            state.GameBeforeReload = Current.Game;
            state.UpdatesWaiting = 0;
            Log.Message("[WotF-Lab] merged reload of " + state.PendingReloads.Count + " test(s) reloading save " + save);
            GameDataSaveLoader.LoadGame(save);
            stage = Stage.AwaitReload;
        }

        /// <summary>Runs the second half of every test parked for the one merged reload.</summary>
        private static void RunMergedReloads()
        {
            PendingReload[] pending = state.PendingReloads.ToArray();
            state.PendingReloads.Clear();
            foreach (PendingReload reload in pending)
            {
                LoadPending(reload);
                CompleteReloadedTest();
            }

            CompleteMergedReload();
        }

        /// <summary>True while a reload test is still waiting for its first half to run.</summary>
        private static bool ReloadPending()
        {
            for (int i = state.Index; i < state.Pending.Count; i++)
            {
                if (state.Pending[i].Reload)
                {
                    return true;
                }
            }

            return false;
        }

        private static void LoadPending(PendingReload pending)
        {
            state.Current = pending.Entry;
            state.Instance = pending.Instance;
            state.Outcome = pending.Outcome;
            state.Context = pending.Context;
            state.Arena = pending.Arena;
            state.ArenaOrigin = pending.Arena.Origin;
            state.ArenaWidth = pending.Arena.Width;
            state.ArenaHeight = pending.Arena.Height;
            state.Index = state.Pending.Count;
        }

        private static void CompleteMergedReload()
        {
            state.PendingReloads.Clear();
            state.Index = state.ResumeIndex;
            stage = Stage.RunTest;
        }

        private static void FailPendingReloads(string message)
        {
            Log.Error("[WotF-Lab] merged reload failed: " + message);
            foreach (PendingReload pending in state.PendingReloads)
            {
                pending.Outcome.Status = "failed";
                pending.Outcome.Message = message;
                state.Outcomes.Add(pending.Outcome);
                pending.Arena?.Dispose();
            }

            state.PendingReloads.Clear();
            state.Index = state.ResumeIndex;
            DeleteReloadSave();
            stage = Stage.RunTest;
        }
    }
}

