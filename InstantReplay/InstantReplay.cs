using BepInEx;
using System.Security.Permissions;
using UnityEngine;
using UnityEngine.Rendering;
using BepInEx.Logging;
using InstantReplay.Overlays;
using System;
using Unity.Profiling;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace InstantReplay;

[BepInPlugin(MOD_ID, "InstantReplay", "1.1.0")]
public class InstantReplay : BaseUnityPlugin
{
    public const string MOD_ID = "Gamer025.InstantReplay";

    //Config
#pragma warning disable CA2211
    // Non-constant fields should not be visible
    public static Configurable<int> FPS;
    public static Configurable<int> maxSecs;
    public static Configurable<bool> downscaleReplay;
    public static Configurable<bool> muteGame;
    public static Configurable<bool> autoPauseGameover;
    public static Configurable<DefaultReplayMode> replayMode;
    public static Configurable<bool> increasedMaxRam;
    //Keybind Configs
    public static Configurable<KeyCode> enableKey;
    public static Configurable<KeyCode> pauseKey;
    public static Configurable<KeyCode> forwardKey;
    public static Configurable<KeyCode> rewindKey;
    public static Configurable<KeyCode> fullscreenKey;
    public static Configurable<KeyCode> exportPNGKey;
    public static Configurable<KeyCode> exportGifKey;
    //Export Configs
    public static Configurable<string> imageSavePath;
    public static Configurable<int> gifMaxLength;
    public static Configurable<float> gifScale;

#pragma warning restore CA2211
    //Time between frames (to capture)
    float TBF = 0;
    bool pauseCapture = false;
    //We are shutdown because memory was low
    bool shutdown = true;
    private ScreenOverlay screenOverlay;
    public ReplayOverlayState ReplayState = ReplayOverlayState.Shutdown;
    private StatusHUD statusHUD;

    //Split-Screen Mod integration
    public static bool splitScreenModEnabled = false;
    public static object splitScreenMB;
    public static Type splitScreenenumType;
    public static FieldInfo splitScreenEnum;
    public static int splitScreenNoSplit;
    public static MethodInfo splitScreenUpdateMethod;

    private FrameCompressor compressorWorker;
    private ProfilerRecorder systemMemoryRecorder;

    private List<Process> gifMakers = new List<Process>();

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
        On.RainWorld.PostModsInit += RainWorld_PostModsInit;
        systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
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
            //Failsafe for if scene gets switched while player is active
            On.ProcessManager.PostSwitchMainProcess += ProcessManager_PostSwitchMainProcessHook;

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

