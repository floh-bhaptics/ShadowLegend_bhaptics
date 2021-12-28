using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MelonLoader;
using HarmonyLib;

using MyBhapticsTactsuit;
using UnityEngine;
using VitruviusVR;

namespace ShadowLegend_bhaptics
{
    public class ShadowLegend_bhaptics : MelonMod
    {
        public static TactsuitVR tactsuitVr;
        private static PlayerHand rightHand;
        private static bool weaponRightHand = true;
        private static bool arrowFromCrossBow = false;
        private static bool staffRightHand = true;
        private static bool rightFoot = true;


        public override void OnApplicationStart()
        {
            base.OnApplicationStart();
            tactsuitVr = new TactsuitVR();
            tactsuitVr.PlaybackHaptics("HeartBeat");
        }

        [HarmonyPatch(typeof(VitruviusVR.PlayerController), "OnEnable", new Type[] { })]
        public class bhaptics_EnablePlayerController
        {
            [HarmonyPostfix]
            public static void Postfix(VitruviusVR.PlayerController __instance)
            {
                rightHand = __instance.CurrentRightHand;
            }
        }

        #region Weapon handling

        [HarmonyPatch(typeof(PlayerMeleeDamage), "ApplyDamage", new Type[] { typeof(DamagableObject), typeof(Vector3) })]
        public class bhaptics_MeleeDamage
        {
            [HarmonyPostfix]
            public static void Postfix(PlayerMeleeDamage __instance)
            {
                bool isRightHand = __instance.PlayerWeapon.Hand.Equals(rightHand);
                float weaponSpeed = __instance.PlayerWeapon.WeaponTipVelocity.magnitude;
                float intensity = Math.Min(((weaponSpeed / 80f) + 0.5f), 1.0f);
                tactsuitVr.Recoil("Blade", isRightHand, intensity);
                //tactsuitVr.LOG("MeleeDamage: " + weaponSpeed.ToString());
            }
        }

        [HarmonyPatch(typeof(HandCannon), "CannonInteractionStart", new Type[] { typeof(InteractableObject), typeof(PlayerHand) })]
        public class bhaptics_GrabCannon
        {
            [HarmonyPostfix]
            public static void Postfix(PlayerHand hand)
            {
                weaponRightHand = hand.Equals(rightHand);
            }
        }

