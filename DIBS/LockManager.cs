using System;
using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace DIBS;

internal class LockManager
{
    // Locks are the visual indicator for dibs
    // They are handled independently which is kinda awkward
    private GameObject _purchaseLockPrefab = Addressables
        .LoadAssetAsync<GameObject>("RoR2/Base/Teleporters/PurchaseLock.prefab")
        .WaitForCompletion();

    private Dictionary<NetworkInstanceId, List<GameObject>> _idToLocks = new();

    public void InstantiateLock(NetworkInstanceId targetId)
    {
        var targetObject = ClientScene.FindLocalObject(targetId);

        List<GameObject> lockObjects = new();
        if (targetObject.GetComponent<ShopTerminalBehavior>())
        {
            var terminalBehavior = targetObject.GetComponent<ShopTerminalBehavior>();
            var terminals = terminalBehavior.serverMultiShopController.terminalGameObjects;
            foreach (var terminal in terminals)
            {
                GameObject lockObject = _purchaseLockPrefab.InstantiateClone("LockObject");
                lockObject.transform.position = terminal.transform.position;
                lockObjects.Add(lockObject);
            }
        }
        else
        {
            GameObject lockObject = _purchaseLockPrefab.InstantiateClone("LockObject");
            lockObject.transform.position = targetObject.transform.position;
            lockObjects.Add(lockObject);
        }


        _idToLocks.Add(targetId, lockObjects);
    }

    public void DestroyLock(NetworkInstanceId targetId)
    {
        if (_idToLocks.TryGetValue(targetId, out var lockObjects))
        {
            foreach (var lockObject in lockObjects)
            {
                NetworkServer.Destroy(lockObject);
            }
        }
    }
}