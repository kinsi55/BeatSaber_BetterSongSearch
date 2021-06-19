using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using HMUI;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSongSearch.UI {
	static class Manager {
		public static void Init() {
			MenuButtons.instance.RegisterButton(new MenuButton("Better Song Search", "Search songs, but better", ShowFlow, true));
		}

		internal static FlowCoordinator _parentFlow { get; private set; }
		internal static BSSFlowCoordinator _flow { get; private set; }
		internal static Button.ButtonClickedEvent goToSongSelect { get; private set; } = null;

		public static void ShowFlow() {
			goToSongSelect =
				(GameObject.Find("SoloButton") ?? GameObject.Find("Wrapper/BeatmapWithModifiers/BeatmapSelection/EditButton"))
				?.GetComponent<NoTransitionsButton>()?.onClick;

			if(_flow == null)
				_flow = BeatSaberUI.CreateFlowCoordinator<BSSFlowCoordinator>();

			_parentFlow = BeatSaberUI.MainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();

			BeatSaberUI.PresentFlowCoordinator(_parentFlow, _flow);
		}
	}
}
