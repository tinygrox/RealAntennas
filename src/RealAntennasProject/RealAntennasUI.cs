using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

namespace RealAntennas
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    internal class RealAntennasUI : MonoBehaviour
    {
        private const string modName = "RealAntennas";
        private const string icon = "RealAntennas/RealAntennas";

        private bool showUI = false;
        private ApplicationLauncherButton button;

        private Rect winPos = new Rect(450, 100, 400, 100);
        private const int winID = 731806;
        private GameObject antennaConsoleGO = null;

        protected void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
        }

        private void Update()
        {
            if (GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(KeyCode.I))
                showUI = !showUI;
        }

        private void OnGUI()
        {
            if (showUI)
            {
                winPos = GUILayout.Window(winID, winPos, WindowGUI, $"{modName}", GUILayout.MinWidth(200));
            }
        }

        private void WindowGUI(int ID)
        {
            GUILayout.BeginVertical();
            RACommNetScenario scen = RACommNetScenario.Instance as RACommNetScenario;
            VesselCounts(out int vessels, out int groundStations, out int antennas, out string net);
            GUILayout.Label($"{RACommNetScenario.assembly.GetName().Name} v{RACommNetScenario.info.FileVersion}");
            GUILayout.Label($"{net}");

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("#RealAntennas_Label_Vessels", vessels));//$"Vessels: {vessels}"
            GUILayout.Label(Localizer.Format("#RealAntennas_Label_GroundStations", groundStations));//$"GroundStations: {groundStations}"
            GUILayout.Label(Localizer.Format("#RealAntennas_Label_Antennas_Vessel", $"{(float)antennas / vessels:F1}"));//$"Antennas/vessel: {}"
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localizer.Format("#RealAntennas_Label_Name"), GUILayout.ExpandWidth(true));//"Name"
            GUILayout.Label(Localizer.Format("#RealAntennas_Label_Iterations"), GUILayout.ExpandWidth(true));//"Iterations"
            GUILayout.Label(Localizer.Format("#RealAntennas_Label_AvgTime"), GUILayout.ExpandWidth(true));//"Avg Time (ms)"
            GUILayout.Label(Localizer.Format("#RealAntennas_Label_RunTime"), GUILayout.ExpandWidth(true));//"Runs/sec"
            GUILayout.EndHorizontal();
            foreach (KeyValuePair<string, MetricsElement> kvp in scen.metrics.data)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{kvp.Key}", GUILayout.ExpandWidth(true));
                GUILayout.Label($"{kvp.Value.iterations}");
                GUILayout.Label($"{kvp.Value.hysteresisTime:F4}");
                GUILayout.Label($"{kvp.Value.iterations / Time.timeSinceLevelLoad:F1}");
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            if (MapView.fetch is MapView map && MapView.MapIsEnabled)
            {
                MapUI.NetUIConfigurationWindow win = scen.UI.configWindow;
                if (GUILayout.Button(Localizer.Format("#RealAntennas_Label_HideOrShowWindow", $"{(win.showUI ? Localizer.Format("#RealAntennas_Label_Hide") : Localizer.Format("#RealAntennas_Label_Show"))}")))//$"{(win.showUI ? "Hide" : "Show")} Config Window"
                {
                    if (win.showUI) win.HideWindow(); else win.ShowWindow();
                }
            }

            if (antennaConsoleGO is null && GUILayout.Button(Localizer.Format("#RealAntennas_Button_LaunchControlConsole")))//"Launch Control Console"
            {
                antennaConsoleGO = new GameObject();
                antennaConsoleGO.AddComponent(typeof(RemoteAntennaControlUI));
            } else if (antennaConsoleGO is GameObject && GUILayout.Button(Localizer.Format("#RealAntennas_Button_CloseControlConsole"))) {//"Close Control Console"
                antennaConsoleGO.DestroyGameObject();
                antennaConsoleGO = null;
            }
            GUI.DragWindow();
        }

        private void ShowWindow() => showUI = true;
        private void HideWindow() => showUI = false;
        private void OnSceneChange(GameScenes s) => showUI = false;

        private void VesselCounts(out int vessels, out int groundStations, out int antennas, out string net)
        {
            vessels = groundStations = antennas = 0;
            if ((RACommNetScenario.Instance as RACommNetScenario)?.Network?.CommNet is RACommNetwork racn)
            {
                net = $"{racn}";
                foreach (RACommNode node in racn.Nodes)
                {
                    if (node.isHome)
                    {
                        groundStations++;
                    }
                    else
                    {
                        vessels++;
                        antennas += node.RAAntennaList.Count;
                    }
                }
            }
            else
            {
                net = string.Empty;
            }
        }

        private void OnGuiAppLauncherReady()
        {
            if (HighLogic.CurrentGame.Parameters.CustomParams<RAParameters>().performanceUI)
            {
                try
                {
                    button = ApplicationLauncher.Instance.AddModApplication(
                        ShowWindow,
                        HideWindow,
                        null,
                        null,
                        null,
                        null,
                        ApplicationLauncher.AppScenes.ALWAYS & ~ApplicationLauncher.AppScenes.MAINMENU,
                        GameDatabase.Instance.GetTexture($"{icon}", false));
                    GameEvents.onGameSceneLoadRequested.Add(OnSceneChange);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{modName} failed to register button");
                    Debug.LogException(ex);
                }
            }
        }

        public void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
            if (button != null)
                ApplicationLauncher.Instance.RemoveModApplication(button);
        }
    }
}