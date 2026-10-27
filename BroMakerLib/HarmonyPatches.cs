using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BroMakerLib.CustomObjects;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.CustomObjects.Projectiles;
using BroMakerLib.Infos;
using BroMakerLib.Loaders;
using BroMakerLib.Loggers;
using BroMakerLib.Storages;
using BroMakerLib.Unlocks;
using HarmonyLib;
using UnityEngine;
using BSett = BroMakerLib.Settings;

namespace BroMakerLib.HarmonyPatches
{
    // Collect Logs
    [HarmonyPatch(typeof(BMLogger), "Log", new Type[] { typeof(string), typeof(LogType), typeof(bool) })]
    static class BMLogger_Log_Patch
    {
        static void Postfix()
        {
            if (Main.enabled)
            {
                Main.Log(BMLogger.logs.Last(), Log.PREFIX);
            }

        }
    }

    [HarmonyPatch(typeof(BMLogger), "Debug")]
    static class BMLogger_Debug_Patch
    {
        static void Postfix()
        {
            if (Main.enabled && BSett.instance.debugLogs)
                Main.Log(BMLogger.debugLogs.Last(), Log.PREFIX);
        }
    }

    // Automatic spawn
    [HarmonyPatch(typeof(Player), "SpawnHero")]
    static class Player_SpawnHero_Patch
    {
        static void Prefix(Player __instance, ref HeroType nextHeroType)
        {
            if (Main.enabled)
            {
                CustomPockettedSpecial.ClearPockettedSpecials(__instance.playerNum);
                if (BSett.instance.overrideNextBroSpawn)
                {
                    LoadHero.willReplaceBro[__instance.playerNum] = true;
                    nextHeroType = HeroType.Rambro;
                    return;
                }
                else if (BSett.instance.disableSpawning)
                {
                    LoadHero.willReplaceBro[__instance.playerNum] = false;
                    return;
                }

                // Force custom bro this level if enabled
                if (BroSpawnManager.ForceCustomThisLevel)
                {
                    LoadHero.willReplaceBro[__instance.playerNum] = true;
                }
                // Don't replace forced bros
                else if (Map.MapData != null && (Map.MapData.forcedBro != HeroType.Random || (Map.MapData.forcedBros != null && Map.MapData.forcedBros.Count() > 0)))
                {
                    LoadHero.willReplaceBro[__instance.playerNum] = false;
                }
                // Handle IronBro spawning
                else if (GameModeController.IsHardcoreMode)
                {
                    if (BSett.instance.automaticSpawn && BroSpawnManager.EnabledBros.Count > 0)
                    {
                        // Check if we're unlocking a bro
                        if (PlayerProgress.Instance.yetToBePlayedUnlockedHeroes.Count() > 0)
                        {
                            if (BSett.instance.onlyCustomInHardcore)
                            {
                                LoadHero.willReplaceBro[__instance.playerNum] = true;

                                PlayerProgress.Instance.yetToBePlayedUnlockedHeroes.Remove(nextHeroType);
                                GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Remove(nextHeroType);
                                if (BroSpawnManager.HardcoreBrosNotYetUnlocked.Count() > 0)
                                {
                                    BroSpawnManager.RescuingHardcoreBro = true;
                                    LoadHero.playCutscene = true;
                                }
                            }
                            else
                            {
                                // Probability of a custom bro being unlocked should be the number of custom bros / (number of custom bros + notUnlocked vanilla bros + dead vanilla bros)
                                // We add 1 because the chosen bro will have already been added to the available bros
                                LoadHero.willReplaceBro[__instance.playerNum] = UnityEngine.Random.value <= (BroSpawnManager.HardcoreBrosNotYetUnlocked.Count() /
                                    (BroSpawnManager.HardcoreBrosNotYetUnlocked.Count() + PlayerProgress.Instance.unlockedHeroes.Count() -
                                    GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Count() - GameState.Instance.currentWorldmapSave.hardcoreModeDeadBros.Count() + 1.0f));

                                // If replacing hero, remove previously unlocked one from available bros
                                if (LoadHero.willReplaceBro[__instance.playerNum])
                                {
                                    PlayerProgress.Instance.yetToBePlayedUnlockedHeroes.Remove(nextHeroType);
                                    GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Remove(nextHeroType);
                                    BroSpawnManager.RescuingHardcoreBro = true;
                                    LoadHero.playCutscene = true;
                                }
                            }
                        }
                        else if (BSett.instance.onlyCustomInHardcore)
                        {
                            // Check if this is the first character being spawned
                            if (BroSpawnManager.HardcoreAvailableBros.Count() == 0)
                            {
                                // We use this function to add a character to the pool to start with
                                BroSpawnManager.RescuingHardcoreBro = true;
                                BroSpawnManager.GetRandomSpawnableBro();
                            }
                            LoadHero.willReplaceBro[__instance.playerNum] = true;
                        }
                        else if (BroSpawnManager.HardcoreAvailableBros.Count() > 0)
                        {
                            LoadHero.willReplaceBro[__instance.playerNum] = UnityEngine.Random.value <= (BroSpawnManager.HardcoreAvailableBros.Count() /
                            ((float)BroSpawnManager.HardcoreAvailableBros.Count() + GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Count()));
                        }
                    }
                }
                // Check if we just unlocked a custom bro
                else if (BroUnlockManager.HasPendingUnlockedBro())
                {
                    LoadHero.willReplaceBro[__instance.playerNum] = true;
                    LoadHero.willPlayCutscene[__instance.playerNum] = true;
                }
                // Handle normal spawning
                else if (BSett.instance.automaticSpawn && BroSpawnManager.EnabledBros.Count > 0)
                {
                    LoadHero.willReplaceBro[__instance.playerNum] = UnityEngine.Random.value <= (BroSpawnManager.CalculateSpawnProbability() / 100.0f);
                }

                if (LoadHero.willReplaceBro[__instance.playerNum])
                {
                    // Ensure player doesn't spawn as boondock bros
                    nextHeroType = HeroType.Rambro;
                }
                LoadHero.playerNum = __instance.playerNum;
            }

        }
        static void Postfix(Player __instance)
        {
            try
            {
                if (Main.enabled && LoadHero.willReplaceBro[__instance.playerNum])
                {
                    LoadHero.willReplaceBro[__instance.playerNum] = false;
                    LoadHero.spawningCustomBro[__instance.playerNum] = true;
                    LoadHero.willPlayCutscene[__instance.playerNum] = false;
                    LoadHero.anyCustomSpawning = true;
                    Storages.StoredHero choice;
                    choice = BroSpawnManager.GetRandomSpawnableBro(true);

                    if (BroSpawnManager.LastSpawnWasUnlock)
                    {
                        var info = choice.GetInfo();
                        var unlockConfig = info?.UnlockConfig;

                        if (info.Cutscene.Count > 0 && info.Cutscene[0].playCutsceneOnFirstSpawn)
                        {
                            LoadHero.playCutscene = true;
                        }
                    }

                    LoadHero.spawnFromPlayer = (__instance.rescuingThisBro != null);

                    TestVanDammeAnim bro = choice.LoadBro(__instance.playerNum);

                    if (LoadHero.playCutscene)
                    {
                        Cutscenes.CustomCutsceneController.LoadHeroCutscene(BroMakerUtilities.GetVariantValue(choice.GetInfo().Cutscene, (bro as CustomHero).CurrentVariant));
                        LoadHero.playCutscene = false;
                    }
                    __instance.changingBroFromTrigger = false;
                    LoadHero.spawningCustomBro[__instance.playerNum] = false;
                    LoadHero.anyCustomSpawning = false;
                    LoadHero.broBeingRescued = false;
                }
            }
            catch (Exception e)
            {
                BMLogger.ExceptionLog(e);
            }
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "RecallBro")]
    static class TestVanDammeAnim_RecallBro_Patch
    {
        static void Postfix(ref bool ___ElgilbleToBeKicked)
        {
            // Recalled remote heroes keep updating with playerNum=5 until they fade out.
            // Their playerBubble is null, so they must no longer be kick candidates.
            ___ElgilbleToBeKicked = false;
        }
    }

