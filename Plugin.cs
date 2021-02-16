using System;
using System.IO;
using MCGalaxy.Config;
using MCGalaxy.Events;
using MCGalaxy.DB;
using MCGalaxy.Events.PlayerEvents;
using Discord;

namespace MCGalaxy {
	public class PluginDiscord : Plugin {
		public override string name { get { return "Discord"; } }
		public override string MCGalaxy_Version { get { return "1.9.1.4"; } }
		public override string creator { get { return ""; } }

		public const string ConfigFile = "properties/discord.properties";
		static DiscordConfig config = new DiscordConfig();

		public static Discord.Discord dc;
		bool registered;

		public override void Load(bool startup) {
			config.LoadConfig();
			if (config.Token == "" || config.ChannelID == "") {
				Logger.Log(LogType.Warning, "Invalid config! Please setup the Discord bot in discord.properties! (plugin reload required)");
				return;
			}

			dc = new Discord.Discord(config.Token, config.ChannelID);

			OnPlayerConnectEvent.Register(PlayerConnect, Priority.Low);
			OnPlayerDisconnectEvent.Register(PlayerDisconnect, Priority.Low);
			OnPlayerChatEvent.Register(PlayerChat, Priority.Low);
			OnPlayerCommandEvent.Register(PlayerCommand, Priority.Low);

			Command.Register(new CmdDiscordBot());
			registered = true;
		}

		public override void Unload(bool shutdown) {
			if (dc != null) dc.Dispose();
			if (!registered) return;
			OnPlayerConnectEvent.Unregister(PlayerConnect);
			OnPlayerDisconnectEvent.Unregister(PlayerDisconnect);
			OnPlayerChatEvent.Unregister(PlayerChat);
			OnPlayerCommandEvent.Unregister(PlayerCommand);

			Command.Unregister(Command.Find("DiscordBot"));
		}

		void PlayerCommand(Player p, string cmd, string args, CommandData data) {
			if (cmd != "hide") return;

			// Offset the player count by one if player is going to hide
			// Has to be done because this event is called before /hide is called
			if (p.hidden) SetPresence(1);
			else SetPresence(-1);
		}

		void PlayerChat(Player p, string message) {
			SetPresence();

			message = config.MessagePrefix + p.DisplayName + config.MessageSeperator + " " + message;
			SendMessage(message);
		}

		void PlayerDisconnect(Player p, string reason) {
			SetPresence();

			if (p.hidden) return;
			if (reason == null) reason = PlayerDB.GetLogoutMessage(p);
			string message = config.MessagePrefix + config.DisconnectPrefix + " " + p.DisplayName + " " + reason;
			SendMessage(message);
		}

		void PlayerConnect(Player p) {
			SetPresence();

			if (p.hidden) return;
			string message = config.MessagePrefix + config.ConnectPrefix + " " + p.DisplayName + " " + PlayerDB.GetLoginMessage(p);
			SendMessage(message);
		}

		static void SetPresence(int offset = 0) {
			int count = PlayerInfo.NonHiddenCount();
			if (offset != 0) count += offset;

			string s = count == 1 ? "" : "s";
			string message = config.ActivityName.Replace("{p}", count.ToString()).Replace("{s}", s);

			dc.SendStatusUpdate(config.Status.ToString(), message, (int)config.Activity); // 3 = watching
		}

		public static void SendMessage(string message) {
			dc.SendMessage(config.ChannelID, message);
		}


		public static void ReloadConfig() {
			config.LoadConfig();
		}

		class DiscordConfig {
			[ConfigString("token", "Account", "", true)]
			public string Token = "";

			[ConfigString("channel-id", "Account", "", true)]
			public string ChannelID = "";

			[ConfigEnum("status", "Status", ClientStatus.online, typeof(ClientStatus))]
			public ClientStatus Status = ClientStatus.online;

