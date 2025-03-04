﻿using System;
using System.Linq;
using static PKHeX.Core.LegalityCheckStrings;
using static PKHeX.Core.MemoryPermissions;

namespace PKHeX.Core
{
    /// <summary>
    /// Verifies the <see cref="IMemoryOT.OT_Memory"/>, <see cref="IMemoryHT.HT_Memory"/>, and associated values.
    /// </summary>
    public sealed class MemoryVerifier : Verifier
    {
        protected override CheckIdentifier Identifier => CheckIdentifier.Memory;

        public override void Verify(LegalityAnalysis data)
        {
            if (data.pkm.BDSP)
            {
                VerifyOTMemoryIs(data, 0, 0, 0, 0);
                VerifyHTMemoryNone(data, (ITrainerMemories)data.pkm);
                return;
            }
            VerifyOTMemory(data);
            VerifyHTMemory(data);
        }

        private CheckResult VerifyCommonMemory(PKM pkm, int handler, int gen, LegalInfo info, MemoryContext context)
        {
            var memory = MemoryVariableSet.Read((ITrainerMemories)pkm, handler);

            // Actionable HM moves
            int matchingMoveMemory = Array.IndexOf(MemoryContext6.MoveSpecificMemories[0], memory.MemoryID);
            if (matchingMoveMemory != -1)
            {
                if (gen != 6) // Gen8 has no HMs, so this memory can never exist.
                    return GetInvalid(string.Format(LMemoryArgBadMove, memory.Handler));

                if (pkm.Species != (int)Species.Smeargle)
                {
                    if (!GetCanLearnMachineMove(pkm, info.EvoChainsAllGens[gen], MemoryContext6.MoveSpecificMemories[1][matchingMoveMemory], 6))
                        return GetInvalid(string.Format(LMemoryArgBadMove, memory.Handler));
                }
            }

            if (context.IsInvalidGeneralLocationMemoryValue(memory.MemoryID, memory.Variable, info.EncounterMatch, pkm))
                return GetInvalid(string.Format(LMemoryArgBadLocation, memory.Handler));

            if (context.IsInvalidMiscMemory(memory.MemoryID, memory.Variable))
                return GetInvalid(string.Format(LMemoryArgBadID, memory.Handler));

            switch (memory.MemoryID)
            {
                case 19 when pkm.Species is (int)Species.Urshifu   && memory.Variable is not 34: // tall building is the only location for evolving Urshifu
                case 19 when pkm.Species is (int)Species.Runerigus && memory.Variable is not 72: // vast field is the only location for evolving Runerigus
                    return GetInvalid(string.Format(LMemoryArgBadLocation, memory.Handler));

                // {0} saw {2} carrying {1} on its back. {4} that {3}.
                case 21 when gen != 6 || !GetCanLearnMachineMove(new PK6 {Species = memory.Variable, EXP = Experience.GetEXP(100, PersonalTable.XY.GetFormIndex(memory.Variable, 0))}, (int)Move.Fly, 6):
                    return GetInvalid(string.Format(LMemoryArgBadMove, memory.Handler));

                // {0} used {2} at {1}’s instruction, but it had no effect. {4} that {3}.
                // The Move Deleter that {0} met through {1} made it forget {2}. {4} that {3}.
                case 16 or 48 when !CanKnowMove(pkm, memory, gen, info, memory.MemoryID == 16):
                    return GetInvalid(string.Format(LMemoryArgBadMove, memory.Handler));

                case 49 when memory.Variable == 0 || !GetCanRelearnMove(pkm, memory.Variable, gen, info.EvoChainsAllGens[gen]):
                    return GetInvalid(string.Format(LMemoryArgBadMove, memory.Handler));

                // Dynamaxing
                // {0} battled at {1}’s side against {2} that Dynamaxed. {4} that {3}.
                case 71 when !GetCanDynamaxTrainer(memory.Variable, 8, handler == 0 ? (GameVersion)pkm.Version : GameVersion.Any):
                // {0} battled {2} and Dynamaxed upon {1}’s instruction. {4} that {3}.
                case 72 when !((PersonalInfoSWSH)PersonalTable.SWSH[memory.Variable]).IsPresentInGame:
                    return GetInvalid(string.Format(LMemoryArgBadSpecies, memory.Handler));

                // Move
                // {0} studied about how to use {2} in a Box, thinking about {1}. {4} that {3}.
                // {0} practiced its cool pose for the move {2} in a Box, wishing to be praised by {1}. {4} that {3}.
                case 80 or 81 when !CanKnowMove(pkm, memory, gen, info):
                    return Get(string.Format(LMemoryArgBadMove, memory.Handler), Severity.Invalid);

                // Species
                // With {1}, {0} went fishing, and they caught {2}. {4} that {3}.
                case 7 when !GetCanFishSpecies(memory.Variable, gen, handler == 0 ? (GameVersion)pkm.Version : GameVersion.Any):
                    return GetInvalid(string.Format(LMemoryArgBadSpecies, memory.Handler));

                // {0} saw {1} paying attention to {2}. {4} that {3}.
                // {0} fought hard until it had to use Struggle when it battled at {1}’s side against {2}. {4} that {3}.
                // {0} was taken to a Pokémon Nursery by {1} and left with {2}. {4} that {3}.
                case 9 or 60 or 75 when gen == 8 && !((PersonalInfoSWSH)PersonalTable.SWSH[memory.Variable]).IsPresentInGame:
                    return GetInvalid(string.Format(LMemoryArgBadSpecies, memory.Handler));

                // {0} had a great chat about {1} with the {2} that it was in a Box with. {4} that {3}.
                // {0} became good friends with the {2} in a Box, practiced moves with it, and talked about the day that {0} would be praised by {1}. {4} that {3}.
                // {0} got in a fight with the {2} that it was in a Box with about {1}. {4} that {3}.
                case 82 or 83 or 87 when !((PersonalInfoSWSH)PersonalTable.SWSH[memory.Variable]).IsPresentInGame:
                    return GetInvalid(string.Format(LMemoryArgBadSpecies, memory.Handler));

                // {0} had a very hard training session with {1}. {4} that {3}.
                case 53 when gen == 8 && pkm is IHyperTrain t && !t.IsHyperTrained():
                    return GetInvalid(string.Format(LMemoryArgBadID, memory.Handler));

                // Item
                // {0} went to a Pokémon Center with {1} to buy {2}. {4} that {3}.
                case 5 when !CanBuyItem(gen, memory.Variable, handler == 0 ? (GameVersion)pkm.Version : GameVersion.Any):
                // {1} used {2} when {0} was in trouble. {4} that {3}.
                case 15 when !CanUseItem(gen, memory.Variable, pkm.Species):
                // {0} saw {1} using {2}. {4} that {3}.
                case 26 when !CanUseItemGeneric(gen, memory.Variable):
                // {0} planted {2} with {1} and imagined a big harvest. {4} that {3}.
                case 34 when !CanPlantBerry(gen, memory.Variable):
                // {1} had {0} hold items like {2} to help it along. {4} that {3}.
                case 40 when !CanHoldItem(gen, memory.Variable):
                // {0} was excited when {1} won prizes like {2} through Loto-ID. {4} that {3}.
                case 51 when !CanWinLotoID(gen, memory.Variable):
                // {0} was worried if {1} was looking for the {2} that it was holding in a Box. {4} that {3}.
                // When {0} was in a Box, it thought about the reason why {1} had it hold the {2}. {4} that {3}.
                case 84 or 88 when !Legal.HeldItems_SWSH.Contains((ushort)memory.Variable) || pkm.IsEgg:
                    return GetInvalid(string.Format(LMemoryArgBadItem, memory.Handler));
            }

            return VerifyCommonMemoryEtc(memory, context);
        }

