using UnityEngine;

namespace SephiriaDamageMeter
{
    /// <summary>
    /// AddOn entry point. The game's AddOnLoader instantiates this class from
    /// metadata.json (entryClass) and calls OnModLoaded.
    /// </summary>
    public sealed class DamageMeterMod : HorayModBase
    {
        private GameObject _controllerObject;

        protected override void OnModLoaded()
        {
            DamageMeterLog.Initialize();
            DamageMeterLog.Info("Loading v" + (metadata.modVersion ?? "?") + " ...");

            if (DamageMeterController.Instance != null)
            {
                DamageMeterLog.Warn("Controller already exists; reusing it.");
                _controllerObject = DamageMeterController.Instance.gameObject;
                return;
            }

            _controllerObject = new GameObject("SephiriaDamageMeterController");
            Object.DontDestroyOnLoad(_controllerObject);
            _controllerObject.AddComponent<DamageMeterController>();
            DamageMeterLog.Info("Loaded. Panel will appear below the minimap once a run starts.");
        }

        protected override void OnModUnloaded()
        {
            DamageMeterLog.Info("Unloading ...");
            if (_controllerObject != null)
            {
                Object.Destroy(_controllerObject);
                _controllerObject = null;
            }
        }
    }
}
