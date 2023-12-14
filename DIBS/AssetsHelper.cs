using RoR2;
using UnityEngine;

namespace DIBS;

public static class AssetsHelper
{
    internal static NetworkSoundEventDef CreateNetworkSoundEventDef(string eventName)
    {
        var networkSoundEventDef = ScriptableObject.CreateInstance<NetworkSoundEventDef>();
        networkSoundEventDef.akId = AkSoundEngine.GetIDFromString(eventName);
        networkSoundEventDef.eventName = eventName;

        R2API.ContentAddition.AddNetworkSoundEventDef(networkSoundEventDef);

        return networkSoundEventDef;
    }
}