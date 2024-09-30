﻿using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BetterSongSearch.Configuration;
using BetterSongSearch.Util;
using HMUI;
using System.Linq;
using TMPro;
using UnityEngine.UI;

namespace BetterSongSearch.UI.SplitViews {
	class Presets {
		public static readonly Presets instance = new Presets();
		Presets() { }

		class FilterPresetRow {
			public readonly string name;
			[UIComponent("label")] readonly TextMeshProUGUI label = null;

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

			BSMLStuff.GetScrollbarForTable(presetList.TableView.gameObject, _presetScrollbarContainer.transform);

			// BSML / HMUI my beloved
			newPresetName.ModalKeyboard.ModalView._animateParentCanvas = false;
		}


		[UIComponent("loadButton")] readonly NoTransitionsButton loadButton = null;
		[UIComponent("deleteButton")] readonly NoTransitionsButton deleteButton = null;
		[UIComponent("presetList")] readonly CustomCellListTableData presetList = null;
		[UIComponent("newPresetName")] readonly StringSetting newPresetName = null;
		[UIComponent("presetScrollbarContainer")] readonly VerticalLayoutGroup _presetScrollbarContainer = null;
		internal void ReloadPresets() {
			presetList.Data = FilterPresets.presets.Select(x => new FilterPresetRow(x.Key)).ToList<object>();
			presetList.TableView.ReloadData();
			presetList.TableView.ClearSelection();

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
			PlaylistCreation.nameToUseOnNextOpen = curSelected;

			BSSFlowCoordinator.filterView.SetFilter(FilterPresets.presets[curSelected]);
		}
		void DeletePreset() {
			FilterPresets.Delete(curSelected);
			ReloadPresets();
		}
	}
}