    [HarmonyPatch(typeof(InstantiationController), "InstantiateRPC")]
    static class InstantiationController_InstantiateRPC_Patch
    {
        private struct CustomSpawnState
        {
            public bool isCustomBro;
            public bool activateInstance;
            public CustomBroInfo previousInfo;
            public int previousPlayerNum;
        }

        static void Prefix(ref int indexOfPrefab, byte[] instantiationDataAsBytes, out CustomSpawnState __state)
        {
            __state = new CustomSpawnState();
            if (instantiationDataAsBytes == null)
                return;

            object[] instantiationData = TypeSerializer.DeserializeParameterList(instantiationDataAsBytes);
            if (instantiationData.Length < 2 || !(instantiationData[0] is string) || !(instantiationData[1] is string))
                return;

            string marker = (string)instantiationData[0];
            if (marker == LoadHero.NETWORK_PROJECTILE_MARKER)
            {
                GameObject projectilePrefab = FindCustomProjectilePrefab((string)instantiationData[1]);
                if (projectilePrefab == null || !CustomProjectileNetwork.TryRegisterCustomNetworkPrefab(projectilePrefab))
                    return;

                __state.activateInstance = true;
                indexOfPrefab = InstantiationController.GetPrefabIndex(projectilePrefab);
                return;
            }

            if (marker != LoadHero.NETWORK_PREFAB_MARKER || instantiationData.Length < 3 || !(instantiationData[2] is int))
                return;

            StoredHero storedHero;
            string customBroName = (string)instantiationData[1];
            if (!BroMakerStorage.GetStoredHeroByName(customBroName, out storedHero))
                return;

            __state.isCustomBro = true;
            __state.activateInstance = true;
            __state.previousInfo = LoadHero.currentInfo;
            __state.previousPlayerNum = LoadHero.playerNum;
            LoadHero.playerNum = (int)instantiationData[2];
            LoadHero.currentInfo = storedHero.GetInfo();
            GameObject prefab = LoadHero.GetOrCreateNetworkPrefab(LoadHero.currentInfo, PresetManager.GetHeroPreset(LoadHero.currentInfo.CharacterPreset));
            indexOfPrefab = InstantiationController.GetPrefabIndex(prefab);
        }

        static void Postfix(NID networkID, CustomSpawnState __state)
        {
            if (!__state.activateInstance)
                return;

            GameObject instance = Registry.GetObject(networkID) as GameObject;
            if (instance != null)
            {
                instance.SetActive(true);
                Projectile projectile = instance.GetComponent<Projectile>();
                if (projectile != null)
                    projectile.enabled = true;
            }
        }

        static Exception Finalizer(Exception __exception, CustomSpawnState __state)
        {
            if (__state.isCustomBro)
            {
                LoadHero.currentInfo = __state.previousInfo;
                LoadHero.playerNum = __state.previousPlayerNum;
            }
            return __exception;
        }

