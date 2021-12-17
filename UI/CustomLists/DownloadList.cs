using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using HMUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using static BetterSongSearch.UI.DownloadHistoryView;
using static BetterSongSearch.UI.DownloadHistoryView.DownloadHistoryEntry;

namespace BetterSongSearch.UI.CustomLists {
    static class DownloadListTableData {
        const string ReuseIdentifier = "REUSECustomDownloadListTableCell";

        public static CustomDownloadListTableCell GetCell(TableView tableView) {
            var tableCell = tableView.DequeueReusableCellForIdentifier(ReuseIdentifier);

            if(tableCell == null) {
                tableCell = new GameObject("CustomDownloadListTableCell", new[] { typeof(Touchable) }).AddComponent<CustomDownloadListTableCell>();
                tableCell.interactable = true;

                tableCell.reuseIdentifier = ReuseIdentifier;
                BSMLParser.instance.Parse(
                    Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "BetterSongSearch.UI.CustomLists.DownloadListCell.bsml"),
                    tableCell.gameObject, tableCell
                );
            }

            return (CustomDownloadListTableCell)tableCell;
        }
    }

    class CustomDownloadListTableCell : TableCell {
        [UIComponent("songName")] TextMeshProUGUI songName = null;
        [UIComponent("levelAuthorName")] TextMeshProUGUI levelAuthorName = null;
        [UIComponent("statusLabel")] TextMeshProUGUI statusLabel = null;

        DownloadHistoryEntry entry = null;

        public CustomDownloadListTableCell PopulateWithSongData(DownloadHistoryEntry entry) {
            songName.text = entry.songName;
            levelAuthorName.text = entry.levelAuthorName;
            statusLabel.text = entry.statusMessage;
            this.entry = entry;
            entry.UpdateProgressHandler = UpdateProgress;

            UpdateProgress();

            return this;
        }

        protected override void SelectionDidChange(TransitionType transitionType) => RefreshBgState();
        protected override void HighlightDidChange(TransitionType transitionType) => RefreshBgState();
        protected override void WasPreparedForReuse() => entry.UpdateProgressHandler = null;


        [UIComponent("bgContainer")] ImageView bg = null;
        [UIComponent("bgProgress")] ImageView bgProgress = null;
        [UIAction("refresh-visuals")]
        public void RefreshBgState() {
            bg.color = new Color(0, 0, 0, highlighted ? 0.8f : 0.45f);

            RefreshBar();
        }

        void RefreshBar() {
            if(entry == null)
                return;

            var clr = entry.status == DownloadStatus.Failed ? Color.red : entry.status != DownloadStatus.Queued ? Color.green : Color.gray;

            clr.a = 0.5f + (entry.downloadProgress * 0.4f);
            bgProgress.color = clr;

            var x = (bgProgress.gameObject.transform as RectTransform);
            if(x == null)
                return;

            x.anchorMax = new Vector2(entry.downloadProgress, 1);
            x.ForceUpdateRectTransforms();
        }

        public void UpdateProgress() {
            statusLabel.text = entry.statusMessage;

            RefreshBar();
        }
    }
}
