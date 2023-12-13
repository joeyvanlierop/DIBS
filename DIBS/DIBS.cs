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
    // Mod metadata stuff
    public const string PluginGuid = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "joeyvanlierop";
    public const string PluginName = "DIBS";
    public const string PluginVersion = "0.0.1";

    // This is the central claiming authority
    private ClaimManager _claimManager;
    
    // Sound bites
    private const string NewClaimSound = "Play_gravekeeper_attack2_shoot_singleChain";
    private const string RedeemClaimSound = "Play_gravekeeper_attack2_shoot_singleChain";
    private const string FailClaimSound = "Play_UI_insufficient_funds";

    public void OnEnable()
    {
        // Create our claim manager
        LockManager lockManager = new();
        _claimManager = new ClaimManager(lockManager);

        // Set up all of our hooks
        Stage.Start += Stage_Start;
        PingerController.SetCurrentPing += PingerController_SetCurrentPing;
        Interactor.AttemptInteraction += Interactor_AttemptInteraction;
        Chat.UserChatMessage.ConstructChatString += Chat_UserChatMessage_ConstructChatString;
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
        var pinger = UsersHelper.GetUser(controller); // This is the user that pinged
        var pinged = pingInfo.targetGameObject; // This is the object that the pinger hit

        // Guard for the pinging a claimable object 
        // (make sure its a chest, cradle, triple, chance, barrel, etc.)
        if (pinged == null || !ClaimManager.IsValidObject(pinged))
        {
            orig(controller, pingInfo);
            return;
        }

        // Guard for the user having an active claim 
        // (dont let the user claim multiple chests)
        if (_claimManager.TryGetClaim(pinger.netId, out _))
        {
            RoR2.Util.PlaySound(FailClaimSound, pinged);
            orig(controller, pingInfo);
            return;
        }

        // Guard for the object already being claimed
        // (only one claim can exist per object)
        if (_claimManager.GetClaimer(pingInfo.targetNetworkIdentity.netId) != null)
        {
            RoR2.Util.PlaySound(FailClaimSound, pinged);
            orig(controller, pingInfo);
            return;
        }

        // You've passed the test!
        // Enjoy your claim
        var target = pingInfo.targetNetworkIdentity;
        _claimManager.SetClaim(pinger.netId, target.netId);
        RoR2.Util.PlayAttackSpeedSound(NewClaimSound, pinged, 1);

        // ...and pass on the ping
        orig(controller, pingInfo);
    }

    private void Interactor_AttemptInteraction(Interactor.orig_AttemptInteraction orig, RoR2.Interactor interactor,
        GameObject target)
    {
        // Guard for the object being purchasable 
        // i.e. make sure its a chest, cradle, triple, chance, barrel, etc.
        if (!ClaimManager.IsValidObject(target))
        {
            orig(interactor, target);
            return;
        }

        // Extract the purchase interaction from the target
        var purchaseInteraction = target.GetComponent<PurchaseInteraction>();

        // Guard for the user having enough money
        if (!purchaseInteraction.CanBeAffordedByInteractor(interactor))
        {
            orig(interactor, target);
            return;
        }

        // Get the user and target network ids
        var user = UsersHelper.GetUser(interactor);
        var playerId = user.netId;
        var targetId = target.GetComponent<NetworkIdentity>().netId;

        // Guard for correct claim
        // i.e. is the object claimed, and if so, does the interactor have dibs
        var claimer = _claimManager.GetClaimer(targetId);
        if (claimer != null && claimer != playerId)
        {
            RoR2.Util.PlaySound(FailClaimSound, target);
            return;
        }

        // Guard for interactor having another claim
        // This handles the case where the object isn't claimed, but the user has an active claim
        if (_claimManager.TryGetClaim(playerId, out var claimedId) && claimedId != targetId)
        {
            RoR2.Util.PlaySound(FailClaimSound, target);
            return;
        }

        // Now we can redeem the claim if needed
        if (claimer == playerId)
        {
            _claimManager.RemoveClaim(playerId);
            RoR2.Util.PlayAttackSpeedSound(RedeemClaimSound, target, 1);
        }

        // ...and finally continue the interaction
        orig(interactor, target);
    }

    // Silly chat command
    // TODO: Make this even sillier
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