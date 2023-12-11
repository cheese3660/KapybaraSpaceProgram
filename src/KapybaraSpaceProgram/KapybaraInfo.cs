using JetBrains.Annotations;
using SpaceWarp.API.Assets;
using UnityEngine;

namespace KapybaraSpaceProgram;

public class KapybaraInfo
{
    public string Filename;
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;
    public Vector3 LocalScale;
    public Dictionary<string, string> ReparentingMap;
    public List<string> HeadObjectNames;
    public List<string> FemaleObjectNames;
    public List<string> MaleObjectNames;
    
    private GameObject _gameObject;
    public GameObject GameObject => _gameObject ??=
        AssetManager.GetAsset<GameObject>($"{KapybaraSpaceProgramPlugin.Instance.SWMetadata.Guid}/kapybara/{Filename}");
}