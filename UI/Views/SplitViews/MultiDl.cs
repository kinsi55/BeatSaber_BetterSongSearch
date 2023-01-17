using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;

namespace BetterSongSearch.UI.SplitViews {
	class MultiDl {
		public static readonly MultiDl instance = new MultiDl();
		MultiDl() { }

		[UIComponent("multiDlCountSlider")] readonly SliderSetting multiDlCountSlider = null;
		[UIAction("StartMultiDownload")]
		void StartMultiDownload() {
			for(int i = BSSFlowCoordinator.songListView.songList.GetVisibleCellsIdRange().Item1, downloaded = 0; i < SongListController.searchedSongsList.Count; i++) {
				if(SongListController.searchedSongsList[i].CheckIsDownloaded() || !SongListController.searchedSongsList[i].CheckIsDownloadable())
					continue;

				if(!BSSFlowCoordinator.downloadHistoryView.TryAddDownload(SongListController.searchedSongsList[i], true))
					continue;

				if(++downloaded >= multiDlCountSlider.Value)
					break;
			}

			BSSFlowCoordinator.downloadHistoryView.RefreshTable(true);
		}
	}
}
