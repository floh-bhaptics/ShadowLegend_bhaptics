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
                // getting information on which hand is used can sometimes be a pain. Beware!
                bool isRightHand = __instance.PlayerWeapon.Hand.Equals(rightHand);
                float weaponSpeed = __instance.PlayerWeapon.WeaponTipVelocity.magnitude;
                // scale intensity from speed [0, 80] to [0.5, 1], cap at 1.0
                float intensity = Math.Min(((weaponSpeed / 80f) + 0.5f), 1.0f);
                tactsuitVr.Recoil("Blade", isRightHand, intensity);
            }
        }

        [HarmonyPatch(typeof(HandCannon), "CannonInteractionStart", new Type[] { typeof(InteractableObject), typeof(PlayerHand) })]
        public class bhaptics_GrabCannon
        {
            [HarmonyPostfix]
            public static void Postfix(PlayerHand hand)
            {
                // here is an example: The shoot cannon function does not have a reference to any hand.
                // So I am saving which hand grabbed it...
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
            }
        }

        [HarmonyPatch(typeof(CrossBow), "InteractionStart", new Type[] { typeof(InteractableObject), typeof(PlayerHand) })]
        public class bhaptics_GrabCrossBow
        {
            [HarmonyPostfix]
            public static void Postfix(PlayerHand hand)
            {
                // Again, not shoot function does not know the hand, so I am saving it internally
                weaponRightHand = hand.Equals(rightHand);
                // There is no Bow function to hook into. So I use the arrow shoot function, but not when
                // shooting with the crossbow. So remember if I am using the crossbow
                arrowFromCrossBow = true;
            }
        }


        [HarmonyPatch(typeof(CrossBow), "Shoot", new Type[] { })]
        public class bhaptics_ShootCrossBow
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                // right hand is where arrow was, so fire from not rightHand
                tactsuitVr.Recoil("Gun", !weaponRightHand);
                arrowFromCrossBow = false;
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
                if (__instance.IsReadyToShoot) { tactsuitVr.Recoil("Blade", isRightHand); }
            }
        }

        #endregion

        #region Player damage

        private static KeyValuePair<float, float> getAngleAndShift(Transform player, Vector3 hit)
        {
            // bhaptics pattern starts in the front, then rotates to the left. 0° is front, 90° is left, 270° is right.
            // y is "up", z is "forward" in local coordinates
            Vector3 patternOrigin = new Vector3(0f, 0f, 1f);
            Vector3 hitPosition = hit - player.position;
            Quaternion myPlayerRotation = player.rotation;
            Vector3 playerDir = myPlayerRotation.eulerAngles;
            // get rid of the up/down component to analyze xz-rotation
            Vector3 flattenedHit = new Vector3(hitPosition.x, 0f, hitPosition.z);

            // get angle. .Net < 4.0 does not have a "SignedAngle" function...
            float hitAngle = Vector3.Angle(flattenedHit, patternOrigin);
            // check if cross product points up or down, to make signed angle myself
            Vector3 crossProduct = Vector3.Cross(flattenedHit, patternOrigin);
            if (crossProduct.y > 0f) { hitAngle *= -1f; }
            // relative to player direction
            float myRotation = hitAngle - playerDir.y;
            // switch directions (bhaptics angles are in mathematically negative direction)
            myRotation *= -1f;
            // convert signed angle into [0, 360] rotation
            if (myRotation < 0f) { myRotation = 360f + myRotation; }


            // up/down shift is in y-direction
            // in Shadow Legend, the torso Transform has y=0 at the neck,
            // and the torso ends at roughly -0.5 (that's in meters)
            // so cap the shift to [-0.5, 0]...
            float hitShift = hitPosition.y;
            float upperBound = 0.0f;
            float lowerBound = -0.5f;
            if (hitShift > upperBound) { hitShift = 0.5f; }
            else if (hitShift < lowerBound) { hitShift = -0.5f; }
            // ...and then spread/shift it to [-0.5, 0.5]
            else { hitShift = (hitShift - lowerBound) / (upperBound - lowerBound) - 0.5f; }

            //tactsuitVr.LOG("Relative x-z-position: " + relativeHitDir.x.ToString() + " "  + relativeHitDir.z.ToString());
            //tactsuitVr.LOG("HitAngle: " + hitAngle.ToString());
            //tactsuitVr.LOG("HitShift: " + hitShift.ToString());

            // No tuple returns available in .NET < 4.0, so this is the easiest quickfix
            return new KeyValuePair<float, float>(myRotation, hitShift);
        }

        [HarmonyPatch(typeof(VitruviusVR.PlayerController), "TakeDamage", new Type[] { typeof(DamagableObject), typeof(DamageAttack) })]
        public class bhaptics_TakeDamage
        {
            [HarmonyPostfix]
            public static void Postfix(VitruviusVR.PlayerController __instance, DamagableObject damagableObject, DamageAttack attack)
            {
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

                Vector3 hitPosition = attack.WorldHitPosition;
                Transform playerPosition = damagableObject.transform;
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
                //tactsuitVr.LOG("BurningStart");
            }
        }

        #endregion

        #region World Interaction

        [HarmonyPatch(typeof(HealthNumber), "HealthChange", new Type[] { typeof(int) })]
        public class bhaptics_PlayerHealth
        {
            [HarmonyPostfix]
            public static void Postfix(HealthNumber __instance, int currentHealth)
            {
                if (currentHealth < 30) { tactsuitVr.StartHeartBeat(); }
                else { tactsuitVr.StopHeartBeat(); }
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
            public static void Postfix(PlayerMovementController __instance, bool landingFromFalling)
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

        [HarmonyPatch(typeof(VitruviusVR.PlayerController), "Heal", new Type[] { typeof(int) })]
        public class bhaptics_HealPlayer
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("Healing");
            }
        }

        [HarmonyPatch(typeof(VitruviusVR.PlayerController), "FallInLava", new Type[] {  })]
        public class bhaptics_FallInLava
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("Burning");
            }
        }

        [HarmonyPatch(typeof(VitruviusVR.Eatable), "Eat", new Type[] {  })]
        public class bhaptics_Eating
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("Eating");
            }
        }

        #endregion

        #region Gargoyle

        [HarmonyPatch(typeof(VitruviusVR.GargoyleBossDealLightningDamageAction), "DealDamageToPlayer", new Type[] { typeof(GargoyleBossStateController) })]
        public class bhaptics_GargoyleLightning
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("Electrocution");
            }
        }

        [HarmonyPatch(typeof(VitruviusVR.GargoyleBossRockProjectile), "Explode", new Type[] {  })]
        public class bhaptics_GargoyleRockExplode
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("ExplosionBelly");
            }
        }

        [HarmonyPatch(typeof(VitruviusVR.GargoyleBossFightController), "BossLand", new Type[] { })]
        public class bhaptics_GargoyleLand
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("StompBelly");
            }
        }

        [HarmonyPatch(typeof(VitruviusVR.GargoyleBossFightController), "PillarDestroyed", new Type[] { })]
        public class bhaptics_GargoylePillarDestroyed
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("NeckTingleShort");
            }
        }

        [HarmonyPatch(typeof(VitruviusVR.GargoyleBossFightController), "Footstep", new Type[] { typeof(Vector3) })]
        public class bhaptics_GargoyleFootStep
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("StompBelly");
            }
        }


        #endregion

    }
}
