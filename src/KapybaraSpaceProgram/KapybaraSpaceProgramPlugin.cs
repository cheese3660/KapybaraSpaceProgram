using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using KSP.Game;
using KSP.Modding.Variety;
using KSP.Sim.impl;
using KSP.UI;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using UnityEngine;
using Debug = System.Diagnostics.Debug;
using Random = System.Random;

namespace KapybaraSpaceProgram;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class KapybaraSpaceProgramPlugin : BaseSpaceWarpPlugin
{
    // These are useful in case some other mod wants to add a dependency to this one
    [PublicAPI] public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    [PublicAPI] public const string ModName = MyPluginInfo.PLUGIN_NAME;
    [PublicAPI] public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

    // Singleton instance of the plugin class
    public static KapybaraSpaceProgramPlugin Instance { get; set; }


    // private static GameObject _kapybaraObject;
    private static KapybaraInfo KapybaraInfo =>
        KapybaraInfos[Instance.CurrentlySelectedKapybaraType.Value];
    private static List<string> _surnames;
    private static readonly Dictionary<string, string> ChosenSurnamesPerFirstName = new();
    public static readonly Dictionary<string,List<Material>> Materials = new();


    public static Dictionary<string, KapybaraInfo> KapybaraInfos = new()
    {
        ["Normal"] = new KapybaraInfo
        {
            Filename = "kapybara_v4.1.prefab",
            LocalPosition = new Vector3(-0.18f, -0.0047f, 0),
            LocalRotation = Quaternion.Euler(90, 270, 0),
            LocalScale = new Vector3(0.85f, 0.75f, 0.75f),
            ReparentingMap = new Dictionary<string, string>
            {
                ["mesh_male_eyes_01"] = "mesh_male_head_01",
                ["mesh_female_eyes_01"] = "mesh_female_head_01"
            },
            HeadObjectNames =
            [
                "mesh_male_head_01",
                "mesh_female_head_01"
            ],
            FemaleObjectNames = ["mesh_female_head_01", "mesh_female_eyes_01"],
            MaleObjectNames = ["mesh_male_head_01", "mesh_male_eyes_01"],
        },
        ["Realistic"] = new KapybaraInfo
        {
            Filename = "realisticcapybara.prefab",
            LocalPosition = new Vector3(-0.18f, -0.0047f, 0),
            LocalRotation = Quaternion.Euler(90, 270, 0),
            LocalScale = new Vector3(0.85f, 0.75f, 0.75f),
            ReparentingMap = new Dictionary<string, string>(),
            HeadObjectNames = ["Head"],
            FemaleObjectNames = [],
            MaleObjectNames = []
        },
        ["Realistic V2"] = new KapybaraInfo
        {
            Filename = "realistic_kapybara.prefab",
            LocalPosition = new Vector3(-0.18f, -0.0047f, 0),
            LocalRotation = Quaternion.Euler(90, 270, 0),
            LocalScale = new Vector3(0.85f, 0.75f, 0.75f),
            ReparentingMap = new Dictionary<string, string>(),
            HeadObjectNames =
            [
                "mesh_male_head_01",
                "mesh_female_head_01"
            ],
            FemaleObjectNames = ["mesh_female_head_01"],
            MaleObjectNames = ["mesh_male_head_01"],
        }
    };

    public ConfigEntry<string> CurrentlySelectedKapybaraType;
    
    
    public override void OnPreInitialized()
    {
        Instance = this;
        CurrentlySelectedKapybaraType = Config.Bind("Kapybara", "Kapybara Type", "Realistic V2",
            new ConfigDescription("What type of Kapybara model do you wish to use",new AcceptableValueList<string>(KapybaraInfos.Keys.ToArray())));
        Harmony.CreateAndPatchAll(GetType());
    }
    
    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();


