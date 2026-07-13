using UnityEngine;
using Rollrate.Core;

namespace Rollrate.UI
{
    /// <summary>
    /// DEBUG ONLY - temporary way to enter the Shop node before the Map
    /// exists. Wire a test button in the Combat scene to EnterShop().
    /// Once the Map is built, nodes will call NodeSceneLoader.EnterNode(...)
    /// directly themselves, and this script (and its test button) can be deleted.
    /// </summary>
    public class DebugMapNodeButtons : MonoBehaviour
    {
        [SerializeField] private string shopSceneName = "ShopScene";

        public void EnterShop()
        {
            NodeSceneLoader.EnterNode(shopSceneName);
        }
    }
}