    private void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
    {
        orig(self);
        if (self.options.enabledMods.Contains("henpemaz_splitscreencoop"))
        {
            MonoBehaviour[] allMono = FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour mono in allMono)
            {
                if (mono.GetType().Name == "SplitScreenCoop")
                {
                    try
                    {
                        Logger_p.LogInfo("Detected SplitScreen mod.");
                        //We only set the bool to true if we also find the MB
                        splitScreenModEnabled = true;
                        splitScreenMB = mono;
                        Logger_p.LogInfo($"splitScreenMB: {splitScreenMB}");
                        splitScreenenumType = mono.GetType().GetField("preferedSplitMode").FieldType;
                        Logger_p.LogInfo($"splitScreenenumType: {splitScreenenumType}");
                        splitScreenEnum = mono.GetType().GetField("preferedSplitMode");
                        Logger_p.LogInfo($"splitScreenEnum: {splitScreenEnum}");
                        FieldInfo NoSplitEnumEntry = splitScreenenumType.GetField("NoSplit");
                        Logger_p.LogInfo($"NoSplitEnumEntry: {NoSplitEnumEntry}");
                        splitScreenNoSplit = (int)NoSplitEnumEntry.GetValue(splitScreenenumType);
                        Logger_p.LogInfo($"splitScreenNoSplit: {splitScreenNoSplit}");
                        splitScreenUpdateMethod = mono.GetType().GetMethod("SetSplitMode");
                        Logger_p.LogInfo($"splitScreenNoSplit: {splitScreenUpdateMethod}");
                    }
                    catch (Exception ex)
                    {
                        Logger_p.LogError(ex.Message);
                        Logger_p.LogError(ex.StackTrace);
                    }
                }
            }
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

    private void ProcessManager_PostSwitchMainProcessHook(On.ProcessManager.orig_PostSwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
    {
        ReplayState = ReplayOverlayState.Shutdown;
        if (screenOverlay != null)
        {
            screenOverlay.Dispose();
            screenOverlay = null;
        }
        AudioListener.pause = false;
        orig(self, ID);
    }

    void HUDInitSinglePlayerHudHook(On.HUD.HUD.orig_InitSinglePlayerHud orig, HUD.HUD self, RoomCamera cam)
    {
        orig(self, cam);
        statusHUD = new StatusHUD(self);
        self.AddPart(statusHUD);
    }

    bool toggleDown = false;
    bool gifDown = false;
    float captureTimestacker = 0;
    float secondsTimestacker = 0;
    float gameOverCount = 0;
    int errorCount = 0;
    int splitScreenOldValue;
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
            secondsTimestacker += dt;

            if (secondsTimestacker > 1f)
            {
                secondsTimestacker = 0;
                MemoryWatcher(self);
                for (int i = 0; i < gifMakers.Count; i++)
                {
                    if (gifMakers[i].HasExited)
                    {
                        if (gifMakers[i].ExitCode != 0)
                        {
                            statusHUD.AddStatusMessage($"Gif creation failed! Err:{gifMakers[i].ExitCode}", 30);
                            Logger_p.LogError($"GifMaker.exe failed with exit code: {gifMakers[i].ExitCode}");
                            string gifMakerGifPath = gifMakers[i].StartInfo.Arguments;
                            Logger_p.LogDebug($"gifMakerGifPath: {gifMakerGifPath}, data: {gifMakerGifPath + ".data"}, exists? {File.Exists(gifMakerGifPath + ".data")}");
                            if (File.Exists(gifMakerGifPath + ".data"))
                                File.Delete(gifMakerGifPath + ".data");
                            if (File.Exists(gifMakerGifPath + ".meta"))
                                File.Delete(gifMakerGifPath + ".meta");
                            gifMakers[i] = null;
                        }
                        else
                        {
                            statusHUD.AddStatusMessage("Gif creation finished!", 20);
                            gifMakers[i] = null;
                        }
                    }
                }
                gifMakers.RemoveAll(item => item == null);
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

                    //Splitscreen is enabled, we need to disable it while the player is active
                    if (InstantReplay.splitScreenModEnabled)
                    {
                        splitScreenOldValue = (int)splitScreenEnum.GetValue(splitScreenMB);
                        object newEnumValue = Enum.ToObject(splitScreenenumType, splitScreenNoSplit);
                        splitScreenEnum.SetValue(null, newEnumValue);
                        splitScreenUpdateMethod.Invoke(splitScreenMB, new object[2] { splitScreenEnum.GetValue(null), self });
                    }
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

            bool GifExportKey = Input.GetKey(exportGifKey.Value);
            if (GifExportKey && !gifDown)
            {
                if (gifMakers.Count > 2)
                {
                    ME.Logger_p.LogInfo($"Already creating more than 2 gifs, cancelling.");
                }
                else
                {
                    string filename = $"{string.Format("{0:yyyy-MM-dd_HH-mm-ss-fff}", DateTime.Now)}.gif";
                    string path = Path.Combine(InstantReplay.imageSavePath.Value, filename);
                    ME.Logger_p.LogInfo($"Export Gif data to: {path}");
                    statusHUD.AddStatusMessage($"Creating new Gif {filename}...", 30);
                    try
                    {
                        gifMakers.Add(compressorWorker.Capture.ExportGifData(path));
                    }
                    catch (Exception ex)
                    {
                        ME.Logger_p.LogError($"Error exporting: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
            gifDown = GifExportKey;

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

                    //Make sure we put everything back like we left it
                    if (InstantReplay.splitScreenModEnabled)
                    {
                        object newEnumValue = Enum.ToObject(splitScreenenumType, splitScreenOldValue);
                        splitScreenEnum.SetValue(null, newEnumValue);
                        splitScreenUpdateMethod.Invoke(splitScreenMB, new object[2] { splitScreenEnum.GetValue(null), self });
                    }
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
                self.paused = false;
                AudioListener.pause = false;
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
        if (usedRAM > 3000000000 || (usedRAM > 2700000000 && !increasedMaxRam.Value))
        {
            Logger_p.LogError($"Rain World memory usage above {(increasedMaxRam.Value ? "3.0GB" : "2.7GB")}, is at {usedRAM} bytes, exiting!\n Compressed frames size: {compressorWorker.Capture.FrameBytes} bytes.");
            statusHUD?.SetError("Rain Worlds free memory is critically low!\nInstant Replay is now exiting and will be disabled for the remaining cycle/round.");
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