using System;
using System.IO;
using System.Collections.Generic;
using MCGalaxy.Config;
using MCGalaxy.Events;
using MCGalaxy.DB;
using MCGalaxy.Tasks;
using MCGalaxy.Events.PlayerEvents;
using Discord;

namespace MCGalaxy {
	public class PluginDiscord : Plugin {
		public override string name { get { return "Discord"; } }
		public override string MCGalaxy_Version { get { return "1.9.1.4"; } }
		public override string creator { get { return ""; } }

		public const string ConfigFile = "properties/discord.properties";
		public static DiscordConfig config = new DiscordConfig();

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

			OnMessageReceivedEvent.Register(DiscordMessage, Priority.Low);

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

			OnMessageReceivedEvent.Unregister(DiscordMessage);

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
			message = config.DiscordPrefix + config.DiscordMessage.Replace("{name}", p.DisplayName).Replace("{msg}", message);
			SendMessage(message);
		}

		void PlayerDisconnect(Player p, string reason) {
			SetPresence();

			if (p.hidden) return;
			if (reason == null) reason = PlayerDB.GetLogoutMessage(p);
			string message = config.DiscordPrefix + config.DisconnectPrefix + " " + p.DisplayName + " " + reason;
			SendMessage(message);
		}

		void PlayerConnect(Player p) {
			SetPresence();

			if (p.hidden) return;
			string message = config.DiscordPrefix + config.ConnectPrefix + " " + p.DisplayName + " " + PlayerDB.GetLoginMessage(p);
			SendMessage(message);
		}

		void DiscordMessage(string nick, string message) {
			message = config.IngameMessage.Replace("{name}", nick).Replace("{msg}", message);
			Chat.Message(ChatScope.Global, message, null, (Player pl, object arg) => !pl.Ignores.IRC);
		}


		static void SetPresence(int offset = 0) {
			int count = PlayerInfo.NonHiddenCount();
			if (offset != 0) count += offset;

			string s = count == 1 ? "" : "s";
			string message = config.ActivityName.Replace("{p}", count.ToString()).Replace("{s}", s);

			dc.SendStatusUpdate(config.Status.ToString(), message, (int)config.Activity);
		}

		public static void SendMessage(string message) {
			// Queue a message
			Server.Background.QueueOnce(SendMessage, message, TimeSpan.Zero);
		}

		static void SendMessage(SchedulerTask task) {
			dc.SendMessage(config.ChannelID, (string)task.State);
		}

		public static void ReloadConfig() {
			config.LoadConfig();
		}




		public class DiscordConfig {
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

			[ConfigString("discord-prefix", "Formatting", "", true)]
			public string DiscordPrefix = "";

			[ConfigString("discord-message", "Formatting", "{name}: {msg}", true)]
			public string DiscordMessage = "{name}: {msg}";

			[ConfigString("ingame-message", "Formatting", "(Discord) &f{name}: {msg}}", true)]
			public string IngameMessage = Server.Config.IRCColor + "(Discord) &f{name}: {msg}";

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

				if (config.DiscordPrefix != "") config.DiscordPrefix += " "; // add space after prefix, trim removes it
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
				w.WriteLine("# discord-prefix adds a prefix to messages from Discord to CC (including connect/disconnect)");
				w.WriteLine("# discord-message is message sent from CC to Discord");
				w.WriteLine("# ingame-message is message sent from Discord to CC");
				w.WriteLine("# {name} is replaced with the player name");
				w.WriteLine("# {msg} is replaced with the message");
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
		public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }

		public override void Use(Player p, string message, CommandData data) {
			if (message == "") { Help(p); return; }
			string[] args = message.SplitSpaces(2);

			switch(args[0]) {
				case "reload": ReloadConfig(p); return;
				case "restart": RestartBot(p); return;
				case "message": SendMessage(args); return;
			}
		}

		void ReloadConfig(Player p) {
			PluginDiscord.ReloadConfig();
			p.Message("Discord config reloaded.");
		}

		void RestartBot(Player p) {
			PluginDiscord.dc.Dispose();
			PluginDiscord.dc = new Discord.Discord(PluginDiscord.config.Token, PluginDiscord.config.ChannelID);
			p.Message("Discord bot restarted.");
		}

		void SendMessage(string[] args) {
			PluginDiscord.SendMessage(args[1]);
		}

		public override void Help(Player p) {
			p.Message("%T/DiscordBot reload - %HReload config files");
			p.Message("%T/DiscordBot restart - %HRestart the bot");
			p.Message("%HToken or Channel ID changes require a restart after reloading the config");
		}
	}
}