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
                    return StatusCode(503, "Game has ended. Waiting for new game to start.");
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
                        Slot = g.Key,
                        LongName = g.Value?.Long, 
                        ShortName = g.Value?.Short, 
                        ItemValue = g.Value?.Value 
                    }),
                    Position = new
                    {
                        X = player.Position.X,
                        Y = player.Position.Y,
                        Z = player.Position.Z
                    },
                    Rotation = new
                    {
                        Yaw = player.Rotation.X,  // Horizontal rotation
                        Pitch = player.Rotation.Y // Vertical rotation
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
                    return StatusCode(503, "Game has ended. Waiting for new game to start.");
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
                    return StatusCode(503, "Game has ended. Waiting for new game to start.");
                }

                if (Memory.Loot == null || Memory.Loot.Loot == null)
                {
                    Console.WriteLine("Loot data is still loading.");
                    return StatusCode(503, "Loot data is still loading. Please try again later.");
                }
        
                var loot = Memory.Loot.Loot.OfType<LootItem>().ToList();
                if (!loot.Any())
                {
                    return NotFound("No loose loot found.");
                }
        
                var lootData = loot.Select(item => new
                {
                    item.Name,
                    item.ID,
                    item.Value,
                    Position = new
                    {
                        X = item.Position.X,
                        Y = item.Position.Y,
                        Z = item.Position.Z
                    }
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
                    return StatusCode(503, "Game has ended. Waiting for new game to start.");
                }

                if (Memory.Loot == null || !Memory.Loot.HasCachedItems)
                {
                    return NotFound("Loot data is not available.");
                }

                var lootContainers = Memory.Loot.Loot.OfType<LootContainer>()
                    .Select(container => new
                    {
                        container.Name,
                        container.Items,
                        container.Value,
                        container.Important, 
                        Position = new
                        {
                            X = container.Position.X,
                            Y = container.Position.Y,
                            Z = container.Position.Z
                        }
                    })
                    .ToList();

                return Ok(lootContainers);
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
                    return StatusCode(503, "Game has ended. Waiting for new game to start.");
                }

                if (Memory.Loot == null || !Memory.Loot.HasCachedItems)
                {
                    return NotFound("Loot data is not available.");
                }

                var lootCorpses = Memory.Loot.Loot.OfType<LootCorpse>()
                    .Select(corpse => new
                    {
                        corpse.Name,
                        corpse.Items,
                        corpse.Value,
                        corpse.Important,
                        Position = new
                        {
                            X = corpse.Position.X,
                            Y = corpse.Position.Y,
                            Z = corpse.Position.Z
                        }
                    })
                    .ToList();

                return Ok(lootCorpses);
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
                    return StatusCode(503, "Game has ended. Waiting for new game to start.");
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
                    })
                    .ToList();
        
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
                    })
                    .ToList();
        
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
                var gameState = new
                {
                    InGame = Memory.InGame,
                    InHideout = Memory.InHideout,
                    IsScav = Memory.IsScav
                };

                if (gameState.InGame)
                {
                    Console.WriteLine("Starting WebSocket");
                    using WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                    await SendUpdates(webSocket);
                }
                else
                {
                    HttpContext.Response.StatusCode = 503; // Service Unavailable
                    Console.WriteLine("Stopping WebSocket: Game not in progress");
                }
            }
            else
            {
                HttpContext.Response.StatusCode = 400; // Bad Request
            }
        }

        private async Task SendUpdates(WebSocket webSocket)
        {
            while (webSocket.State == WebSocketState.Open)
            {
                if (!Memory.InGame)
                {
                    var endGameMessage = new { message = "Game has ended. Waiting for new game to start." };
                    var endGameJson = JsonSerializer.Serialize(endGameMessage);
                    var endGameBytes = Encoding.UTF8.GetBytes(endGameJson);
                    var endGameBuffer = new ArraySegment<byte>(endGameBytes);
        
                    await webSocket.SendAsync(endGameBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                    break; // Exit the loop to close the connection
                }
        
                var containerSettings = Program.Config.DefaultContainerSettings;
        
                var updateData = new
                {
                    players = Memory.Players.Values.Select(player => new
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
                        player.Type,
                        Gear = player.Gear.Select(g => new
                        {
                            Slot = g.Key,
                            LongName = g.Value?.Long,
                            ShortName = g.Value?.Short,
                            ItemValue = g.Value?.Value
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
                    }).ToList(),
        
                    loot = Memory.Loot.Loot.OfType<LootItem>().Select(l => new
                    {
                        l.Name,
                        l.ID,
                        l.Value,
                        Position = new { l.Position.X, l.Position.Y, l.Position.Z }
                    }).ToList(),
        
                    exfils = Memory.Exfils.Select(exfil => new
                    {
                        exfil.Name,
                        exfil.Status,
                        Position = new { exfil.Position.X, exfil.Position.Y, exfil.Position.Z }
                    }).ToList(),
        
                    corpses = Memory.Loot.Loot.OfType<LootCorpse>().Select(corpse => new
                    {
                        corpse.Name,
                        corpse.Items,
                        corpse.Value,
                        corpse.Important,
                        Position = new
                        {
                            X = corpse.Position.X,
                            Y = corpse.Position.Y,
                            Z = corpse.Position.Z
                        }
                    }).ToList(),
        
                    containers = containerSettings["Enabled"]
                        ? Memory.Loot.Loot.OfType<LootContainer>().Select(container => new
                        {
                            container.Name,
                            container.Items,
                            container.Value,
                            container.Important,
                            Position = new
                            {
                                X = container.Position.X,
                                Y = container.Position.Y,
                                Z = container.Position.Z
                            }
                        }).ToList()
                        : null
                };
        
                var updateJson = JsonSerializer.Serialize(updateData);
                var updateBytes = Encoding.UTF8.GetBytes(updateJson);
                var updateBuffer = new ArraySegment<byte>(updateBytes);
        
                await webSocket.SendAsync(updateBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                await Task.Delay(100); // Adjust the delay to control the update frequency
            }
        }
    }

    [HttpGet("/ws/connect_v2")]
    public async Task Connect_c2()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            await HandleWebSocketAsync(webSocket);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task HandleWebSocketAsync(WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];
        WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        var lastLootUpdate = DateTime.UtcNow; // 记录上次更新的时间
        var lootData = new List<object>(); // 使用 object 类型
        var exfilsData = new List<object>(); // 使用 object 类型
        var containersData = new List<object>(); // 使用 object 类型
        var corpsesData = new List<object>(); // 使用 object 类型


        while (!result.CloseStatus.HasValue)
        {
            // 处理接收到的消息
            string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            if (message == "get_data")
            {

                if (!Memory.InGame)
                {
                    var endGameMessage = new { message = "Game has ended. Waiting for new game to start." };
                    var endGameJson = JsonSerializer.Serialize(endGameMessage);
                    var endGameBytes = Encoding.UTF8.GetBytes(endGameJson);
                    var endGameBuffer = new ArraySegment<byte>(endGameBytes);

                    await webSocket.SendAsync(endGameBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                    break; // Exit the loop to close the connection
                }

                var containerSettings = Program.Config.DefaultContainerSettings;
                var lootSettings = Program.Config.ProcessLoot;
                // 每五秒更新 loot 数据
                if ((DateTime.UtcNow - lastLootUpdate).TotalSeconds >= 5)
                {
                    exfilsData = Memory.Exfils.Select(exfil => (object)new
                    {
                        exfil.Name,
                        exfil.Status,
                        Position = new { exfil.Position.X, exfil.Position.Y, exfil.Position.Z }
                    }).ToList();

                    corpsesData = Memory.Loot.Loot.OfType<LootCorpse>().Select(corpse => (object)new
                    {
                        corpse.Name,
                        corpse.Items,
                        corpse.Value,
                        corpse.Important,
                        Position = new
                        {
                            X = corpse.Position.X,
                            Y = corpse.Position.Y,
                            Z = corpse.Position.Z
                        }
                    }).ToList();

                    if (lootSettings)
                    {
                        lootData = Memory.Loot.Loot.OfType<LootItem>().Select(l => (object)new
                        {
                            l.Name,
                            l.ID,
                            l.Value,
                            Position = new { l.Position.X, l.Position.Y, l.Position.Z }
                        }).ToList();


                    }
                    else
                    {
                        lootData = null; // 非五秒情况下 loot 数据为 null

                    }

                    lastLootUpdate = DateTime.UtcNow; // 更新上次更新时间
                }
                else
                {
                    exfilsData = null;
                    corpsesData = null;
                    lootData = null;
                }

                var Mypdd = Memory.Players;

                int playerCount = Mypdd.Count;

                //Debug.WriteLine($"playerCount_Mypdd: {playerCount}");

                var playersData2 = Mypdd.Select(player => new
                {
                    Name = player.Value.Name ?? "Unknown",
                    IsPMC = player.Value.IsPMC,
                    IsLocalPlayer = player.Value.IsLocalPlayer,
                    IsAlive = player.Value.IsAlive,
                    IsActive = player.Value.IsActive,
                    Lvl = player.Value.Lvl,
                    KDA = player.Value.KDA,
                    ProfileID = player.Value.ProfileID ?? "DefaultProfileID",
                    AccountID = player.Value.AccountID ?? "DefaultAccountID",
                    HasExfild = player.Value.HasExfild,
                    Type = player.Value.Type,
                    Gear = player.Value.Gear.Select(g => new
                    {
                        Slot = g.Key,
                        LongName = g.Value?.Long ?? "DefaultLongName",
                        ShortName = g.Value?.Short ?? "DefaultShortName",
                        ItemValue = g.Value?.Value ?? 0
                    }).ToList(),
                    Position = new
                    {
                        X = player.Value.Position.X,
                        Y = player.Value.Position.Y,
                        Z = player.Value.Position.Z
                    },
                    Rotation = new
                    {
                        Yaw = player.Value.Rotation.X,
                        Pitch = player.Value.Rotation.Y
                    }
                }).ToList();

                //Debug.WriteLine($"-----------------------");
                //Debug.WriteLine($"playerCount_Mypdd2: {playersData2.Count}");

                var updateData = new
                {
                    timestamp = DateTime.UtcNow, // 添加短时间戳

                    players = playersData2,

                    loot = lootData,

                    exfils = exfilsData,

                    corpses = corpsesData,

                    // containers = containersData,
                };


                var updateJson = JsonSerializer.Serialize(updateData);
                var updateBytes = Encoding.UTF8.GetBytes(updateJson);
                var updateBuffer = new ArraySegment<byte>(updateBytes);

                await webSocket.SendAsync(updateBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                Debug.WriteLine("循环");
            }

        }

        //// 发送消息
        //var responseMessage = Encoding.UTF8.GetBytes("Message received");
        //await webSocket.SendAsync(new ArraySegment<byte>(responseMessage), WebSocketMessageType.Text, true, CancellationToken.None);
        //result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None 

        // 关闭 WebSocket
        Debug.WriteLine("CLOSE");
        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    }
}
}