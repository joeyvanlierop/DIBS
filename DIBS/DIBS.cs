using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using Console = System.Console;
using Random = System.Random;

namespace DIBS
{
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
        
        // Lock stuff
        static GameObject _purchaseLockPrefab;
        readonly List<GameObject> _spawnedLockObjects = new();

        public void Awake()
        {
            _purchaseLockPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Teleporters/PurchaseLock.prefab").WaitForCompletion();
            
            On.RoR2.PingerController.SetCurrentPing += PingerController_SetCurrentPing;
            On.RoR2.Stage.Start += Stage_Start;
            On.RoR2.Interactor.AttemptInteraction += Interactor_AttemptInteraction;
            On.RoR2.Chat.UserChatMessage.ConstructChatString += Chat_UserChatMessage_ConstructChatString;
        }
        
        public static NetworkInstanceId? GetDibber(NetworkInstanceId targetId)
        {
            if (!dibs.ContainsValue(targetId))
            {
                return null;
            }
            
            var dibber = dibs.First(x => x.Value == targetId).Key;
            return dibber;
        }
        
        private bool TryGetDibs(NetworkInstanceId playerId, out NetworkInstanceId targetId)
        {
            return dibs.TryGetValue(playerId, out targetId);
        }

        private void SetDibs(NetworkInstanceId playerId, NetworkInstanceId targetId)
        {
            Chat.AddMessage($"Set dibs: {playerId} {targetId}");
            dibs[playerId] = targetId;
            
            GameObject obj = ClientScene.FindLocalObject(targetId);
            Chat.AddMessage($"DEBUG0");
            var purchaseInteraction = obj.GetComponent<PurchaseInteraction>();
            Chat.AddMessage($"DEBUG0.5");
            LockInteractable(purchaseInteraction);
        }

        private static void RemoveDibs(NetworkInstanceId playerId)
        {
            dibs.Remove(playerId);
        }
        
        private static void ClearDibs()
        {
            dibs.Clear();
        }

        private void PingerController_SetCurrentPing(On.RoR2.PingerController.orig_SetCurrentPing orig, PingerController controller, PingerController.PingInfo pingInfo)
        {
            var user = UsersHelper.GetUser(controller);
            try
            {
                Chat.AddMessage(
                    $"DEBUGP: {user.name} {user.netId}, {pingInfo.targetGameObject.name} {pingInfo.targetNetworkIdentity.netId} ");
            }
            catch (Exception)
            {
                // ignored
            }

            if (pingInfo.targetGameObject)
            {
                var targetObject = pingInfo.targetGameObject;
                
                if ((targetObject.GetComponent<ChestBehavior>() || targetObject.GetComponent<ShopTerminalBehavior>() ||
                     targetObject.GetComponent<ShrineChanceBehavior>()) && !targetObject.name.Contains("Lockbox"))
                {
                    if (TryGetDibs(user.netId, out _))
                    {
                        Chat.AddMessage($"You already have dibs");
                        return;
                    }
                    
                    var target = pingInfo.targetNetworkIdentity;
                    SetDibs(user.netId, target.netId);
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
            var user = UsersHelper.GetUser(self);
            Chat.AddMessage($"DEBUGI: {user.name} {user.netId}, {target.name} {target.GetComponent<NetworkIdentity>().netId} ");
            var purchaseInteraction = target.GetComponent<PurchaseInteraction>();
            if (purchaseInteraction && purchaseInteraction.CanBeAffordedByInteractor(self))
            {
                var playerId = user.netId;
                var targetId = target.GetComponent<NetworkIdentity>().netId;

                Chat.AddMessage(TryGetDibs(playerId, out var dibsId2)
                    ? $"{playerId} has dibs on chest:  {dibsId2}"
                    : $"{playerId} does not have dibs");

                var dibber = GetDibber(targetId);
                if (dibber != null && dibber != playerId)
                {
                    Chat.AddMessage($"Chest has been dibbed by: {dibber}");
                    return;
                }
                
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
        
        private string Chat_UserChatMessage_ConstructChatString(On.RoR2.Chat.UserChatMessage.orig_ConstructChatString orig, Chat.UserChatMessage message)
        {
            if (message.text.Contains("undibs"))
            {
                Chat.AddMessage($"DEBUG UNDIBS: {message.sender}");
                var user = message.sender.GetComponent<NetworkUser>();
                Chat.AddMessage($"Removing dibs for user: {user.netId}");
                RemoveDibs(user.netId);
            }

            return orig(message);
        }
        
        private void LockInteractable(PurchaseInteraction purchaseInteraction)
        {
            Chat.AddMessage($"DEBUG1");
            Chat.AddMessage($"DEBUG2");
            Chat.AddMessage($"{purchaseInteraction} {_purchaseLockPrefab}");
            GameObject lockObject;
            try
            {
                lockObject = Instantiate(_purchaseLockPrefab, purchaseInteraction.transform.position, Quaternion.Euler(0f, 0f, 0f));
            }
            catch (Exception e)
            {
                Chat.AddMessage($"ERR: {e.Message}");
                return;
            }
            Chat.AddMessage($"DEBUG3");
            NetworkServer.Spawn(lockObject);
            Chat.AddMessage($"DEBUG4");
            // purchaseInteraction.NetworklockGameObject = lockObject;
            // Chat.AddMessage($"DEBUG5");

            _spawnedLockObjects.Add(lockObject);
        }
        
        private void unlockInteractable(GameObject lockObject)
        {
            NetworkServer.Destroy(lockObject);
        }
    }
}
