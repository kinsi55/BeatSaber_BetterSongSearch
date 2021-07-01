using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BetterSongSearch.Util {
	class NotifiableSettingsObj : INotifyPropertyChanged {
		public event PropertyChangedEventHandler PropertyChanged;
		internal void NotifyPropertyChanged([CallerMemberName] string propertyName = "") {
			try {
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			} catch(Exception ex) {
				Plugin.Log?.Error($"Error Invoking PropertyChanged: {ex.Message}");
				Plugin.Log?.Error(ex);
			}
		}

		internal void NotifyPropertiesChanged() {
			foreach(var x in AccessTools.GetDeclaredProperties(GetType()))
				NotifyPropertyChanged(x.Name);
		}
	}
}