        [HarmonyPatch(typeof(HandCannon), "ShootCannon", new Type[] {  })]
        public class bhaptics_ShootCannon
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.Recoil("Cannon", weaponRightHand);
                //tactsuitVr.LOG("MeleeDamage: " + weaponSpeed.ToString());
            }
        }

        [HarmonyPatch(typeof(CrossBow), "InteractionStart", new Type[] { typeof(InteractableObject), typeof(PlayerHand) })]
        public class bhaptics_GrabCrossBow
        {
            [HarmonyPostfix]
            public static void Postfix(PlayerHand hand)
            {
                weaponRightHand = hand.Equals(rightHand);
                arrowFromCrossBow = true;
            }
        }


        [HarmonyPatch(typeof(CrossBow), "Shoot", new Type[] { })]
        public class bhaptics_ShootCrossBow
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                // right hand is where arrow was
                tactsuitVr.Recoil("Gun", !weaponRightHand);
                arrowFromCrossBow = false;
                //tactsuitVr.LOG("MeleeDamage: " + weaponSpeed.ToString());
            }
        }

        [HarmonyPatch(typeof(ArrowHand), "InteractionStart", new Type[] { typeof(InteractableObject), typeof(PlayerHand) })]
        public class bhaptics_GrabArrowHand
        {
            [HarmonyPostfix]
            public static void Postfix(PlayerHand hand)
            {
                weaponRightHand = hand.Equals(rightHand);
            }
        }


        [HarmonyPatch(typeof(ArrowHand), "ShootArrow", new Type[] { })]
        public class bhaptics_ShootArrowHand
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                // Right hand: arrow!!!
                if (!arrowFromCrossBow) { tactsuitVr.Recoil("Bow", weaponRightHand); }
            }
        }

        [HarmonyPatch(typeof(PlayerWeapon), "InteractionStart", new Type[] { typeof(InteractableObject), typeof(PlayerHand) })]
        public class bhaptics_WeaponInteract
        {
            [HarmonyPostfix]
            public static void Postfix(PlayerWeapon __instance, InteractableObject interactableObject, PlayerHand hand)
            {
                if ((interactableObject.IsMeleeWeapon) | (interactableObject.IsRangeWeapon))
                {
                    bool isRightHand = hand.Equals(rightHand);
                    float weaponSpeed = __instance.WeaponTipVelocity.magnitude;
                    float intensity = Math.Min(((weaponSpeed / 80f) + 0.5f), 1.0f);
                    tactsuitVr.Recoil("Blade", isRightHand, intensity);
                }
                else
                {
                    tactsuitVr.LOG("Weapon interact with: " + interactableObject.name);
                }
            }
        }

        [HarmonyPatch(typeof(Staff), "TriggerButtonDown", new Type[] { typeof(PlayerHand) })]
        public class bhaptics_ShootStaff
        {
            [HarmonyPostfix]
            public static void Postfix(Staff __instance, PlayerHand hand)
            {
                bool isRightHand = hand.Equals(rightHand);
                staffRightHand = isRightHand;
                if (__instance.IsReadyToShoot) { tactsuitVr.Recoil("Blade", isRightHand); }
            }
        }

        #endregion

        #region Player damage

        private static KeyValuePair<float, float> getAngleAndShift(Transform player, Vector3 hit)
        {
            // bhaptics starts in the front, then rotates to the left. 0° is front, 90° is left, 270° is right.
            Vector3 patternOrigin = new Vector3(0f, 0f, 1f);
            // y is "up", z is "forward" in local coordinates
            Vector3 hitPosition = hit - player.position;
            Quaternion PlayerRotation = player.rotation;
            Vector3 playerDir = PlayerRotation.eulerAngles;
            Vector3 flattenedHit = new Vector3(hitPosition.x, 0f, hitPosition.z);
            float earlyhitAngle = Vector3.Angle(flattenedHit, patternOrigin);
            Vector3 earlycrossProduct = Vector3.Cross(flattenedHit, patternOrigin);
            if (earlycrossProduct.y > 0f) { earlyhitAngle *= -1f; }
            tactsuitVr.LOG("EarlyHitAngle: " + earlyhitAngle.ToString());
            float myRotation = earlyhitAngle - playerDir.y;
            myRotation *= -1f;
            if (myRotation < 0f) { myRotation = 360f + myRotation; }
            tactsuitVr.LOG("mHitAngle: " + myRotation.ToString());


            float hitShift = hitPosition.y;
            tactsuitVr.LOG("HitShift: " + hitShift.ToString());
            if (hitShift > 1.8f) { hitShift = 0.5f; }
            else if (hitShift < 1.0f) { hitShift = -0.5f; }
            else { hitShift = (hitShift - 1.0f) / 0.8f - 0.5f; }
            //tactsuitVr.LOG("HitShift: " + hitShift.ToString());
            //tactsuitVr.LOG(" ");

            //tactsuitVr.LOG("Relative x-z-position: " + relativeHitDir.x.ToString() + " "  + relativeHitDir.z.ToString());
            //tactsuitVr.LOG("HitAngle: " + hitAngle.ToString());
            //tactsuitVr.LOG("HitShift: " + hitShift.ToString());

            return new KeyValuePair<float, float>(myRotation, hitShift);
        }


        [HarmonyPatch(typeof(Stats), "TakeDamage", new Type[] { typeof(DamageAttack) })]
        public class bhaptics_StatsTakeDamage
        {
            [HarmonyPostfix]
            public static void Postfix(Stats __instance, DamageAttack attack)
            {
                // damage function is for all entities
                if (!__instance.name.Contains("Player")) { return; }

                string feedbackKey = "Impact";
                switch (attack.AttackDamageType)
                {
                    case DamageType.Melee:
                        feedbackKey = "BladeHit";
                        break;
                    case DamageType.Range:
                        feedbackKey = "BulletHit";
                        break;
                    case DamageType.Fire:
                        feedbackKey = "Burning";
                        break;
                    case DamageType.Explosion:
                        feedbackKey = "Impact";
                        break;
                    default:
                        break;
                }

                tactsuitVr.LOG("Damage: " + attack.AttackDamageType.ToString());
                Vector3 hitPosition = attack.WorldHitPosition;
                Transform playerPosition = __instance.transform;
                var angleShift = getAngleAndShift(playerPosition, hitPosition);
                tactsuitVr.PlayBackHit(feedbackKey, angleShift.Key, angleShift.Value);
            }
        }

        [HarmonyPatch(typeof(VitruviusVR.PlayerController), "BurningStart", new Type[] { typeof(IncendiaryObject) })]
        public class bhaptics_Burning
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("Burning");
                tactsuitVr.LOG("BurningStart");
            }
        }

        #endregion

        [HarmonyPatch(typeof(HealthNumber), "HealthChange", new Type[] { typeof(int) })]
        public class bhaptics_PlayerHealth
        {
            [HarmonyPostfix]
            public static void Postfix(HealthNumber __instance, int currentHealth)
            {
                if (currentHealth < 30) { tactsuitVr.StartHeartBeat(); }
                else { tactsuitVr.StopHeartBeat(); }
                //tactsuitVr.LOG("Heal: " + currentHealth.ToString());
            }
        }

        [HarmonyPatch(typeof(PlayerMovementController), "FallLandCoroutine", new Type[] {  })]
        public class bhaptics_FallLand
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("FallDamage");
                tactsuitVr.PlaybackHaptics("FallDamageFeet");
            }
        }

        [HarmonyPatch(typeof(PlayerMovementController), "FootStep", new Type[] { typeof(bool) })]
        public class bhaptics_FootStep
        {
            [HarmonyPostfix]
            public static void Postfix(bool landingFromFalling)
            {
                if (rightFoot)
                {
                    tactsuitVr.PlaybackHaptics("FootStep_R");
                    rightFoot = false;
                }
                else
                {
                    tactsuitVr.PlaybackHaptics("FootStep_L");
                    rightFoot = true;
                }
            }
        }

        [HarmonyPatch(typeof(PlayerMovementController), "PlayerHandInteracting", new Type[] { typeof(PlayerHand) })]
        public class bhaptics_PlayerHandInteract
        {
            [HarmonyPostfix]
            public static void Postfix(PlayerHand hand)
            {
                if (hand.Equals(rightHand)) { tactsuitVr.PlaybackHaptics("TouchHands_R"); tactsuitVr.PlaybackHaptics("Recoil_R", 0.5f); }
                else { tactsuitVr.PlaybackHaptics("TouchHands_L"); tactsuitVr.PlaybackHaptics("Recoil_L", 0.5f); }
            }
        }


    }
}
