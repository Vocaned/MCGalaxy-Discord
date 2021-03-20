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
		public override string name { get { return "DiscordPlugin"; } }
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
			// TODO: mod action event

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

			//TODO: Show fake login msg
		}

		void PlayerChat(Player p, string message) {
			if (p.cancelchat) return;
			message = config.DiscordPrefix + config.DiscordMessage.Replace("{name}", p.DisplayName).Replace("{msg}", message);
			SendMessage(Colors.Strip(message));
		}

		void PlayerDisconnect(Player p, string reason) {
			SetPresence();

			if (p.hidden) return;
			if (reason == null) reason = PlayerDB.GetLogoutMessage(p);
			string message = config.DiscordPrefix + config.DisconnectPrefix + " " + p.DisplayName + " " + reason;
			SendMessage(Colors.Strip(message));
		}

		void PlayerConnect(Player p) {
			SetPresence();

			if (p.hidden) return;
			string message = config.DiscordPrefix + config.ConnectPrefix + " " + p.DisplayName + " " + PlayerDB.GetLoginMessage(p);
			SendMessage(Colors.Strip(message));
		}

		void DiscordMessage(string nick, string message) {
			if (message.CaselessEq(".who") || message.CaselessEq(".players") || message.CaselessEq("!players")) {
				Constants.Embed embed = new Constants.Embed();
				embed.color = config.EmbedColor;
				embed.title = Server.Config.Name;

				Dictionary<string, List<string>> ranks = new Dictionary<string, List<string>>();

				int totalPlayers = 0;
				List<Who.GroupPlayers> allPlayers = new List<Who.GroupPlayers>();

				if (totalPlayers == 1) embed.description = "**There is 1 player online**\n\n";
				else embed.description = "**There are " + PlayerInfo.Online.Count + " players online**\n\n";

				if (config.zsmode && Games.ZSGame.Instance.Running) {
					foreach (Group grp in Group.GroupList) {
						allPlayers.Add(Who.Make(grp, false, ref totalPlayers));
					}

					for (int i = allPlayers.Count - 1; i >= 0; i--) {
						embed.description += Who.Output(allPlayers[i]);
					}

					embed.description += "\n" + "Map: `" + Games.ZSGame.Instance.Map.name + "`";
				} else {
					foreach (Group grp in Group.GroupList) {
						allPlayers.Add(Who.Make(grp, true, ref totalPlayers));
					}

					for (int i = allPlayers.Count - 1; i >= 0; i--) {
						embed.description += Who.Output(allPlayers[i]);
					}
				}

				SendMessage(embed);
				return;
			}

			message = config.IngameMessage.Replace("{name}", nick).Replace("{msg}", message);
			Chat.Message(ChatScope.Global, message, null, (Player pl, object arg) => !pl.Ignores.IRC);
		}


		static void SetPresence(int offset = 0) {
			if (!config.UseStatus) return;
			int count = PlayerInfo.NonHiddenCount();
			if (offset != 0) count += offset;

			string s = count == 1 ? "" : "s";
			string message = config.ActivityName.Replace("{p}", count.ToString()).Replace("{s}", s);

			dc.SendStatusUpdate(config.Status.ToString(), message, (int)config.Activity);
		}

		public static void SendMessage(Constants.Embed message) {
			// Queue a message so the message doesn't have to wait until discord receives it to display in chat
			Server.Background.QueueOnce(SendMessage, message, TimeSpan.Zero);
		}
		public static void SendMessage(string message) {
			Server.Background.QueueOnce(SendMessage, message, TimeSpan.Zero);
		}

		static void SendMessage(SchedulerTask task) {
			if (task.State is Constants.Embed) {
				dc.SendMessage(config.ChannelID, (Constants.Embed)task.State);
			} else if (task.State is string) {
				dc.SendMessage(config.ChannelID, (string)task.State);
			}
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

			[ConfigBool("use-status", "Status", true)]
			public bool UseStatus = true;

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

			[ConfigInt("embed-color", "Formatting", 0xaafaaa)]
			public int EmbedColor = 0xaafaaa;

			[ConfigBool("zsmode", "Formatting", false)]
			public bool zsmode = false;

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
				w.WriteLine("#");
				w.WriteLine("# embed-color is the color used in embeds, as an integer");
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

		public override void Help(Player p) {
			p.Message("%T/DiscordBot reload - %HReload config files");
			p.Message("%T/DiscordBot restart - %HRestart the bot");
			p.Message("%HToken or Channel ID changes require a restart after reloading the config");
		}
	}
}