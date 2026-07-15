using UnityEngine;
using Rollrate.Core;

namespace Rollrate.UI
{
    /// <summary>
    /// DEBUG ONLY - temporary way to enter Map nodes before the real Map
    /// exists. Wire test buttons in the Combat scene to these methods.
    /// Once the Map is built, nodes will call NodeSceneLoader.EnterNode(...)
    /// directly themselves, and this script (and its test buttons) can be deleted.
    /// </summary>
    public class DebugMapNodeButtons : MonoBehaviour
    {
        [SerializeField] private string shopSceneName = "ShopScene";
        [SerializeField] private string collectionSceneName = "CollectionScene";
        [SerializeField] private string restSceneName = "RestNodeScene";
        [SerializeField] private string dismantleSceneName = "DismantleScene";

        /// <summary>Merchant node: buy dice/modules, reroll offers, paid Repair/Increase Max HP.</summary>
        public void EnterShop()
        {
            NodeSceneLoader.EnterNode(shopSceneName);
        }

        /// <summary>Collection screen: equip/swap owned modules per slot. Free, no HP/Scrap effect.</summary>
        public void EnterCollection()
        {
            NodeSceneLoader.EnterNode(collectionSceneName);
        }

        /// <summary>Rest node (Falò): free half-missing-HP heal (once per visit), then can open Collection.</summary>
        public void EnterRest()
        {
            NodeSceneLoader.EnterNode(restSceneName);
        }

        /// <summary>Dismantle node: destroy an owned die/module for Grade-based Scrap.</summary>
        public void EnterDismantle()
        {
            NodeSceneLoader.EnterNode(dismantleSceneName);
        }
    }
}
