using BetterSongSearch.UI.SplitViews;
using HarmonyLib;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
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
		public void Init(IPALogger logger, Config conf) {
			Log = logger;
			Log.Info("BetterSongSearch initialized.");
			Settings.cfgInstance = PluginConfig.Instance = conf.Generated<PluginConfig>();
			new Harmony("Kinsi55.BeatSaber.BetterSongSearch").PatchAll(Assembly.GetExecutingAssembly());
		}

		[OnStart]
		public void OnApplicationStart() {
			BeatSaberMarkupLanguage.Util.MainMenuAwaiter.MainMenuInitializing += MainMenuInit;
		}

		public void MainMenuInit() {
			UI.Manager.Init();
			SharedCoroutineStarter.Init();
		}

		[OnExit]
		public void OnApplicationQuit() {

		}
	}
}
