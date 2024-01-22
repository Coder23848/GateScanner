using System.Collections.Generic;
using System.Linq;
using MoreSlugcats;
using UnityEngine;

namespace GateScanner
{
    public class GateScannerObject
    {
        /// <summary>
        /// The gate this <see cref="GateScannerObject"/> belongs to.
        /// </summary>
        public RegionGate Gate { get; }
        /// <summary>
        /// The pearl that this gate is currently scanning. If <see cref="null"/>, the gate is not scanning a pearl.
        /// </summary>
        public DataPearl HeldPearl { get; set; }
        /// <summary>
        /// The side of the gate that the pearl is being scanned at. <see cref="false"/> is the left, side, <see cref="true"/> is the right side. If <see cref="null"/>, the gate is not scanning a pearl.
        /// </summary>
        public bool? HeldPearlSide { get; set; }
        /// <summary>
        /// The amount of time spent in step 1.
        /// </summary>
        public int Step1Timer { get; set; }
        /// <summary>
        /// The amount of time required for step 1.
        /// </summary>
        public int Step1TimeRequired { get; set; }
        /// <summary>
        /// The amount of time spent in step 2.
        /// </summary>
        public int Step2Timer { get; set; }
        /// <summary>
        /// The amount of time required for step 2.
        /// </summary>
        public int Step2TimeRequired { get; set; }
        /// <summary>
        /// The amount of time spent displaying an error message. Counts down, unlike the other timers.
        /// </summary>
        public int ErrorTimer { get; set; }
        /// <summary>
        /// The <see cref="SLOracleBehaviorHasMark.MoonConversation"/> that provides the pearl dialogue. Attatched to a skeleton of an <see cref="Oracle"/>. If <see cref="null"/>, no one is talking.
        /// </summary>
        public SLOracleBehaviorHasMark.MoonConversation Speaker { get; set; }
        /// <summary>
        /// A list of pearls that do not trigger the scanner.
        /// </summary>
        public List<DataPearl> AlreadyScanned { get; }
        /// <summary>
        /// The flag in the save file that determines the value of <see cref="AnyScannerUsedBefore"/>.
        /// </summary>
        public const string GATESCANNER_USEDBEFORE_SAVE_STRING = "23848.gatescanner.USEDGATESCANNERBEFORE";
        /// <summary>
        /// Whether or not the player has ever successfully scanned a pearl.
        /// </summary>
        public bool AnyScannerUsedBefore // I really hope this doesn't break anything!
        {
            get
            {
                return Gate.room.game.GetStorySession.saveState.miscWorldSaveData.unrecognizedSaveStrings.Contains(GATESCANNER_USEDBEFORE_SAVE_STRING);
            }
            set
            {
                if (value)
                {
                    if (!AnyScannerUsedBefore)
                    {
                        Gate.room.game.GetStorySession.saveState.miscWorldSaveData.unrecognizedSaveStrings.Add(GATESCANNER_USEDBEFORE_SAVE_STRING);
                    }
                }
                else
                {
                    if (AnyScannerUsedBefore)
                    {
                        Gate.room.game.GetStorySession.saveState.miscWorldSaveData.unrecognizedSaveStrings.Remove(GATESCANNER_USEDBEFORE_SAVE_STRING);
                    }
                }
            }
        }
        /// <summary>
        /// The height (in the room) that the pearl is held at.
        /// </summary>
        public float PearlHoldHeight => 260;
        /// <summary>
        /// The position of the <see cref="HeldPearl"/>.
        /// </summary>
        public Vector2 PearlHoldPos => new(Gate.room.PixelWidth / 2 + (HeldPearlSide.Value ? 90 : -90), PearlHoldHeight);
        /// <summary>
        /// If true, the water level in the room is too high for the scanner to be functional.
        /// </summary>
        public bool ScannerUnderWater
        {
            get
            {
                if (Gate.room.waterObject != null)
                {
                    if (Gate.room.waterInverted)
                    {
                        return Gate.room.waterObject.fWaterLevel - 10 < PearlHoldHeight;
                    }
                    else
                    {
                        return Gate.room.waterObject.fWaterLevel + 10 > PearlHoldHeight;
                    }
                }
                else
                {
                    return false;
                }
            }
        }
        /// <summary>
        /// A multiplier for the volume of the scanner sound effects.
        /// </summary>
        public const float BEEPVOLUME = 1.5f;

