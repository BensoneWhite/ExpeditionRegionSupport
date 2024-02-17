﻿using BepInEx;
using BepInEx.Logging;
using DependencyFlags = BepInEx.BepInDependency.DependencyFlags;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Expedition;
using ExpeditionRegionSupport.Interface;
using ExpeditionRegionSupport.Regions;
using ExpeditionRegionSupport.Regions.Restrictions;
using ExpeditionRegionSupport.Settings;
using Menu;
using MoreSlugcats;
using UnityEngine;
using ExpeditionRegionSupport.Challenges;

namespace ExpeditionRegionSupport
{
    [BepInDependency("slime-cubed.slugbase", DependencyFlags.SoftDependency)]
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "fluffball.expeditionregionsupport";
        public const string PLUGIN_NAME = "Expedition Region Support";
        public const string PLUGIN_VERSION = "0.9.4";

        public static new Logging.Logger Logger;

        public static bool SlugBaseEnabled;
        public static WorldState ActiveWorldState;
        public static List<string> RegionsVisited;

        private SimpleButton settingsButton;

        public void OnEnable()
        {
            Logger = new Logging.Logger(base.Logger);

            try
            {
                //User Interface
                On.Menu.ExpeditionMenu.ctor += ExpeditionMenu_ctor;
                On.Menu.ExpeditionMenu.Update += ExpeditionMenu_Update;
                On.Menu.ExpeditionMenu.UpdatePage += ExpeditionMenu_UpdatePage;

                FilterDialogHooks.ApplyHooks();

                //User Input
                On.Menu.ExpeditionMenu.Singal += ExpeditionMenu_Singal;

                //CharacterSelect
                On.Menu.CharacterSelectPage.UpdateSelectedSlugcat += CharacterSelectPage_UpdateSelectedSlugcat;

                //Random Spawn hooks
                On.Menu.ChallengeSelectPage.StartButton_OnPressDone += ChallengeSelectPage_StartButton_OnPressDone;
                IL.Menu.ChallengeSelectPage.StartButton_OnPressDone += ChallengeSelectPage_StartButton_OnPressDone;

                On.Expedition.ExpeditionGame.ExpeditionRandomStarts += ExpeditionGame_ExpeditionRandomStarts;
                IL.Expedition.ExpeditionGame.ExpeditionRandomStarts += ExpeditionGame_ExpeditionRandomStarts;

                IL.SaveState.setDenPosition += SaveState_setDenPosition;

                //Misc.
                On.RegionGate.customOEGateRequirements += RegionGate_customOEGateRequirements;
                On.RainWorld.PostModsInit += RainWorld_PostModsInit;

                //Allow communication with Log Manager
                Logging.Logger.ApplyHooks();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private void CharacterSelectPage_UpdateSelectedSlugcat(On.Menu.CharacterSelectPage.orig_UpdateSelectedSlugcat orig, CharacterSelectPage self, int slugcatIndex)
        {
            SlugcatStats.Name lastSelected = ExpeditionData.slugcatPlayer;
            orig(self, slugcatIndex);

            if (lastSelected != ExpeditionData.slugcatPlayer)
            {
                ActiveWorldState = RegionUtils.GetWorldStateFromStoryRegions(ExpeditionData.slugcatPlayer);

                if (ExpeditionSettings.Filters.VisitedRegionsOnly.Value)
                    UpdateRegionsVisited();
            }
        }

        private void ExpeditionMenu_UpdatePage(On.Menu.ExpeditionMenu.orig_UpdatePage orig, ExpeditionMenu self, int pageIndex)
        {
            orig(self, pageIndex);
            settingsButton.RemoveSprites();
            settingsButton.RemoveSubObject(settingsButton);
            settingsButton = createSettingsButton(self, self.pages[self.currentPage]);
            self.pages[self.currentPage].subObjects.Add(settingsButton);
        }

        /// <summary>
        /// A reference to the PlayerProgression outside of Expedition mode
        /// </summary>
        public static PlayerProgression CurrentProgression;

        /// <summary>
        /// This hook stores save data needed to validate region spawning, and sets a button for custom dialogue page.
        /// </summary>
        private void ExpeditionMenu_ctor(On.Menu.ExpeditionMenu.orig_ctor orig, ExpeditionMenu self, ProcessManager manager)
        {
            CurrentProgression = manager.rainWorld.progression; //This data is going to be overwritten in the constructor, but this mod still needs access to it.
            orig(self, manager);

            float y = (manager.rainWorld.options.ScreenSize.x != 1024f) ? 695f : 728f;

            settingsButton = createSettingsButton(self, self.pages[1]);// new SimpleButton(self, self.pages[1], self.Translate("SETTINGS"), "SETTINGS", new Vector2(self.rightAnchor - 150f, y - 40), new Vector2(100f, 30f));
            self.pages[1].subObjects.Add(settingsButton);
        }

        private SimpleButton createSettingsButton(ExpeditionMenu menu, Page page)
        {
            float y = (menu.manager.rainWorld.options.ScreenSize.x != 1024f) ? 695f : 728f;

            Vector2 settingsButtonOrigPos = new Vector2(menu.rightAnchor - 150f, y - 40);

            return new SimpleButton(menu, page, menu.Translate("SETTINGS"), "SETTINGS", settingsButtonOrigPos, new Vector2(100f, 30f));
        }

        private void ExpeditionMenu_Update(On.Menu.ExpeditionMenu.orig_Update orig, ExpeditionMenu self)
        {
            orig(self);

            if (self.pagesMoving)
            {
                float movementAdjustedX = self.rightAnchor - (self.leftAnchor + 150f);
                float buttonOffsetY = 40 + (self.manager.rainWorld.options.ScreenSize.x != 1024f ? 695f : 728f);

                settingsButton.pos = new Vector2(self.manualButton.pos.x, self.manualButton.pos.y - 40);// - self.manualButton.page.pos;
                settingsButton.lastPos = new Vector2(self.manualButton.lastPos.x, self.manualButton.lastPos.y - 40);// - self.manualButton.page.lastPos;
                //settingsButton.page.pos = new Vector2(self.manualButton.page.pos.x, self.manualButton.page.pos.y - 40);
                //settingsButton.page.lastPos = new Vector2(self.manualButton.page.lastPos.x, self.manualButton.page.lastPos.y - 40);

                //settingsButton.pos = new Vector2(movementAdjustedX, settingsButton.pos.y - buttonOffsetY) - settingsButton.page.pos;
                //settingsButton.lastPos = new Vector2(movementAdjustedX, settingsButton.lastPos.y - buttonOffsetY) - settingsButton.page.lastPos;
            }
        }

        private void ExpeditionMenu_Singal(On.Menu.ExpeditionMenu.orig_Singal orig, ExpeditionMenu self, MenuObject sender, string message)
        {
            if (message == "SETTINGS")
            {
                try
                {
                    ExpeditionSettingsDialog settingsDialog = new ExpeditionSettingsDialog(self.manager, self.challengeSelect);
                    self.PlaySound(SoundID.MENU_Player_Join_Game);
                    self.manager.ShowDialog(settingsDialog);

                    settingsDialog.OnDialogClosed += SettingsDialog_OnDialogClosed;
                }
                catch(Exception ex)
                {
                    Logger.LogError(ex);
                }
            }

            orig(self, sender, message);
        }

        public void SettingsDialog_OnDialogClosed(ExpeditionSettingsDialog sender)
        {
            ChallengeFilter.CurrentFilter = ChallengeFilterOptions.None;

            if (ExpeditionSettings.Filters.VisitedRegionsOnly.Value)
            {
                ChallengeFilter.CurrentFilter = ChallengeFilterOptions.VisitedRegions;
                UpdateRegionsVisited();
            }


            sender.OnDialogClosed -= SettingsDialog_OnDialogClosed;
        }

        public void UpdateRegionsVisited()
        {
            RegionsVisited = RegionUtils.GetVisitedRegions(ExpeditionData.slugcatPlayer);

            Logger.LogInfo(RegionsVisited.Count + " regions visited detected");
            foreach (string region in RegionsVisited)
                Logger.LogInfo(region);
        }

        private void ChallengeSelectPage_StartButton_OnPressDone(On.Menu.ChallengeSelectPage.orig_StartButton_OnPressDone orig, Menu.ChallengeSelectPage self, Menu.Remix.MixedUI.UIfocusable trigger)
        {
            ActiveWorldState = RegionUtils.GetWorldStateFromStoryRegions(ExpeditionData.slugcatPlayer);

            Logger.LogInfo("WS " + ActiveWorldState);

            orig(self, trigger);
        }

        private bool RegionGate_customOEGateRequirements(On.RegionGate.orig_customOEGateRequirements orig, RegionGate self)
        {
            if (ModManager.MSC && (/*self.room.world.name == "OE" ||*/ ModManager.Expedition && self.room.game.rainWorld.ExpeditionMode
                                                                    && (ActiveWorldState & (WorldState.Vanilla | WorldState.Gourmand)) != 0
                                                                    && ExpeditionGame.unlockedExpeditionSlugcats.Contains(MoreSlugcatsEnums.SlugcatStatsName.Gourmand)))
                return true;

            return orig(self);
        }

        private void SaveState_setDenPosition(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            cursor.GotoNext(MoveType.After, x => x.Match(OpCodes.Ret));
            cursor.GotoNext(MoveType.After, x => x.Match(OpCodes.Ret)); //Move to second return statement
            cursor.GotoNext(MoveType.After, //Move to just after the check for ExpeditionMode
                x => x.MatchLdfld<SaveState>(nameof(SaveState.progression)),
                x => x.MatchLdfld<PlayerProgression>(nameof(PlayerProgression.rainWorld)),
                x => x.Match(OpCodes.Callvirt),
                x => x.Match(OpCodes.Brfalse_S));
            cursor.EmitDelegate(checkForExpeditionDen); //Check for null startingDen

            int cursorIndex = cursor.Index;
            cursor.GotoNext(MoveType.Before, //Move to within an if (startingDen != null) check
                x => x.Match(OpCodes.Ldstr),
                x => x.MatchLdsfld(typeof(ExpeditionData).GetField("startingDen")),
                x => x.Match(OpCodes.Call),
                x => x.Match(OpCodes.Call));

            ILLabel branchTarget = il.DefineLabel();
            cursor.MarkLabel(branchTarget); //Set the branch target

            //In original code a local variable called text was assigned the new den position when it should have assigned to startingDen.
            //Fix that behavior with these emits.
            cursor.GotoNext(MoveType.After, x => x.MatchStfld<SaveState>(nameof(SaveState.lastVanillaDen)));
            cursor.Emit(OpCodes.Ldarg_0); //Load SaveState (this) onto the stack.
            cursor.Emit<SaveState>(OpCodes.Ldfld, nameof(SaveState.lastVanillaDen)); //Push newly assigned field onto stack
            cursor.Emit(OpCodes.Stsfld, typeof(ExpeditionData).GetField("startingDen")); //Update startingDen with new den information

            //Finish branch logic
            //In original code, a new den is always processed even if it isn't used.
            //Fix this by branching past assignment when startingDen already exists.
            cursor.Index = cursorIndex;
            cursor.Emit(OpCodes.Brtrue, branchTarget);
        }

        /// <summary>
        /// This checks whether or not the den position is null. 
        /// </summary>
        private bool checkForExpeditionDen()
        {
            return ExpeditionData.startingDen != null;
        }

        /// <summary>
        /// This hook changes Expedition.startingDen to null instead of generating a new starting den.
        /// </summary>
        private void ChallengeSelectPage_StartButton_OnPressDone(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            cursor.GotoNext(x => x.MatchCallvirt<PlayerProgression>(nameof(PlayerProgression.WipeSaveState))); //Go to save wipe logic
            cursor.GotoNext(x => x.Match(OpCodes.Ldstr)); //Go to a String.Empty check after save wipe logic. (A point before the Ldarg used by the method call)
            cursor.GotoNext(MoveType.Before, x => x.Match(OpCodes.Ldarg_0)); //This should be where data is pushed onto the stack for the method call.

            int cursorIndex = cursor.Index;
            cursor.GotoNext(MoveType.Before, x => x.Match(OpCodes.Stsfld)); //The field we want to modify value assignment with
            
            ILLabel jumpLabel = il.DefineLabel();
            cursor.MarkLabel(jumpLabel); //Label after the method call.
            cursor.Emit(OpCodes.Pop); //This removes pointer information off the stack. Hook will fail without this.
            cursor.Emit(OpCodes.Ldnull); //Push a null reference onto the stack instead.

            cursor.Index = cursorIndex;
            //cursor.GotoPrev(MoveType.Before, x => x.Match(OpCodes.Ldarg_0)); //Go to before method arguments are loaded onto the stack.
            cursor.Emit(OpCodes.Br_S, jumpLabel); //Jump over method arguments and method call.
        }

        private bool hasProcessedRooms;

        /// <summary>
        /// The number of attempts made to find a valid room spawn in Expedition mode.
        /// </summary>
        private short attemptsToFindDenSpawn = 0;

        /// <summary>
        /// The maximum number of times a room can be rerolled
        /// </summary>
        private const short max_attempts_allowed = 3;

        private string ExpeditionGame_ExpeditionRandomStarts(On.Expedition.ExpeditionGame.orig_ExpeditionRandomStarts orig, RainWorld rainWorld, SlugcatStats.Name activeMenuSlugcat)
        {
            if (RegionSelector.Instance == null)
            {
                hasProcessedRooms = false;
                RegionSelector.Instance = new RegionSelector(activeMenuSlugcat);
            }
            else if (RegionSelector.Instance.ActiveSlugcat != activeMenuSlugcat)
            {
                hasProcessedRooms = false;
                RegionSelector.Instance.ActiveSlugcat = activeMenuSlugcat;
            }

            if (!hasProcessedRooms)
            {
                orig(rainWorld, activeMenuSlugcat);

                RegionSelector.Instance.RemoveEmptyRegions();
                hasProcessedRooms = true;
            }

            Plugin.Logger.LogInfo("LOGGING");

            RegionSelector.Instance.RegionsAvailable.ForEach(r => Logger.LogInfo(r.RegionCode + " " + r.AvailableRooms.Count));

            string spawnLocation = RegionSelector.Instance.RandomRoom();

            if (!RegionUtils.RoomExists(spawnLocation))
            {
                Logger.LogWarning($"Room {spawnLocation} does not exist");

                attemptsToFindDenSpawn++; //Tracks all attempts, not just reattempts
                if (spawnLocation == string.Empty || attemptsToFindDenSpawn >= max_attempts_allowed) //These is no hope for finding a new room
                {
                    Logger.LogWarning("Using fallback");
                    return SaveState.GetFinalFallbackShelter(activeMenuSlugcat);
                }

                return ExpeditionGame.ExpeditionRandomStarts(rainWorld, activeMenuSlugcat);
            }

            attemptsToFindDenSpawn = 0;
            return spawnLocation;
        }

        private void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
        {
            Logger = new Logging.Logger("ErsLog", true); //Override BepInEx logger

            orig(self);

            ChallengeFilter.ApplyHooks(); //This needs to be handled in PostModsInIt or Expedition.ChallengeTools breaks
            SlugBaseEnabled = ModManager.ActiveMods.Exists(m => m.id == "slime-cubed.slugbase");
        }

        private void ExpeditionGame_ExpeditionRandomStarts(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            cursor.GotoNext(MoveType.After, x => x.Match(OpCodes.Br)); //Move to within for loop
            cursor.GotoNext(MoveType.After, x => x.Match(OpCodes.Ble)); //Move to just past the length check.

            int cursorIndex = cursor.Index;

            cursor.GotoNext(MoveType.Before, //Move to loop iteration logic
                x => x.MatchLdloc(10),
                x => x.MatchLdcI4(1),
                x => x.Match(OpCodes.Add));

            ILLabel loopReturnLabel = il.DefineLabel();
            cursor.MarkLabel(loopReturnLabel);
            cursor.Index = cursorIndex; //Go back and handle emits

            cursor.Emit(OpCodes.Ldloc, 4);
            cursor.Emit(OpCodes.Ldloc, 10);
            cursor.Emit(OpCodes.Ldelem_Ref); //Get value at current index in array

            cursor.EmitDelegate(handleRandomStarts); //Pass string stored at array[i] to hook
            cursor.Emit(OpCodes.Br_S, loopReturnLabel); //Shortcut loop

            cursor.GotoNext(MoveType.After, //Move to after loop ends
                x => x.Match(OpCodes.Ldlen),
                x => x.Match(OpCodes.Conv_I4),
                x => x.Match(OpCodes.Blt));

            cursor.Emit(OpCodes.Ldstr, string.Empty); //Return requires a string, but we don't need the return value anymore.
            cursor.Emit(OpCodes.Ret);
        }

        private void handleRandomStarts(string roomInfo)
        {
            Logger.LogDebug(roomInfo);
            RegionSelector.Instance.AddRoom(roomInfo);
        }

        public bool MineForGameComplete(SlugcatStats.Name name, RainWorld rainWorld)
        {
            if (!rainWorld.progression.IsThereASavedGame(name))
                return false;
            
            if (rainWorld.progression.currentSaveState != null && rainWorld.progression.currentSaveState.saveStateNumber == name)
                return rainWorld.progression.currentSaveState.deathPersistentSaveData.ascended || rainWorld.progression.currentSaveState.deathPersistentSaveData.altEnding;

            string[] progLinesFromMemory = rainWorld.progression.GetProgLinesFromMemory();
            if (progLinesFromMemory.Length == 0)
                return false;

            for (int i = 0; i < progLinesFromMemory.Length; i++)
            {
                string[] array = Regex.Split(progLinesFromMemory[i], "<progDivB>");
                if (array.Length == 2 && array[0] == "SAVE STATE" && array[1][21].ToString() == name.value)
                {
                    List<SaveStateMiner.Target> list = new List<SaveStateMiner.Target>();
                    list.Add(new SaveStateMiner.Target(">ASCENDED", null, "<dpA>", 20));
                    list.Add(new SaveStateMiner.Target(">ALTENDING", null, "<dpA>", 20));
                    List<SaveStateMiner.Result> list2 = SaveStateMiner.Mine(rainWorld, array[1], list);
                    bool flag = false;
                    bool flag2 = false;
                    for (int j = 0; j < list2.Count; j++)
                    {
                        string name_ = list2[j].name;
                        if (name_ == ">ASCENDED")
                        {
                            flag = true;
                        }
                        else if (name_ == ">ALTENDING")
                        {
                            flag2 = true;
                        }
                    }
                    return flag || flag2;
                }
            }
            return false;
        }

        private bool checkRegionRequirements(string text, SlugcatStats.Name slugcat, string[] storyRegions)
        {
            if (ExpeditionGame.lastRandomRegion == text) return false;

            bool checkFlag = false;

            if (storyRegions.Contains(text))
                checkFlag = true;
            else if (ModManager.MSC && (slugcat == SlugcatStats.Name.White || slugcat == SlugcatStats.Name.Yellow))
            {
                if (text == "OE" && ExpeditionGame.unlockedExpeditionSlugcats.Contains(MoreSlugcatsEnums.SlugcatStatsName.Gourmand))
                {
                    checkFlag = true;
                }
                else if (text == "LC" && ExpeditionGame.unlockedExpeditionSlugcats.Contains(MoreSlugcatsEnums.SlugcatStatsName.Artificer))
                {
                    checkFlag = true;
                }
                else if (text == "MS" && ExpeditionGame.unlockedExpeditionSlugcats.Contains(MoreSlugcatsEnums.SlugcatStatsName.Rivulet))
                {
                    checkFlag = true;
                }
            }
            else //Likely a custom region that has already been validated. Good to go!
                checkFlag = true;

            return checkFlag;
        }
    }
}