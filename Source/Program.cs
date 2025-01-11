using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.WebSockets;

namespace eft_dma_radar
{
    static class Program
    {
        private static readonly Mutex _mutex;
        private static readonly bool _singleton;
        private static readonly Config _config;
        private static readonly LootFilterManager _lootFilterManager;
        private static readonly Watchlist _watchlist;
        private static readonly AIFactionManager _aiFactionManager;
        private static readonly object _logLock = new();
        private static readonly StreamWriter _log;

        public static Config Config => _config;
        public static Watchlist Watchlist => _watchlist;
        public static AIFactionManager AIFactionManager => _aiFactionManager;
        public static LootFilterManager LootFilterManager => _lootFilterManager;

        #region Static Constructor
        static Program()
        {
            _mutex = new Mutex(true, "9A19103F-16F7-4668-BE54-9A1E7A4F7556", out _singleton);

            if (Config.TryLoadConfig(out _config) is not true)
                _config = new Config();

            if (LootFilterManager.TryLoadLootFilterManager(out _lootFilterManager) is not true)
                _lootFilterManager = new LootFilterManager();

            if (Watchlist.TryLoadWatchlist(out _watchlist) is not true)
                _watchlist = new Watchlist();

            if (AIFactionManager.TryLoadAIFactions(out _aiFactionManager) is not true)
                _aiFactionManager = new AIFactionManager();

            if (_config.Logging)
            {
                _log = File.AppendText("log.txt");
                _log.AutoFlush = true;
            }
        }
        #endregion

        #region Program Entry Point
    static void Main(string[] args)
    {
        try
        {
            InitializeRadar();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Radar initialization failed: " + ex.Message);
        }
    }
        #endregion

        private static void InitializeRadar()
        {
            Console.OutputEncoding = System.Text.Encoding.Unicode; // allow Russian chars
            try
            {
                if (_singleton)
                {
                    RuntimeHelpers.RunClassConstructor(typeof(TarkovDevManager).TypeHandle); // invoke static constructor
                    RuntimeHelpers.RunClassConstructor(typeof(Memory).TypeHandle); // invoke static constructor
                    RuntimeHelpers.RunClassConstructor(typeof(LootManager).TypeHandle);
                    ApplicationConfiguration.Initialize();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(true);
                    Application.Run(new frmMain());
                }
                else
                {
                    throw new Exception("The Application Is Already Running!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "EFT Radar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(webBuilder =>
        {
            // Set the content root to the directory of the executable
            webBuilder.UseContentRoot(AppContext.BaseDirectory);

            webBuilder.UseKestrel(serverOptions =>
            {
                // Bind to all IP addresses on port 80
                serverOptions.ListenAnyIP(8080);

                // Additionally bind to specific hostnames
                serverOptions.Listen(System.Net.IPAddress.Loopback, 8080); // localhost
                serverOptions.Listen(System.Net.IPAddress.IPv6Loopback, 8080); // localhost IPv6
            });

            webBuilder.ConfigureServices(services =>
            {
                services.AddCors(options =>
                {
                    options.AddPolicy("AllowAllOrigins",
                        builder => builder
                            .AllowAnyOrigin()
                            .AllowAnyHeader()
                            .AllowAnyMethod());
                });
                services.AddControllers();
                services.AddWebSockets(options => { options.KeepAliveInterval = TimeSpan.FromSeconds(120); });
            });

            webBuilder.Configure((context, app) =>
            {
                app.UseDeveloperExceptionPage();

                // Middleware to redirect from / to /index.html
                app.Use(async (context, next) =>
                {
                    if (context.Request.Path == "/")
                    {
                        context.Response.Redirect("/index.html");
                        return;
                    }
                    await next();
                });

                // Serve static files from the directory where the EXE is located
                string exePath = AppContext.BaseDirectory;
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(exePath),
                    RequestPath = ""
                });

                app.UseRouting();
                app.UseCors("AllowAllOrigins");
                app.UseWebSockets();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.Map("/ws/connect", async context =>
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                            // Handle WebSocket connection here
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                        }
                    });
                });
            });
        });

        #region Methods
        public static void Log(string msg)
        {
            Debug.WriteLine(msg);
            if (_config?.Logging ?? false)
            {
                lock (_logLock) // Sync access to File IO
                {
                    _log.WriteLine($"{DateTime.Now}: {msg}");
                }
            }
        }

        public static void HideConsole()
        {
            ShowWindow(GetConsoleWindow(), ((_config?.Logging ?? false) ? 1 : 0)); // 0 : SW_HIDE
        }
        #endregion

        #region P/Invokes
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
        #endregion
    }
}