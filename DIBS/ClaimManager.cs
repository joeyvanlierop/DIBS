using System.Collections.Generic;
using System.Linq;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DIBS;

internal class ClaimManager
{
    // This is the dictionary that keeps track of which players currently have claims
    private Dictionary<NetworkInstanceId, NetworkInstanceId> _claims = new();
    private LockManager _lockManager;

    public ClaimManager(LockManager lockManager)
    {
        _lockManager = lockManager;
    }

    public NetworkInstanceId? GetClaimer(NetworkInstanceId targetId)
    {
        return _claims.FirstOrDefault(x => x.Value == targetId).Key;
    }

    public bool TryGetClaim(NetworkInstanceId playerId, out NetworkInstanceId targetId)
    {
        return _claims.TryGetValue(playerId, out targetId);
    }

    public void SetClaim(NetworkInstanceId playerId, NetworkInstanceId targetId)
    {
        // Create the claim and destroy the lock
        _claims[playerId] = targetId;
        _lockManager.InstantiateLock(targetId);
    }

    public void RemoveClaim(NetworkInstanceId playerId)
    {
        // Guard for when the player doesnt have a claim
        if (!_claims.TryGetValue(playerId, out var targetId)) return;

        // Remove the claim and destroy the lock
        _claims.Remove(playerId);
        _lockManager.DestroyLock(targetId);
    }

    public void ClearDibs()
    {
        // A little verbose just to make sure we really cleanup the dibs
        // We cant just use the dictionaries built-in clear function
        foreach (var targetId in _claims.Keys)
        {
            RemoveClaim(targetId);
        }
    }

    public static bool IsValidObject(GameObject target)
    {
        // TODO: Maybe swap the inner to be the guard?
        if ((target.GetComponent<ChestBehavior>() && !target.name.Contains("Lockbox")) ||
            (target.GetComponent<ShopTerminalBehavior>() && !target.name.Contains("Duplicator")) ||
            (target.GetComponent<ShrineChanceBehavior>()))
        {
            var purchaseInteraction = target.GetComponent<PurchaseInteraction>();
            return purchaseInteraction.available;
        }

        return false;
    }
}