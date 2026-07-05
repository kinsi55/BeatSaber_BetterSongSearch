using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using HMUI;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSongSearch.UI {
	static class Manager {
		public static void Init() {
			MenuButtons.Instance.RegisterButton(new MenuButton("Better Song Search", "Search songs, but better", ShowFlow, true));
		}

		internal static FlowCoordinator _parentFlow { get; private set; }
		internal static BSSFlowCoordinator _flow { get; private set; }
		//internal static Button.ButtonClickedEvent goToSongSelect { get; private set; } = null;
		internal static MapPlayDelegate playSongCallback { get; private set; } = null;

		public delegate void MapPlayDelegate(BeatmapLevel level, BeatmapKey key);

		public static void ShowFlow() => ShowFlow(false);
		public static void ShowFlow(bool immediately) {
			var goToSongSelect =
				(GameObject.Find("SoloButton") ?? GameObject.Find("Wrapper/BeatmapWithModifiers/BeatmapSelection/EditButton"))
				?.GetComponent<NoTransitionsButton>()?.onClick;

			ShowFlow(immediately, goToSongSelect == null ? 
				(MapPlayDelegate)null : 
				(_level, _key) => {
					goToSongSelect.Invoke();
				}
			);
		}
		public static void ShowFlow(bool immediately, MapPlayDelegate playSongCallback = null) {
			Manager.playSongCallback = playSongCallback;

			if(_flow == null)
				_flow = BeatSaberUI.CreateFlowCoordinator<BSSFlowCoordinator>();

			_parentFlow = BeatSaberUI.MainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();

			BeatSaberUI.PresentFlowCoordinator(_parentFlow, _flow, immediately: immediately);
		}
	}
}
