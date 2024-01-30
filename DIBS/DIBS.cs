using BepInEx;
using RoR2;
using RoR2.Audio;
using UnityEngine;
using UnityEngine.Networking;
using Chat = On.RoR2.Chat;
using GlobalEventManager = On.RoR2.GlobalEventManager;
using Interactor = On.RoR2.Interactor;
using PingerController = On.RoR2.PingerController;
using Stage = On.RoR2.Stage;

namespace DIBS;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class DIBS : BaseUnityPlugin
{
    // Mod metadata stuff
    public const string PluginGuid = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "joeyvanlierop";
    public const string PluginName = "DIBS";
    public const string PluginVersion = "1.0.6";

    // This is the central claiming authority
    private ClaimManager _claimManager;
    
    // Sound bites
    private const string NewClaimSound = "Play_gravekeeper_attack2_shoot_singleChain";
    private const string RedeemClaimSound = "Play_gravekeeper_attack2_shoot_singleChain";
    private const string FailClaimSound = "Play_UI_insufficient_funds";
    
    // Networked Sounds
    private readonly NetworkSoundEventDef NewClaimSoundNet = AssetsHelper.CreateNetworkSoundEventDef(NewClaimSound);
    private readonly NetworkSoundEventDef RedeemClaimSoundNet = AssetsHelper.CreateNetworkSoundEventDef(RedeemClaimSound);
    private readonly NetworkSoundEventDef FailClaimSoundNet = AssetsHelper.CreateNetworkSoundEventDef(FailClaimSound);

    public void OnEnable()
    {
        // Create our claim manager
        LockManager lockManager = new();
        _claimManager = new ClaimManager(lockManager);
        
        // Set up all of our hooks
        Stage.Start += Stage_Start;
        PingerController.SetCurrentPing += PingerController_SetCurrentPing;
        Interactor.PerformInteraction += Interactor_PerformInteraction;
        Chat.UserChatMessage.ConstructChatString += Chat_UserChatMessage_ConstructChatString;
        GlobalEventManager.OnPlayerCharacterDeath += GlobalEventManager_OnPlayerCharacterDeath;
    }

    private void GlobalEventManager_OnPlayerCharacterDeath(GlobalEventManager.orig_OnPlayerCharacterDeath orig, RoR2.GlobalEventManager self, DamageReport damageReport, NetworkUser victim)
    {
        // Remove the dead players dibs (if they exist)
        _claimManager.RemoveClaim(victim.netId);

        orig(self, damageReport, victim);
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
            Util.PlaySound(FailClaimSound, pinged);
            orig(controller, pingInfo);
            return;
        }

        // Guard for the object already being claimed
        // (only one claim can exist per object)
        if (_claimManager.GetClaimer(pingInfo.targetNetworkIdentity.netId) != null)
        {
            Util.PlaySound(FailClaimSound, pinged);
            orig(controller, pingInfo);
            return;
        }

        // You've passed the test!
        // Enjoy your claim
        var target = pingInfo.targetNetworkIdentity;
        _claimManager.SetClaim(pinger.netId, target.netId);
        Util.PlayAttackSpeedSound(NewClaimSound, pinged, 1);

        // ...and pass on the ping
        orig(controller, pingInfo);
    }

    private void Interactor_PerformInteraction(Interactor.orig_PerformInteraction orig, RoR2.Interactor interactor,
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
            EntitySoundManager.EmitSoundServer(FailClaimSoundNet.akId, target);
            return;
        }

        // Guard for interactor having another claim
        // This handles the case where the object isn't claimed, but the user has an active claim
        if (_claimManager.TryGetClaim(playerId, out var claimedId) && claimedId != targetId)
        {
            EntitySoundManager.EmitSoundServer(FailClaimSoundNet.akId, target);
            return;
        }

        // Now we can redeem the claim if needed
        if (claimer == playerId)
        {
            EntitySoundManager.EmitSoundServer(RedeemClaimSoundNet.akId, target);
            _claimManager.RemoveClaim(playerId);
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