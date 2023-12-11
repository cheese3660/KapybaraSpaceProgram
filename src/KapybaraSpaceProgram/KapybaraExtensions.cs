using UnityEngine;

namespace KapybaraSpaceProgram;

public static class KapybaraExtensions
{
    public static Transform FirstChildOrDefault(this Transform parent, Func<Transform, bool> query, bool recurse = false)
    {
        Transform result = null;
        // for (int i = 0; i < parent.childCount; i++)
        // {
        //     var child = parent.GetChild(i);
        //     if (query(child))
        //     {
        //         return child;
        //     }
        //     result = FirstChildOrDefault(child, query);
        // }
        foreach (Transform child in parent)
        {
            if (query(child))
            {
                return child;
            }
            if (recurse)
                result = FirstChildOrDefault(child, query,true);
        }

        return result;
    }

    private static string Repeat(string s, int times)
    {
        var result = "";
        for (int i = 0; i < times; i++)
        {
            result += s;
        }
        return result;
    }
    public static void DumpTree(this Transform parent, int depth = 0)
    {
        KapybaraSpaceProgramPlugin.Instance.SWLogger.LogInfo($"{Repeat("    ", depth)}{parent.name}");
        foreach (Transform child in parent)
        {
            child.DumpTree(depth + 1);
        }
    }
}