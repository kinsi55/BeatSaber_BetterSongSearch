using HarmonyLib;
using IPA;
using System.Reflection;
using IPALogger = IPA.Logging.Logger;

namespace BetterSongSearch {
	[Plugin(RuntimeOptions.SingleStartInit)]
	public class Plugin {
		internal static IPALogger Log { get; private set; }

		[Init]
		/// <summary>
		/// Called when the plugin is first loaded by IPA (either when the game starts or when the plugin is enabled if it starts disabled).
		/// [Init] methods that use a Constructor or called before regular methods like InitWithConfig.
		/// Only use [Init] with one Constructor.
		/// </summary>
		public void Init(IPALogger logger) {
			Log = logger;
			Log.Info("BetterSongSearch initialized.");

			UI.Manager.Init();

			new Harmony("Kinsi55.BeatSaber.BetterSongSearch").PatchAll(Assembly.GetExecutingAssembly());
		}

		#region BSIPA Config
		//Uncomment to use BSIPA's config
		/*
        [Init]
        public void InitWithConfig(Config conf)
        {
            Configuration.PluginConfig.Instance = conf.Generated<Configuration.PluginConfig>();
            Log.Debug("Config loaded");
        }
        */
		#endregion

		[OnStart]
		public void OnApplicationStart() {


		}

		[OnExit]
		public void OnApplicationQuit() {

		}
	}
}
