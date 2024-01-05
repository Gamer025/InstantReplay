//TODO:
//Gif export (Figure out memory mapped files and send data to a 64bit process)
using BepInEx;
using System.Security.Permissions;
using UnityEngine;
using UnityEngine.Rendering;
using BepInEx.Logging;
using InstantReplay.Overlays;
using System;
using Unity.Profiling;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace InstantReplay;

[BepInPlugin(MOD_ID, "InstantReplay", "1.0.0")]
public class InstantReplay : BaseUnityPlugin
{
    public const string MOD_ID = "Gamer025.InstantReplay";

    //Config
#pragma warning disable CA2211
    public static Configurable<int> FPS;
 // Non-constant fields should not be visible
    public static Configurable<int> maxSecs;
    public static Configurable<bool> downscaleReplay;
    public static Configurable<bool> muteGame;
    public static Configurable<bool> autoPauseGameover;
    public static Configurable<DefaultReplayMode> replayMode;
    public static Configurable<string> imageSavePath;
    //Keybind Configs
    public static Configurable<KeyCode> enableKey;
    public static Configurable<KeyCode> pauseKey;
    public static Configurable<KeyCode> forwardKey;
    public static Configurable<KeyCode> rewindKey;
    public static Configurable<KeyCode> fullscreenKey;
    public static Configurable<KeyCode> exportPNGKey;
#pragma warning restore CA2211
    //Time between frames (to capture)
    float TBF = 0;
    bool pauseCapture = false;
    //We are shutdown because memory was low
    bool shutdown = true;
    private ScreenOverlay screenOverlay;
    internal ReplayOverlayState ReplayState = ReplayOverlayState.Shutdown;
    private StatusHUD statusHUD;

    private FrameCompressor compressorWorker;
    private ProfilerRecorder systemMemoryRecorder;
    //private ProfilerRecorder gcMemoryRecorder;
    //private ProfilerRecorder gcMemoryRecorder2;

    private static WeakReference __me;
    public static InstantReplay ME => __me?.Target as InstantReplay;
    public ManualLogSource Logger_p => Logger;
    public readonly static Version modVersion = ((BepInPlugin)Attribute.GetCustomAttribute(typeof(InstantReplay), typeof(BepInPlugin))).Version;
    private AssetBundle IRAssetBundle;
    public InstantReplay()
    {
        __me = new(this);
    }

    public void OnEnable()
    {
        On.RainWorld.OnModsInit += OnModsInitHook;
        systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        //gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
        //gcMemoryRecorder2 = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
    }

    bool initDone = false;
    private void OnModsInitHook(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        if (!initDone)
        {
            Logger_p.Log(LogLevel.Info, "InstantReplay init");
            try
            {
                MachineConnector.SetRegisteredOI(MOD_ID, new InstantReplayOI());
            }
            catch (Exception e)
            {
                Logger_p.Log(LogLevel.Error, $"Error creating options interface:\n {e}");
            }
            Logger_p.Log(LogLevel.Info, $"FPS: {FPS.Value}");
            Logger_p.Log(LogLevel.Info, $"Capture length: {maxSecs.Value}");
            Logger_p.Log(LogLevel.Info, $"Image save path: {imageSavePath.Value}");
            //Used for trigger frame capture and drive Overlay
            On.RainWorldGame.RawUpdate += RainWorldGame_RawUpdate;
            //Used for var inits
            On.RainWorldGame.ctor += RainWorldGame_ctor;
            //Used to run the ScreenOverlay because RoomCameraDrawUpdateHook will no longer run once the game gets paused
            On.RainWorldGame.GrafUpdate += RainWorldGame_GrafUpdateHook;
            //Game over string
            On.HUD.TextPrompt.UpdateGameOverString += TextPrompt_UpdateGameOverStringHook;
            //Add own HUD to the game
            On.HUD.HUD.InitSinglePlayerHud += HUDInitSinglePlayerHudHook;

            try
            {
                IRAssetBundle = AssetBundle.LoadFromFile(AssetManager.ResolveFilePath("AssetBundles/gamer025.instantreplay.assets"));
                if (IRAssetBundle == null)
                {
                    ME.Logger_p.Log(LogLevel.Error, $"InstantReplay: Failed to load AssetBundle from {AssetManager.ResolveFilePath("AssetBundles/gamer025.instantreplay.assets")}");
                    Destroy(this);
                }
                ME.Logger_p.Log(LogLevel.Debug, $"Assetbundle content: {String.Join(", ", IRAssetBundle.GetAllAssetNames())}");
                self.Shaders.Add("InstantReplay", FShader.CreateShader("InstantReplay", IRAssetBundle.LoadAsset<Shader>("instantreplay.shader")));
                Futile.atlasManager.LoadAtlasFromTexture("InstantReplayPlay", IRAssetBundle.LoadAsset<Texture2D>("play.png"), false);
                Futile.atlasManager.LoadAtlasFromTexture("InstantReplayPause", IRAssetBundle.LoadAsset<Texture2D>("pause.png"), false);
            }
            catch (Exception e)
            {
                ME.Logger_p.Log(LogLevel.Error, $"Error loading asset bundle:\n {e}");
            }

            initDone = true;
        }
    }

