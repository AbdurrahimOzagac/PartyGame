using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterParty
{
    // First scene of every build (SP_Boot). Decides what this process is from its command line:
    //   -party-server ...   a room's dedicated server (ServerRuntime)
    //   -autopilot ...      a self-playing client (Autopilot)
    //   nothing             the game: go to the menu
    public class Bootstrap : MonoBehaviour
    {
        public string menuScene = "SP_Menu";

        void Start()
        {
            var argv = Environment.GetCommandLineArgs();
            if (ServerArgs.TryParse(argv, Environment.GetEnvironmentVariables(), out var server, out var error))
            {
                gameObject.AddComponent<ServerRuntime>().Begin(server);
                return;
            }
            if (error != null)
            {
                Console.Out.WriteLine("@@FATAL " + error);
                Debug.LogError("[Bootstrap] " + error);
                Application.Quit(2);
                return;
            }
            if (AutopilotArgs.TryParse(argv, out var pilot))
            {
                gameObject.AddComponent<Autopilot>().Begin(pilot);
                return;
            }
            if (Application.CanStreamedLevelBeLoaded(menuScene)) SceneManager.LoadScene(menuScene);
        }
    }
}
