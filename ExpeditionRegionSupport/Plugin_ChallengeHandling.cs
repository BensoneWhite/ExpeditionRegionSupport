﻿using Expedition;
using ExpeditionRegionSupport.Filters;
using ExpeditionRegionSupport.Filters.Utils;
using ExpeditionRegionSupport.HookUtils;
using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpeditionRegionSupport
{
    public partial class Plugin
    {
        private void ChallengeSelectPage_Singal(On.Menu.ChallengeSelectPage.orig_Singal orig, ChallengeSelectPage self, MenuObject sender, string signalText)
        {
            try
            {
                //Log any signals that get triggers as their const name
                Logger.LogInfo(ExpeditionConsts.Signals.GetName(signalText).Replace('_', ' '));

                if (signalText.StartsWith(ExpeditionConsts.Signals.CHALLENGE_REPLACE))
                {
                    if (ExpeditionData.activeMission != string.Empty)
                    {
                        self.menu.PlaySound(SoundID.MENU_Error_Ping);
                        return;
                    }

                    int slotIndex; //Not zero-based
                    int.TryParse(signalText.Remove(0, 3), NumberStyles.Any, CultureInfo.InvariantCulture, out slotIndex);

                    Logger.LogInfo($"Slot {slotIndex} targeted");

                    if (ChallengeSlot.AbortedSlotCount > 0)
                    {
                        if (slotIndex - 1 >= ChallengeSlot.MaxSlotsAllowed)
                            throw new IndexOutOfRangeException("Tried to assign to an invalid slot index");

                        if (ChallengeSlot.IsAbortedSlot(slotIndex - 1))
                        {
                            int challengeCount = ChallengeSlot.SlotChallenges.Count; //Challenge count may change during assignment
                            int slotsToUnlock = slotIndex - challengeCount;

                            ChallengeAssignment.OnProcessStart(slotsToUnlock);
                            for (int slotOffset = 0; slotOffset < slotsToUnlock; slotOffset++)
                                ChallengeOrganizer.AssignChallenge(challengeCount + slotOffset, false); //Assign challenges to slots up to and including targeted slot
                            ChallengeAssignment.OnProcessFinish();
                            return;
                        }
                    }
                }

                orig(self, sender, signalText);
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogError(ex);

                if (ChallengeAssignment.AssignmentInProgress)
                {
                    ChallengeAssignment.Aborted = true;
                    ChallengeAssignment.OnProcessFinish();
                }

                self.menu.PlaySound(SoundID.MENU_Error_Ping);
            }
        }

        private void ChallengeSelectPage_Singal(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            //The order that these methods are called is important
            applyChallengeAssignmentIL(cursor, ExpeditionConsts.Signals.CHALLENGE_REPLACE, false);
            applyChallengeAssignmentIL(cursor, ExpeditionConsts.Signals.DESELECT_MISSION, true);
            applyChallengeAssignmentIL(cursor, ExpeditionConsts.Signals.CHALLENGE_RANDOM, true);
            applyChallengeAssignmentIL(cursor, ExpeditionConsts.Signals.ADD_SLOT, false);
            applyChallengeAssignmentIL(cursor, ExpeditionConsts.Signals.CHALLENGE_HIDDEN, false);
        }

        private static ILWrapper challengeWrapper;
        private static ILWrapper challengeWrapperLoop;

        private static void assignWrappers()
        {
            if (challengeWrapper != null) return; //Only need to create wrappers once

            //Wrap assignment process with start/finish handlers
            challengeWrapper = new ILWrapper(
            before =>
            {
                before.Emit(OpCodes.Ldc_I4_1); //Push expected challenge request amount onto stack
                before.EmitDelegate(ChallengeAssignment.OnProcessStart);
            },
            after => after.EmitDelegate(ChallengeAssignment.OnProcessFinish));

            bool handled = false;
            challengeWrapperLoop = new ILWrapper(
            before =>
            {
                before.Emit(OpCodes.Dup);
                before.EmitDelegate<Action<int>>((requestAmount) => //Pass loop index limiter into delegate
                {
                    if (handled) return; //This delegate is forced to be part of the loop. Only handle once

                    handled = true;
                    ChallengeAssignment.OnProcessStart(requestAmount);
                });
            },
            after =>
            {
                after.EmitDelegate(() =>
                {
                    handled = false; //Reset flag for the next loop
                    ChallengeAssignment.OnProcessFinish();
                });
            });
        }

        /// <summary>
        /// Challenge assignment is handled in multiple places with the same emit logic
        /// </summary>
        private static void applyChallengeAssignmentIL(ILCursor cursor, string signalText, bool isLoop)
        {
            assignWrappers();

            if (signalText != null)
                cursor.GotoNext(MoveType.After, x => x.MatchLdstr(signalText));

            if (isLoop) //Process for a loop is slightly more complicated versus a single request
            {
                if (signalText == ExpeditionConsts.Signals.CHALLENGE_RANDOM)
                {

                    cursor.GotoNext(MoveType.After, //Move to just after loop iteration begins
                        x => x.MatchStloc(4),
                        x => x.MatchBr(out _));
                    cursor.GotoNext(MoveType.After,
                        x => x.MatchCall(typeof(ExpeditionData).GetMethod("get_challengeList")),
                        x => x.MatchLdloc(4));

                    int restoreIndex = cursor.Index;
                    ILCursor locateCursor = new ILCursor(cursor);

                    locateCursor.GotoNext(MoveType.Before, x => x.MatchLdstr(ExpeditionConsts.Signals.REMOVE_SLOT));
                    Instruction nextSignalInst = locateCursor.Context.Instrs[locateCursor.Index];

                    if (cursor.TryGotoNext(MoveType.Before, //Check for problmatic instructions
                        x => x.Match(OpCodes.Callvirt),
                        x => x.MatchLdfld<Challenge>(nameof(Challenge.hidden))))
                    {
                        if (cursor.IsBefore(nextSignalInst)) //Ensure that our instruction match isn't a false positive
                        {
                            cursor.RemoveRange(2); //Remove conflicting IL - store returned value on stack using existing stloc
                        }
                        else
                        {
                            cursor.Index = restoreIndex; //Reset index, the conflicting IL instructions are not there anymore
                        }
                    }

                    //Account for a possible index out of range exception by checking hidden status through a delegate
                    cursor.EmitDelegate<Func<List<Challenge>, int, bool>>((challengeList, index) =>
                    {
                        bool hidden = false;
                        if (index < challengeList.Count)
                            hidden = challengeList[index].hidden;
                        return hidden;
                    });
                }

                //The cursor will be moved to just after the index limit for the loop
                cursor.GotoNext(MoveType.After, x => x.MatchCall(typeof(ChallengeOrganizer).GetMethod("AssignChallenge")));
                cursor.GotoNext(MoveType.After, x => x.MatchAdd()); //Get closer to loop iterator
                cursor.GotoNext(MoveType.Before, x => x.MatchBlt(out _));

                if (signalText == ExpeditionConsts.Signals.CHALLENGE_RANDOM)
                {
                    cursor.EmitDelegate<Func<int, int>>(challengesWanted => challengesWanted + ChallengeSlot.AbortedSlotCount);
                    //cursor.Emit(OpCodes.Ldsfld, typeof(ChallengeSlot).GetField("AbortedSlotCount"));
                    //cursor.Emit(OpCodes.Add); //Include aborted slots as part of the random selection
                }

                challengeWrapperLoop.Apply(cursor);
            }
            else
            {
                cursor.GotoNext(MoveType.Before, x => x.MatchCall(typeof(ChallengeOrganizer).GetMethod("AssignChallenge")));
                challengeWrapper.Apply(cursor);
            }
        }

        private void ChallengeSelectPage_ctor(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            applyChallengeAssignmentIL(cursor, null, false);
        }

        private void CharacterSelectPage_Update(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            applyChallengeAssignmentIL(cursor, null, true);
        }

        private void ChallengeSelectPage_Update(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            cursor.GotoNext(MoveType.After, x => x.MatchStloc(3)); //Loop index is defined
            cursor.GotoNext(MoveType.After, x => x.MatchBgt(out _)); //if statement checking index against challengeList count
            cursor.Emit(OpCodes.Ldloc_3); //Push loop index onto stack
            cursor.EmitDelegate<Func<int, bool>>(slotIndex => //Access Disabled flag from CWT. Color assignment needs to be avoided if true
            {
                if (slotIndex >= ChallengeSlot.SlotChallenges.Count)
                    return false;

                return ChallengeSlot.SlotChallenges[slotIndex].GetCWT().Disabled;
            });
            cursor.BranchTo(OpCodes.Brtrue, MoveType.After, //Bypasses rectColor set as it has already been handled
                x => x.Match(OpCodes.Newobj),
                x => x.Match(OpCodes.Newobj),
                x => x.Match(OpCodes.Stfld));
        }

        private void ChallengeSelectPage_UpdateChallengeButtons(On.Menu.ChallengeSelectPage.orig_UpdateChallengeButtons orig, ChallengeSelectPage self)
        {
            ChallengeSlot.SlotButtons = self.challengeButtons;
            orig(self);
        }

        private void ChallengeSelectPage_UpdateChallengeButtons(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            cursor.GotoNext(MoveType.After, //Move after logic that resets the greyed out status of a challenge button
                x => x.MatchLdfld<ChallengeSelectPage>(nameof(ChallengeSelectPage.challengeButtons)),
                x => x.MatchLdloc(1),
                x => x.MatchLdelemRef(),
                x => x.MatchLdfld<ButtonTemplate>(nameof(ButtonTemplate.buttonBehav)),
                x => x.MatchLdcI4(0),
                x => x.MatchStfld<ButtonBehavior>(nameof(ButtonBehavior.greyedOut)));

            //Get closer to the target instruction, just before UpdateDescription is called
            cursor.GotoNext(MoveType.After, x => x.MatchLdfld<Challenge>(nameof(Challenge.hidden)));
            cursor.GotoNext(MoveType.After,
                x => x.MatchStfld<BigSimpleButton>(nameof(BigSimpleButton.labelColor)),
                x => x.MatchCall(typeof(ExpeditionData).GetMethod("get_challengeList")),
                x => x.MatchLdloc(1));
            cursor.Emit(OpCodes.Dup);
            cursor.EmitDelegate(ChallengeSlot.UpdateSlotVisuals); //Send it to this method to apply extra slot processing logic
            
            //This is in the else branch, where we need to handle frozen slots
            cursor.GotoNext(MoveType.After, x => x.MatchLdstr("EMPTY"));
            cursor.Emit(OpCodes.Ldloc_1); //Push loop index onto stack
            cursor.EmitDelegate<Func<string, int, string>>((slotMessage, slotIndex) => //Send both into a delegate
            {
                ChallengeSlot.UpdateSlotVisuals(slotIndex); //Changes the appearance of disabled/frozen slots to distinguish them from open slots

                if (ChallengeSlot.IsAbortedSlot(slotIndex))
                    return "UNAVAILABLE";
                return slotMessage;
            });
        }
    }
}