    private void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
    {
        orig(self, manager);
        shutdown = false;
        pauseCapture = false;
        TBF = 1f / FPS.Value;
        compressorWorker?.Dispose();
        compressorWorker = new FrameCompressor(FPS.Value * maxSecs.Value, (int)self.rainWorld.options.ScreenSize.x, (int)self.rainWorld.options.ScreenSize.y, FPS.Value);
        screenOverlay = new ScreenOverlay(self.rainWorld);
    }

    void HUDInitSinglePlayerHudHook(On.HUD.HUD.orig_InitSinglePlayerHud orig, HUD.HUD self, RoomCamera cam)
    {
        orig(self, cam);
        statusHUD = new StatusHUD(self);
        self.AddPart(statusHUD);
    }

    bool toggleDown = false;
    float captureTimestacker = 0;
    float memoryTimestacker = 0;
    float gameOverCount = 0;
    int errorCount = 0;
    private void RainWorldGame_RawUpdate(On.RainWorldGame.orig_RawUpdate orig, RainWorldGame self, float dt)
    {
        try
        {
            if (shutdown)
            {
                orig(self, dt);
                return;
            }

            captureTimestacker += dt;
            memoryTimestacker += dt;

            if (memoryTimestacker > 1f)
            {
                memoryTimestacker = 0;
                MemoryWatcher(self);
            }

            if (captureTimestacker > TBF)
            {
                //Capture 1 extra second
                if (autoPauseGameover.Value)
                {
                    if (self.GameOverModeActive)
                    {
                        gameOverCount = Math.Min(gameOverCount + dt, 1.0f);
                    }
                    else
                    {
                        gameOverCount = 0;
                    }
                }

                if (!pauseCapture && gameOverCount < 1.0f)
                    CaptureFrame();
                captureTimestacker = 0;
            }

            bool toggleKey = Input.GetKey(enableKey.Value);
            if (toggleKey && !toggleDown)
            {
                //If the player is not running start it and pause the game + capture
                if (ReplayState == ReplayOverlayState.Shutdown)
                {
                    Logger_p.LogInfo("Starting replay viewer");
                    ReplayState = ReplayOverlayState.Starting;
                    //Prevent some weirdness if the enableKey is equal to Space ...
                    self.cameras[0].hud.textPrompt.restartNotAllowed += 5;
                    //When we are replaying the game will pause and no new captures will happen
                    pauseCapture = true;
                    self.paused = true;
                    if (muteGame.Value)
                        AudioListener.pause = true;
                }
                //If it runs get it into the Exiting state
                if (ReplayState == ReplayOverlayState.Running)
                {
                    Logger_p.LogInfo("Stopping replay viewer");
                    ReplayState = ReplayOverlayState.Exiting;
                    self.cameras[0].hud.textPrompt.restartNotAllowed += 5;
                }
                //If the player is exiting/starting do nothing but thats unlikely to even happen
            }
            toggleDown = toggleKey;

            //Just to make sure people don't get trapped in the viewer 
            if (Input.GetKey("escape") && ReplayState == ReplayOverlayState.Running)
            {
                Logger_p.LogInfo("Stopping replay viewer");
                ReplayState = ReplayOverlayState.Exiting;
                //Prevent the game from going to the death screen if the player exited the interface via the escape key
                self.cameras[0].hud.textPrompt.restartNotAllowed += 10;
            }

            //The replayer is always active expect when it isn't
            if (ReplayState != ReplayOverlayState.Shutdown)
            {
                screenOverlay.ReplayUpdate(dt, compressorWorker.Capture, ref ReplayState, self.rainWorld);
                //Replayer got one tick to shut its stuff down and we can transition into the shutdown state
                if (ReplayState == ReplayOverlayState.Exiting)
                {
                    ReplayState = ReplayOverlayState.Shutdown;
                    pauseCapture = false;
                    self.paused = false;
                    if (muteGame.Value)
                        AudioListener.pause = false;
                }
            }

        }
        catch (Exception ex)
        {

            errorCount++;
            Logger_p.LogError($"Error {errorCount} in RawUpdate Hook: {ex.Message}\n{ex.StackTrace}");
            if (errorCount > 10)
            {
                On.RainWorldGame.RawUpdate -= RainWorldGame_RawUpdate;
            }
        }
        orig(self, dt);
    }

