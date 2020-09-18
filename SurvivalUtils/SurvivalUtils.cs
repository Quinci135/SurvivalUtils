using System;
using TShockAPI;
using Terraria;
using TerrariaApi.Server;
using Microsoft.Xna.Framework;
using Terraria.ID;
using System.IO.Streams;
using System.IO;

namespace SurvivalUtils
{
	[ApiVersion(2, 1)]
	public class SurvivalUtils : TerrariaPlugin
	{
		public override string Author => "Quinci";
		public override string Description => "Random things for surival";
		public override string Name => "Survival Utils";
		public override Version Version => new Version(1, 0, 0, 0);
		private Vector2 dungeonPos => new Vector2(Main.dungeonX * 16, Main.dungeonY * 16);
		public SurvivalUtils(Main game) : base(game) 
		{
			Order = 200;
		}

		public override void Initialize()
		{
			if (Main.ServerSideCharacter)
			{
				ServerApi.Hooks.NetGetData.Register(this, OnGetData);
				ServerApi.Hooks.GameInitialize.Register(this, OnInit);
				ServerApi.Hooks.NpcAIUpdate.Register(this, OnNPCAIUpdate);
			}
			else
			{
				Dispose(false);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInit);
				ServerApi.Hooks.NpcAIUpdate.Deregister(this, OnNPCAIUpdate);
			}
			base.Dispose(disposing);
		}

		private void OnInit(EventArgs args)
		{
			Commands.ChatCommands.Add(new Command(Permissions.summonboss, SummonEOL, "summoneol", "summonempressoflight") { HelpText = "Summons the empress of light. Requires a prismatic lacewing in your inventory.", AllowServer = false });
		}

		private void OnNPCAIUpdate(NpcAiUpdateEventArgs args)
		{
			if (args.Npc.type == NPCID.CultistArcherBlue || args.Npc.type == NPCID.CultistDevote)
			{
				if (args.Npc.position.Distance(dungeonPos) > 288) //18 blocks away from the middle
				{
					args.Npc.Teleport(new Vector2(dungeonPos.X, dungeonPos.Y - 96), 1); //6 blocks above so they are not stuck in the ground
					args.Npc.velocity = args.Npc.DirectionTo(dungeonPos);
					return;
				}
			}
			if (args.Npc.type == NPCID.EmpressButterfly) //Make it immortal
			{
				args.Npc.life = short.MaxValue;
				args.Npc.lifeMax = short.MaxValue;
				args.Npc.defense = short.MaxValue;
			}
		}

		private void SummonEOL(CommandArgs args)
		{
			if (args.Player.AwaitingResponse.ContainsKey("yes"))
			{
				args.Player.SendInfoMessage($"Confirm Empress of Light Summon: {TShock.Config.CommandSpecifier}yes OR {TShock.Config.CommandSpecifier}no");
				return;
			}
			args.Player.SendInfoMessage($"Do you want to summon the Empress of Light?\nConfirm Empress of Light Summon: {TShock.Config.CommandSpecifier}yes OR {TShock.Config.CommandSpecifier}no");
			args.Player.AddResponse("yes", a =>
			{
				args.Player.AwaitingResponse.Remove("no");
				if (NPC.AnyNPCs(NPCID.HallowBoss))
				{
					args.Player.SendErrorMessage("There is already an exsting empress of light");
				}
				bool hasButterfly = false;
				for (int i = 0; i < NetItem.InventorySlots; i++)
				{
					if (args.TPlayer.inventory[i].stack > 0 && args.TPlayer.inventory[i].type == ItemID.EmpressButterfly)
					{
						NetMessage.SendData((int)PacketTypes.PlayerSlot, -1, -1, null, args.Player.Index, i, --args.TPlayer.inventory[i].stack);
						hasButterfly = true;
						break;
					}
				}
				if (!hasButterfly)
				{
					args.Player.SendErrorMessage($"You need a prismatic lacewing in your inventory to summon the Empress of Light.");
				}
				else
				{
					args.Player.SetBuff(BuffID.ShadowDodge, 60); //In case it spawns inside the player
					TSPlayer.Server.SpawnNPC(NPCID.HallowBoss, "Empress of Light", 1, args.Player.TileX, args.Player.TileY);
					args.Player.SendSuccessMessage("Spawned the Empress of Light.");
					TSPlayer.All.SendInfoMessage($"{args.Player.Name} has summoned the Empress of Light.");
				}
			});
			args.Player.AddResponse("no", a =>
			{
				args.Player.AwaitingResponse.Remove("yes");
				args.Player.SendSuccessMessage("Spawning cancelled.");
			});
		}

		private void OnGetData(GetDataEventArgs args)
		{
			TSPlayer player = TShock.Players[args.Msg.whoAmI];
			switch (args.MsgID)
			{
				case PacketTypes.NpcAddBuff:
					{
						using (MemoryStream data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))
						{
							int index = data.ReadInt16();
							if (index > 200 || index < 0)
							{
								return;
							}
							if (!Main.npc[index].active)
							{
								return;
							}
							if (Main.npc[index].type == NPCID.EmpressButterfly)
							{
								args.Handled = true;
								return;
							}
							if (Main.npc[index].type == NPCID.CultistArcherBlue || Main.npc[index].type == NPCID.CultistDevote)
							{
								if (!player.HasPermission(Permissions.summonboss))
								{
									args.Handled = true;
								}
							}
							break;
						}
					}
				case PacketTypes.NpcStrike:
					{
						using (MemoryStream data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))
						{
							int index = data.ReadInt16();
							if (index > 200 || index < 0)
							{
								return;
							}
							if (!Main.npc[index].active)
							{
								return;
							}
							if (Main.npc[index].type == NPCID.EmpressButterfly)
							{
								if (player.HasPermission(Permissions.summonboss))
								{
									player.SendInfoMessage($"You can summon the empress of light with the {TShock.Config.CommandSpecifier}summoneol command if you have a prismatic lacewing in your inventory.");
								}
								else
								{
									player.SendErrorMessage("You do not have permission to summon the empress of light.");
								}
								args.Handled = true;
								return;
							}
							if (Main.npc[index].type == NPCID.CultistArcherBlue || Main.npc[index].type == NPCID.CultistDevote)
							{
								if (!player.HasPermission(Permissions.summonboss))
								{
									args.Handled = true;
									player.SendErrorMessage("You do not have permission to summon the lunatic cultist.");
								}
							}
							break;
						}
					}
			}
		}
	}
}
