using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BetterSongSearch.Configuration;
using BetterSongSearch.Util;
using HMUI;
using IPA.Utilities;
using System.Linq;
using TMPro;
using UnityEngine.UI;

namespace BetterSongSearch.UI.SplitViews {
	class Presets {
		public static readonly Presets instance = new Presets();
		Presets() { }

		class FilterPresetRow {
			public readonly string name;
			[UIComponent("label")] TextMeshProUGUI label = null;

			public FilterPresetRow(string name) => this.name = name;

			[UIAction("refresh-visuals")]
			public void Refresh(bool selected, bool highlighted) {
				label.color = new UnityEngine.Color(
					selected ? 0 : 255,
					selected ? 128 : 255,
					selected ? 128 : 255,
					highlighted ? 0.9f : 0.6f
				);
			}
		}

		[UIAction("#post-parse")]
		void Parsed() {
			FilterPresets.Init();

			BSMLStuff.GetScrollbarForTable(presetList.tableView.gameObject, _presetScrollbarContainer.transform);

			// BSML / HMUI my beloved
			ReflectionUtil.SetField(newPresetName.modalKeyboard.modalView, "_animateParentCanvas", false);
		}


		[UIComponent("loadButton")] private NoTransitionsButton loadButton = null;
		[UIComponent("deleteButton")] private NoTransitionsButton deleteButton = null;
		[UIComponent("presetList")] private CustomCellListTableData presetList = null;
		[UIComponent("newPresetName")] private StringSetting newPresetName = null;
		[UIComponent("presetScrollbarContainer")] private VerticalLayoutGroup _presetScrollbarContainer = null;
		internal void ReloadPresets() {
			presetList.data = FilterPresets.presets.Select(x => new FilterPresetRow(x.Key)).ToList<object>();
			presetList.tableView.ReloadData();
			presetList.tableView.ClearSelection();

			loadButton.interactable = false;
			deleteButton.interactable = false;

			newPresetName.Text = "";
		}

		string curSelected;
		void PresetSelected(object _, FilterPresetRow row) {
			loadButton.interactable = true;
			deleteButton.interactable = true;
			newPresetName.Text = curSelected = row.name;
		}

		void AddPreset() {
			FilterPresets.Save(newPresetName.Text);
			ReloadPresets();
		}

		void LoadPreset() {
			BSSFlowCoordinator.filterView.SetFilter(FilterPresets.presets[curSelected]);
		}
		void DeletePreset() {
			FilterPresets.Delete(curSelected);
			ReloadPresets();
		}
	}
}