        // _kapybaraObject =
        //     AssetManager.GetAsset<GameObject>($"{SWMetadata.Guid}/kapybara/kapybara_v4.1.prefab");
        _surnames = File.ReadAllText(Path.Combine(SWMetadata.Folder.FullName, "surnames.txt")).Replace("\r", "").Split('\n').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x))
            .ToList();
        
        SpaceWarp.API.UI.Appbar.Appbar.RegisterGameAppbarMenu<KapybaraEditorWindow>("Kapybara Color Editor", "Kapybara Editor Window","BTN-Kapybara Color Editor", AssetManager.GetAsset<Texture2D>($"{SWMetadata.Guid}/images/icon.png"));
        foreach (KapybaraInfo info in KapybaraInfos.Values)
        {
            foreach (Transform child in info.GameObject.transform)
            {
                if (child.gameObject.GetComponent<MeshRenderer>() is not { } meshRenderer) continue;
                foreach (var material in meshRenderer.materials)
                {
                    // Materials.Add(material);
                    if (Materials.ContainsKey(material.name))
                    {
                        Materials[material.name].Add(material);
                    }
                    else
                    {
                        Materials[material.name] = new() { material };
                    }
                }

                child.gameObject.layer = 17;
            }
        }


    }



    static void Kapybarize(Transform spacesuit, bool addHelmet)
    {
        if (ReferenceEquals(spacesuit, null))
        {
            Instance.Logger.LogInfo("Could not find spacesuit, returning");
            return;
        }

        int layer = spacesuit.gameObject.layer;

        // Instance.Logger.LogInfo("Dumping spacesuit");
        // spacesuit.DumpTree();
        bool female = false;
        var head = spacesuit.FirstChildOrDefault(x =>
            x.name.StartsWith("mesh_male_head") || x.name.StartsWith("mesh_female_head"),false);
        if (!ReferenceEquals(head, null))
        {
            female = head.name.StartsWith("mesh_female");
            head.gameObject.SetActive(false);
        }
        else
        {
            Instance.Logger.LogInfo("Could not find head!");
        }

        // var helmet = __instance.transform.Find("bone_kerbal_helmet").gameObject;
        var helmet = spacesuit.FindChildRecursive("bone_kerbal_helmet");
        Material helmetMaterial = null;
        Material visorMaterial = null;
        if (!ReferenceEquals(helmet, null))
        {
            // helmet.gameObject.SetActive(false);
            var mesh = helmet.FirstChildOrDefault(x => x.name.StartsWith("helm_spacesuit"), false);
            if (mesh != null)
            {
                foreach (Transform child in mesh)
                {
                    Instance.Logger.LogInfo($"Found helmet child {child.name}");
                    if (child.name.StartsWith("mesh_helmet"))
                    {
                        child.name = "disabled_" + child.name;
                        if (child.name.EndsWith("visor"))
                        {
                            visorMaterial = child.gameObject.GetComponent<MeshRenderer>().material;
                        }
                        else
                        {
                            helmetMaterial = child.gameObject.GetComponent<MeshRenderer>().material;
                        }
                
                        child.gameObject.SetActive(false);
                    }
                }

                helmet = mesh.Find("mesh_spacesuit_01_helmetbase");
            }
        }
        else
        {
            Instance.Logger.LogInfo("Could not find helmet!");
        }

        var neck = spacesuit.FindChildRecursive("bone_kerbal_neck");
        if (!ReferenceEquals(neck, null))
        {
            var instance = Instantiate(KapybaraInfo.GameObject, neck);
            SetLayer(instance.transform);
            instance.SetActive(true);
            // instance.transform.Find("mesh_helmet_01").gameObject.SetActive(false);
            // instance.transform.Find("mesh_helmet_01_visor").gameObject.SetActive(false);
            var helmetTransform = instance.transform.Find("mesh_helmet_01");
            var visorTransform = instance.transform.Find("mesh_helmet_01_visor");
            

            instance.transform.localPosition = KapybaraInfo.LocalPosition;
            instance.transform.localRotation = KapybaraInfo.LocalRotation;
            instance.transform.localScale = KapybaraInfo.LocalScale;
            if (helmetMaterial != null)
            {
                // helmetTransform.gameObject.GetComponent<SkinnedMeshRenderer>().material = helmetMaterial;
                if (helmetTransform.gameObject.GetComponent<SkinnedMeshRenderer>() is { } skinnedMeshRenderer)
                {
                    skinnedMeshRenderer.material = helmetMaterial;
                }
                else if (helmetTransform.gameObject.GetComponent<MeshRenderer>() is { } meshRenderer)
                {
                    meshRenderer.material = helmetMaterial;
                }
            }
            else
            {
                helmetTransform.gameObject.SetActive(false);
            }

            if (visorMaterial != null)
            {
                if (visorTransform.gameObject.GetComponent<SkinnedMeshRenderer>() is { } skinnedMeshRenderer)
                {
                    skinnedMeshRenderer.material = visorMaterial;
                }
                else if (visorTransform.gameObject.GetComponent<MeshRenderer>() is { } meshRenderer)
                {
                    meshRenderer.material = visorMaterial;
                }
            }
            else
            {
                visorTransform.gameObject.SetActive(false);
            }
            if (!addHelmet)
            {
                helmetTransform.gameObject.SetActive(false);
                visorTransform.gameObject.SetActive(false);
            }

            if (female)
            {
                // instance.transform.Find("mesh_male_head_01").gameObject.SetActive(false);
                // instance.transform.Find("mesh_male_eyes_01").gameObject.SetActive(false);
                foreach (var go in KapybaraInfo.MaleObjectNames)
                    instance.transform.Find(go).gameObject.SetActive(false);
            }
            else
            {
                foreach (var go in KapybaraInfo.FemaleObjectNames)
                    instance.transform.Find(go).gameObject.SetActive(false);
            }
            
            var head_bone = neck.Find("bone_kerbal_head");
            instance.transform.SetParent(helmet);
            if (!ReferenceEquals(head_bone, null))
            {
                // head_bone.gameObject.SetActive(false);
                foreach (Transform child in head_bone)
                {
                    child.gameObject.SetActive(false);
                }
                // instance.transform.Find("mesh_male_eyes_01").SetParent(instance.transform.Find("mesh_male_head_01"));
                // instance.transform.Find("mesh_female_eyes_01").SetParent(instance.transform.Find("mesh_female_head_01"));
                foreach (var value in KapybaraInfo.ReparentingMap)
                {
                    instance.transform.Find(value.Key).SetParent(instance.transform.Find(value.Value));
                }
                // instance.transform.Find("mesh_male_head_01").SetParent(head_bone);
                // instance.transform.Find("mesh_female_head_01").SetParent(head_bone);
                foreach (var go in KapybaraInfo.HeadObjectNames)
                {
                    instance.transform.Find(go).SetParent(head_bone);
                }
            }
            else
            {
                Instance.Logger.LogInfo("Could not find head bone!");
            }

            void SetLayer(Transform transform)
            {
                transform.gameObject.layer = layer;
                foreach (Transform child in transform)
                {
                    SetLayer(child);
                }
            }
        }
        else
        {
            Instance.Logger.LogInfo("Could not find neck!");
        }
    }

    [HarmonyILManipulator]
    [HarmonyPatch(typeof(Kerbal3DModel), nameof(Kerbal3DModel.BuildCharacterFromLoadedAttributes),
        new Type[]
        {
            typeof(Dictionary<string, KerbalVarietyAttributeRule>), typeof(Dictionary<string, VarietyPreloadInfo>)
        })]
    private static void BuildCharacterPatch(ILContext context, ILLabel ilLabel)
    {
        var setExistsField = AccessTools.Field(typeof(Kerbal3DModel), "CharacterExists");
        ILCursor cursor = new(context);
        cursor.GotoNext(MoveType.After, instruction => instruction.MatchStfld(setExistsField));
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate(ReplaceModelling);
    }
    
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(Kerbal3DModel), nameof(Kerbal3DModel.BuildCharacterFromLoadedAttributes),
    // new Type[]
    // {
    //     typeof(Dictionary<string, KerbalVarietyAttributeRule>), typeof(Dictionary<string, VarietyPreloadInfo>)
    // })]
    private static void ReplaceModelling(Kerbal3DModel __instance)
    {
        Instance.Logger.LogInfo($"Replacing modelling for {__instance.name}");
        // __instance.transform.DumpTree();
        foreach (Transform child in __instance.transform)
        {
            Instance.Logger.LogInfo($"Child {child.name}");
        }
        
        var spacesuit = __instance.transform.FirstChildOrDefault(x => x.name.StartsWith("body_spacesuit"));
        Kapybarize(spacesuit, true);
        var head = __instance.transform.FirstChildOrDefault(x => x.name.StartsWith("head"));
        Kapybarize(head, false);
        var eyes = __instance.transform.FirstChildOrDefault(x => x.name.StartsWith("eyes"));
        Kapybarize(eyes, false);

        // var empty = new GameObject("kapybarized");
        // empty.transform.SetParent(__instance.transform);
    }

    private static string GetLastName(string firstName)
    {
        firstName = GameManager.Instance.Game.SessionManager.ActiveCampaignName + " - " + firstName;
        if (!ChosenSurnamesPerFirstName.ContainsKey(firstName))
        {
            using var md5 = MD5.Create();
            md5.Initialize();
            md5.ComputeHash(Encoding.UTF8.GetBytes(firstName));
            ulong index = 0;
            for (var i = 0; i < 16; i += 2)
            {
                index |= (ulong)md5.Hash[i] << (i * 4);
            }
            var rand = new Random((int)index);
            Instance.Logger.LogInfo($"{_surnames.Count}");
            ChosenSurnamesPerFirstName[firstName] = _surnames[rand.Next(_surnames.Count)];
        }
        return ChosenSurnamesPerFirstName[firstName];
    }

    [HarmonyPatch(typeof(KerbalAttributes), nameof(KerbalAttributes.Surname), MethodType.Getter)]
    [HarmonyPostfix]
    private static void ReplaceLastName(ref KerbalAttributes __instance,  ref string __result)
    {
        if (__result == "Kerman")
        {
            __result = GetLastName(__instance.FirstName);
        }
    }

    [HarmonyPatch(typeof(KerbalManager), nameof(KerbalManager.GenerateKerbalPanel))]
    [HarmonyPrefix]
    private static void ReplaceLastNameInManager(List<KerbalInfo> kerbals)
    {
        foreach (var kerbal in kerbals)
        {
            kerbal.Attributes.SetAttribute<string>("SURNAME", GetLastName(kerbal.Attributes.FirstName));
        }
    }
    
    [HarmonyPatch(typeof(KerbalBehavior),"IFixedUpdate.OnFixedUpdate")]
    [HarmonyFinalizer]
    private static Exception SuppressExceptions()
    {
        return null;
    }
}
