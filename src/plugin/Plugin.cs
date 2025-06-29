﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using MoreSlugcats;
using UnityEngine;
using static GateScanner.GateScannerObject;

namespace GateScanner
{
    [BepInPlugin("com.coder23848.gatescanner", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("lb-fgf-m4r-ik.chatoyant-waterfalls-but-real", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
#pragma warning disable IDE0051 // Visual Studio is whiny
        private void OnEnable()
#pragma warning restore IDE0051
        {
            // Plugin startup logic
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;

            On.RegionGate.ctor += RegionGate_ctor;
            On.RegionGate.Update += RegionGate_Update;
            On.ElectricGate.Update += ElectricGate_Update;
            On.GateKarmaGlyph.ctor += GateKarmaGlyph_ctor;
            On.GateKarmaGlyph.Update += GateKarmaGlyph_Update;
            On.GateKarmaGlyph.DrawSprites += GateKarmaGlyph_DrawSprites;
            On.GateKarmaGlyph.ShouldPlayCitizensIDAnimation += GateKarmaGlyph_ShouldPlayCitizensIDAnimation;

            On.SLOracleBehaviorHasMark.MoonConversation.AddEvents += MoonConversation_AddEvents;
            On.SLOracleBehaviorHasMark.MoonConversation.PearlIntro += MoonConversation_PearlIntro;
            On.SLOracleBehaviorHasMark.MoonConversation.PebblesPearl += MoonConversation_PebblesPearl;
            On.SLOracleBehaviorHasMark.MoonConversation.MiscPearl += MoonConversation_MiscPearl;

            On.PlayerGraphics.PlayerObjectLooker.HowInterestingIsThisObject += PlayerObjectLooker_HowInterestingIsThisObject;

            On.RainWorldGame.ctor += RainWorldGame_ctor;
        }

        // TODO: Bug causing Chasing Wind pearl intro line to repeat itself? Chasing Wind iterator pearl dialogue (after the in-person version is done), Chasing Wind Spearmaster pearl dialogue (only after the in-person version is done), Pearlcat Five Pebbles support, Pebbles Reads Pearls support, Hunter Expansion support, fix Hunter Expansion positioning bug (according to Steam comments), translation support?

        /// <summary>
        /// Whether or not the Pearlcat mod is active.
        /// </summary>
        public static bool PearlcatEnabled { get; set; }
        /// <summary>
        /// Whether or not the Chasing Wind mod is active.
        /// </summary>
        public static bool ChasingWindEnabled { get; set; }
        /// <summary>
        /// The speed at which the "progress bar" moves in the scanning animation.
        /// </summary>
        const int PROGRESS_BAR_TICK_TIME = 25;
        /// <summary>
        /// The speed of the scrolling animation, in ticks per frame.
        /// </summary>
        const int TICKS_PER_SCROLL_FRAME = 3;

        public static readonly ConditionalWeakTable<RainWorldGame, List<(DialogueType, EntityID)>> PreviousIteratorTable = new(); // The list contains every iterator-pearl combination that has been used that cycle.
        private void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
        {
            orig(self, manager);
            PreviousIteratorTable.Add(self, new());
        }

        // Gate handling
        static readonly ConditionalWeakTable<RegionGate, GateScannerObject> gateScannerTable = new();
        public bool GetGateScannerObject(RegionGate regionGate, out GateScannerObject scanner)
        {
            return gateScannerTable.TryGetValue(regionGate, out scanner);
        }
        public bool GetGateScannerSignData(GateKarmaGlyph self, out GateScannerSignData signData)
        {
            return gateScannerSignTable.TryGetValue(self, out signData);
        }

        public GateScannerSettings GetGateScannerSettingsFromFile(AbstractRoom room)
        {
            // default if file is not found
            GateScannerSettings settings = new();

            // find file
            string file = WorldLoader.FindRoomFile(room.name, includeRootDirectory: false, "_gatescannersettings.txt");
            if (File.Exists(file))
            {
                string[] lines = File.ReadAllLines(file);
                foreach (string line in lines)
                {
                    if (line == "NOSCANNER")
                    {
                        settings.NoScanner = true;
                    }
                    else if (line == "BROKENTRANSMITTER")
                    {
                        settings.BrokenTransmitter = true;
                    }
                }
            }

            return settings;
        }

        static bool initScanner = false;

        private void RegionGate_ctor(On.RegionGate.orig_ctor orig, RegionGate self, Room room)
        {
            GateScannerSettings scannerSettings = GetGateScannerSettingsFromFile(room.abstractRoom);

            initScanner = !scannerSettings.NoScanner;

            orig(self, room);

            initScanner = false;
            if (!scannerSettings.NoScanner)
            {
                gateScannerTable.Add(self, new(self, scannerSettings.BrokenTransmitter));
            }
        }
        private void RegionGate_Update(On.RegionGate.orig_Update orig, RegionGate self, bool eu)
        {
            if (!GetGateScannerObject(self, out GateScannerObject thisScanner))
            {
                orig(self, eu);
                return;
            }
            thisScanner.Update(eu);
            orig(self, eu);

            if (thisScanner.HeldPearl != null)
            {
                self.startCounter = 0; // Prevent the gate from activating while a pearl is being read
            }
        }
        private void ElectricGate_Update(On.ElectricGate.orig_Update orig, ElectricGate self, bool eu)
        {
            orig(self, eu);

            // Turn off gate lamps on scanning side
            if (GetGateScannerObject(self, out GateScannerObject thisScanner) && thisScanner.HeldPearl != null)
            {
                self.lampsOn[thisScanner.HeldPearlSide.Value ? 0 : 1] = false;
                self.lampsOn[thisScanner.HeldPearlSide.Value ? 3 : 2] = false;
            }
        }

        // Gate sign handling
        static readonly ConditionalWeakTable<GateKarmaGlyph, GateScannerSignData> gateScannerSignTable = new();
        private void GateKarmaGlyph_ctor(On.GateKarmaGlyph.orig_ctor orig, GateKarmaGlyph self, bool side, RegionGate gate, RegionGate.GateRequirement requirement)
        {
            orig(self, side, gate, requirement);

            if (initScanner)
            {
                gateScannerSignTable.Add(self, new());
            }
        }
        private void GateKarmaGlyph_Update(On.GateKarmaGlyph.orig_Update orig, GateKarmaGlyph self, bool eu)
        {
            //GateScannerObject thisScanner = gateScannerTable.GetValue(self.gate, x => throw new System.Exception("Gate " + self.room.abstractRoom.name + " does not have a scanner for some reason."));
            //GateScannerSignData thisData = gateScannerSignTable.GetValue(self, x => throw new System.Exception("Unable to load data for gate sign " + self.ToString()));

            orig(self, eu);

            if (!GetGateScannerObject(self.gate, out GateScannerObject thisScanner) || !GetGateScannerSignData(self, out GateScannerSignData thisData))
            {
                return;
            }

            // Check for scan stops and starts
            if (thisScanner.HeldPearl != null && thisScanner.HeldPearlSide == self.side && !thisData.InScanMode)
            {
                thisData.WantToSwitch = true;
            }
            if (thisScanner.HeldPearl == null && thisScanner.ErrorTimer == 0 && thisData.InScanMode)
            {
                thisData.WantToSwitch = true;
            }

            if (thisScanner.Step1Timer > 0)
            {
                thisData.Step1AnimationTimer++;
                if (thisData.Step1AnimationTimer % PROGRESS_BAR_TICK_TIME == 0 && thisData.Step1AnimationTimer / PROGRESS_BAR_TICK_TIME % 4 != 3 && thisData.Step2AnimationTimer == 0 && thisScanner.HeldPearl != null)
                {
                    self.gate.room.PlaySound(SoundID.SS_AI_Text, thisScanner.PearlHoldPos, 0.5f * GateScannerObject.BEEPVOLUME, 1f);
                }
            }
            else
            {
                thisData.Step1AnimationTimer = 0;
            }
            if (thisScanner.Step2Timer > 0)
            {
                thisData.Step2AnimationTimer++;
                if (thisData.Step1AnimationTimer % PROGRESS_BAR_TICK_TIME == 0 && thisData.Step2AnimationTimer / PROGRESS_BAR_TICK_TIME % 4 != 3 && thisScanner.Speaker == null && thisScanner.HeldPearl != null)
                {
                    self.gate.room.PlaySound(SoundID.SS_AI_Text, thisScanner.PearlHoldPos, 0.5f * GateScannerObject.BEEPVOLUME, 1f);
                }
            }
            else
            {
                thisData.Step2AnimationTimer = 0;
            }
            if (thisScanner.Speaker != null)
            {
                thisData.ScrollCounter++;
                thisData.ScrollCounter %= 9 * TICKS_PER_SCROLL_FRAME;
            }
            else
            {
                thisData.ScrollCounter = 0;
            }

            thisData.LastFadeMultipler = thisData.FadeMultiplier;
            thisData.LastIteratorColorIntensity = thisData.IteratorColorIntensity;
            thisData.LastErrorColorIntensity = thisData.ErrorColorIntensity;

            // Handle the fade-out animation needed to switch sprites
            if (thisData.WantToSwitch)
            {
                thisData.FadeMultiplier -= 0.05f; // Fade out
                if (thisData.FadeMultiplier < 0)
                {
                    thisData.FadeMultiplier = 0;
                    if (thisData.LastFadeMultipler == 0)
                    {
                        if (thisData.InScanMode)
                        {
                            thisData.InScanMode = false;
                            thisData.IteratorColorIntensity = 0;
                            thisData.ErrorColorIntensity = 0;
                            self.symbolDirty = true; // If this is true, the sprite gets reset at the next draw call
                        }
                        else
                        {
                            thisData.InScanMode = true;
                        }
                        thisData.WantToSwitch = false;
                    }
                }
            }
            else
            {
                thisData.FadeMultiplier += 0.05f; // Fade in
                if (thisData.FadeMultiplier > 1)
                {
                    thisData.FadeMultiplier = 1;
                }
            }

            // Fade to iterator color when iterator is talking
            if (thisScanner.Speaker != null)
            {
                thisData.IteratorColorIntensity += 0.05f;
                if (thisData.IteratorColorIntensity > 1)
                {
                    thisData.IteratorColorIntensity = 1;
                }
            }
            else
            {
                thisData.IteratorColorIntensity -= 0.05f;
                if (thisData.IteratorColorIntensity < 0)
                {
                    thisData.IteratorColorIntensity = 0;
                }
            }
            // Fade to red when an error appears
            if (thisScanner.ErrorTimer > 0)
            {
                thisData.ErrorColorIntensity = Mathf.Lerp(thisData.ErrorColorIntensity, 0.4f + 0.5f * Mathf.Sin((60 - thisScanner.ErrorTimer) / 12f), 0.2f); // This replicates the vanilla behavior for signs on locked gates.
            }
            else
            {
                thisData.ErrorColorIntensity -= 0.05f;
                if (thisData.ErrorColorIntensity < 0)
                {
                    thisData.ErrorColorIntensity = 0;
                }
            }

            // Reevaluate iterator color if possible
            if (thisScanner.ThisIterator != null && thisData.IteratorColorIntensity == 0 && thisData.LastIteratorColorIntensity == 0)
            {
                thisData.IteratorColor = thisScanner.IteratorColor();
            }

            // Manage Sofanthiel
            if (!self.controllingRobo) // I really hope this doesn't cause any problems with Sofanthiel's usual functionality.
            {
                if (thisScanner.HeldPearl != null && thisScanner.HeldPearlSide == self.side && thisScanner.Speaker == null)
                {
                    for (int i = 0; i < self.gate.room.game.Players.Count; i++)
                    {
                        if (self.gate.room.game.Players[i].realizedCreature != null && (self.gate.room.game.Players[i].realizedCreature as Player).myRobot != null)
                        {
                            (self.gate.room.game.Players[i].realizedCreature as Player).myRobot.lockTarget = new Vector2(self.pos.x, self.pos.y + 40f);
                            thisData.ScanControllingRobo = true;
                        }
                    }
                }
                else if (thisData.ScanControllingRobo)
                {
                    for (int j = 0; j < self.gate.room.game.Players.Count; j++)
                    {
                        if (self.gate.room.game.Players[j].realizedCreature != null && (self.gate.room.game.Players[j].realizedCreature as Player).myRobot != null)
                        {
                            (self.gate.room.game.Players[j].realizedCreature as Player).myRobot.lockTarget = null;
                            thisData.ScanControllingRobo = false;
                        }
                    }
                }
            }
            else
            {
                if (thisData.ScanControllingRobo)
                {
                    Debug.LogError("Sofanthiel control conflict between gate " + self.gate.room.abstractRoom.name + " and its scanner. Control deferred to gate.");
                    thisData.ScanControllingRobo = false;
                }
            }
        }
        private void GateKarmaGlyph_DrawSprites(On.GateKarmaGlyph.orig_DrawSprites orig, GateKarmaGlyph self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            //GateScannerObject thisScanner = gateScannerTable.GetValue(self.gate, x => throw new System.Exception("Gate " + self.room.abstractRoom.name + " does not have a scanner for some reason."));
            //GateScannerSignData thisData = gateScannerSignTable.GetValue(self, x => throw new System.Exception("Unable to load data for gate sign " + self.ToString()));

            orig(self, sLeaser, rCam, timeStacker, camPos);

            if (!GetGateScannerObject(self.gate, out GateScannerObject thisScanner) || !GetGateScannerSignData(self, out GateScannerSignData thisData))
            {
                return;
            }

            if (thisData.InScanMode)
            {
                if (thisScanner.ErrorTimer > 0)
                {
                    sLeaser.sprites[1].element = Futile.atlasManager.GetElementWithName("gatescanner/gateScanCircle-17");
                }
                else
                {
                    if (thisScanner.Step1Timer == 0)
                    {
                        sLeaser.sprites[1].element = Futile.atlasManager.GetElementWithName("gatescanner/gateScanCircle-1");
                    }
                    else
                    {
                        if (thisScanner.Step2Timer == 0)
                        {
                            int index = thisData.Step1AnimationTimer / PROGRESS_BAR_TICK_TIME % 4;
                            sLeaser.sprites[1].element = index switch
                            {
                                0 => Futile.atlasManager.GetElementWithName("gatescanner/gateScanCircle-2"),
                                1 => Futile.atlasManager.GetElementWithName("gatescanner/gateScanCircle-3"),
                                2 => Futile.atlasManager.GetElementWithName("gatescanner/gateScanCircle-4"),
                                3 => Futile.atlasManager.GetElementWithName("gatescanner/gateScanCircle-4"),
                                _ => throw new System.InvalidOperationException("Animation index modulo 4 is " + index + ". Something is very wrong here.")
                            };
                        }
                        else
                        {
                            if (thisScanner.Speaker == null)
                            {
                                int index = thisData.Step2AnimationTimer / PROGRESS_BAR_TICK_TIME % 4;
                                sLeaser.sprites[1].element = index switch
                                {
                                    0 => Futile.atlasManager.GetElementWithName("gatescanner/gateScanCircle-5"),
                                    1 => Futile.atlasManager.GetElementWithName("gatescanner/gateScanCircle-6"),
                                    2 => Futile.atlasManager.GetElementWithName("gatescanner/gateScanCircle-7"),
                                    3 => Futile.atlasManager.GetElementWithName("gatescanner/gateScanCircle-7"),
                                    _ => throw new System.InvalidOperationException("Animation index modulo 4 is " + index + ". Something is very wrong here.")
                                };
                            }
                            else
                            {
                                sLeaser.sprites[1].element = Futile.atlasManager.GetElementWithName("gatescanner/gateScanCircle-" + (thisData.ScrollCounter / TICKS_PER_SCROLL_FRAME % 9 + 8));
                            }
                        }
                    }
                }
                self.symbolDirty = false;
            }

            Color spriteColor = Color.Lerp(
                Color.Lerp(
                    self.myDefaultColor,
                    thisData.IteratorColor,
                    Mathf.Lerp(thisData.LastIteratorColorIntensity, thisData.IteratorColorIntensity, timeStacker)),
                new Color(1f, 0f, 0f),
                Mathf.Lerp(thisData.LastErrorColorIntensity, thisData.ErrorColorIntensity, timeStacker)
            ); // This is a wonderful line of code.
            for (int i = 0; i < 2; i++)
            {
                sLeaser.sprites[i].alpha *= Mathf.Lerp(thisData.LastFadeMultipler, thisData.FadeMultiplier, timeStacker);
                if (thisData.InScanMode)
                {
                    sLeaser.sprites[i].color = spriteColor;
                }
            }
            if (ModManager.MSC && self.requirement == MoreSlugcatsEnums.GateRequirement.RoboLock) // City gate
            {
                for (int i = 2; i < 11; i++) // City gates have a bunch of extra sprites
                {
                    sLeaser.sprites[i].alpha *= Mathf.Lerp(thisData.LastFadeMultipler, thisData.FadeMultiplier, timeStacker);
                    if (thisData.InScanMode)
                    {
                        sLeaser.sprites[i].color = spriteColor;
                        sLeaser.sprites[i].alpha = 0;
                    }
                }
            }
        }
        private int GateKarmaGlyph_ShouldPlayCitizensIDAnimation(On.GateKarmaGlyph.orig_ShouldPlayCitizensIDAnimation orig, GateKarmaGlyph self)
        {
            //GateScannerSignData thisData = gateScannerSignTable.GetValue(self, x => throw new System.Exception("Unable to load data for gate sign " + self.ToString()));

            return (GetGateScannerSignData(self, out GateScannerSignData thisData) && thisData.InScanMode) ? 0 : orig(self); // Disable City gate mechanisms while scanning
        }

        // Custom dialogue
        public static bool IsInGateConversationInit = false;
        public static GateScannerObject CurrentlyInitializingScanner = null;
        public static bool ConversationUsesPearlIntro = false;

        /// <summary>
        /// Requires Chasing Wind to function.
        /// </summary>
        private void CWPearlConversation_OnAddEvents(SLOracleBehaviorHasMark.MoonConversation self, ref bool runOriginalCode) // The function signature can't use a CWPearlConversation or it'll crash if CWStuff isn't installed.
        {
            ConversationUsesPearlIntro = false;

            if (IsInGateConversationInit && !CurrentlyInitializingScanner.ChasingWindHasDialogueForPearl(CurrentlyInitializingScanner.HeldPearl))
            {
                // Generic dialogue in the case that Chasing Wind doesn't have unique dialogue for a pearl
                Debug.Log("Chasing Wind doesn't have any dialogue for pearl " + CurrentlyInitializingScanner.HeldPearl.AbstractPearl.dataPearlType);
                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("I'm sorry, whatever data was on this pearl has been scrambled by a flaw in the Archive System's translation module. I am unable to see it."), 10));
                runOriginalCode = false;
            }
        }
        private void MoonConversation_AddEvents(On.SLOracleBehaviorHasMark.MoonConversation.orig_AddEvents orig, SLOracleBehaviorHasMark.MoonConversation self)
        {
            ConversationUsesPearlIntro = false;

            if (IsInGateConversationInit)
            {
                Debug.Assert(CurrentlyInitializingScanner != null);
                if (ModManager.MSC && CurrentlyInitializingScanner.Gate.room.game.GetStorySession.characterStats.name == MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel)
                {
                    self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello, gamer."), 10));
                    self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("I can't read this, as it isn't actually a pearl."), 10));
                    self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("It's a bomb encased in a diamond sphere."), 10));
                    self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Whoops!"), 10));

