using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Quests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace SuperAardvark.AntiSocial
{
    /// <summary>
    /// This class can be copied into any mod to provide ad-hoc AntiSocial functionality.  Just call AntiSocialManager.DoSetupIfNecessary in your mod's Entry method.
    /// </summary>
    public class AntiSocialManager
    {
        public const string AssetName = "Data/AntiSocialNPCs";
        public const string OriginModId = "SuperAardvark.AntiSocial";

        private static Mod modInstance;
        private static Harmony harmonyInstance;
        private static bool adHoc = false;

        public static AntiSocialManager Instance { get; private set; }

        /// <summary>
        /// Checks for the AntiSocial stand-alone mod before running setup.
        /// </summary>
        /// <param name="modInstance">A reference to your Mod class.</param>
        public static void DoSetupIfNecessary(Mod modInstance)
        {
            if (modInstance.ModManifest.UniqueID.Equals(OriginModId))
            {
                modInstance.Monitor.Log("AntiSocial Mod performing stand-alone setup.", LogLevel.Debug);
                adHoc = false;
                DoSetup(modInstance);
            }
            else if (modInstance.Helper.ModRegistry.IsLoaded(OriginModId))
            {
                modInstance.Monitor.Log("AntiSocial Mod loaded.  Skipping ad hoc setup.", LogLevel.Debug);
            }
            else if (AntiSocialManager.modInstance != null)
            {
                modInstance.Monitor.Log("AntiSocial setup was already completed.", LogLevel.Debug);
            }
            else
            {
                modInstance.Monitor.Log($"AntiSocial Mod not loaded.  No problem; performing ad hoc setup for {modInstance.ModManifest.Name}.", LogLevel.Debug);
                adHoc = true;
                DoSetup(modInstance);
            }
        }

        /// <summary>
        /// Sets up AntiSocial.
        /// </summary>
        /// <param name="modInstance"></param>
        private static void DoSetup(Mod modInstance)
        {
            if (Instance != null)
            {
                modInstance.Monitor.Log($"AntiSocial setup already completed by {AntiSocialManager.modInstance.ModManifest.Name} ({AntiSocialManager.modInstance.ModManifest.UniqueID}).", LogLevel.Warn);
                return;
            }

            Instance = new AntiSocialManager();
            AntiSocialManager.modInstance = modInstance;

            modInstance.Helper.Events.Content.AssetRequested += OnAssetRequested;

            harmonyInstance = new(OriginModId);
            harmonyInstance.Patch(original: AccessTools.Method(typeof(NPC), "get_CanSocialize"), 
                                  postfix: new HarmonyMethod(typeof(AntiSocialManager), "get_CanSocialize_Postfix"));
            harmonyInstance.Patch(original: AccessTools.Method(typeof(Utility), "getRandomTownNPC", new Type[] { typeof(Random) }),
                                  transpiler: new HarmonyMethod(typeof(AntiSocialManager), "getRandomTownNPC_Transpiler"));
            harmonyInstance.Patch(original: AccessTools.Method(typeof(SocializeQuest), "loadQuestInfo"),
                                  transpiler: new HarmonyMethod(typeof(AntiSocialManager), "loadQuestInfo_Transpiler"));

        }

        private static void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(AssetName))
            {
                e.LoadFrom(() => new Dictionary<string, string>(), AssetLoadPriority.Low);
            }
        }

        public static bool get_CanSocialize_Postfix(
            bool __result,
            NPC __instance)
        {
            try
            {
                if (__result && Game1.content.Load<Dictionary<string, string>>(AssetName).ContainsKey(__instance.Name))
                {
                    // Log($"Overriding CanSocialize for {__instance.Name}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log($"Error in get_CanSocialize postfix patch: {ex}", LogLevel.Error);
            }
            return __result;
        }

        public static IEnumerable<CodeInstruction> getRandomTownNPC_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            try 
            { 
                Log("Patching getRandomTownNPC...", LogLevel.Trace);
                return PatchNPCDispositions(instructions);
            }
            catch (Exception ex)
            {
                Log($"Error in getRandomTownNPC transpiler patch: {ex}", LogLevel.Error);
                return instructions;
            }
        }

        public static IEnumerable<CodeInstruction> loadQuestInfo_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            try
            {
                Log("Patching loadQuestInfo...", LogLevel.Trace);
                return PatchNPCDispositions(instructions);
            }
            catch (Exception ex)
            {
                Log($"Error in loadQuestInfo transpiler patch: {ex}", LogLevel.Error);
                return instructions;
            }
        }

        private static IEnumerable<CodeInstruction> PatchNPCDispositions(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();
            for (int i = 0; i < codes.Count; i++)
            {
                CodeInstruction instr = codes[i];
                //Log($"{instr.opcode} : {instr.operand}");
                if (instr.opcode == OpCodes.Callvirt && instr.operand is MethodInfo method && method.Name == "Load")
                {
                    CodeInstruction prevInstr = codes[i - 1];
                    if (prevInstr.opcode == OpCodes.Ldstr && prevInstr.operand.Equals("Data\\NPCDispositions"))
                    {
                        Log($"Adding call to RemoveAntiSocialNPCs at index {i + 1}");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AntiSocialManager), "RemoveAntiSocialNPCs")));
                    }
                }
            }
            return codes;
        }

        public static Dictionary<string, string> RemoveAntiSocialNPCs(Dictionary<string, string> dict)
        {
            try
            {
                int original = dict.Count;

                foreach ((string k, string _) in Game1.content.Load<Dictionary<string, string>>(AssetName))
                {
                    dict.Remove(k);
                }
                Log($"Initially {original} NPCs, removed anti-social ones, returning {dict.Count}");
                if (dict.Count == 0)
                {
                    Log($"No social NPCs found", LogLevel.Warn);
                }
                return dict;
            }
            catch (Exception ex)
            {
                Log($"Error in RemoveAntiSocialNPCs: {ex}", LogLevel.Error);
            }
            return dict;
        }

        private static void Log(String message, LogLevel level = LogLevel.Trace)
        {
            modInstance?.Monitor.Log((adHoc ? "[AntiSocial] " + message : message), level);
        }
    }
}
