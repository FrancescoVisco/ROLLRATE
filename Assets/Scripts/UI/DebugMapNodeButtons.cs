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
        [SerializeField] private string restSceneName = "RestNodeScene";
        [SerializeField] private string furnaceSceneName = "FurnaceScene";

        /// <summary>Merchant node: buy dice, reroll offers, paid Increase Max HP.</summary>
        public void EnterShop()
        {
            NodeSceneLoader.EnterNode(shopSceneName);
        }

        /// <summary>Rest node (Falò): free half-missing-HP heal (once per visit).</summary>
        public void EnterRest()
        {
            NodeSceneLoader.EnterNode(restSceneName);
        }

        /// <summary>Furnace node: fuse 2 owned dice of the same Type.</summary>
        public void EnterFurnace()
        {
            NodeSceneLoader.EnterNode(furnaceSceneName);
        }
    }
}