                    ConversationUsesPearlIntro = false;
                    return;
                }
                else if (ModManager.MSC && CurrentlyInitializingScanner.HeldPearl.AbstractPearl.dataPearlType == MoreSlugcatsEnums.DataPearlType.Spearmasterpearl) // Custom dialogue for Spearmaster's pearl, since it cannot technically be read normally.
                {
                    if (CurrentlyInitializingScanner.Gate.room.game.GetStorySession.saveState.miscWorldSaveData.smPearlTagged)
                    {
                        // Looks to the Moon reading Spearmaster's pearl, after she writes to it
                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello, little messenger!"), 10));
                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("I am glad to see that my message is safe."), 10));
                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("There is not much time left. Please hurry!"), 10));
                    }
                    else
                    {
                        // Looks to the Moon reading Spearmaster's pearl, before she writes to it
                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Strange..."), 10));
                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("The internal lattice of this pearl seems to have been partially replaced by organic matter."), 10));
                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("While it may still contain readable data, such an unusual format defeats the devices used in the Archive System."), 10));
                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("If you are determined to know what it says, you will have to bring it to my facility."), 10));
                    }
                    ConversationUsesPearlIntro = false;
                    return;
                }
            }

            orig(self);

            if (!IsInGateConversationInit && !self.myBehavior.oracle.room.game.GetStorySession.saveState.miscWorldSaveData.unrecognizedSaveStrings.Contains(GateScannerObject.GATESCANNER_USEDBEFORE_SAVE_STRING) && ConversationUsesPearlIntro) // A call to PearlIntro() is used to detect that the conversation is about a pearl. Yes, this is a messy implementation that probably misses some edge cases, but those probably don't matter anyways.
            {
                if (self.myBehavior.oracle.ID == Oracle.OracleID.SL)
                {
                    if (self.State.totalPearlsBrought + self.State.miscPearlCounter == 2 && self.myBehavior.oracle.room.game.GetStorySession.characterStats.name != SlugcatStats.Name.Red && !(ModManager.MSC && self.myBehavior.oracle.room.game.GetStorySession.characterStats.name == MoreSlugcatsEnums.SlugcatStatsName.Saint))
                    {
                        self.events.Add(new Conversation.TextEvent(self, 10, self.Translate("By the way, I can't imagine carrying these pearls to my structure is an easy task."), 10));
                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("If you ever find one that is too far away, try placing it inside one of the gates in the facility.<LINE>There are devices there that should allow me to access your pearls remotely."), 10));
                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Assuming they are still functioning, of course."), 10));
                    }
                }
                else if (self.myBehavior.oracle.ID == Oracle.OracleID.SS)
                {
                    if (self.State.totalPearlsBrought + self.State.miscPearlCounter >= 2)
                    {
                        self.events.Add(new Conversation.TextEvent(self, 10, self.Translate("If you are determined to collect these, there are better ways to do so than what you are doing now."), 10));
                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("In case you are not aware, all of the gates in this facility are equipped<LINE>with systems that allow me to remotely access pearls placed inside them."), 10));
                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Messages from those devices are much less of an interruption than your visits."), 10));
                    }
                }
            }

            ConversationUsesPearlIntro = false;
        }
        /// <summary>
        /// Requires Chasing Wind to function.
        /// </summary>
        private void CWOracleHooks_OnPearlIntro(SLOracleBehaviorHasMark.MoonConversation self, ref bool runOriginalCode)
        {
            ConversationUsesPearlIntro = true;

            if (IsInGateConversationInit && !CurrentlyInitializingScanner.ChasingWindContactedBefore && !self.myBehavior.isRepeatedDiscussion)
            {
                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello little one, this is Chasing Wind! I guess you would like to show me something?"), 10));
                runOriginalCode = false;
            }
        }
        private void MoonConversation_PearlIntro(On.SLOracleBehaviorHasMark.MoonConversation.orig_PearlIntro orig, SLOracleBehaviorHasMark.MoonConversation self)
        {
            ConversationUsesPearlIntro = true;

            if (IsInGateConversationInit)
            {
                if (self.myBehavior.isRepeatedDiscussion)
                {
                    self.events.Add(new Conversation.TextEvent(self, 0, self.myBehavior.AlreadyDiscussedItemString(true), 10));
                    return;
                }

                // Custom pearl introductions
                if (self.myBehavior.oracle.ID == Oracle.OracleID.SL)
                {
                    if (!CurrentlyInitializingScanner.VanillaIteratorContactedBefore)
                    {
                        if (CurrentlyInitializingScanner.Gate.room.game.GetStorySession.characterStats.name == SlugcatStats.Name.Red)
                        {
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello, <PLAYERNAME>! It's me, Looks to the Moon."), 10));
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("It appears that the Archive System is still mostly functional. I'm as surprised as you!"), 10));
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Would you like me to read this pearl?"), 10));
                        }
                        else
                        {
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello, <PLAYERNAME>! It's me, Looks to the Moon."), 10));
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Would you like me to read this pearl?"), 10));
                        }
                    }
                    else
                    {
                        switch (self.State.totalPearlsBrought + self.State.miscPearlCounter)
                        {
                            case 0: // I'm pretty sure this can't happen, since the first pearl ever read would also be the first pearl ever scanned. Whatever.
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello, <PLAYERNAME>!"), 10));
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Would you like me to read this pearl?"), 10));
                                break;
                            case 1:
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello again, <PLAYERNAME>! Would you like me to read this?"), 10));
                                break;
                            case 2:
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("You found another one, <PLAYERNAME>? I will read it to you."), 10));
                                break;
                            case 3:
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Another one, <PLAYERNAME>? You're no better than the scavengers!"), 10));
                                break;
                            default:
                                switch (Random.Range(0, 4))
                                {
                                    case 0:
                                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello again, <PLAYERNAME>! Would you like me to read this?"), 10));
                                        break;
                                    case 1:
                                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello again, <PLAYERNAME>! The scavengers must be jealous of you, finding all these..."), 10));
                                        break;
                                    case 2:
                                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Another one, <PLAYERNAME>? I will read it to you."), 10));
                                        break;
                                    default:
                                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello again! How do you find so many of these?"), 10));
                                        break;
                                }
                                break;
                        }
                    }
                }
                else if (self.myBehavior.oracle.ID == Oracle.OracleID.SS)
                {
                    switch (self.State.totalPearlsBrought + self.State.miscPearlCounter)
                    {
                        case 0:
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Oh, it's you. Have found me something to read?"), 10));
                            break;
                        case 1:
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("You again. Have found something new for me to read?"), 10));
                            break;
                        case 2:
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello again."), 10));
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("I am surprised you have found so many of these."), 10));
                            break;
                        case 3:
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello again."), 10));
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("I wonder, just how much time has passed since some of these were written."), 10));
                            break;
                        default:
                            switch (Random.Range(0, 4))
                            {
                                case 0:
                                    self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Another pearl? Let us see if there is anything important written on this."), 10));
                                    break;
                                case 1:
                                    self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello again. Have you found something new?"), 10));
                                    break;
                                case 2:
                                    self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("What have you found this time?"), 10));
                                    break;
                                default:
                                    self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello again. Let us see what you have found."), 10));
                                    break;
                            }
                            break;
                    }
                }
                else if (ModManager.MSC && self.myBehavior.oracle.ID == MoreSlugcatsEnums.OracleID.DM)
                {
                    if (!CurrentlyInitializingScanner.VanillaIteratorContactedBefore)
                    {
                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Oh, hello, <PLAYERNAME>! It's me, Looks to the Moon."), 10));
                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Would you like me to read this pearl?"), 10));
                    }
                    else
                    {
                        switch (self.State.totalPearlsBrought + self.State.miscPearlCounter)
                        {
                            case 0: // I'm pretty sure this can't happen, since the first pearl ever read would also be the first pearl ever scanned. Whatever.
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello, <PLAYERNAME>! Would you like me to read this?"), 10));
                                break;
                            case 1:
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello again, <PLAYERNAME>! Would you like me to read this?"), 10));
                                break;
                            case 2:
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("You found another one, <PLAYERNAME>? I will read it to you."), 10));
                                break;
                            case 3:
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Another one, <PLAYERNAME>? To be honest, I'm as curious to see it as you are."), 10));
                                break;
                            default:
                                switch (Random.Range(0, 4))
                                {
                                    case 0:
                                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello again, <PLAYERNAME>! Would you like me to read this?"), 10));
                                        break;
                                    case 1:
                                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello again, <PLAYERNAME>! What have you found this time?"), 10));
                                        break;
                                    case 2:
                                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Another one, <PLAYERNAME>? I will read it to you."), 10));
                                        break;
                                    default:
                                        self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello again! How do you find so many of these?"), 10));
                                        break;
                                }
                                break;
                        }
                    }
                }
                else if (self.myBehavior.oracle.ID.value == "CW")
                {
                    orig(self); // will be caught by CWOracleHooks_OnPearlIntro
                }
                else
                {
                    Debug.Log("Unrecognized iterator ID \"" + self.myBehavior.oracle.ID.value + "\". This shouldn't happen!");
                }
            }
            else
            {
                orig(self);
            }
        }
        private void MoonConversation_PebblesPearl(On.SLOracleBehaviorHasMark.MoonConversation.orig_PebblesPearl orig, SLOracleBehaviorHasMark.MoonConversation self)
        {
            if (IsInGateConversationInit) // Iterators' RAM pearls cannot be read by the iterator in vanilla, so custom dialogue is needed. Also, some pieces of dialogue in this case need to be changed anyways, since it references properties of the pearl that would not be apparent from a scan.
            {
                if (self.myBehavior.oracle.ID == Oracle.OracleID.SL)
                {
                    // Looks to the Moon reading Five Pebbles' pearl (post-collapse)
                    switch (Random.Range(0, 4))
                    {
                        case 0:
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello, <PLAYERNAME>! Would you like me to read this?"), 10));
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("This pearl is crystal clear - it was used just recently."), 10));
                            self.LoadEventsFromFile(40, true, CurrentlyInitializingScanner.HeldPearl.abstractPhysicalObject.ID.RandomSeed);
                            break;
                        case 1:
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello, <PLAYERNAME>! Would you like me to read this?"), 10));
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Strange... it seems to have been used not too long ago."), 10));
                            self.LoadEventsFromFile(40, true, CurrentlyInitializingScanner.HeldPearl.abstractPhysicalObject.ID.RandomSeed);
                            break;
                        case 2:
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("This pearl appears to have been written to while you were scanning it!"), 10));
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Unfortunately, that means the data I received is corrupted. You could try scanning it again, if you want."), 10));
                            break;
                        default:
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("This pearl is fresh! It was not long ago this data was written to it!"), 10));
                            self.LoadEventsFromFile(40, true, CurrentlyInitializingScanner.HeldPearl.abstractPhysicalObject.ID.RandomSeed);
                            break;
                    }
                }
                else if (self.myBehavior.oracle.ID == Oracle.OracleID.SS)
                {
                    // Five Pebbles reading Five Pebbles' pearl
                    switch (CurrentlyInitializingScanner.Gate.room.game.SeededRandomRange(CurrentlyInitializingScanner.HeldPearl.abstractPhysicalObject.ID.RandomSeed, 0, 6))
                    {
                        case 0:
                        case 1:
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("This is one of my pearls. Please give it back."), 10));
                            break;
                        case 2:
                        case 3:
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("You took this from my facility. Please return it."), 10));
                            break;
                        default:
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Are you doing this to taunt me?"), 10));
                            self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("You stole this from my facility. Return it at once."), 10));
                            break;
                    }
                }
                else if (ModManager.MSC && self.myBehavior.oracle.ID == MoreSlugcatsEnums.OracleID.DM)
                {
                    if (self.myBehavior is SLOracleBehaviorHasMark hasMark && hasMark.holdingObject is PebblesPearl thisPearl && (thisPearl.abstractPhysicalObject as PebblesPearl.AbstractPebblesPearl).number > 0)
                    {
                        // Looks to the Moon reading Five Pebbles' pearl (pre-collapse)
                        switch (Random.Range(0, 4))
                        {
                            case 0:
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello, <PLAYERNAME>! Would you like me to read this?"), 10));
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("This pearl is crystal clear - it was used just recently."), 10));
                                self.LoadEventsFromFile(168, true, CurrentlyInitializingScanner.HeldPearl.abstractPhysicalObject.ID.RandomSeed);
                                break;
                            case 1:
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Hello, <PLAYERNAME>! Would you like me to read this?"), 10));
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Strange... it seems to have been used not too long ago."), 10));
                                self.LoadEventsFromFile(168, true, CurrentlyInitializingScanner.HeldPearl.abstractPhysicalObject.ID.RandomSeed);
                                break;
                            case 2:
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("This pearl appears to have been written to while you were scanning it."), 10));
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Unfortunately, that means the data I received is corrupted. You could try scanning it again, if you want."), 10));
                                break;
                            default:
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("This pearl is fresh! It was not long ago this data was written to it!"), 10));
                                self.LoadEventsFromFile(168, true, CurrentlyInitializingScanner.HeldPearl.abstractPhysicalObject.ID.RandomSeed);
                                break;
                        }
                    }
                    else
                    {
                        // Looks to the Moon reading Looks to the Moon's pearl (pre-collapse)
                        switch (CurrentlyInitializingScanner.Gate.room.game.SeededRandomRange(CurrentlyInitializingScanner.HeldPearl.abstractPhysicalObject.ID.RandomSeed, 0, 2))
                        {
                            case 0:
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Oh! This is one of my pearls. Did you take it from my puppet chamber?"), 10));
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("It's a piece of active working memory - raw data that I will later move to my memory conflux."), 10));
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Please do not take pearls from my facility. That data is valuable to me!"), 10));
                                break;
                            default:
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("I recognize this pearl. Did you take it from my facility?"), 10));
                                self.events.Add(new Conversation.TextEvent(self, 0, self.Translate("Please do not do that. My memories and data are very important to me!"), 10));
                                break;
                        }
                    }
                }
            }
            else
            {
                orig(self);
            }
        }

        // Thanks to what appears to be a bug in Downpour, misc pearls read (via puppet) by Five Pebbles and pre-collapse Looks to the Moon do not respect the ID of the pearl. Broadcast pearls have this problem too, but that's harder to fix and harder to notice. This bug has not been fixed as of v1.9.15.
        private void MoonConversation_MiscPearl(On.SLOracleBehaviorHasMark.MoonConversation.orig_MiscPearl orig, SLOracleBehaviorHasMark.MoonConversation self, bool miscPearl2)
        {
            OracleBehavior actualMyBehavior = self.myBehavior;
            if (self.myBehavior is SSOracleBehavior ssob && ssob.inspectPearl != null)
            {
                self.myBehavior = (SLOracleBehaviorHasMark)System.Runtime.Serialization.FormatterServices.GetSafeUninitializedObject(typeof(SLOracleBehaviorHasMark));
                (self.myBehavior as SLOracleBehaviorHasMark).holdingObject = ssob.inspectPearl;
                (self.myBehavior as SLOracleBehaviorHasMark).oracle = ssob.oracle;
            }
            orig(self, miscPearl2);
            self.myBehavior = actualMyBehavior;
        }

        private float PlayerObjectLooker_HowInterestingIsThisObject(On.PlayerGraphics.PlayerObjectLooker.orig_HowInterestingIsThisObject orig, PlayerGraphics.PlayerObjectLooker self, PhysicalObject obj)
        {
            if (self.owner.player.room != null && self.owner.player.room.regionGate != null && GetGateScannerObject(self.owner.player.room.regionGate, out GateScannerObject thisScanner))
            {
                //GateScannerObject thisScanner = gateScannerTable.GetValue(self.owner.player.room.regionGate, x => throw new System.Exception("Gate " + self.owner.player.room.abstractRoom.name + " does not have a scanner for some reason."));
                if (obj == thisScanner.HeldPearl && thisScanner.Step1Timer > 0)
                {
                    float newValue = (thisScanner.Speaker == null) ? 20f : 500f;
                    // apply the same distance falloff that the function usually uses
                    newValue /= Mathf.Lerp(Mathf.Pow(Vector2.Distance(self.owner.player.mainBodyChunk.pos, obj.bodyChunks[0].pos), 1.5f), 1f, 0.995f);
                    return newValue;
                }
            }
            return orig(self, obj);
        }

        /// <summary>
        /// Loads the Pearlcat library in advance so the game doesn't freeze later. Requires Pearlcat to function.
        /// </summary>
        private void LoadPearlcat()
        {
            // This is a very strange solution to an equally strange bug. Even when pearlcat isn't installed, something in (presumably) the library loading process causes a half-second freeze the first time the runtime thinks the pearlcat library might get referenced. By "possibly" calling this function in OnModsInit, we ensure that this freeze happens when loading the game rather than mid-cycle.
            var pearlCatHooks = typeof(Pearlcat.Hooks);
        }
        /// <summary>
        /// Requires Chasing Wind to function.
        /// </summary>
        private void HookChasingWind()
        {
            // Chasing Wind has several events in its code, apparently just so that other modders can access it easily. Thanks M4rbleL1ne!
            CWStuff.CWOracleHooks.OnPearlIntro += CWOracleHooks_OnPearlIntro;
            CWStuff.CWPearlConversation.OnAddEvents += CWPearlConversation_OnAddEvents;
        }
        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            Debug.Log("Gate Scanner config setup: " + MachineConnector.SetRegisteredOI(PluginInfo.PLUGIN_GUID, PluginOptions.Instance));
            try
            {
                Futile.atlasManager.LoadAtlas("assets/gatescanner/gatescannergatesprites");
                Debug.Log("Gate Scanner assets loaded successfully!");
            }
            catch (System.Exception e)
            {
                Debug.Log("Error loading Gate Scanner assets!");
                throw e;
            }

            PearlcatEnabled = ModManager.ActiveMods.Any(x => x.id == "pearlcat");
            if (PearlcatEnabled)
            {
                LoadPearlcat();
                Debug.Log("Gate Scanner detected Pearlcat!");
            }
            ChasingWindEnabled = ModManager.ActiveMods.Any(x => x.id == "myr.chasing_wind");
            if (ChasingWindEnabled)
            {
                HookChasingWind();
                Debug.Log("Gate Scanner detected Chasing Wind!");
            }
        }
    }
}