			[ConfigEnum("activity", "Status", Activities.playing, typeof(Activities))]
			public Activities Activity = Activities.playing;

			[ConfigString("activity-name", "Status", "with {p} players", false)]
			public string ActivityName = "with {p} players";

			[ConfigString("message-prefix", "Formatting", "", false)]
			public string MessagePrefix = "";

			[ConfigString("message-seperator", "Formatting", ":", false)]
			public string MessageSeperator = ": ";

			[ConfigString("connect-prefix", "Formatting", "+", false)]
			public string ConnectPrefix = "+";

			[ConfigString("disconnect-prefix", "Formatting", "-", false)]
			public string DisconnectPrefix = "-";

			public enum ClientStatus {
				online,
				dnd,
				idle,
				invisible
			}

			public enum Activities {
				playing,
				streaming, // unused
				listening,
				watching, // undocumented
				custom, // unused
				competing
			}

			internal static ConfigElement[] cfg;
			public void LoadConfig() {
				if (cfg == null) cfg = ConfigElement.GetAll(typeof(DiscordConfig));
				PropertiesFile.Read(ConfigFile, LineProcessor);
				SaveConfig();
			}

			void LineProcessor(string key, string value) {
				ConfigElement.Parse(cfg, config, key, value);
			}

			readonly object saveLock = new object();
			public void SaveConfig() {
				if (cfg == null) cfg = ConfigElement.GetAll(typeof(DiscordConfig));
				try {
					lock (saveLock) {
						using (StreamWriter w = new StreamWriter(ConfigFile))
							SaveProps(w);
					}
				} catch (Exception ex) {
					Logger.LogError("Error saving " + ConfigFile, ex);
				}
			}

			void SaveProps(StreamWriter w) {
				w.WriteLine("# To get the token, go to https://discord.com/developers/applications and create a new application.");
				w.WriteLine("# Select the app and go to the Bot tab. Add bot and copy the token below");
				w.WriteLine("# Make sure the bot is invited to the server that contains the channel ID provided");
				w.WriteLine("# Invite URL can be generated by going to the OAuth2 tab and ticking bot in the scopes");
				w.WriteLine("#");
				w.WriteLine("# Account settings require restarting. Other settings can be reloaded with /DiscordBot reload");
				w.WriteLine("#");
				w.WriteLine("# Possible status values: online, dnd, idle, invisible");
				w.WriteLine("# Possible activity values: playing, listening, watching, competing");
				w.WriteLine("# {p} is replaced with the player count in activity-name. {s} is 's' when there are multiple players online, and empty when there's one");
				w.WriteLine("#");
				w.WriteLine("# Message formatting is:");
				w.WriteLine("# [message-prefix]<name>[message-seperator] <message>");
				w.WriteLine("# Message prefix is shown on all messages, including join/leave");
				w.WriteLine("#");
				w.WriteLine("# Connect formatting is:");
				w.WriteLine("# [message-prefix][connect-prefix] <name> <joinmessage>");
				w.WriteLine("# Disconnect formatting is the same");
				w.WriteLine();

				ConfigElement.Serialise(cfg, w, config);
			}
		}
	}

	public sealed class CmdDiscordBot : Command2 {
		public override string name { get { return "DiscordBot"; } }
		public override string type { get { return CommandTypes.Other; } }

		public override void Use(Player p, string message, CommandData data) {
			string[] args = message.SplitSpaces(2);
			if (args.Length < 1) { Help(p); return; }

			switch(args[0]) {
				case "reload": ReloadConfig(); return;
				case "message": SendMessage(args); return;
			}
		}

		void ReloadConfig() {
			PluginDiscord.ReloadConfig();
		}

		void SendMessage(string[] args) {
			PluginDiscord.SendMessage(args[1]);
		}

		public override void Help(Player p) {
			p.Message("%T/DiscordBot reload - %HReload config files");
			p.Message("%HToken or Channel ID changes require a restart after reloading the config");
		}
	}
}