        public GateScannerObject(RegionGate gate)
        {
            Gate = gate;
            AlreadyScanned = new();
            Step1Timer = 0;
            Step1TimeRequired = 0;
            Step2Timer = 0;
            Step2TimeRequired = 0;
            ErrorTimer = 0;
            Speaker = null;
        }

        public List<DataPearl> PearlsInGateRange()
        {
            List<DataPearl> ret = new();
            foreach (List<PhysicalObject> list in Gate.room.physicalObjects)
            {
                foreach (PhysicalObject obj in list)
                {
                    if (obj is DataPearl pearl)
                    {
                        Vector2 position = pearl.firstChunk.pos;
                        // This was copied from the code for detecting players in the gate. I can't be bothered to simplify it, and there is the possibility that the separate cases will become important later.
                        if (position.x < Gate.room.PixelWidth / 2 - 160)
                        {
                            continue; // Left of gate
                        }
                        else if (position.x < Gate.room.PixelWidth / 2)
                        {
                            ret.Add(pearl); // Left side
                        }
                        else if (position.x < Gate.room.PixelWidth / 2 + 160)
                        {
                            ret.Add(pearl); // Right side
                        }
                        else
                        {
                            continue; // Right of gate
                        }
                    }
                }
            }
            return ret;
        }

        public T GetUninit<T>()
        {
            return (T)System.Runtime.Serialization.FormatterServices.GetSafeUninitializedObject(typeof(T)); // This creates an object without calling its constructor.
        }
        /// <summary>
        /// Starts a pearl dialogue for the current <see cref="HeldPearl"/>.
        /// </summary>
        public void StartConversation()
        {
            if (Gate.room.game.cameras[0].hud.dialogBox == null)
            {
                Gate.room.game.cameras[0].hud.InitDialogBox();
            }

            // A lot of this code is an amalgamation of the vanilla pearl reading functions.

            SLOrcacleState slOracleState = Gate.room.game.GetStorySession.saveState.miscWorldSaveData.SLOracleState;

            Conversation.ID id = Conversation.DataPearlToConversation(HeldPearl.AbstractPearl.dataPearlType);
            if (id == Conversation.ID.None)
            {
                if (HeldPearl.AbstractPearl.dataPearlType == DataPearl.AbstractDataPearl.DataPearlType.Misc || HeldPearl.AbstractPearl.dataPearlType.index == -1)
                {
                    id = Conversation.ID.Moon_Pearl_Misc;
                }
                if (HeldPearl.AbstractPearl.dataPearlType == DataPearl.AbstractDataPearl.DataPearlType.Misc2)
                {
                    id = Conversation.ID.Moon_Pearl_Misc2;
                }
                if (HeldPearl.AbstractPearl.dataPearlType == DataPearl.AbstractDataPearl.DataPearlType.PebblesPearl)
                {
                    id = Conversation.ID.Moon_Pebbles_Pearl;
                }
                if (ModManager.MSC && HeldPearl.AbstractPearl.dataPearlType == MoreSlugcatsEnums.DataPearlType.BroadcastMisc)
                {
                    id = MoreSlugcatsEnums.ConversationID.Moon_Pearl_BroadcastMisc;
                }
            }

            bool important =
                HeldPearl.AbstractPearl.dataPearlType != DataPearl.AbstractDataPearl.DataPearlType.Misc &&
                HeldPearl.AbstractPearl.dataPearlType.Index != -1 &&
                HeldPearl.AbstractPearl.dataPearlType != DataPearl.AbstractDataPearl.DataPearlType.Misc2 &&
                HeldPearl.AbstractPearl.dataPearlType != DataPearl.AbstractDataPearl.DataPearlType.PebblesPearl &&
                !(ModManager.MSC && HeldPearl.AbstractPearl.dataPearlType == MoreSlugcatsEnums.DataPearlType.BroadcastMisc);
            bool saintBleachedPearl = ModManager.MSC && Gate.room.world.game.GetStorySession.saveStateNumber == MoreSlugcatsEnums.SlugcatStatsName.Saint && HeldPearl.AbstractPearl.dataPearlType != MoreSlugcatsEnums.DataPearlType.RM && HeldPearl.AbstractPearl.dataPearlType != DataPearl.AbstractDataPearl.DataPearlType.LF_west;

            // Make a fake Looks to the Moon / Five Pebbles to do the talking
            SLOracleBehaviorHasMark dummyOracleBehavior = GetUninit<SLOracleBehaviorHasMark>();
            dummyOracleBehavior.oracle = GetUninit<Oracle>();
            dummyOracleBehavior.oracle.room = Gate.room;
            dummyOracleBehavior.holdingObject = HeldPearl;
            dummyOracleBehavior.isRepeatedDiscussion = false;

            SlugcatStats.Name slugcatName = Gate.room.game.GetStorySession.saveStateNumber;
            if (ModManager.MSC && slugcatName == MoreSlugcatsEnums.SlugcatStatsName.Spear)
            {
                dummyOracleBehavior.oracle.ID = MoreSlugcatsEnums.OracleID.DM;
                if (important)
                {
                    dummyOracleBehavior.isRepeatedDiscussion = Gate.room.game.rainWorld.progression.miscProgressionData.GetDMPearlDeciphered(HeldPearl.AbstractPearl.dataPearlType);
                    Gate.room.game.rainWorld.progression.miscProgressionData.SetDMPearlDeciphered(HeldPearl.AbstractPearl.dataPearlType, false);
                }
            }
            else if (ModManager.MSC && slugcatName == MoreSlugcatsEnums.SlugcatStatsName.Artificer)
            {
                dummyOracleBehavior.oracle.ID = Oracle.OracleID.SS;
                if (important)
                {
                    dummyOracleBehavior.isRepeatedDiscussion = Gate.room.game.rainWorld.progression.miscProgressionData.GetPebblesPearlDeciphered(HeldPearl.AbstractPearl.dataPearlType);
                    Gate.room.game.rainWorld.progression.miscProgressionData.SetPebblesPearlDeciphered(HeldPearl.AbstractPearl.dataPearlType, false);
                }
            }
            else
            {
                dummyOracleBehavior.oracle.ID = Oracle.OracleID.SL;
                dummyOracleBehavior.isRepeatedDiscussion = slOracleState.significantPearls.Contains(HeldPearl.AbstractPearl.dataPearlType);
                if (saintBleachedPearl)
                {
                    if (Gate.room.game.rainWorld.progression.miscProgressionData.GetFuturePearlDeciphered(DataPearl.AbstractDataPearl.DataPearlType.CC))
                    {
                        dummyOracleBehavior.isRepeatedDiscussion = true;
                    }
                    id = MoreSlugcatsEnums.ConversationID.Moon_PearlBleaching;
                    if (!slOracleState.significantPearls.Contains(DataPearl.AbstractDataPearl.DataPearlType.CC))
                    {
                        slOracleState.significantPearls.Add(DataPearl.AbstractDataPearl.DataPearlType.CC);
                    }
                    Gate.room.game.rainWorld.progression.miscProgressionData.SetFuturePearlDeciphered(DataPearl.AbstractDataPearl.DataPearlType.CC, true);
                }
                else
                {
                    if (!dummyOracleBehavior.isRepeatedDiscussion && ModManager.MSC)
                    {
                        if (Gate.room.world.game.GetStorySession.saveStateNumber == MoreSlugcatsEnums.SlugcatStatsName.Saint)
                        {
                            Gate.room.game.rainWorld.progression.miscProgressionData.SetFuturePearlDeciphered(HeldPearl.AbstractPearl.dataPearlType, false);
                        }
                        else
                        {
                            Gate.room.game.rainWorld.progression.miscProgressionData.SetPearlDeciphered(HeldPearl.AbstractPearl.dataPearlType);
                        }
                    }
                }
            }

            if (important && !saintBleachedPearl && !slOracleState.significantPearls.Contains(HeldPearl.AbstractPearl.dataPearlType))
            {
                slOracleState.significantPearls.Add(HeldPearl.AbstractPearl.dataPearlType);
            }

            Plugin.IsInGateConversationInit = true;
            Plugin.CurrentlyInitializingScanner = this;
            Speaker = new SLOracleBehaviorHasMark.MoonConversation(id, dummyOracleBehavior, SLOracleBehaviorHasMark.MiscItemType.NA);
            Plugin.CurrentlyInitializingScanner = null;
            Plugin.IsInGateConversationInit = false;

            if (!dummyOracleBehavior.isRepeatedDiscussion)
            {
                slOracleState.totalItemsBrought++;
                slOracleState.AddItemToAlreadyTalkedAbout(HeldPearl.abstractPhysicalObject.ID);

                if (important && !saintBleachedPearl)
                {
                    slOracleState.totalPearlsBrought++;
                    if (RainWorld.ShowLogs)
                    {
                        Debug.Log("pearls brought up: " + slOracleState.totalPearlsBrought.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Turns off the scanner.
        /// </summary>
        public void DropHeldPearl()
        {
            if (HeldPearl != null)
            {
                if (ModManager.MSC && Gate.room.game.GetStorySession.characterStats.name == MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel && Step1Timer > 0)
                {
                    AbstractPhysicalObject abstractPhysicalObject = new(Gate.room.world, MoreSlugcatsEnums.AbstractObjectType.SingularityBomb, null, Gate.room.GetWorldCoordinate(PearlHoldPos), Gate.room.world.game.GetNewID());
                    Gate.room.abstractRoom.AddEntity(abstractPhysicalObject);
                    abstractPhysicalObject.RealizeInRoom();
                    SingularityBomb bomb = abstractPhysicalObject.realizedObject as SingularityBomb;
                    bomb.Explode();
                    bomb.Destroy();
                    HeldPearl.Destroy();
                }
                HeldPearl.gravity = 0.9f;
                HeldPearl = null;
            }
            else
            {
                Debug.Log("GateScanner.GateScannerObject.DropHeldPearl() called without a pearl to drop. This isn't going to break anything, but it probably shouldn't happen.");
            }
            HeldPearlSide = null;
            Step1Timer = 0;
            Step1TimeRequired = -1;
            Step2Timer = 0;
            Step2TimeRequired = -1;
            if (Speaker != null)
            {
                Speaker.Interrupt("...", 0);
                Speaker.Destroy();
                Speaker = null;
            }
        }

        public bool PearlcatCanAccessLooksToTheMoon(StoryGameSession session)
        {
            return LooksToTheMoonAvailable(session);
        }
        public bool PearlcatCanAccessFivePebbles(StoryGameSession session)
        {
            return PluginOptions.UnlockScannerCheat.Value || session.saveState.miscWorldSaveData.SSaiConversationsHad > 0;
        }
        /// <summary>
        /// Determines hether or not (post-collapse) Looks to the Moon is available for pearl-reading in the current session.
        /// </summary>
        /// <param name="session">The session to check.</param>
        /// <returns>Whether or not (post-collapse) Looks to the Moon is available for pearl-reading in the current session. Returns <see cref="false"/> if Looks to the Moon is dead, ascended, unaware of the player, or unwilling to respond.</returns>
        public bool LooksToTheMoonAvailable(StoryGameSession session)
        {
            return !session.saveState.deathPersistentSaveData.ripMoon &&
                   session.saveState.miscWorldSaveData.SLOracleState.neuronsLeft > 0 &&
                   (PluginOptions.UnlockScannerCheat.Value || (session.saveState.miscWorldSaveData.SLOracleState.playerEncountersWithMark > 0 && session.saveState.miscWorldSaveData.SLOracleState.SpeakingTerms));
        }
        /// <summary>
        /// Determines whether or not an iterator will respond when a pearl is scanned.
        /// </summary>
        /// <returns>Whether or not an iterator will respond when a pearl is scanned.</returns>
        public bool CanHaveConversation()
        {
            StoryGameSession session = Gate.room.game.GetStorySession;

            if (!(session.saveState.deathPersistentSaveData.theMark || (ModManager.MSC && session.characterStats.name == MoreSlugcatsEnums.SlugcatStatsName.Saint)))
            {
                return false;
            }
            else if (ModManager.MSC && session.characterStats.name == MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel)
            {
                return true;
            }
            else if (ModManager.MSC && session.saveStateNumber == MoreSlugcatsEnums.SlugcatStatsName.Spear)
            {
                return PluginOptions.UnlockScannerCheat.Value || session.saveState.miscWorldSaveData.SLOracleState.playerEncounters > 0 || session.saveState.miscWorldSaveData.SLOracleState.playerEncountersWithMark > 0; // Expedition Mode sets playerEncountersWithMark only, the first visit to Looks to the Moon sets playerEncounters only.
            }
            else if (ModManager.MSC && session.saveStateNumber == MoreSlugcatsEnums.SlugcatStatsName.Artificer)
            {
                return PluginOptions.UnlockScannerCheat.Value || (session.saveState.miscWorldSaveData.SSaiConversationsHad > 0 && session.saveState.hasRobo);
            }
            else if (Plugin.PearlcatEnabled && session.saveStateNumber.value == "Pearlcat")
            {
                return PearlcatCanAccessLooksToTheMoon(session); // || PearlcatCanAccessFivePebbles(session);
            }
            else
            {
                return LooksToTheMoonAvailable(session);
            }
        }
        public bool PlayerInRoom()
        {
            return Gate.room.game.Players.Any(x => x.pos.room == Gate.room.abstractRoom.index && x.state.alive);
        }

        /// <summary>
        /// Checks whether or not a pearl is being carried by a creature in some way. Such pearls should not be scanned.
        /// </summary>
        /// <param name="pearl">The pearl to check.</param>
        /// <returns>Whether or not the pearl is currently being carried.</returns>
        public static bool PearlOwnedByCreature(DataPearl pearl) // This function exists because of Pearlcat's inventory.
        {
            if (pearl.grabbedBy.Count > 0)
            {
                return true;
            }
            if (Plugin.PearlcatEnabled)
            {
                if (PearlcatInventoryCheck(pearl))
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Checks whether or not a pearl is owned by Pearlcat. Requires Pearlcat to function.
        /// </summary>
        /// <param name="pearl">The Pearl to check.</param>
        /// <returns>Whether or not the pearl is owned by Pearlcat.</returns>
        public static bool PearlcatInventoryCheck(DataPearl pearl)
        {
            return Pearlcat.Hooks.IsPlayerObject(pearl.abstractPhysicalObject);
        }

        /// <summary>
        /// Determines the ID color of the iterator that reads pearls.
        /// </summary>
        /// <returns>The ID color of the iterator that reads pearls.</returns>
        public Color IteratorColor()
        {
            if (ModManager.MSC && Gate.room.game.GetStorySession.characterStats.name == MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel)
            {
                return new(0f, 1f, 0f);
            }
            else if (ModManager.MSC && Gate.room.game.GetStorySession.characterStats.name == MoreSlugcatsEnums.SlugcatStatsName.Artificer)
            {
                return new(0.44705883f, 0.9019608f, 0.76862746f);
            }
            else
            {
                return new(1f, 0.8f, 0.3f);
            }
        }

        public void Update(bool eu)
        {
            if (Gate.room.game.session is not StoryGameSession)
            {
                return;
            }
            if (HeldPearl != null && (HeldPearl.slatedForDeletetion || HeldPearl.room == null || HeldPearl.room.abstractRoom.index != Gate.room.abstractRoom.index))
            {
                Debug.Log("Pearl removed from room or deleted, scanner disabled");
                DropHeldPearl();
            }
            AlreadyScanned.RemoveAll(x => x.slatedForDeletetion || x.room == null || x.room.abstractRoom == null || x.room.abstractRoom.index != Gate.room.abstractRoom.index || x.firstChunk.pos.x < Gate.room.PixelWidth / 2 - 160 || x.firstChunk.pos.x >= Gate.room.PixelWidth / 2 + 160); // Pearls that have been removed from the gate may be scanned again
            // Turn off scanner if no one is around to listen
            if (HeldPearl != null && !PlayerInRoom())
            {
                Debug.Log("All players left the room, scanner disabled");
                AlreadyScanned.Add(HeldPearl);
                DropHeldPearl();
            }
            // Turn scanner off if the room floods
            if (HeldPearl != null && ScannerUnderWater)
            {
                Debug.Log("Scanner submerged");
                DropHeldPearl();
            }
            if (ErrorTimer > 0)
            {
                ErrorTimer--;
            }
            if (Gate.mode == RegionGate.Mode.MiddleClosed || Gate.mode == RegionGate.Mode.Closed || Gate.mode == RegionGate.Mode.Broken) // The scanner only works when the gate is not being used as a gate
            {
                if (HeldPearl != null)
                {
                    if (!PearlOwnedByCreature(HeldPearl))
                    {
                        // Move pearl into position
                        HeldPearl.firstChunk.vel *= RWCustom.Custom.LerpMap(HeldPearl.firstChunk.vel.magnitude, 1f, 6f, 0.9f, 0.8f);
                        HeldPearl.firstChunk.vel += Vector2.ClampMagnitude(PearlHoldPos - HeldPearl.firstChunk.pos, 100f) / 100f * 0.4f;
                        HeldPearl.gravity = 0f;
                        if (HeldPearl.firstChunk.vel.magnitude < 0.001f && Vector2.Distance(HeldPearl.firstChunk.pos, PearlHoldPos) < 0.1f) // Pearl is in position
                        {
                            // Lock pearl into position
                            HeldPearl.firstChunk.vel = new(0, 0);
                            HeldPearl.firstChunk.pos = PearlHoldPos;
                            if (Step1Timer == 0)
                            {
                                Gate.room.PlaySound(SoundID.SS_AI_Text, PearlHoldPos, 0.5f * BEEPVOLUME, 1.25f);
                                if (ModManager.MSC && Gate.room.game.GetStorySession.characterStats.name == MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel)
                                {
                                    Gate.room.lockedShortcuts.Clear();
                                    for (int i = 0; i < Gate.room.shortcutsIndex.Length; i++)
                                    {
                                        Gate.room.lockedShortcuts.Add(Gate.room.shortcutsIndex[i]);
                                    }
                                }
                            }
                            Step1Timer++;
                            if (Step1Timer > Step1TimeRequired && Step1TimeRequired > 0)
                            {
                                Step2Timer++;
                                if (Step2Timer > Step2TimeRequired && Step2TimeRequired > 0)
                                {
                                    if (Speaker == null) // Scan complete, reading not started. In other words, the frame the scan finishes.
                                    {
                                        if (CanHaveConversation())
                                        {
                                            Gate.room.PlaySound(SoundID.SS_AI_Text, PearlHoldPos, BEEPVOLUME, 1.5f);
                                            // Start pearl dialogue
                                            Debug.Log("Scanned!");
                                            StartConversation();

                                            if (!AnyScannerUsedBefore)
                                            {
                                                Debug.Log("First time using gate scanner!");
                                                AnyScannerUsedBefore = true;
                                            }
                                        }
                                        else
                                        {
                                            Gate.room.PlaySound(SoundID.SS_AI_Text, PearlHoldPos, BEEPVOLUME, 0.9f);
                                            // But nobody came...
                                            Debug.Log("Scan request at " + Gate.room.abstractRoom.name + " was unable to be completed");
                                            AlreadyScanned.Add(HeldPearl);
                                            DropHeldPearl();
                                            ErrorTimer = 60;
                                        }
                                    }
                                    else
                                    {
                                        Speaker.Update();
                                        if (Speaker.events.Count == 0)
                                        {
                                            Debug.Log("Scan finished at " + Gate.room.abstractRoom.name);
                                            AlreadyScanned.Add(HeldPearl);
                                            Speaker.Destroy();
                                            Speaker = null;
                                            DropHeldPearl();
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            Step1Timer = 0;
                            Step2Timer = 0;
                            {
                                if (Speaker != null) // Conversation interrupted by movement of pearl
                                {
                                    Debug.Log("Scan interrupted at " + Gate.room.abstractRoom.name);
                                    DropHeldPearl();
                                }
                            }
                        }
                    }
                    else // Something grabbed the pearl out of the scanner
                    {
                        Debug.Log("Pearl removed from gate scanner " + Gate.room.abstractRoom.name);
                        DropHeldPearl();
                    }
                }
                else
                {
                    Step1Timer = 0;
                    Step2Timer = 0;
                    if (!ScannerUnderWater && ErrorTimer == 0) // The gate symbols don't display properly underwater.
                    {
                        List<DataPearl> readablePearls = PearlsInGateRange().Where(x => !PearlOwnedByCreature(x) && !x.slatedForDeletetion && x.room != null && x.room.abstractRoom.index == Gate.room.abstractRoom.index && !AlreadyScanned.Contains(x)).ToList();
                        if (readablePearls.Count > 0 && PlayerInRoom())
                        {
                            HeldPearl = readablePearls[Random.Range(0, readablePearls.Count)];
                            HeldPearlSide = HeldPearl.firstChunk.pos.x > Gate.room.PixelWidth / 2;
                            Step1TimeRequired = Random.Range(10, 30);
                            Step2TimeRequired = Random.Range(60, 120);
                            if (ModManager.MSC && Gate.room.game.GetStorySession.characterStats.name == MoreSlugcatsEnums.SlugcatStatsName.Saint)
                            {
                                Step1TimeRequired *= 2;
                                Step2TimeRequired *= 2;
                            }
                            if (!CanHaveConversation())
                            {
                                Step2TimeRequired = 300;
                            }
                            if (ModManager.MSC && Gate.room.game.GetStorySession.characterStats.name == MoreSlugcatsEnums.SlugcatStatsName.Artificer && (HeldPearl.AbstractPearl.dataPearlType == DataPearl.AbstractDataPearl.DataPearlType.Misc || HeldPearl.AbstractPearl.dataPearlType == DataPearl.AbstractDataPearl.DataPearlType.Misc2) && Gate.room.game.SeededRandomRange(HeldPearl.abstractPhysicalObject.ID.RandomSeed, 0, 47) == 45)
                            {
                                // For testing purposes, ID 3 is malicious.
                                Debug.Log("This is a malicious pearl. The data is meaningless, but the way it is formatted would cause older machinery to get stuck in an infinite recursion trying to read it.");
                                Step1TimeRequired = -1;
                            }
                            Debug.Log("Gate scanner " + Gate.room.abstractRoom.name + " found pearl " + HeldPearl.AbstractPearl.dataPearlType);
                        }
                    }
                }
            }
            else
            {
                // The gate is supposed to be suppressed while the scanner is active, but if it does somehow activate it should get priority.
                if (HeldPearl != null)
                {
                    Debug.Log("Gate " + Gate.room.abstractRoom.name + " active, scanner disabled");
                    DropHeldPearl();
                }
            }
        }
    }
}
