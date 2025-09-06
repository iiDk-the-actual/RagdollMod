﻿using BepInEx;
using GorillaExtensions;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using Photon.Voice.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;

namespace RagdollMod
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;
        public void Start()
        {
            instance = this;
            HarmonyPatches.ApplyHarmonyPatches();
        }

        private static AssetBundle assetBundle;
        public static GameObject LoadAsset(string assetName)
        {
            GameObject gameObject = null;

            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RagdollMod.Resources.ragdoll");
            if (stream != null)
            {
                if (assetBundle == null)
                    assetBundle = AssetBundle.LoadFromStream(stream);
                
                gameObject = Instantiate<GameObject>(assetBundle.LoadAsset<GameObject>(assetName));
            }
            else
            {
                Debug.LogError("Failed to load asset from resource: " + assetName);
            }

            return gameObject;
        }

        public static Dictionary<string, AudioClip> audioPool = new Dictionary<string, AudioClip> { };
        public static AudioClip LoadSoundFromResource(string resourcePath)
        {
            AudioClip sound = null;

            if (!audioPool.ContainsKey(resourcePath))
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RagdollMod.Resources.ragdoll");
                if (stream != null)
                {
                    if (assetBundle == null)
                    {
                        assetBundle = AssetBundle.LoadFromStream(stream);
                    }
                    sound = assetBundle.LoadAsset(resourcePath) as AudioClip;
                    audioPool.Add(resourcePath, sound);
                }
                else
                {
                    Debug.LogError("Failed to load sound from resource: " + resourcePath);
                }
            }
            else
            {
                sound = audioPool[resourcePath];
            }

            return sound;
        }

        private static List<GameObject> portedCosmetics = new List<GameObject> { };
        public static void DisableCosmetics()
        {
            try
            {
                GorillaTagger.Instance.offlineVRRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/TransferrableItemLeftShoulder").gameObject.SetActive(false);
                GorillaTagger.Instance.offlineVRRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/TransferrableItemRightShoulder").gameObject.SetActive(false);
                GorillaTagger.Instance.offlineVRRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/head/gorillaface").gameObject.layer = LayerMask.NameToLayer("Default");

                foreach (GameObject Cosmetic in GorillaTagger.Instance.offlineVRRig.cosmetics)
                {
                    if (Cosmetic.activeSelf && Cosmetic.transform.parent == GorillaTagger.Instance.offlineVRRig.mainCamera.transform.Find("HeadCosmetics"))
                    {
                        portedCosmetics.Add(Cosmetic);
                        Cosmetic.transform.SetParent(GorillaTagger.Instance.offlineVRRig.headMesh.transform, false);
                        Cosmetic.transform.localPosition += new Vector3(0f, 0.1333f, 0.1f);
                    }
                }
            }
            catch { }
        }

        public static void EnableCosmetics()
        {
            GorillaTagger.Instance.offlineVRRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/TransferrableItemLeftShoulder").gameObject.SetActive(true);
            GorillaTagger.Instance.offlineVRRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/TransferrableItemRightShoulder").gameObject.SetActive(true);

            GorillaTagger.Instance.offlineVRRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/head/gorillaface").gameObject.layer = LayerMask.NameToLayer("MirrorOnly");
            foreach (GameObject Cosmetic in portedCosmetics)
            {
                Cosmetic.transform.SetParent(GorillaTagger.Instance.offlineVRRig.mainCamera.transform.Find("HeadCosmetics"), false);
                Cosmetic.transform.localPosition -= new Vector3(0f, 0.1333f, 0.1f);
            }

            portedCosmetics.Clear();
        }

        public void Die()
        {
            if (Ragdoll != null)
                UnityEngine.Object.Destroy(Ragdoll);

            GorillaTagger.Instance.offlineVRRig.enabled = false;
            DisableCosmetics();

            PreviousSerializationRate = PhotonNetwork.SerializationRate;
            PhotonNetwork.SerializationRate *= 3;

            GorillaLocomotion.GTPlayer.Instance.rightControllerTransform.parent.rotation *= Quaternion.Euler(0f, 180f, 0f);

            endDeathSoundTime = Time.time + 5.265f;

            Ragdoll = LoadAsset("ragdoll");
            Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.position = GorillaTagger.Instance.offlineVRRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body").position;
            Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.rotation = GorillaTagger.Instance.offlineVRRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body").rotation;

            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.position = GorillaTagger.Instance.offlineVRRig.leftHand.rigTarget.transform.position;
            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.rotation = GorillaTagger.Instance.offlineVRRig.leftHand.rigTarget.transform.rotation;

            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.position = GorillaTagger.Instance.offlineVRRig.rightHand.rigTarget.transform.position;
            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.rotation = GorillaTagger.Instance.offlineVRRig.rightHand.rigTarget.transform.rotation;

            string[] velocitySets = new string[]
            {
                "Stand/Gorilla Rig/body",
                "Stand/Gorilla Rig/body/head",
                "Stand/Gorilla Rig/body/shoulder.L",
                "Stand/Gorilla Rig/body/shoulder.R",
                "Stand/Gorilla Rig/body/shoulder.L/upper_arm.L",
                "Stand/Gorilla Rig/body/shoulder.R/upper_arm.R",
                "Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L",
                "Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R",
            };
            foreach (string velocity in velocitySets)
            {
                Ragdoll.transform.Find(velocity).GetComponent<Rigidbody>().linearVelocity = GorillaTagger.Instance.rigidbody.linearVelocity;
            }

            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").GetComponent<Rigidbody>().linearVelocity = GorillaLocomotion.GTPlayer.Instance.leftHandCenterVelocityTracker.GetAverageVelocity(true, 0);
            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").GetComponent<Rigidbody>().angularVelocity = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/LeftHand Controller").GetOrAddComponent<GorillaVelocityEstimator>().angularVelocity;

            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").GetComponent<Rigidbody>().linearVelocity = GorillaLocomotion.GTPlayer.Instance.rightHandCenterVelocityTracker.GetAverageVelocity(true, 0);
            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").GetComponent<Rigidbody>().angularVelocity = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/RightHand Controller").GetOrAddComponent<GorillaVelocityEstimator>().angularVelocity;

            Ragdoll.transform.Find("Stand/Gorilla Rig/body/head").transform.rotation = GorillaTagger.Instance.headCollider.transform.rotation;

            GorillaTagger.Instance.offlineVRRig.head.rigTarget.transform.rotation = Ragdoll.transform.Find("Stand/Gorilla Rig/body/head").transform.rotation;

            Ragdoll.transform.Find("Stand/Mesh").gameObject.GetComponent<Renderer>().renderingLayerMask = 0;

            startForward = Ragdoll.transform.forward;

            if (uiCoroutine != null)
            {
                StopCoroutine(uiCoroutine);
                uiCoroutine = null;
            } else
            {
                uiCoroutine = StartCoroutine(ShowGModUI());
            }

            AudioClip Sound = LoadSoundFromResource("GMOD-Net");
            if (GorillaTagger.Instance.myRecorder != null)
            {
                GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.AudioClip;
                GorillaTagger.Instance.myRecorder.AudioClip = Sound;
                GorillaTagger.Instance.myRecorder.RestartRecording(true);
            }
        }

        public static Vector3 World2Player(Vector3 world)
        {
            return world - GorillaTagger.Instance.bodyCollider.transform.position + GorillaTagger.Instance.transform.position;
        }

        public bool GetRightJoystickDown()
        {
            if (IsSteam)
                return SteamVR_Actions.gorillaTag_RightJoystickClick.GetState(SteamVR_Input_Sources.RightHand);
            else
            {
                bool rightJoystickClick;
                ControllerInputPoller.instance.rightControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out rightJoystickClick);
                return rightJoystickClick;
            }
        }

        public bool hasInit;
        public bool IsSteam;
        public float endDeathSoundTime = -1f;
        public bool lastLeftHeld;
        public GameObject ui;
        public Coroutine uiCoroutine;

        public IEnumerator ShowGModUI()
        {
            ui = LoadAsset("UI");
            ui.transform.parent = GameObject.Find("Main Camera").transform;
            ui.transform.localPosition = Vector3.zero;
            ui.transform.localRotation = Quaternion.identity;

            ui.transform.Find("Cube/Canvas/Name").GetComponent<Text>().text = PhotonNetwork.NickName;
            ui.transform.Find("Cube/Canvas/Name/Shadow").GetComponent<Text>().text = PhotonNetwork.NickName;

            float startTime = Time.time + 5f;
            while (Time.time < startTime)
            {
                ui.transform.Find("Cube").gameObject.GetComponent<Renderer>().material.color = new Color(0.8980392157f, 0.2274509804f, 0.1294117647f, Mathf.Lerp(0f, 0.15f, (startTime - Time.time) / 5f));
                yield return null;
            }

            ui.transform.Find("Cube").gameObject.GetComponent<Renderer>().material.color = Color.clear;
            yield return new WaitForSeconds(5f);
            Destroy(ui);

            Coroutine thisCoroutine = uiCoroutine;
            uiCoroutine = null;
            StopCoroutine(thisCoroutine);
        }

        public void Update()
        {
            if (!hasInit && GorillaLocomotion.GTPlayer.Instance != null)
            {
                hasInit = true;
                IsSteam = Traverse.Create(PlayFabAuthenticator.instance).Field("platform").GetValue().ToString().ToLower() == "steam";
            }

            if ((GetRightJoystickDown() || UnityInput.Current.GetKey(KeyCode.B)) && !lastLeftHeld)
            {
                isDead = !isDead;

                if (isDead)
                    Die();
            }

            lastLeftHeld = GetRightJoystickDown() || UnityInput.Current.GetKey(KeyCode.B);

            if (Time.time > endDeathSoundTime && endDeathSoundTime > 0)
            {
                if (GorillaTagger.Instance.myRecorder != null)
                {
                    GorillaTagger.Instance.myRecorder.AudioClip = LoadSoundFromResource("Silence");
                    GorillaTagger.Instance.myRecorder.RestartRecording(true);
                }
                endDeathSoundTime = -1;
            }

            if (isDead)
            {
                if (Ragdoll != null)
                {
                    GorillaTagger.Instance.offlineVRRig.enabled = false;
                    GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;

                    UpdateRigPos();
                }
            }
            else
            {
                if (Ragdoll != null)
                {
                    GorillaTagger.Instance.offlineVRRig.enabled = true;
                    EnableCosmetics();

                    if (PreviousSerializationRate > 0)
                        PhotonNetwork.SerializationRate = PreviousSerializationRate;

                    Destroy(Ragdoll);

                    if (GorillaTagger.Instance.myRecorder != null)
                    {
                        GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.Microphone;
                        GorillaTagger.Instance.myRecorder.AudioClip = null;
                        GorillaTagger.Instance.myRecorder.RestartRecording(true);
                    }

                    if (uiCoroutine != null)
                    {
                        StopCoroutine(uiCoroutine);
                        uiCoroutine = null;
                    }

                    if (ui != null)
                        Destroy(ui);

                    GorillaLocomotion.GTPlayer.Instance.TeleportTo(World2Player(Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.position), GorillaLocomotion.GTPlayer.Instance.transform.rotation);
                    GorillaLocomotion.GTPlayer.Instance.rightControllerTransform.parent.rotation *= Quaternion.Euler(0f, 180f, 0f);
                }
            }
        }

        public void UpdateRigPos()
        {
            GorillaLocomotion.GTPlayer.Instance.TeleportTo(World2Player(Ragdoll.transform.Find("Stand/Gorilla Rig/body").gameObject.transform.position + (startForward * 2f) + new Vector3(0f, 2f, 0f)), GorillaLocomotion.GTPlayer.Instance.transform.rotation);
            GorillaTagger.Instance.leftHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position;
            GorillaTagger.Instance.rightHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position;

            GorillaTagger.Instance.offlineVRRig.transform.position = Ragdoll.transform.Find("Stand/Gorilla Rig/body").gameObject.transform.position;
            GorillaTagger.Instance.offlineVRRig.transform.rotation = Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.rotation;

            GorillaTagger.Instance.offlineVRRig.leftHand.rigTarget.transform.position = Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.position;
            GorillaTagger.Instance.offlineVRRig.rightHand.rigTarget.transform.position = Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.position;

            GorillaTagger.Instance.offlineVRRig.leftHand.rigTarget.transform.rotation = Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.rotation;
            GorillaTagger.Instance.offlineVRRig.rightHand.rigTarget.transform.rotation = Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.rotation;

            GorillaTagger.Instance.offlineVRRig.head.rigTarget.transform.rotation = Ragdoll.transform.Find("Stand/Gorilla Rig/body/head").transform.rotation;
        }

        public static Vector3 startForward;
        public static bool isDead;
        public static int PreviousSerializationRate = -1;

        public static GameObject Ragdoll;
    }
}
