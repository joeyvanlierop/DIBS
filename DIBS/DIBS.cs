using System.Collections.Generic;
using BepInEx;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DIBS
{
    [BepInDependency(NetworkingAPI.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class DIBS : BaseUnityPlugin
    {
        // Mod stuff
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "joeyvanlierop";
        public const string PluginName = "DIBS";
        public const string PluginVersion = "0.0.1";
        
        // This is the dictionary that keeps track of which players currently have dibs
        private static Dictionary<NetworkInstanceId, NetworkInstanceId> dibs = new();

        public void Awake()
        {
            NetworkingAPI.RegisterMessageType<SyncDibsMessage>();

            On.RoR2.PingerController.SetCurrentPing += PingerController_SetCurrentPing;
            On.RoR2.Stage.Start += Stage_Start;
            On.RoR2.Interactor.AttemptInteraction += Interactor_AttemptInteraction;
        }
        
        public static bool TryGetDibs(NetworkInstanceId playerId, out NetworkInstanceId targetId)
        {
            return dibs.TryGetValue(playerId, out targetId);
        }

        public static void SetDibs(NetworkInstanceId playerId, NetworkInstanceId targetId)
        {
            Chat.AddMessage($"Set dibs: {playerId} {targetId}");
            dibs[playerId] = targetId;
        }

        public static void RemoveDibs(NetworkInstanceId playerId)
        {
            // GameObject obj = ClientScene.FindLocalObject(targetId);
            dibs.Remove(playerId);
        }
        
        public static void ClearDibs()
        {
            dibs.Clear();
        }

        private void PingerController_SetCurrentPing(On.RoR2.PingerController.orig_SetCurrentPing orig, PingerController controller, PingerController.PingInfo pingInfo)
        {
            Chat.AddMessage($"DEBUGP: {controller.name} {controller.netId}, {pingInfo.targetGameObject.name} {pingInfo.targetNetworkIdentity.netId} ");
            if (pingInfo.targetGameObject)
            {
                var targetObject = pingInfo.targetGameObject;
                
                if (targetObject.GetComponent<ChestBehavior>() || targetObject.GetComponent<ShopTerminalBehavior>())
                {
                    if (TryGetDibs(controller.netId, out _))
                    {
                        Chat.AddMessage($"You already have dibs");
                        return;
                    }
                    var targetIdentity = pingInfo.targetNetworkIdentity;
                    new SyncDibsMessage(controller.netId, targetIdentity.netId).Send(NetworkDestination.Clients);
                }
            }

            orig(controller, pingInfo);
        }

        private void Stage_Start(On.RoR2.Stage.orig_Start orig, RoR2.Stage stage)
        {
            ClearDibs();

            orig(stage);
        }
        
        private void Interactor_AttemptInteraction(On.RoR2.Interactor.orig_AttemptInteraction orig, Interactor self, GameObject target)
        {
            
            Chat.AddMessage($"DEBUGI: {self.name} {self.netId}, {target.name} {target.GetComponent<NetworkIdentity>().netId} ");
            var purchaseInteraction = target.GetComponent<PurchaseInteraction>();
            if (purchaseInteraction && purchaseInteraction.CanBeAffordedByInteractor(self))
            {
                var playerId = self.netId;
                var targetId = target.GetComponent<NetworkIdentity>().netId;

                Chat.AddMessage(TryGetDibs(playerId, out var dibsId2)
                    ? $"{playerId} has dibs on chest:  {dibsId2}"
                    : $"{playerId} does not have dibs");

                if (TryGetDibs(playerId, out var dibsId))
                {
                    if (dibsId != targetId)
                    {
                        Chat.AddMessage($"You have dibs on another chest: {playerId} {dibsId}");
                        return;
                    }
                    
                    Chat.AddMessage($"Removed dibs: {playerId} {dibsId}");
                    RemoveDibs(playerId);
                }
            }

            orig(self, target);
        }
    }
    
    public class SyncDibsMessage : INetMessage
    {
        private NetworkInstanceId playerId;
        private NetworkInstanceId targetId;
        
        public SyncDibsMessage()
        {
        }

        public SyncDibsMessage(NetworkInstanceId playerId, NetworkInstanceId targetId)
        {
            this.playerId = playerId;
            this.targetId = targetId;
        }

        public void Serialize(NetworkWriter writer)
        {
            Chat.AddMessage($"SEND Player: {playerId}, Chest: {targetId}");
            writer.Write(playerId);
            writer.Write(targetId);
        }

        public void Deserialize(NetworkReader reader)
        {
            playerId = reader.ReadNetworkId();
            targetId = reader.ReadNetworkId();
        }

        public void OnReceived()
        {
            // GameObject targetObject = Util.FindNetworkObject(targetId);
            DIBS.SetDibs(playerId, targetId);
        }
    }

}