        private CheckResult VerifyCommonMemoryEtc(MemoryVariableSet memory, MemoryContext context)
        {
            if (!context.CanHaveIntensity(memory.MemoryID, memory.Intensity))
            {
                var min = context.GetMinimumIntensity(memory.MemoryID);
                return GetInvalid(string.Format(LMemoryIndexIntensityMin, memory.Handler, min));
            }

            if (!context.CanHaveFeeling(memory.MemoryID, memory.Feeling, memory.Variable))
                return GetInvalid(string.Format(LMemoryFeelInvalid, memory.Handler));

            return GetValid(string.Format(LMemoryF_0_Valid, memory.Handler));
        }

        /// <summary>
        /// Used for enforcing a fixed memory detail.
        /// </summary>
        /// <param name="data">Output storage</param>
        /// <param name="m">Memory ID</param>
        /// <param name="i">Intensity</param>
        /// <param name="t">Text Variable</param>
        /// <param name="f">Feeling</param>
        private void VerifyOTMemoryIs(LegalityAnalysis data, int m, int i, int t, int f)
        {
            var pkm = (ITrainerMemories)data.pkm;
            if (pkm.OT_Memory != m)
                data.AddLine(GetInvalid(string.Format(LMemoryIndexID, L_XOT, m)));
            if (pkm.OT_Intensity != i)
                data.AddLine(GetInvalid(string.Format(LMemoryIndexIntensity, L_XOT, i)));
            if (pkm.OT_TextVar != t)
                data.AddLine(GetInvalid(string.Format(LMemoryIndexVar, L_XOT, t)));
            if (pkm.OT_Feeling != f)
                data.AddLine(GetInvalid(string.Format(LMemoryIndexFeel, L_XOT, f)));
        }

