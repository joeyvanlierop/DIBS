using System.Collections.Generic;
using System.Linq;
using R2API.Utils;
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
        if (!_claims.ContainsValue(targetId))
        {
            return null;
        }
        return _claims.First(x => x.Value == targetId).Key;
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
        // We cant just use the dictionaries built-in clear function
        // TODO: Review this, because it throws an error when new stage is called (I think)
        //  Might have to do with host vs client?
        _claims.Keys.ForEachTry(RemoveClaim);
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