        private static GameObject FindCustomProjectilePrefab(string typeName)
        {
            foreach (KeyValuePair<Type, CustomProjectile> entry in CustomProjectile.CustomProjectilePrefabs)
            {
                if (MatchesTypeName(entry.Key, typeName))
                    return entry.Value != null ? entry.Value.gameObject : null;
            }

            foreach (KeyValuePair<Type, CustomGrenade> entry in CustomGrenade.CustomGrenadePrefabs)
            {
                if (MatchesTypeName(entry.Key, typeName))
                    return entry.Value != null ? entry.Value.gameObject : null;
            }

            foreach (KeyValuePair<Type, CustomSachelPack> entry in CustomSachelPack.CustomSachelPackPrefabs)
            {
                if (MatchesTypeName(entry.Key, typeName))
                    return entry.Value != null ? entry.Value.gameObject : null;
            }

            return null;
        }

        private static bool MatchesTypeName(Type type, string typeName)
        {
            return type != null && (type.AssemblyQualifiedName == typeName || type.FullName == typeName || type.Name == typeName);
        }
    }

    // Fix vanilla bro being visible for 1 frame in certain spawning situations
    [HarmonyPatch(typeof(Player), "InstantiateHero")]
    static class Player_InstantiateHero_Patch
    {
        static void Postfix(Player __instance, ref TestVanDammeAnim __result)
        {
            // If mod is disabled or if we aren't loading a custom character don't disable
            if (!Main.enabled || !LoadHero.willReplaceBro[__instance.playerNum])
            {
                return;
            }

            __result.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(Map), "AddBroToHeroTransport")]
    static class Map_AddBroToHeroTransport_Patch
    {
        static bool Prefix(Map __instance, ref TestVanDammeAnim Bro)
        {
            // If mod is disabled or if we aren't loading a custom character don't disable
            return !Main.enabled || !LoadHero.willReplaceBro[Bro.playerNum];
        }
    }

