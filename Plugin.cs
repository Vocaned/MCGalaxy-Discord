﻿using System;
using System.IO;
using MCGalaxy.Config;
using MCGalaxy.Events;
using MCGalaxy.Events.PlayerEvents;
using Discord;

namespace MCGalaxy {
	public class PluginDiscord : Plugin {
		public override string name { get { return "Discord"; } }
		public override string MCGalaxy_Version { get { return "1.9.1.4"; } }
		public override string creator { get { return ""; } }

		public const string ConfigFile = "properties/discord.properties";
		static DiscordConfig config = new DiscordConfig();

		Discord.Discord dc;
		bool registered;

		public override void Load(bool startup) {
			LoadConfig();
			if (config.Token == "" || config.ChannelID == 0) {
				Logger.Log(LogType.Warning, "Invalid config! Please setup the Discord bot in discord.properties! (plugin reload required)");
				return;
			}

			dc = new Discord.Discord(config.Token, config.ChannelID);

			OnPlayerConnectEvent.Register(PlayerCountChange, Priority.Low);
			OnPlayerDisconnectEvent.Register(PlayerCountChange, Priority.Low);
			//OnPlayerCommandEvent.Register(HandleOnPlayerCommand, Priority.Low);
			registered = true;
		}

		public override void Unload(bool shutdown) {
			if (dc != null) dc.Dispose();
			if (!registered) return;
			OnPlayerConnectEvent.Unregister(PlayerCountChange);
			OnPlayerDisconnectEvent.Unregister(PlayerCountChange);
			//OnPlayerCommandEvent.Unregister(HandleOnPlayerCommand);
		}


		/*void HandleOnPlayerCommand(Player p, string cmd, string args, CommandData data) {
			if (cmd.CaselessEq("hide")) PlayerCountChange(p);
		}*/


		void PlayerCountChange(Player p, string reason) { PlayerCountChange(p); }
		void PlayerCountChange(Player p) {
			string count = PlayerInfo.NonHiddenCount().ToString();
			if (count == "1") count += " player";
			else count += " players";
			dc.SendStatusUpdate("online", count, 3); // 3 = watching
		}


		internal static ConfigElement[] cfg;
		class DiscordConfig {
			[ConfigString("token", "", "", true)]
			public string Token = "";

			[ConfigInt("channel-id", "", 0)]
			public int ChannelID;
		}

		public static void LoadConfig() {
			if (cfg == null) cfg = ConfigElement.GetAll(typeof(DiscordConfig));
			PropertiesFile.Read(ConfigFile, LineProcessor);
			SaveConfig();
		}

		static void LineProcessor(string key, string value) {
			ConfigElement.Parse(cfg, config, key, value);
		}
	
		static readonly object saveLock = new object();
		static void SaveConfig() {
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

		static void SaveProps(StreamWriter w) {
			w.WriteLine("# To get the token, go to https://discord.com/developers/applications and create a new application.");
			w.WriteLine("# Select the app and go to the Bot tab. Add bot and copy the token below");
			w.WriteLine("# Make sure the bot is invited to the server that contains the channel ID provided");
			w.WriteLine("# Invite URL can be generated by going to the OAuth2 tab and ticking bot in the scopes");

			ConfigElement.Serialise(cfg, w, config);
		}
	}
}