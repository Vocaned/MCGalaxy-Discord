using System;
using MCGalaxy;
using System.Collections.Generic;
using System.Text;

namespace MCGalaxy {
	public class Who {
		public struct GroupPlayers { public Group group; public StringBuilder builder; }
		public static GroupPlayers Make(Group group, bool showmaps, ref int totalPlayers) {
			GroupPlayers list;
			list.group = group;
			list.builder = new StringBuilder();

			Player[] online = PlayerInfo.Online.Items;
			foreach (Player pl in online) {
				if (pl.hidden) continue; // Never show hidden players
				if (pl.group != group) continue;
				totalPlayers++;
				Append(list, pl, showmaps);
			}
			return list;
		}

		static void Append(GroupPlayers list, Player p, bool showmaps) {
			StringBuilder data = list.builder;
			data.Append(' ');
			if (p.voice) { data.Append("+").Append(list.group.Color); }
			data.Append(Colors.Strip(Player.Console.FormatNick(p)));

			if (p.muted) data.Append("-muted");
			if (p.frozen) data.Append("-frozen");
			if (p.Game.Referee) data.Append("-ref");
			if (p.IsAfk) data.Append("-afk");
			if (p.Unverified) data.Append("-unverified");

			if (!showmaps) { data.Append(","); return; }

			string lvlName = Colors.Strip(p.level.name); // for museums
			data.Append(" (").Append(lvlName).Append("),");
		}

		public static string GetPlural(string name) {
			if (name.Length < 2) return name;

			string last2 = name.Substring(name.Length - 2).ToLower();
			if ((last2 != "ed" || name.Length <= 3) && last2[1] != 's')
				return name + "s";
			return name;
		}

		public static string Output(GroupPlayers list) {
			StringBuilder data = list.builder;
			if (data.Length == 0) return null;
			if (data.Length > 0) data.Remove(data.Length - 1, 1);

			string title = "**" + GetPlural(list.group.Name) + "**:";
			return title + data + "\n";
		}
	}
}
