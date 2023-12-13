using BepInEx;
using On.RoR2;
using UnityEngine;
using UnityEngine.Networking;
using NetworkUser = RoR2.NetworkUser;
using PurchaseInteraction = RoR2.PurchaseInteraction;

namespace DIBS;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class DIBS : BaseUnityPlugin
{
    // Mod stuff
    public const string PluginGuid = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "joeyvanlierop";
    public const string PluginName = "DIBS";
    public const string PluginVersion = "0.0.1";

    // Managers
    private readonly ClaimManager _claimManager;

    public DIBS()
    {
        LockManager lockManager = new();
        _claimManager = new ClaimManager(lockManager);
    }

    public void OnEnable()
    {
        Stage.Start += Stage_Start;
        PingerController.SetCurrentPing += PingerController_SetCurrentPing;
        Interactor.AttemptInteraction += Interactor_AttemptInteraction;
        Chat.UserChatMessage.ConstructChatString += Chat_UserChatMessage_ConstructChatString;
    }

    public void OnDisable()
    {
        Stage.Start -= Stage_Start;
        PingerController.SetCurrentPing -= PingerController_SetCurrentPing;
        Interactor.AttemptInteraction -= Interactor_AttemptInteraction;
        Chat.UserChatMessage.ConstructChatString -= Chat_UserChatMessage_ConstructChatString;
    }

    private void Stage_Start(Stage.orig_Start orig, RoR2.Stage stage)
    {
        // Start from a clean slate at the start of each stage
        _claimManager.ClearDibs();

        // And definitely do not remove this
        orig(stage);
    }

    private void PingerController_SetCurrentPing(PingerController.orig_SetCurrentPing orig,
        RoR2.PingerController controller, RoR2.PingerController.PingInfo pingInfo)
    {
        // This is the user that pinged
        var pinger = UsersHelper.GetUser(controller);

        // Guard for ping hitting something
        if (pingInfo.targetGameObject)
        {
            orig(controller, pingInfo);
            return;
        }

        // This is the object that the pingee hit
        var pingee = pingInfo.targetGameObject;

        // Guard for the object actually being claimable 
        // i.e. make sure its a chest, cradle, triple, chance, barrel, etc.
        if (ClaimManager.IsValidObject(pingee))
        {
            orig(controller, pingInfo);
            return;
        }

        // Guard for the user having an active claim 
        // (dont let the user claim multiple chests)
        if (_claimManager.TryGetClaim(pinger.netId, out _))
        {
            orig(controller, pingInfo);
            return;
        }

        // Guard for the object already being claimed
        // (dont let someone else steal the claim)
        if (_claimManager.GetClaimer(pingInfo.targetNetworkIdentity.netId) != null)
        {
            orig(controller, pingInfo);
            return;
        }


        // You've passed the test!
        // Enjoy your claim
        var target = pingInfo.targetNetworkIdentity;
        _claimManager.SetClaim(pinger.netId, target.netId);

        // ...and pass on the ping
        orig(controller, pingInfo);
    }


    private void Interactor_AttemptInteraction(Interactor.orig_AttemptInteraction orig, RoR2.Interactor self,
        GameObject target)
    {
        var user = UsersHelper.GetUser(self);
        var purchaseInteraction = target.GetComponent<PurchaseInteraction>();
        if (purchaseInteraction && purchaseInteraction.CanBeAffordedByInteractor(self))
        {
            var playerId = user.netId;
            var targetId = target.GetComponent<NetworkIdentity>().netId;

            var dibber = _claimManager.GetClaimer(targetId);
            if (dibber != null && dibber != playerId)
            {
                return;
            }

            if (_claimManager.TryGetClaim(playerId, out var dibsId))
            {
                if (dibsId != targetId)
                {
                    return;
                }

                _claimManager.RemoveClaim(playerId);
            }
        }

        orig(self, target);
    }

    private string Chat_UserChatMessage_ConstructChatString(
        Chat.UserChatMessage.orig_ConstructChatString orig, RoR2.Chat.UserChatMessage message)
    {
        if (message.text.Contains("undibs"))
        {
            var user = message.sender.GetComponent<NetworkUser>();
            _claimManager.RemoveClaim(user.netId);
        }

        return orig(message);
    }
}