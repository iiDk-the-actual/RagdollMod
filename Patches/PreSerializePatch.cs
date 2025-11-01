using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace RagdollMod.Patches
{
    [HarmonyPatch(typeof(PhotonNetwork), "RunViewUpdate")]
    public class PreSerialize
    {
        public static void Prefix()
        {
            if (Plugin.isDead)
            {
                GorillaTagger.Instance.transform.position = Plugin.World2Player(Plugin.Ragdoll.transform.Find("Stand/Gorilla Rig/body").gameObject.transform.position + (Plugin.startForward * 2f) + new Vector3(0f, 2f, 0f));
                GorillaTagger.Instance.leftHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position;
                GorillaTagger.Instance.rightHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position;

                VRRig.LocalRig.transform.position = Plugin.Ragdoll.transform.Find("Stand/Gorilla Rig/body").gameObject.transform.position + (Plugin.Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.up * 0.375f);
                VRRig.LocalRig.transform.rotation = Quaternion.Euler(new Vector3(0f, Plugin.Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.rotation.eulerAngles.y, 0f));

                VRRig.LocalRig.leftHand.rigTarget.transform.position = Plugin.Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.position;
                VRRig.LocalRig.rightHand.rigTarget.transform.position = Plugin.Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.position;

                VRRig.LocalRig.leftHand.rigTarget.transform.rotation = Plugin.Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.rotation * Quaternion.Euler(0, 0, 75);
                VRRig.LocalRig.rightHand.rigTarget.transform.rotation = Plugin.Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.rotation * Quaternion.Euler(180, 0, -75);

                VRRig.LocalRig.head.rigTarget.transform.rotation = Plugin.Ragdoll.transform.Find("Stand/Gorilla Rig/body/head").transform.rotation;
            }
        }

        public static void Postfix()
        {
            if (Plugin.isDead)
            {
                Plugin.instance.UpdateRigPos();
            }
        }
    }
}