    private void RainWorldGame_GrafUpdateHook(On.RainWorldGame.orig_GrafUpdate orig, RainWorldGame self, float timeStacker)
    {
        orig(self, timeStacker);

    }

    private void TextPrompt_UpdateGameOverStringHook(On.HUD.TextPrompt.orig_UpdateGameOverString orig, HUD.TextPrompt self, Options.ControlSetup.Preset controllerType)
    {
        orig(self, controllerType);
        self.gameOverString += $" or {enableKey.Value} for instant replay";
    }


    public void CaptureFrame()
    {
        //Sadly we can't asnyc readback to a compressed texture ...
        AsyncGPUReadback.Request(Futile.screen.renderTexture, 0, TextureFormat.RGB24, CaptureFrameCallback);
    }

    public void CaptureFrameCallback(AsyncGPUReadbackRequest req)
    {
        //In case we are fail fasting and the worker is gone ...
        if (compressorWorker == null)
            return;
        //MRE is still set, so worker stilly busy, discard frame
        if (compressorWorker.MRE.WaitOne(0))
        {
            Logger_p.LogWarning("Discarded frame because compress worker busy!");
            return;
        }
        compressorWorker.frameToCompress = req.GetData<byte>().ToArray();
        compressorWorker.MRE?.Set();
    }

    //Will free most of InstantReplays resources if memory usage gets too high
    //Seemingly this doesn't release Unitys memory instantly
    //As in the memory usage inside taskmanager stays the same but Unity is most likely keeping it allocated for performance reason
    //Might also be memory fragmentation?
    //Normally after hibernation or going to the main menu RAM usage fixes itself. Strangly enough restarting with dev tools most of the time takes 2 restarts to free the memory
    //Interestingly enough when discarding the compressorWorker and creating a new one at for example 2GB usage memory usage will also go down after a few captures frames
    //Similar can be achieved if this code hits, the game is at 2.7GB usage and one just spam loads regions with Warp mods or similar, eventually memory drop to 1GB???
    //So presumably the RAM freed by this will hopefully actually be available to other code :)
    private void MemoryWatcher(RainWorldGame game)
    {
        long usedRAM = systemMemoryRecorder.LastValue;
        //Logger_p.LogDebug($"System usage: {systemMemoryRecorder.LastValue}");
        //Logger_p.LogDebug($"GC RAM usage: {gcMemoryRecorder.LastValue}");
        //Logger_p.LogDebug($"GC Reserved : {gcMemoryRecorder2.LastValue}");
        //Logger_p.LogDebug($"GC Re - used: {gcMemoryRecorder2.LastValue - gcMemoryRecorder.LastValue}");
        //High watermark, fail fast
        if (usedRAM > 2700000000)
        {
            Logger_p.LogError($"Rain World memory usage above 2.7GB, is at {usedRAM} bytes, exiting!\n Compressed frames size: {compressorWorker.Capture.FrameBytes} bytes.");
            statusHUD?.SetStatus("Rain Worlds free memory is critically low!\nInstant Replay is now exiting and will be disabled for the remaining cycle/round.", Color.red);
            pauseCapture = true;
            shutdown = true;
            if (ReplayState != ReplayOverlayState.Shutdown)
            {
                ReplayState = ReplayOverlayState.Exiting;
                //Make sure the overlay cleans ups
                screenOverlay.ReplayUpdate(0f, compressorWorker.Capture, ref ReplayState, game.rainWorld);
                ReplayState = ReplayOverlayState.Shutdown;
                //Restore
                game.paused = false;
                if (muteGame.Value)
                    AudioListener.pause = false;
            }
            screenOverlay = null;
            //This should be the majority of our memory usage
            compressorWorker.Dispose();
            compressorWorker = null;
            GC.Collect();
        }
    }
}

public enum DefaultReplayMode
{
    Fullscreen,
    Popup
}