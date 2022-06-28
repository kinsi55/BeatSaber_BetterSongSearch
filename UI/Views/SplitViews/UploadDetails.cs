using BeatSaberMarkupLanguage.Attributes;
using BetterSongSearch.Util;
using HMUI;
using System;
using System.Linq;
using System.Threading.Tasks;
using TMPro;

namespace BetterSongSearch.UI.SplitViews {
	class UploadDetails {
		public static readonly UploadDetails instance = new UploadDetails();
		UploadDetails() { }

		[UIComponent("selectedCharacteristics")] TextMeshProUGUI selectedCharacteristics = null;
		[UIComponent("selectedSongKey")] TextMeshProUGUI selectedSongKey = null;
		[UIComponent("selectedSongDescription")] CurvedTextMeshPro selectedSongDescription = null;
		[UIComponent("selectedRating")] TextMeshProUGUI selectedRating = null;
		//[UIComponent("selectedDownloadCount")] TextMeshProUGUI selectedDownloadCount = null;
		[UIComponent("songDetailsLoading")] ImageView songDetailsLoading = null;

		public void Populate(SongSearchSong selectedSong) {
			selectedCharacteristics.text = String.Join(", ", selectedSong.detailsSong.difficulties.GroupBy(x => x.characteristic).Select(x => $"{x.Count()}x {x.Key}"));
			selectedSongKey.text = selectedSong.detailsSong.key;
			//selectedDownloadCount.text = selectedSong.detailsSong.downloadCount.ToString("N0");
			selectedRating.text = selectedSong.detailsSong.rating.ToString("0.0%");
			selectedSongDescription.text = "";

			songDetailsLoading.gameObject.SetActive(true);

			Task.Run(async () => {
				string desc = "Failed to load description";
				try {
					desc = await BSSFlowCoordinator.assetLoader.GetSongDescription(selectedSong.detailsSong.key, BSSFlowCoordinator.closeCancelSource.Token);
				} catch { }

				_ = IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(() => {
					songDetailsLoading.gameObject.SetActive(false);
					// If we dont do that, the description is long and contains unicode the game crashes. Fun.
					selectedSongDescription.text = desc;
					selectedSongDescription.gameObject.SetActive(false);
					selectedSongDescription.gameObject.SetActive(true);
				});
			}).ConfigureAwait(false);
		}
	}
}
