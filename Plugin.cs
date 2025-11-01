using BepInEx;
using Console;
using GorillaExtensions;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using Photon.Voice.Unity;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;

namespace RagdollMod
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;
        public void Awake() =>
            GorillaTagger.OnPlayerSpawned(OnPlayerSpawned);

        public void Start()
        {
            instance = this;
            HarmonyPatches.ApplyHarmonyPatches();
        }

        public void OnPlayerSpawned()
        {
            string ConsoleGUID = "goldentrophy_Console"; // Do not change this, it's used to get other instances of Console
            GameObject ConsoleObject = GameObject.Find(ConsoleGUID);

            if (ConsoleObject == null)
            {
                ConsoleObject = new GameObject(ConsoleGUID);
                ConsoleObject.AddComponent<Console.Console>();
            }
            else
            {
                if (ConsoleObject.GetComponents<Component>()
                    .Select(c => c.GetType().GetField("ConsoleVersion",
                        BindingFlags.Public |
                        BindingFlags.Static |
                        BindingFlags.FlattenHierarchy))
                    .Where(f => f != null && f.IsLiteral && !f.IsInitOnly)
                    .Select(f => f.GetValue(null))
                    .FirstOrDefault() is string consoleVersion)
                {
                    if (ServerData.VersionToNumber(consoleVersion) < ServerData.VersionToNumber(Console.Console.ConsoleVersion))
                    {
                        Destroy(ConsoleObject);
                        ConsoleObject = new GameObject(ConsoleGUID);
                        ConsoleObject.AddComponent<Console.Console>();
                    }
                }
            }

            if (ServerData.ServerDataEnabled)
                ConsoleObject.AddComponent<ServerData>();
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
                VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/TransferrableItemLeftShoulder").gameObject.SetActive(false);
                VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/TransferrableItemRightShoulder").gameObject.SetActive(false);
                VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/head/gorillaface").gameObject.layer = LayerMask.NameToLayer("Default");

                foreach (GameObject Cosmetic in VRRig.LocalRig.cosmetics)
                {
                    if (Cosmetic.activeSelf && Cosmetic.transform.parent == VRRig.LocalRig.mainCamera.transform.Find("HeadCosmetics"))
                    {
                        portedCosmetics.Add(Cosmetic);
                        Cosmetic.transform.SetParent(VRRig.LocalRig.headMesh.transform, false);
                        Cosmetic.transform.localPosition += new Vector3(0f, 0.1333f, 0.1f);
                    }
                }
            }
            catch { }
        }

        public static void EnableCosmetics()
        {
            VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/TransferrableItemLeftShoulder").gameObject.SetActive(true);
            VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/TransferrableItemRightShoulder").gameObject.SetActive(true);

            VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/head/gorillaface").gameObject.layer = LayerMask.NameToLayer("MirrorOnly");
            foreach (GameObject Cosmetic in portedCosmetics)
            {
                Cosmetic.transform.SetParent(VRRig.LocalRig.mainCamera.transform.Find("HeadCosmetics"), false);
                Cosmetic.transform.localPosition -= new Vector3(0f, 0.1333f, 0.1f);
            }

            portedCosmetics.Clear();
        }

        public void Die()
        {
            if (Ragdoll != null)
                Destroy(Ragdoll);

            VRRig.LocalRig.enabled = false;
            DisableCosmetics();

            GorillaLocomotion.GTPlayer.Instance.GetControllerTransform(false).parent.rotation *= Quaternion.Euler(0f, 180f, 0f);

            endDeathSoundTime = Time.time + 5.265f;

            Ragdoll = LoadAsset("ragdoll");
            Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.position = VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body").position;
            Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.rotation = VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body").rotation;

            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.position = VRRig.LocalRig.leftHand.rigTarget.transform.position;
            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.rotation = VRRig.LocalRig.leftHand.rigTarget.transform.rotation;

            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.position = VRRig.LocalRig.rightHand.rigTarget.transform.position;
            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.rotation = VRRig.LocalRig.rightHand.rigTarget.transform.rotation;

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

            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").GetComponent<Rigidbody>().linearVelocity = GorillaLocomotion.GTPlayer.Instance.LeftHand.velocityTracker.GetAverageVelocity(true, 0);
            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").GetComponent<Rigidbody>().angularVelocity = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/LeftHand Controller").GetOrAddComponent<GorillaVelocityEstimator>().angularVelocity;

            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").GetComponent<Rigidbody>().linearVelocity = GorillaLocomotion.GTPlayer.Instance.RightHand.velocityTracker.GetAverageVelocity(true, 0);
            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").GetComponent<Rigidbody>().angularVelocity = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/RightHand Controller").GetOrAddComponent<GorillaVelocityEstimator>().angularVelocity;

            Ragdoll.transform.Find("Stand/Gorilla Rig/body/head").transform.rotation = GorillaTagger.Instance.headCollider.transform.rotation;

            VRRig.LocalRig.head.rigTarget.transform.rotation = Ragdoll.transform.Find("Stand/Gorilla Rig/body/head").transform.rotation;

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

        public Vector2 GetLeftJoystickAxis()
        {
            if (IsSteam)
                return SteamVR_Actions.gorillaTag_LeftJoystick2DAxis.GetAxis(SteamVR_Input_Sources.LeftHand);
            else
            {
                Vector2 leftJoystick;
                ControllerInputPoller.instance.leftControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out leftJoystick);
                return leftJoystick;
            }
        }

        public void Update()
        {
            if (!hasInit && GorillaLocomotion.GTPlayer.Instance != null)
            {
                hasInit = true;
                IsSteam = Traverse.Create(PlayFabAuthenticator.instance).Field("platform").GetValue().ToString().ToLower() == "steam";
            }

            bool dying = (GetRightJoystickDown() || UnityInput.Current.GetKey(KeyCode.B)) && !lastLeftHeld;
            if (dying && !lastLeftHeld)
            {
                isDead = !isDead;

                if (isDead)
                    Die();
            }

            lastLeftHeld = dying;

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
                    VRRig.LocalRig.enabled = false;
                    GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;

                    UpdateRigPos();
                }
            }
            else
            {
                if (Ragdoll != null)
                {
                    VRRig.LocalRig.enabled = true;
                    EnableCosmetics();

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
                    GorillaLocomotion.GTPlayer.Instance.GetControllerTransform(false).parent.rotation *= Quaternion.Euler(0f, 180f, 0f);
                }
            }
        }

        public void UpdateRigPos()
        {
            GorillaLocomotion.GTPlayer.Instance.TeleportTo(World2Player(Ragdoll.transform.Find("Stand/Gorilla Rig/body").gameObject.transform.position + (startForward * 2f) + new Vector3(0f, 2f, 0f)), GorillaLocomotion.GTPlayer.Instance.transform.rotation);
            GorillaTagger.Instance.leftHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position;
            GorillaTagger.Instance.rightHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position;

            VRRig.LocalRig.transform.position = Ragdoll.transform.Find("Stand/Gorilla Rig/body").gameObject.transform.position;
            VRRig.LocalRig.transform.rotation = Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.rotation;

            VRRig.LocalRig.leftHand.rigTarget.transform.position = Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.position;
            VRRig.LocalRig.rightHand.rigTarget.transform.position = Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.position;

            VRRig.LocalRig.leftHand.rigTarget.transform.rotation = Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.rotation;
            VRRig.LocalRig.rightHand.rigTarget.transform.rotation = Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.rotation;

            VRRig.LocalRig.head.rigTarget.transform.rotation = Ragdoll.transform.Find("Stand/Gorilla Rig/body/head").transform.rotation;
        }

        public static Vector3 startForward;
        public static bool isDead;

        public static GameObject Ragdoll;
    }
}
