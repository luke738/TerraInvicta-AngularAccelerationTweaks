using System;
using System.Collections.Generic;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using UnityEngine;
using UnityModManagerNet;

namespace AngularAccelerationTweaks
{
    public class AngularAccelerationTweaks
    {
        private static bool Load(UnityModManager.ModEntry modEntry)
        {
            new Harmony(modEntry.Info.Id).PatchAll();
            modEntry.OnToggle = AngularAccelerationTweaks.OnToggle;
            FileLog.Log("[SpikersBoostAngularAcceleration] Loaded");
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            AngularAccelerationTweaks.enabled = value;
            return true;
        }

        public static bool enabled;
    }

    public class AngularAccelerationCalcHelper
    {
        public static float CalculateAngularAccelerationMultiplier(TISpaceShipTemplate template)
        {
            // Get multiplier from spikers
            float spiker_multiplier = 1f;
            foreach (ModuleDataEntry moduleData in template.utilityModules)
            {
                TIUtilityModuleTemplate ref_utilityModule = moduleData.moduleTemplate.ref_utilityModule;
                if (ref_utilityModule != null && ref_utilityModule.thrustMultiplier != 0f)
                {
                    spiker_multiplier *= ref_utilityModule.thrustMultiplier;
                }
            }

            // Get hull mass as a fraction of corvette hull mass (hard-coded), minimum 1 to avoid nerf to gunships and escorts
            float mass_ratio = Math.Max(template.hullTemplate.mass_tons / 300f, 1f);

            return spiker_multiplier * mass_ratio;
        }
    }

    [HarmonyPatch(typeof(TISpaceShipState), "GetUndamagedAngularAcceleration_rads2")]
    public class AngularAccelerationCalculationPatch
    {
        private static void Postfix(ref float __result, TISpaceShipState __instance)
        {
            if (AngularAccelerationTweaks.enabled)
            {
                float multiplier = AngularAccelerationCalcHelper.CalculateAngularAccelerationMultiplier(__instance.template);

                // Scale angular accel by spiker multiplier and hull mass
                FileLog.Log(string.Format("[SpikersBoostAngularAcceleration] Game set {0} undamaged, replaced with {1}", __result, __result* multiplier));
                __result *= multiplier;
            }
        }
    }

    [HarmonyPatch(typeof(TISpaceShipTemplate), "get_baseAngularAcceleration_rads2")]
    public class AngularAccelerationBaseCalculationPatch
    {
        private static void Postfix(ref float __result, TISpaceShipTemplate __instance)
        {
            if (AngularAccelerationTweaks.enabled)
            {
                float multiplier = AngularAccelerationCalcHelper.CalculateAngularAccelerationMultiplier(__instance);

                // Scale angular accel by spiker multiplier and hull mass
                FileLog.Log(string.Format("[SpikersBoostAngularAcceleration] Game set {0} base, replaced with {1}", __result, __result * multiplier));
                __result *= multiplier;
            }
        }
    }

    [HarmonyPatch(typeof(TISpaceShipState), "UpdatePropulsionValues")]
    public class UpdatePropulsionValuesPatch
    {
        private static void Prefix(TISpaceShipState __instance, out Dictionary<string, float> __state)
        {
            __state = new Dictionary<string, float>
            {
                ["cruiseAcceleration_mps"] = __instance.cruiseAcceleration_mps2,
                ["combatAcceleration_mps"] = __instance.combatAcceleration_mps2,
                ["angular_acceleration_rads2"] = __instance.angular_acceleration_rads2,
                ["currentMaxDeltaV_kps"] = __instance.currentMaxDeltaV_kps
            };
        }
        private static void Postfix(TISpaceShipState __instance, Dictionary<string, float> __state)
        {
            if (AngularAccelerationTweaks.enabled)
            {
                // Get multiplier from offline spikers
                float spiker_multiplier = 1f;
                foreach (ModuleDataEntry moduleData in __instance.utilityModules)
                {
                    TIUtilityModuleTemplate ref_utilityModule = moduleData.moduleTemplate.ref_utilityModule;
                    if (ref_utilityModule != null && ref_utilityModule.thrustMultiplier != 0f && __instance.GetPartDamage(moduleData) > 0f)
                    {
                        spiker_multiplier *= ref_utilityModule.thrustMultiplier;
                    }
                }

                // Get species g-limit to use in finding maximum angular velocity
                float g_limit;
                if (__instance.isAlien)
                {
                    g_limit = TemplateManager.global.maxAlienCombatAcceleration_g * 9.80665f;
                }
                else
                {
                    g_limit = TemplateManager.global.baselineMaxHumanCombatAcceleration_g * 9.80665f;
                }
                float angular_velocity_limit_rads = Mathf.Sqrt(g_limit / (__instance.template.hullTemplate.length_m / 2));

                Traverse traverse = Traverse.Create(__instance);

                // Adjust angular acceleration computed by original function
                float old_angular_acceleration_rads2 = __instance.angular_acceleration_rads2;
                float new_angular_acceleration_rads2 = __instance.angular_acceleration_rads2 / spiker_multiplier;

                // Compute max angular velocity again but cap at species g-limit
                float new_max_angular_velocity_rads = Mathf.Max(new_angular_acceleration_rads2 * 5f, angular_velocity_limit_rads);

                // Update ship object
                FileLog.Log(string.Format("[SpikersBoostAngularAcceleration] Game set {0}rad/s^2, {1}rad/s w/damage, replaced with {2}rad/s^2, {3}rad/s", __instance.angular_acceleration_rads2, __instance.max_angular_velocity_rads2, new_angular_acceleration_rads2, new_max_angular_velocity_rads));
                traverse.Property("angular_acceleration_rads2").SetValue(new_angular_acceleration_rads2);
                traverse.Property("max_angular_velocity_rads2").SetValue(new_max_angular_velocity_rads);

                bool angular_accel_changed = Mathf.Abs(__state["angular_acceleration_rads2"] - new_angular_acceleration_rads2) >= __instance.minimumAngularAcceleration_rads2;
                angular_accel_changed &= Mathf.Abs(__state["angular_acceleration_rads2"] - old_angular_acceleration_rads2) < __instance.minimumAngularAcceleration_rads2;
                if (GameControl.spaceCombat == null && angular_accel_changed && !((Mathf.Abs(__state["cruiseAcceleration_mps"] - __instance.cruiseAcceleration_mps2) >= 1E-45f || Mathf.Abs(__state["currentMaxDeltaV_kps"] - __instance.currentMaxDeltaV_kps) >= 1E-45f)))
                {
                    GameControl.eventManager.TriggerEvent(new ShipPropulsionValuesUpdated(__instance), null, new object[]
                    {
                        __instance
                    });
                }
                else if (GameControl.spaceCombat != null && angular_accel_changed && !(Mathf.Abs(__state["combatAcceleration_mps"] - __instance.combatAcceleration_mps2) >= 1E-45f))
                {
                    GameControl.eventManager.TriggerEvent(new CombatShipPropulsionValuesUpdated(__instance), null, new object[]
                    {
                        __instance
                    });
                }
            }
        }
    }
}