        private void VerifyHTMemoryNone(LegalityAnalysis data, ITrainerMemories pkm)
        {
            if (pkm.HT_Memory != 0 || pkm.HT_TextVar != 0 || pkm.HT_Intensity != 0 || pkm.HT_Feeling != 0)
                data.AddLine(GetInvalid(string.Format(LMemoryCleared, L_XHT)));
        }

        private void VerifyOTMemory(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            var mem = (ITrainerMemories)pkm;
            var Info = data.Info;

            // If the encounter has a memory from the OT that could never have it replaced, ensure it was not modified.
            switch (data.EncounterMatch)
            {
                case WC6 {IsEgg: false} g when g.OTGender != 3:
                    VerifyOTMemoryIs(data, g.OT_Memory, g.OT_Intensity, g.OT_TextVar, g.OT_Feeling);
                    return;
                case WC7 {IsEgg: false} g when g.OTGender != 3:
                    VerifyOTMemoryIs(data, g.OT_Memory, g.OT_Intensity, g.OT_TextVar, g.OT_Feeling);
                    return;
                case WC8 {IsEgg: false} g when g.OTGender != 3:
                    VerifyOTMemoryIs(data, g.OT_Memory, g.OT_Intensity, g.OT_TextVar, g.OT_Feeling);
                    return;

                case IMemoryOT t and not MysteryGift: // Ignore Mystery Gift cases (covered above)
                    VerifyOTMemoryIs(data, t.OT_Memory, t.OT_Intensity, t.OT_TextVar, t.OT_Feeling);
                    return;
            }

            int memoryGen = Info.Generation;
            int memory = mem.OT_Memory;

            if (pkm.IsEgg)
            {
                // Traded unhatched eggs in Gen8 have OT link trade memory applied erroneously.
                // They can also have the box-inspect memory!
                if (memoryGen != 8 || !((pkm.Met_Location == Locations.LinkTrade6 && memory == 4) || memory == 85))
                {
                    VerifyOTMemoryIs(data, 0, 0, 0, 0); // empty
                    return;
                }
            }
            else if (!CanHaveMemoryForOT(pkm, memoryGen, memory))
            {
                VerifyOTMemoryIs(data, 0, 0, 0, 0); // empty
                return;
            }

            // Bounds checking
            var context = Memories.GetContext(memoryGen);
            if (!context.CanObtainMemoryOT((GameVersion)pkm.Version, memory))
                data.AddLine(GetInvalid(string.Format(LMemoryArgBadID, L_XOT)));

            // Verify memory if specific to OT
            switch (memory)
            {
                // No Memory
                case 0: // SWSH trades don't set HT memories immediately, which is hilarious.
                    data.AddLine(Get(LMemoryMissingOT, memoryGen == 8 ? Severity.Fishy : Severity.Invalid));
                    VerifyOTMemoryIs(data, 0, 0, 0, 0);
                    return;

                // {0} hatched from an Egg and saw {1} for the first time at... {2}. {4} that {3}.
                case 2 when !Info.EncounterMatch.EggEncounter:
                    data.AddLine(GetInvalid(string.Format(LMemoryArgBadHatch, L_XOT)));
                    break;

                // {0} became {1}’s friend when it arrived via Link Trade at... {2}. {4} that {3}.
                case 4 when Info.Generation == 6: // gen8 applies this memory erroneously
                    data.AddLine(GetInvalid(string.Format(LMemoryArgBadOTEgg, L_XOT)));
                    return;

                // {0} went to the Pokémon Center in {2} with {1} and had its tired body healed there. {4} that {3}.
                case 6 when !context.HasPokeCenter((GameVersion)pkm.Version, mem.OT_TextVar):
                    data.AddLine(GetInvalid(string.Format(LMemoryArgBadLocation, L_XOT)));
                    return;

                // {0} was with {1} when {1} caught {2}. {4} that {3}.
                case 14:
                    var result = GetCanBeCaptured(mem.OT_TextVar, Info.Generation, (GameVersion)pkm.Version) // Any Game in the Handling Trainer's generation
                        ? GetValid(string.Format(LMemoryArgSpecies, L_XOT))
                        : GetInvalid(string.Format(LMemoryArgBadSpecies, L_XOT));
                    data.AddLine(result);
                    return;
            }

            data.AddLine(VerifyCommonMemory(pkm, 0, Info.Generation, Info, context));
        }

