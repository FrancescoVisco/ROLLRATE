using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rollrate.Core
{
    /// <summary>
    /// Generic helper for entering/exiting a "node" scene (Shop, and in the
    /// future Combat, Archive, Glitch, etc.) additively on top of the Map
    /// scene, which stays loaded and alive throughout. GameState/RunManager
    /// are unaffected since they live on a DontDestroyOnLoad object.
    ///
    /// Usage: call EnterNode("ShopScene") when the player selects a node on
    /// the Map; call ExitNode("ShopScene") from a "Leave" button inside that
    /// node's scene to return to the Map.
    /// </summary>
    public static class NodeSceneLoader
    {
        /// <summary>
        /// Loads a node's scene additively on top of whatever is currently
        /// loaded. Does nothing (and logs a warning) if that scene is
        /// already loaded, preventing duplicate/stacked node instances.
        /// </summary>
        public static void EnterNode(string sceneName)
        {
            Scene existing = SceneManager.GetSceneByName(sceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                Debug.LogWarning($"[NodeSceneLoader] {sceneName} is already loaded - ignoring duplicate EnterNode call.");
                return;
            }

            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }

        /// <summary>Unloads a node's scene, returning to whatever remains loaded underneath (the Map).</summary>
        public static void ExitNode(string sceneName)
        {
            SceneManager.UnloadSceneAsync(sceneName);
        }
    }
}