    [HarmonyPatch(typeof(EffectsController), "CreateHeroIndicator")]
    static class EffectsController_CreateHeroIndicator_Patch
    {
        static bool Prefix(ref Unit unit)
        {
            if (!Main.enabled)
            {
                return true;
            }
            // Check Unit's pos because sometimes this function is called with a unit that has not had its position set yet
            else if (LoadHero.anyCustomSpawning && (!LoadHero.broBeingRescued || (unit.X <= 0 && unit.Y <= 0)))
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Player), "WorkOutSpawnPosition")]
    static class Player_WorkOutSpawnPosition_Patch
    {
        static void Prefix(Player __instance, ref TestVanDammeAnim bro)
        {
            if (!Main.enabled)
            {
                return;
            }

            if (!LoadHero.spawningCustomBro[__instance.playerNum] && LoadHero.willReplaceBro[__instance.playerNum])
            {
                LoadHero.wasFirstDeployment[__instance.playerNum] = __instance.firstDeployment;
            }
        }
    }

    [HarmonyPatch(typeof(Player), "WorkOutSpawnScenario")]
    static class Player_WorkOutSpawnScenario_Patch
    {
        static void Postfix(Player __instance, ref Player.SpawnType __result)
        {
            if (!Main.enabled)
            {
                return;
            }
            // Store spawning info of normal character so we can pass it on to the custom character
            else if (LoadHero.willReplaceBro[__instance.playerNum])
            {
                LoadHero.previousSpawnInfo[__instance.playerNum] = __result;
                LoadHero.broBeingRescued = __result == Player.SpawnType.RespawnAtRescueBro;
            }
            // Replace custom characters spawning info with stored info
            else if (LoadHero.spawningCustomBro[__instance.playerNum])
            {
                __result = LoadHero.previousSpawnInfo[__instance.playerNum];
            }
        }
    }

    [HarmonyPatch(typeof(Player), "SetSpawnPositon")]
    static class Player_SetSpawnPositon_Patch
    {
        public static void Prefix(Player __instance, ref TestVanDammeAnim bro)
        {
            if (!Main.enabled || !LoadHero.willReplaceBro[__instance.playerNum] || LoadHero.spawningCustomBro[__instance.playerNum])
            {
                return;
            }

            // Prevent yeah sound from playing for replaced bro
            if (bro != null)
            {
                __instance.firstDeployment = true;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerHUD), "SetGrenadeMaterials", new Type[] { typeof(HeroType) })]
    static class PlayerHUD_SetGrenadeMaterials_Patch
    {
        public static bool Prefix(PlayerHUD __instance, ref HeroType type, int ___playerNum)
        {
            if (!Main.enabled)
            {
                return true;
            }

            int playerNum = ___playerNum;
            TestVanDammeAnim currentCharacter = HeroController.players[playerNum].character;
            if (currentCharacter is ICustomHero)
            {
                ICustomHero customHero = currentCharacter as ICustomHero;
                var materials = customHero.CurrentSpecialMaterials;

                if (materials != null && materials.Count > 0)
                {
                    var offset = customHero.CurrentSpecialMaterialOffset;
                    var spacing = customHero.CurrentSpecialMaterialSpacing;

                    BroMakerUtilities.SetSpecialMaterials(playerNum, materials, offset, spacing);
                    return false;
                }
            }
            else
            {
                for (int i = 0; i < __instance.grenadeIcons.Length; i++)
                {
                    __instance.grenadeIcons[i].SetOffset(new Vector3(0f, 0f, 0f));
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(HeroController), "GetHeroType")]
    static class HeroController_GetHeroType_Patch
    {
        public static bool Prefix(HeroController __instance, ref HeroType __result)
        {
            if (!Main.enabled)
            {
                return true;
            }

            // If there are no available vanilla bros but still more custom bros, make sure the herotype is set to rambro so the game still tries to spawn the player in
            if (GameModeController.IsHardcoreMode && GameState.Instance.currentWorldmapSave != null && GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Count() == 0 && BroSpawnManager.HardcoreAvailableBros.Count() > 0)
            {
                __result = HeroType.Rambro;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(SaveSlotsMenu), "SelectSlot")]
    static class SaveSlotsMenu_SelectSlot_Patch
    {
        public static void Prefix(SaveSlotsMenu __instance, ref int slot)
        {
            if (!Main.enabled)
            {
                return;
            }

            if (SaveSlotsMenu.createNewGame)
            {
                try
                {
                    // If a new save is being created, remake IronBro lists
                    BroSpawnManager.CreateHardcoreLists(slot);
                }
                catch (Exception e)
                {
                    BMLogger.ExceptionLog(e);
                }
            }
        }
    }

    // Ensure we can't gain more lives than we should be able to in IronBro, to prevent the game from softlocking
    [HarmonyPatch(typeof(Player), "AddLife")]
    static class Player_AddLife_Patch
    {
        public static bool Prefix(Player __instance)
        {
            if (!Main.enabled)
            {
                return true;
            }

            if (GameModeController.IsHardcoreMode)
            {
                if (BSett.instance.onlyCustomInHardcore && BroSpawnManager.HardcoreBrosNotYetUnlocked.Count() == 0)
                {
                    return false;
                }
                else if (BroSpawnManager.HardcoreBrosNotYetUnlocked.Count() +
                    GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Count() + PlayerProgress.Instance.yetToBePlayedUnlockedHeroes.Count() == 0)
                {
                    return false;
                }
            }
            return true;
        }
    }

    // Ensure we don't remove other characters if we die with a custom character in IronBro
    [HarmonyPatch(typeof(Player), "RemoveLife")]
    static class Player_RemoveLife_Patch
    {
        public static bool Prefix(Player __instance)
        {
            if (!Main.enabled)
            {
                return true;
            }

            if (GameModeController.IsHardcoreMode && __instance.character is ICustomHero)
            {
                ICustomHero customHero = (__instance.character as ICustomHero);
                string broName = customHero.Info.name;
                BroSpawnManager.HardcoreAvailableBros.Remove(broName);
                __instance.Lives--;
                return false;
            }
            return true;
        }
    }

    // Count custom bros for this function so that cages still spawn on the map
    [HarmonyPatch(typeof(HeroUnlockController), "AreAnyMoreBrosAvailableToBeSavedInHardcoreMode")]
    static class HeroUnlockController_AreAnyMoreBrosAvailableToBeSavedInHardcoreMode_Patch
    {
        public static void Postfix(HeroUnlockController __instance, ref bool __result)
        {
            if (!Main.enabled)
            {
                return;
            }

            if (BSett.instance.onlyCustomInHardcore)
            {
                __result = BroSpawnManager.HardcoreBrosNotYetUnlocked.Count() > 0;
            }
            else
            {
                __result = __result || BroSpawnManager.HardcoreBrosNotYetUnlocked.Count() > 0;
            }
        }
    }

    // Fix being unable to pilot mech
    [HarmonyPatch(typeof(Unit), "PilotUnit")]
    static class Unit_PilotUnit_Patch
    {
        public static bool Prefix(Unit __instance, ref Unit pilotUnit)
        {
            if (!Main.enabled)
            {
                return true;
            }

            // Check CanPilotUnit to ensure custom bros can't pilot things they're not supposed to be able to
            if (pilotUnit is ICustomHero && __instance.CanPilotUnit(pilotUnit.playerNum))
            {
                __instance.PilotUnitRPC(pilotUnit);
                return false;
            }

            return true;
        }
    }

    // Fix being unable to enter worm tunnel
    [HarmonyPatch(typeof(AssMouthOrifice), "TryConsumeObject")]
    static class AssMouthOrifice_TryConsumeObject_Patch
    {
        public static int customObjectIsConsumed = 0;
        public static void Postfix(AssMouthOrifice __instance, ref BroforceObject obj, ref bool __result, ref bool ___playSwallowAnim, ref float ___swallowFrameTimer, ref int ___swallowFrame, List<BroforceObject> ___consumedThisFrame)
        {
            if (!Main.enabled)
            {
                return;
            }

            if (__result && (obj is ICustomHero || obj is ICustomProjectile))
            {
                ++customObjectIsConsumed;
                ___playSwallowAnim = true;
                ___swallowFrameTimer = 0f;
                ___swallowFrame = 0;
                AssMouthTransportWrapper assMouthTransportWrapper = UnityEngine.Object.Instantiate<AssMouthTransportWrapper>(__instance.wrapperPrefab);
                assMouthTransportWrapper.Setup(obj, __instance.root);
                ___consumedThisFrame.Add(obj);
                __instance.open = true;
                if (__instance.enterSound)
                {
                    Sound.GetInstance().PlaySoundEffect(__instance.enterSound, 0.7f);
                }
            }

        }
    }

    // Fix being unable to exit worm tunnel
    [HarmonyPatch(typeof(AssMouthTransportWrapper), "RunAssMouthMovement")]
    static class AssMouthTransportWrapper_RunAssMouthMovement_Patch
    {
        public static void Postfix(AssMouthTransportWrapper __instance, object ___transportedObject, AssMouthBlock ___CurrentAssMouthBlock, AssMouthBlock ___PrevAssMouthBlock)
        {
            if (!Main.enabled)
            {
                return;
            }

            if (AssMouthOrifice_TryConsumeObject_Patch.customObjectIsConsumed > 0)
            {
                if (___CurrentAssMouthBlock == null && (___transportedObject is ICustomHero || ___transportedObject is ICustomProjectile))
                {
                    __instance.ExitAssMouth(___PrevAssMouthBlock.orificeInstance);
                    --AssMouthOrifice_TryConsumeObject_Patch.customObjectIsConsumed;
                }
            }
        }
    }

    // Fix being unable to be impaled by spikes
    [HarmonyPatch(typeof(Impaler), "ImpaleUnit")]
    static class Impaler_ImpaleUnit_Patch
    {
        public static bool Prefix(Impaler __instance, ref TestVanDammeAnim unit)
        {
            if (!Main.enabled)
            {
                return true;
            }

            if (unit.impaledByTransform == null && unit is ICustomHero)
            {
                __instance.ImpaleUnitRPC(unit);
                return false;
            }

            return true;
        }
    }

    // Fix being unable to pickup items
    [HarmonyPatch(typeof(PickupableController), "UsePickupables")]
    static class PickupableController_UsePickupables_Patch
    {
        public static bool Prefix(ref TestVanDammeAnim self, ref float range, ref float x, ref float y, ref bool onlyAmmo)
        {
            if (!Main.enabled)
            {
                return true;
            }

            if (self is ICustomHero)
            {
                List<Pickupable> pickupables = Traverse.Create(typeof(PickupableController)).GetFieldValue("pickupables") as List<Pickupable>;
                for (int i = pickupables.Count - 1; i >= 0; i--)
                {
                    Pickupable pickupable = pickupables[i];
                    if (pickupable != null && (pickupable.pickupType == PickupType.Ammo || !onlyAmmo))
                    {
                        float f = pickupable.X - x;
                        if (Mathf.Abs(f) - range < pickupable.collectionRadius)
                        {
                            float f2 = pickupable.Y + pickupable.yOffset - y;
                            if (Mathf.Abs(f2) - range < pickupable.collectionRadius && pickupable.pickupDelay <= 0f && !pickupable.collected)
                            {
                                if (pickupable.pickupType == PickupType.FlexAirJump || pickupable.pickupType == PickupType.FlexGoldenLight || pickupable.pickupType == PickupType.FlexInvulnerability ||
                                    pickupable.pickupType == PickupType.FlexTeleport || pickupable.pickupType == PickupType.FlexAlluring)
                                {
                                    pickupable.AddFlexPowerRPC(self, pickupable.pickupType, false);
                                }
                                else
                                {
                                    pickupable.Collect(self);
                                }
                            }
                        }
                    }
                }
                return false;
            }

            return true;
        }
    }

    // Fix default character avatar's being overwritten by custom ones
    [HarmonyPatch(typeof(HeroController), "SwitchAvatarMaterial")]
    static class HeroController_SwitchAvatarMaterial_Patch
    {
        public static bool Prefix(ref SpriteSM sprite, ref bool __result)
        {
            if (!Main.enabled)
            {
                return true;
            }

            if (LoadHero.tryReplaceAvatar && HeroController.players[LoadHero.playerNum].character is ICustomHero)
            {
                LoadHero.tryReplaceAvatar = false;

                ICustomHero customHero = HeroController.players[LoadHero.playerNum].character as ICustomHero;
                Material mat = customHero.CurrentFirstAvatar;

                if (mat != null)
                {
                    sprite.GetComponent<Renderer>().material = mat;

                    __result = false;
                    return false;
                }
            }

            return true;
        }
    }

    // Apply the custom avatar after the vanilla hero setup has selected the base hero avatar.
    [HarmonyPatch(typeof(TestVanDammeAnim), "SetUpHero", new Type[] { typeof(int), typeof(HeroType), typeof(bool) })]
    static class TestVanDammeAnim_SetUpHero_Patch
    {
        public static void Postfix(TestVanDammeAnim __instance, int PlayerNum)
        {
            if (!Main.enabled || !(__instance is ICustomHero))
            {
                return;
            }

            ICustomHero customHero = __instance as ICustomHero;
            if (customHero.CurrentFirstAvatar != null)
            {
                HeroController.SetAvatarMaterial(PlayerNum, customHero.CurrentFirstAvatar);
            }
        }
    }

    // Fix custom bros having wrong sprite show up on death screen
    [HarmonyPatch(typeof(GameModeController), "Start")]
    static class GameModeController_Start_Patch
    {
        public static void Prefix()
        {
            if (!Main.enabled)
            {
                return;
            }

            LoadHero.customBroDeaths = new Dictionary<int, CustomBroDeath>();
        }
    }

    // Fix custom bros having wrong sprite show up on death screen
    [HarmonyPatch(typeof(StatisticsController), "NotifyMookDeathType", new Type[] { typeof(TestVanDammeAnim), typeof(DeathType) })]
    static class StatisticsController_NotifyMookDeathType_Patch
    {
        public static void Prefix(ref TestVanDammeAnim vanDamme, ref DeathType deathType)
        {
            if (!Main.enabled || GameModeController.LevelFinished)
            {
                return;
            }

            if (vanDamme is ICustomHero && deathType != DeathType.None)
            {
                ICustomHero customHero = vanDamme as ICustomHero;
                LoadHero.customBroDeaths.Add(SingletonNetObj<StatisticsController>.Instance.currentStats.deathList.Count,
                    new CustomBroDeath(customHero.Info, customHero.CurrentVariant));
            }
        }
    }

    // Fix custom bros having wrong sprite show up on death screen
    [HarmonyPatch(typeof(LevelOverScreen), "AddDeath")]
    static class LevelOverScreen_AddDeath_Patch
    {
        public static bool Prefix(LevelOverScreen __instance, ref int deathNum, ref int totalDeaths, ref Transform parent, ref ShakeM shakeObject)
        {
            if (!Main.enabled)
            {
                return true;
            }

            if (LoadHero.customBroDeaths.ContainsKey(deathNum))
            {
                int num = 40 + totalDeaths / 6;
                int num2 = 1 + totalDeaths / num;
                int num3 = deathNum / num;
                int num4 = deathNum - num3 * num;
                float num5 = 190f / (float)Mathf.Min(num, totalDeaths);
                float num6 = (num2 > 1) ? (50f / ((float)num2 + 0.85f)) : 0f;
                if (num5 > 16f)
                {
                    num5 = 16f;
                }
                float num7 = -num5 * (float)(Mathf.Min(num, totalDeaths) - 1) / 2f;
                float num8 = (num2 > 1) ? (num6 * ((float)(num2 - 1) / 2f) + 10f) : 10f;
                DeathObject deathObject = StatisticsController.GetDeathObject(deathNum);
                Vector3 position = __instance.mookDeathsHolder.transform.position + new Vector3(num7 + (float)num4 * num5, num8 - (float)num3 * num6, 35f * (float)deathNum / (float)totalDeaths - (float)(num3 * 45));

                if (deathObject != null)
                {
                    VictoryMookDeath victoryMookDeath = UnityEngine.Object.Instantiate<VictoryMookDeath>(__instance.broDeathGenericPrefab, position, Quaternion.identity);
                    CustomBroDeath broDeath = LoadHero.customBroDeaths[deathNum];
                    CustomBroInfo bro = broDeath.info;
                    int variant = broDeath.variantIndex;

                    victoryMookDeath.Setup(deathObject, 0.2f, parent, shakeObject);

                    string spritePath = BroMakerUtilities.GetVariantValue(bro.SpritePath, variant);
                    string gunSpritePath = BroMakerUtilities.GetVariantValue(bro.GunSpritePath, variant);
                    Vector2 gunOffset = BroMakerUtilities.GetVariantValue(bro.GunSpriteOffset, variant);

                    victoryMookDeath.GetComponent<MeshRenderer>().material = ResourcesController.GetMaterial(bro.path, spritePath);
                    victoryMookDeath.gunSprite.GetComponent<MeshRenderer>().material = ResourcesController.GetMaterial(bro.path, gunSpritePath);
                    victoryMookDeath.gunSprite.SetOffset(gunOffset.x, gunOffset.y, 0);
                }
                return false;
            }

            return true;
        }
    }

    // Fix avatars flashing for custom bros, for some reason vanilla bros don't have flashing avatars, other than the boondock bros
    [HarmonyPatch(typeof(HeroController), "FlashAvatar")]
    static class HeroController_FlashAvatar_Patch
    {
        public static bool Prefix(ref int playerNum)
        {
            if (!Main.enabled || !BSett.instance.disableCustomAvatarFlash)
            {
                return true;
            }

            if (playerNum < 0 || playerNum > 0 || HeroController.players[playerNum] == null)
            {
                return true;
            }

            return !(HeroController.players[playerNum].character is ICustomHero);
        }
    }

    // Keep custom grenades networked while giving remote peers the matching prefab.
    [HarmonyPatch(typeof(ProjectileController), "SpawnGrenadeOverNetwork")]
    static class ProjectileController_SpawnGrenadeOverNetwork_Patch
    {
        public static bool Prefix(ref Grenade grenadePrefab, ref MonoBehaviour firedBy, ref float x, ref float y, ref float radius, ref float force, ref float xI, ref float yI, ref int playerNum, ref float lifeM, ref Grenade __result)
        {
            if (!Main.enabled || !(firedBy is ICustomHero) || !(grenadePrefab is ICustomProjectile))
            {
                return true;
            }

            if (Connect.IsOffline || !CustomProjectileNetwork.TryRegisterCustomNetworkPrefab(grenadePrefab.gameObject))
            {
                __result = ProjectileController.SpawnGrenadeLocally(grenadePrefab, firedBy, x, y, radius, force, xI, yI,
                    playerNum, UnityEngine.Random.Range(0, 10000));
                if (__result != null && lifeM < 1f)
                    __result.ReduceLife(lifeM);
                return false;
            }

            object[] instantiationData = CustomProjectileNetwork.CreateCustomProjectileInstantiationData(grenadePrefab);
            if (instantiationData == null)
            {
                __result = ProjectileController.SpawnGrenadeLocally(grenadePrefab, firedBy, x, y, radius, force, xI, yI,
                    playerNum, UnityEngine.Random.Range(0, 10000));
                if (__result != null && lifeM < 1f)
                    __result.ReduceLife(lifeM);
                return false;
            }

            GameObject gameObject = Networking.Networking.Instantiate<GameObject>(grenadePrefab.gameObject,
                new Vector3(x, y, 0f), Quaternion.identity, instantiationData, false);
            if (gameObject == null)
                return true;

            gameObject.SetActive(true);
            __result = gameObject.GetComponent<Grenade>();
            if (__result == null)
                return true;
            __result.enabled = true;

            int seed = UnityEngine.Random.Range(0, 10000);
            Networking.Networking.RPC<int, int, MonoBehaviour>(PID.TargetAll,
                new RpcSignature<int, int, MonoBehaviour>(__result.SetupGrenade), seed, playerNum, firedBy, false);
            Networking.Networking.RPC<float, float, float, float>(PID.TargetAll,
                new RpcSignature<float, float, float, float>(__result.Launch), x, y, xI, yI, false);
            if (lifeM < 1f)
            {
                Networking.Networking.RPC<float>(PID.TargetAll,
                    new RpcSignature<float>(__result.ReduceLife), lifeM, false);
            }
            Networking.Networking.RPC<PID>(PID.TargetAll,
                new RpcSignature<PID>(__result.NetworkSetup), PID.MyID, false);
            return false;
        }
    }

    // Keep custom projectiles networked while giving remote peers the matching prefab.
    [HarmonyPatch(typeof(ProjectileController), "SpawnProjectileOverNetwork")]
    static class ProjectileController_SpawnProjectileOverNetwork_Patch
    {
        public static bool Prefix(ref Projectile prefab, ref MonoBehaviour FiredBy, ref float x, ref float y, ref float xI, ref float yI, ref bool synced, ref int playerNum, ref bool AddTemporaryPlayerTarget, ref bool executeImmediately, ref float _zOffset, ref Projectile __result)
        {
            if (!Main.enabled || !(FiredBy is ICustomHero) || !(prefab is ICustomProjectile))
            {
                return true;
            }

            if (Connect.IsOffline || !CustomProjectileNetwork.TryRegisterCustomNetworkPrefab(prefab.gameObject))
            {
                __result = ProjectileController.SpawnProjectileLocally(prefab, FiredBy, x, y, xI, yI,
                    playerNum, AddTemporaryPlayerTarget, _zOffset);
                if (__result != null)
                    __result.SetSeed(UnityEngine.Random.Range(-10000, 10000));
                return false;
            }

            object[] instantiationData = CustomProjectileNetwork.CreateCustomProjectileInstantiationData(prefab);
            if (instantiationData == null)
            {
                __result = ProjectileController.SpawnProjectileLocally(prefab, FiredBy, x, y, xI, yI,
                    playerNum, AddTemporaryPlayerTarget, _zOffset);
                if (__result != null)
                    __result.SetSeed(UnityEngine.Random.Range(-10000, 10000));
                return false;
            }

            GameObject gameObject = Networking.Networking.Instantiate<GameObject>(prefab.gameObject,
                instantiationData, executeImmediately);
            if (gameObject == null)
                return true;

            gameObject.SetActive(true);
            __result = gameObject.GetComponent<Projectile>();
            if (__result == null)
                return true;
            __result.enabled = true;

            Networking.Networking.RPC<float, float, float, float, float, int, MonoBehaviour>(PID.TargetAll,
                new RpcSignature<float, float, float, float, float, int, MonoBehaviour>(__result.Fire),
                x, y, xI, yI, _zOffset, playerNum, FiredBy, executeImmediately);
            Networking.Networking.RPC<int>(PID.TargetAll,
                new RpcSignature<int>(__result.SetSeed), UnityEngine.Random.Range(-10000, 10000), executeImmediately);
            if (synced)
            {
                __result.EnableSyncing(true, executeImmediately);
            }
            if (AddTemporaryPlayerTarget)
            {
                Networking.Networking.RPC<int, Transform>(PID.TargetAll,
                    new RpcSignature<int, Transform>(HeroController.AddTemporaryPlayerTarget), playerNum, __result.transform, executeImmediately);
            }
            return false;
        }
    }

    static class CustomProjectileNetwork
    {
        public static object[] CreateCustomProjectileInstantiationData(UnityEngine.Object prefab)
        {
            if (!(prefab is ICustomProjectile))
                return null;

            Type type = prefab.GetType();
            if (type == null)
                return null;

            return new object[] { LoadHero.NETWORK_PROJECTILE_MARKER, type.AssemblyQualifiedName };
        }

        public static bool TryRegisterCustomNetworkPrefab(GameObject prefab)
        {
            if (prefab == null || InstantiationController.PrefabList == null)
                return false;

            if (!InstantiationController.PrefabList.Contains(prefab))
                InstantiationController.PrefabList.Add(prefab);

            return InstantiationController.GetPrefabIndex(prefab) >= 0;
        }
    }

    // Ensures custom projectiles are activated after instantiation.
    [HarmonyPatch(typeof(ProjectileController), "SpawnGrenadeLocally")]
    static class ProjectileController_SpawnGrenadeLocally_Patch
    {
        public static bool Prefix(ref Grenade grenadePrefab, ref MonoBehaviour firedBy, ref float x, ref float y, ref float radius, ref float force, ref float xI, ref float yI, ref int playerNum, ref int seed, ref Grenade __result)
        {
            if (!Main.enabled || !(grenadePrefab is ICustomProjectile))
            {
                return true;
            }

            GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(grenadePrefab.gameObject);
            gameObject.SetActive(true);
            __result = gameObject.GetComponent<Grenade>();
            __result.enabled = true;
            __result.SetupGrenade(seed, playerNum, firedBy);
            __result.Launch(x, y, xI, yI);
            __result.NetworkSetup(PID.MyID);
            return false;
        }
    }

    // Ensures custom projectiles are activated after instantiation.
    [HarmonyPatch(typeof(ProjectileController), "SpawnProjectileLocally", new Type[] { typeof(Projectile), typeof(MonoBehaviour), typeof(float), typeof(float), typeof(float), typeof(float), typeof(int) })]
    static class ProjectileController_SpawnProjectileLocally_Patch
    {
        public static bool Prefix(ref Projectile projectilePrefab, ref MonoBehaviour FiredBy, ref float x, ref float y, ref float xI, ref float yI, ref int playerNum, ref Projectile __result)
        {
            if (!Main.enabled || !(projectilePrefab is ICustomProjectile))
            {
                return true;
            }

            __result = UnityEngine.Object.Instantiate<Projectile>(projectilePrefab, new Vector3(x, y, 0f), Quaternion.identity);
            __result.gameObject.SetActive(true);
            __result.enabled = true;
            __result.Fire(x, y, xI, yI, 0f, playerNum, FiredBy);
            return false;
        }
    }

    // Ensures custom projectiles are activated after instantiation.
    [HarmonyPatch(typeof(ProjectileController), "SpawnProjectileLocally", new Type[] { typeof(Projectile), typeof(MonoBehaviour), typeof(float), typeof(float), typeof(float), typeof(float), typeof(int), typeof(bool), typeof(float) })]
    static class ProjectileController_SpawnProjectileLocally2_Patch
    {
        public static bool Prefix(ref Projectile prefab, ref MonoBehaviour FiredBy, ref float x, ref float y, ref float xI, ref float yI, ref int playerNum, ref bool AddTemporaryPlayerTarget, ref float _zOffset, ref Projectile __result)
        {
            if (!Main.enabled || !(prefab is ICustomProjectile))
            {
                return true;
            }

            __result = UnityEngine.Object.Instantiate<Projectile>(prefab, new Vector3(x, y, 0f), Quaternion.identity);
            __result.gameObject.SetActive(true);
            __result.enabled = true;
            __result.Fire(x, y, xI, yI, _zOffset, playerNum, FiredBy);
            return false;
        }
    }

    // Ensures custom projectiles are activated after instantiation.
    [HarmonyPatch(typeof(ProjectileController), "SpawnProjectileLocally", new Type[] { typeof(Projectile), typeof(MonoBehaviour), typeof(float), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(int), typeof(bool), typeof(bool), typeof(float) })]
    static class ProjectileController_SpawnProjectileLocally3_Patch
    {
        public static bool Prefix(ref Projectile prefab, ref MonoBehaviour FiredBy, ref float x, ref float y, ref float xI, ref float yI, ref bool synced, ref int playerNum, ref bool AddTemporaryPlayerTarget, ref bool executeImmediately, ref float _zOffset, ref Projectile __result)
        {
            if (!Main.enabled || !(prefab is ICustomProjectile))
            {
                return true;
            }

            __result = UnityEngine.Object.Instantiate(prefab, new Vector3(x, y, 0f), Quaternion.identity);
            __result.gameObject.SetActive(true);
            __result.enabled = true;
            __result.Fire(x, y, xI, yI, _zOffset, playerNum, FiredBy);
            return false;
        }
    }

    // Handle custom pocketted special ammo
    [HarmonyPatch(typeof(BroBase), "SetPlayerHUDAmmo")]
    static class BroBase_SetPlayerHUDAmmo_Patch
    {
        public static bool Prefix(ref BroBase __instance)
        {
            if (!Main.enabled)
            {
                return true;
            }

            if (__instance.pockettedSpecialAmmo.Count > 0 && __instance.pockettedSpecialAmmo[__instance.pockettedSpecialAmmo.Count - 1] == PockettedSpecialAmmoType.None && CustomPockettedSpecial.PockettedSpecials[__instance.playerNum].Count > 0)
            {
                CustomPockettedSpecial.PockettedSpecials[__instance.playerNum].Last().SetSpecialMaterials(__instance);
                __instance.player.hud.SetGrenades(1);
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(BroBase), "StartPockettedSpecial")]
    static class BroBase_StartPockettedSpecial_Patch
    {
        public static void Postfix(ref BroBase __instance, ref PockettedSpecialAmmoType ___usingPockettedSpecialType)
        {
            if (!Main.enabled)
            {
                return;
            }

            if (__instance.pockettedSpecialAmmo.Count > 0 && __instance.pockettedSpecialAmmo[__instance.pockettedSpecialAmmo.Count - 1] == PockettedSpecialAmmoType.None && CustomPockettedSpecial.PockettedSpecials[__instance.playerNum].Count > 0)
            {
                // If the pocketted special uses the throwing animation then we need to set the usingPockettedSpecialType to something other than None, which defaults to the flex animation
                if (CustomPockettedSpecial.PockettedSpecials[__instance.playerNum].Last().UseThrowingAnimation())
                {
                    ___usingPockettedSpecialType = PockettedSpecialAmmoType.Airstrike;
                }
            }
        }
    }

    [HarmonyPatch(typeof(BroBase), "UsePockettedSpecial")]
    static class BroBase_UsePockettedSpecial_Patch
    {
        public static bool Prefix(ref BroBase __instance, ref int ___pressSpecialFacingDirection)
        {
            if (!Main.enabled)
            {
                return true;
            }

            if (__instance.pockettedSpecialAmmo.Count > 0 && __instance.pockettedSpecialAmmo[__instance.pockettedSpecialAmmo.Count - 1] == PockettedSpecialAmmoType.None && CustomPockettedSpecial.PockettedSpecials[__instance.playerNum].Count > 0)
            {
                ___pressSpecialFacingDirection = 0;
                CustomPockettedSpecial special = CustomPockettedSpecial.PockettedSpecials[__instance.playerNum].Last();
                special.UseSpecial(__instance);
                CustomPockettedSpecial.PockettedSpecials[__instance.playerNum].Remove(special);
                __instance.pockettedSpecialAmmo.RemoveAt(__instance.pockettedSpecialAmmo.Count - 1);

                // Reset bro's specials to original count if this pocketted special allows it
                if (special.RefreshAmmo())
                {
                    __instance.ResetSpecialAmmo();
                }

                // Call private method SetPlayerHUDAmmo
                typeof(BroBase).GetMethod("SetPlayerHUDAmmo", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { });
                return false;
            }

            return true;
        }
    }
}