        private static bool CanHaveMemoryForOT(PKM pkm, int origin, int memory)
        {
            switch (origin)
            {
                // Bank Memories only: Gen7 does not set memories.
                case 1 or 2 or 7 when memory != 4: // VC transfers

                // Memories don't exist
                case 7 when pkm.GG: // LGPE does not set memories.
                case 8 when pkm.GO_HOME: // HOME does not set memories.
                case 8 when pkm.Met_Location == Locations.HOME8: // HOME does not set memories.
                case 8 when pkm.BDSP: // BDSP does not set memories.
                    return false;

                // Eggs cannot have memories
                // Cannot have memories if the OT was from a generation prior to Gen6.
                default:
                    return origin >= 6 && !pkm.IsEgg;
            }
        }

        private void VerifyHTMemory(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            var mem = (ITrainerMemories)pkm;
            var Info = data.Info;

            var memory = mem.HT_Memory;

            if (pkm.IsUntraded)
            {
                if (memory == 4 && WasTradedSWSHEgg(pkm))
                {
                    // Untraded link trade eggs in Gen8 have HT link trade memory applied erroneously.
                    // Verify the link trade memory later.
                }
                else
                {
                    VerifyHTMemoryNone(data, mem);
                    return;
                }
            }

            if (pkm.Format == 7)
            {
                VerifyHTMemoryTransferTo7(data, pkm, Info);
                return;
            }

            var memoryGen = pkm.Format >= 8 ? 8 : 6;

            // Bounds checking
            var context = Memories.GetContext(memoryGen);
            if (!context.CanObtainMemoryHT((GameVersion)pkm.Version, memory))
                data.AddLine(GetInvalid(string.Format(LMemoryArgBadID, L_XHT)));

            // Verify memory if specific to HT
            switch (memory)
            {
                // No Memory
                case 0: // SWSH memory application has an off-by-one error: [0,99] + 1 <= chance --> don't apply
                    data.AddLine(Get(LMemoryMissingHT, memoryGen == 8 ? ParseSettings.Gen8MemoryMissingHT : Severity.Invalid));
                    VerifyHTMemoryNone(data, mem);
                    return;

                // {0} met {1} at... {2}. {1} threw a Poké Ball at it, and they started to travel together. {4} that {3}.
                case 1:
                    data.AddLine(GetInvalid(string.Format(LMemoryArgBadCatch, L_XHT)));
                    return;

                // {0} hatched from an Egg and saw {1} for the first time at... {2}. {4} that {3}.
                case 2:
                    data.AddLine(GetInvalid(string.Format(LMemoryArgBadHatch, L_XHT)));
                    return;

                // {0} went to the Pokémon Center in {2} with {1} and had its tired body healed there. {4} that {3}.
                case 6 when !context.HasPokeCenter(GameVersion.Any, mem.HT_TextVar):
                    data.AddLine(GetInvalid(string.Format(LMemoryArgBadLocation, L_XHT)));
                    return;

                // {0} was with {1} when {1} caught {2}. {4} that {3}.
                case 14:
                    var result = GetCanBeCaptured(mem.HT_TextVar, memoryGen, GameVersion.Any) // Any Game in the Handling Trainer's generation
                        ? GetValid(string.Format(LMemoryArgSpecies, L_XHT))
                        : GetInvalid(string.Format(LMemoryArgBadSpecies, L_XHT));
                    data.AddLine(result);
                    return;
            }

            var commonResult = VerifyCommonMemory(pkm, 1, memoryGen, Info, context);
            data.AddLine(commonResult);
        }

        private static bool WasTradedSWSHEgg(PKM pkm) => pkm.SWSH && (!pkm.IsEgg ? pkm.Egg_Location : pkm.Met_Location) is Locations.LinkTrade6;

        private void VerifyHTMemoryTransferTo7(LegalityAnalysis data, PKM pkm, LegalInfo Info)
        {
            var mem = (ITrainerMemories)pkm;
            // Bank Transfer adds in the Link Trade Memory.
            // Trading 7<->7 between games (not Bank) clears this data.
            if (mem.HT_Memory == 0)
            {
                VerifyHTMemoryNone(data, mem);
                return;
            }

            // Transfer 6->7 & withdraw to same HT => keeps past gen memory
            // Don't require link trade memory for these past gen cases
            int gen = Info.Generation;
            if (gen is >= 3 and < 7 && pkm.CurrentHandler == 1)
                return;

            if (mem.HT_Memory != 4)
                data.AddLine(Severity.Invalid, LMemoryIndexLinkHT, CheckIdentifier.Memory);
            if (mem.HT_TextVar != 0)
                data.AddLine(Severity.Invalid, LMemoryIndexArgHT, CheckIdentifier.Memory);
            if (mem.HT_Intensity != 1)
                data.AddLine(Severity.Invalid, LMemoryIndexIntensityHT1, CheckIdentifier.Memory);
            if (mem.HT_Feeling > 10)
                data.AddLine(Severity.Invalid, LMemoryIndexFeelHT09, CheckIdentifier.Memory);
        }
    }
}
