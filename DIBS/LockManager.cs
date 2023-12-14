using RoR2;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace DIBS;

internal class LockManager
{
    // Locks are the visual indicator for dibs
    // They are handled independently which is kinda awkward
    private readonly GameObject _purchaseLockPrefab = Addressables
        .LoadAssetAsync<GameObject>("RoR2/Base/Teleporters/PurchaseLock.prefab")
        .WaitForCompletion();

    private readonly Dictionary<NetworkInstanceId, List<GameObject>> _idToLocks = new();

    public void InstantiateLock(NetworkInstanceId targetId)
    {
        // We should only instantiate the lock on the server(?)
        if (!NetworkServer.active) return;
        
        var targetObject = ClientScene.FindLocalObject(targetId);

        List<GameObject> lockObjects = new();
        if (targetObject.GetComponent<ShopTerminalBehavior>())
        {
            var terminalBehavior = targetObject.GetComponent<ShopTerminalBehavior>();
            var terminals = terminalBehavior.serverMultiShopController.terminalGameObjects;
            foreach (var terminal in terminals)
            {
                var lockObject = Object.Instantiate(_purchaseLockPrefab, terminal.transform.position, Quaternion.identity);
                NetworkServer.Spawn(lockObject);
                lockObjects.Add(lockObject);
            }
        }
        else
        {
            var lockObject = Object.Instantiate(_purchaseLockPrefab, targetObject.transform.position, Quaternion.identity);
            NetworkServer.Spawn(lockObject);
            lockObjects.Add(lockObject);
        }

        _idToLocks[targetId] = lockObjects;
    }

    public void DestroyLock(NetworkInstanceId targetId)
    {
        if (!_idToLocks.TryGetValue(targetId, out var lockObjects)) return;
        
        foreach (var lockObject in lockObjects)
        {
            NetworkServer.Destroy(lockObject);
        }

        _idToLocks.Remove(targetId);
    }
}