using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace eft_dma_radar.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameController : ControllerBase
    {
        [HttpGet("players")]
        public IActionResult GetPlayers()
        {
            try
            {
                if (!Memory.InGame)
                {
                    return StatusCode(503, "Game has ended. Waiting for a new game to start.");
                }

                var players = Memory.Players;
                if (players == null || !players.Any())
                {
                    return NotFound("No players found.");
                }

                var playerData = players.Values.Select(player => new
                {
                    player.Name,
                    player.IsPMC,
                    player.IsLocalPlayer,
                    player.IsAlive,
                    player.IsActive,
                    player.Lvl,
                    player.KDA,
                    player.ProfileID,
                    player.AccountID,
                    Gear = player.Gear.Select(g => new
                    {
                        Slot = g.Slot.Key, // Access the Key from Slot
                        Item = new
                        {
                            g.Item?.Long,
                            g.Item?.Short,
                            g.Item?.Value,
                            Loot = g.Item?.Loot?.Select(l => new
                            {
                                l.Name,
                                l.ID,
                                l.Value,
                                l.Position
                            }),
                            AmmoType = g.Item?.GearInfo.AmmoType,
                            AmmoCount = g.Item?.GearInfo.AmmoCount,
                            Thermal = g.Item?.GearInfo.Thermal,
                            NightVision = g.Item?.GearInfo.NightVision
                        }
                    }),
                    Position = new
                    {
                        X = player.Position.X,
                        Y = player.Position.Y,
                        Z = player.Position.Z
                    },
                    Rotation = new
                    {
                        Yaw = player.Rotation.X,
                        Pitch = player.Rotation.Y
                    }
                });

                return Ok(playerData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetPlayers: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("map")]
        public IActionResult GetMapInfo()
        {
            try
            {
                if (!Memory.InGame)
                {
                    return StatusCode(503, "Game has ended. Waiting for a new game to start.");
                }

                var mapName = Memory.MapNameFormatted;
                var mapState = new
                {
                    MapName = mapName,
                };

                return Ok(mapState);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetMapInfo: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("state")]
        public IActionResult GetGameState()
        {
            try
            {
                var state = new
                {
                    InGame = Memory.InGame,
                    InHideout = Memory.InHideout,
                    IsScav = Memory.IsScav,
                    MapName = Memory.MapNameFormatted,
                    LoadingLoot = Memory.LoadingLoot
                };

                return Ok(state);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetGameState: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("loot/loose")]
        public IActionResult GetLooseLoot()
        {
            try
            {
                if (!Memory.InGame)
                {
                    return StatusCode(503, "Game has ended. Waiting for a new game to start.");
                }

                var looseLoot = Memory.Loot?.Loot?.OfType<LootItem>().ToList();
                if (looseLoot == null || !looseLoot.Any())
                {
                    return NotFound("No loose loot found.");
                }

                var lootData = looseLoot.Select(item => new
                {
                    item.Name,
                    item.ID,
                    item.Value,
                    Position = new
                    {
                        X = item.Position.X,
                        Y = item.Position.Y,
                        Z = item.Position.Z
                    },
                    item.RequiredByQuest,
                    item.Important,
                    item.AlwaysShow
                });

                return Ok(lootData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetLooseLoot: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("loot/containers")]
        public IActionResult GetLootContainers()
        {
            try
            {
                if (!Memory.InGame)
                {
                    return StatusCode(503, "Game has ended. Waiting for a new game to start.");
                }

                var containers = Memory.Loot?.Loot?.OfType<LootContainer>().ToList();
                if (containers == null || !containers.Any())
                {
                    return NotFound("No loot containers found.");
                }

            var containerData = containers.Select(container => new
            {
                container.Name,
                Position = new
                {
                    X = container.Position.X,
                    Y = container.Position.Y,
                    Z = container.Position.Z
                },
                container.Important,
                container.AlwaysShow
            });

                return Ok(containerData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetLootContainers: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("loot/corpses")]
        public IActionResult GetLootCorpses()
        {
            try
            {
                if (!Memory.InGame)
                {
                    return StatusCode(503, "Game has ended. Waiting for a new game to start.");
                }

                var corpses = Memory.Loot?.Loot?.OfType<LootCorpse>().ToList();
                if (corpses == null || !corpses.Any())
                {
                    return NotFound("No loot corpses found.");
                }

            var corpseData = corpses.Select(corpse => new
            {
                corpse.Name,
                Position = new
                {
                    X = corpse.Position.X,
                    Y = corpse.Position.Y,
                    Z = corpse.Position.Z
                },
                corpse.Value,
                corpse.Important,
                corpse.Items
            });

                return Ok(corpseData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetLootCorpses: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("loot/quests")]
        public IActionResult GetQuestItemsAndZones()
        {
            try
            {
                if (!Memory.InGame)
                {
                    return StatusCode(503, "Game has ended. Waiting for a new game to start.");
                }

                if (Memory.QuestManager == null || Memory.QuestManager.QuestItems == null || Memory.QuestManager.QuestZones == null)
                {
                    return NotFound("Quest items or zones data is not available.");
                }

                var questItems = Memory.QuestManager.QuestItems
                    .Where(item => item.Position.X != 0)
                    .Select(item => new
                    {
                        item.Id,
                        item.Name,
                        item.ShortName,
                        item.TaskName,
                        item.Description,
                        Position = new
                        {
                            X = item.Position.X,
                            Y = item.Position.Y,
                            Z = item.Position.Z
                        }
                    }).ToList();

                var questZones = Memory.QuestManager.QuestZones
                    .Where(zone => zone.Position.X != 0)
                    .Select(zone => new
                    {
                        zone.ID,
                        zone.TaskName,
                        zone.Description,
                        zone.ObjectiveType,
                        Position = new
                        {
                            X = zone.Position.X,
                            Y = zone.Position.Y,
                            Z = zone.Position.Z
                        }
                    }).ToList();

                return Ok(new { questItems, questZones });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetQuestItemsAndZones: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("exfils")]
        public IActionResult GetExfils()
        {
            try
            {
                if (!Memory.InGame)
                {
                    return StatusCode(503, "Game has ended. Waiting for new game to start.");
                }

                var exfils = Memory.Exfils;
                if (exfils == null || !exfils.Any())
                {
                    return NotFound("No exfil points found.");
                }

                var exfilData = exfils.Select(exfil => new
                {
                    exfil.Name,
                    exfil.Status,
                    Position = new
                    {
                        X = exfil.Position.X,
                        Y = exfil.Position.Y,
                        Z = exfil.Position.Z
                    }
                });

                return Ok(exfilData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetExfils: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("/ws/connect")]
        public async Task Connect()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                if (Memory.InGame)
                {
                    using WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                    await SendUpdates(webSocket);
                }
                else
                {
                    HttpContext.Response.StatusCode = 503;
                }
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
            }
        }

        private async Task SendUpdates(WebSocket webSocket)
        {
            while (webSocket.State == WebSocketState.Open)
            {
                if (!Memory.InGame)
                {
                    var endGameMessage = new { message = "Game has ended. Waiting for a new game to start." };
                    await SendWebSocketMessage(webSocket, endGameMessage);
                    break;
                }

var updateData = new
{
    players = Memory.Players.Values.Select(player => new
    {
        player.Name,
        player.IsPMC,
        player.IsLocalPlayer,
        player.IsAlive,
        Gear = player.Gear.Select(g => new
        {
            Slot = g.Slot.Key,
            Item = new
            {
                g.Item?.Long,
                g.Item?.Short,
                g.Item?.Value,
                AmmoType = g.Item?.GearInfo.AmmoType,
                AmmoCount = g.Item?.GearInfo.AmmoCount
            }
        }),
        Position = player.Position != null
            ? new
            {
                X = player.Position.X,
                Y = player.Position.Y,
                Z = player.Position.Z
            }
            : new
            {
                X = 0f, // Explicit cast to float
                Y = 0f,
                Z = 0f
            },
        Rotation = player.Rotation != null
            ? new
            {
                Yaw = player.Rotation.X,
                Pitch = player.Rotation.Y
            }
            : new
            {
                Yaw = 0f, // Explicit cast to float
                Pitch = 0f
            }
    }),
    loot = Memory.Loot?.Loot?.Select(l => new
    {
        l.Name,
        l.Value,
        Position = l.Position != null
            ? new
            {
                X = l.Position.X,
                Y = l.Position.Y,
                Z = l.Position.Z
            }
            : new
            {
                X = 0f, // Explicit cast to float
                Y = 0f,
                Z = 0f
            },
        l.Important,
        l.AlwaysShow
    })
};



                await SendWebSocketMessage(webSocket, updateData);
                await Task.Delay(100);
            }
        }

        private async Task SendWebSocketMessage(WebSocket webSocket, object message)
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            var buffer = new ArraySegment<byte>(bytes);

            await webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
