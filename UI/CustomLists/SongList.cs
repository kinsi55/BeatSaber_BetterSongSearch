using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using System.Reflection;
using TMPro;
using UnityEngine;
using BetterSongSearch.Util;

namespace BetterSongSearch.UI.CustomLists {
	static class SongListTableData {
		const string ReuseIdentifier = "REUSECustomSongListTableCell";

		public static CustomSongListTableCell GetCell(TableView tableView) {
			var tableCell = tableView.DequeueReusableCellForIdentifier(ReuseIdentifier);

			if(tableCell == null) {
				tableCell = new GameObject("CustomSongListTableCell", typeof(Touchable)).AddComponent<CustomSongListTableCell>();
				tableCell.interactable = true;

				tableCell.reuseIdentifier = ReuseIdentifier;
				BSMLParser.instance.Parse(
					Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "BetterSongSearch.UI.CustomLists.SongListCell.bsml"),
					tableCell.gameObject, tableCell
				);
			}

			return (CustomSongListTableCell)tableCell;
		}
	}

	class CustomSongListTableCell : TableCell {
		[UIComponent("fullFormattedSongName")] readonly TextMeshProUGUI fullFormattedSongName = null;
		[UIComponent("uploadDateFormatted")] readonly TextMeshProUGUI uploadDateFormatted = null;
		[UIComponent("levelAuthorName")] readonly TextMeshProUGUI levelAuthorName = null;
		[UIComponent("songLengthAndRating")] readonly TextMeshProUGUI songLengthAndRating = null;
		TextMeshProUGUI[] diffs = null;

		[UIComponent("diffs")] readonly Transform diffsContainer = null;

		[UIAction("#post-parse")]
		void Parsed() {
			diffs = diffsContainer.GetComponentsInChildren<TextMeshProUGUI>();
		}

		public CustomSongListTableCell PopulateWithSongData(SongSearchSong song) {
			fullFormattedSongName.text = song.fullFormattedSongName.Replace("\n", " ");
			fullFormattedSongName.color = song.CheckIsDownloaded() ? Color.gray : Color.white;

			uploadDateFormatted.text = song.uploadDateFormatted;
			levelAuthorName.text = song.detailsSong.levelAuthorName;
			songLengthAndRating.text = song.songLengthAndRating;


			var sortedDiffs = song.sortedDiffs.GetEnumerator();
			var diffsLeft = song.diffs.Length;

			for(var i = 0; i < diffs.Length; i++) {
				var isActive = diffsLeft != 0;

				diffs[i].gameObject.SetActive(isActive);

				if(!isActive)
					continue;

				if(diffsLeft != 1 && i == diffs.Length - 1) {
					diffs[i].text = $"<color=#0AD>+{diffsLeft} More";
				} else {
					sortedDiffs.MoveNext();
					diffs[i].text = sortedDiffs.Current.formattedDiffDisplay;
					diffsLeft--;
				}
			}

			SetFontSizes();

			return this;
		}

		public void SetFontSizes() {
			foreach(var d in diffs)
				d.fontSize = PluginConfig.Instance.smallerFontSize ? 2.5f : 2.9f;

			if(PluginConfig.Instance.smallerFontSize) {
				fullFormattedSongName.fontSize = 2.7f;
				uploadDateFormatted.fontSize = 2.7f;
				levelAuthorName.fontSize = 2.3f;
				songLengthAndRating.fontSize = 2.5f;
			} else {
				fullFormattedSongName.fontSize = 3.2f;
				uploadDateFormatted.fontSize = 3.2f;
				levelAuthorName.fontSize = 2.6f;
				songLengthAndRating.fontSize = 3f;
			}
		}

		public override void SelectionDidChange(TransitionType transitionType) => RefreshBgState();

		public override void HighlightDidChange(TransitionType transitionType) => RefreshBgState();


		[UIComponent("bgContainer")] ImageView bg = null;

		void RefreshBgState() {
			bg.color = new Color(0, 0, 0, selected ? 0.8f : highlighted ? 0.6f : 0.45f);
		}
	}
}
