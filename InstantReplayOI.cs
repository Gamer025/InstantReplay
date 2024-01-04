using System;
using UnityEngine;
using Menu.Remix.MixedUI;
using System.IO;

namespace InstantReplay
{
    public class InstantReplayOI : OptionInterface
    {
        public InstantReplayOI()
        {
            InstantReplay.FPS = config.Bind("FPS", 20, new ConfigAcceptableRange<int>(10, 40));
            InstantReplay.maxSecs = config.Bind("maxSecs", 10, new ConfigAcceptableRange<int>(5, 20));
            InstantReplay.replayMode = config.Bind("replayMode", DefaultReplayMode.Popup);
            InstantReplay.downscaleReplay = config.Bind("downscaleReplay", false, new ConfigurableInfo("Faster but more pixelated ingame replay\nEnable if you have performance problems when watching replays", null, "Downscale ingame replay"));
            InstantReplay.imageSavePath = config.Bind("imageSavePath", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Rain World"), new ConfigurableInfo("Save location for images/gifs", null, "Folder to store images in"));
            InstantReplay.muteGame = config.Bind("muteGame", true, new ConfigurableInfo("Mute game when replay active", null, "Mute game on replay"));
            InstantReplay.autoPauseGameover = config.Bind("autoPauseGameover", true, new ConfigurableInfo("Pause capture on Gameover", null, "Pause capture on Gameover"));
            //Keybinds
            InstantReplay.enableKey = config.Bind("enableKey", KeyCode.I);
            InstantReplay.pauseKey = config.Bind("pauseKey", KeyCode.P);
            InstantReplay.forwardKey = config.Bind("forwardKey", KeyCode.RightArrow);
            InstantReplay.rewindKey = config.Bind("backwardsKey", KeyCode.LeftArrow);
            InstantReplay.fullscreenKey = config.Bind("fullscreenKey", KeyCode.F);
            InstantReplay.exportPNGKey = config.Bind("exportPNGKey", KeyCode.E);

            try
            {
                if (InstantReplay.imageSavePath.Value == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Rain World"))
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Rain World"));
                }
            }
            catch
            {
                InstantReplay.ME.Logger_p.LogError("Couldn't create default save location folder path");
            }
        }

        private OpTextBox imagePathTb;
        private OpLabel pathErrorLb;
        public override void Initialize()
        {
            base.Initialize();

            Tabs = new OpTab[2]; // Each OpTab is 600 x 600 pixel sized canvas
            Tabs[0] = new OpTab(this, "Main");
            Tabs[1] = new OpTab(this, "Keybinds");

            //Heading
            Tabs[0].AddItems(new OpLabel(220f, 580f, "Instant Replay Config", bigText: true)
            { description = "Instant Replay Config" });

            //Capture FPS
            Tabs[0].AddItems(new OpLabel(new Vector2(10f, 500f), new Vector2(100f, 20f), "FPS capture rate:", alignment: FLabelAlignment.Right)
            { description = "How often to capture a frame" });
            Tabs[0].AddItems(new OpSlider(InstantReplay.FPS, new Vector2(120f, 495f), 100)
            { description = "How often to capture a frame" });

            //Capture time
            Tabs[0].AddItems(new OpLabel(new Vector2(10f, 460f), new Vector2(100f, 20f), "Seconds to capture:", alignment: FLabelAlignment.Right)
            { description = "How many seconds to capture." });
            Tabs[0].AddItems(new OpSlider(InstantReplay.maxSecs, new Vector2(120f, 455f), 200)
            { description = "How many seconds to capture." });

            //Fullscreen or popup
            Tabs[0].AddItems(new OpResourceSelector(InstantReplay.replayMode, new Vector2(120f, 420f), 100f));
            Tabs[0].AddItems(new OpLabel(new Vector2(10f, 420f), new Vector2(100f, 20f), "Default player mode:", alignment: FLabelAlignment.Right)
            { description = "Fullscreen or popup player for ingame replay player" });

            //Image save path
            pathErrorLb = new OpLabel(260f, 310f, "");
            Tabs[0].AddItems(pathErrorLb);
            imagePathTb = new OpTextBox(InstantReplay.imageSavePath, new Vector2(120f, 338f), 300f);
            Tabs[0].AddItems(imagePathTb);
            Tabs[0].AddItems(new OpLabel(new Vector2(10f, 340f), new Vector2(100f, 20f), "Image save location:", alignment: FLabelAlignment.Right)
            { description = "Folder in which to store images/gifs." });
            OpSimpleButton openButton = new OpSimpleButton(new Vector2(440f, 338f), new Vector2(150, 25f), "Validate path")
            {
                description = "Check if the entered path exists and is writeable"
            };
            openButton.OnClick += ValidatePath;
            Tabs[0].AddItems(openButton);

            //Downscale replayer
            Tabs[0].AddItems(new OpCheckBox(InstantReplay.downscaleReplay, new Vector2(10f, 280f)));
            Tabs[0].AddItems(new OpLabel(45f, 280f, "Downscale ingame replay")
            { description = "Faster but more pixelated ingame replay\nEnable if you have performances problem when watching replays" });

            //Mute game
            Tabs[0].AddItems(new OpCheckBox(InstantReplay.muteGame, new Vector2(10f, 250f)));
            Tabs[0].AddItems(new OpLabel(45f, 250f, "Mute game on replay")
            { description = "Mute the game when a replay is active" });

            //Autopause Gameover
            Tabs[0].AddItems(new OpCheckBox(InstantReplay.autoPauseGameover, new Vector2(10f, 220f)));
            Tabs[0].AddItems(new OpLabel(45f, 220f, "Pause capture on Gameover")
            { description = "Automatically pause the capture while the gameover screen is active.\nThis way you will always be able to rewind to the moment you died." });

            //Credits
            Tabs[0].AddItems(new OpLabel(225f, 5f, $"InstantReplay Version {InstantReplay.modVersion}")
            { description = "Made by Gamer025" });

            //
            //Keybinds
            //
            Tabs[1].AddItems(new OpLabel(220f, 580f, "Keybinds Config", bigText: true)
            { description = "Keybinds Config" });

            //Toogle Key
            Tabs[1].AddItems(new OpLabel(new Vector2(10f, 500f), new Vector2(80f, 20f), "Activation key:", alignment:FLabelAlignment.Right)
            { description = "Enables and disables the instant replay viewer" });
            Tabs[1].AddItems(new OpKeyBinder(InstantReplay.enableKey, new Vector2(100f, 495f), new Vector2(120f, 15f), collisionCheck:false)
            { description = "Enables and disables the instant replay viewer" });

            //Pause Key
            Tabs[1].AddItems(new OpLabel(new Vector2(10f, 460f), new Vector2(80f, 20f), "Pause key:", alignment: FLabelAlignment.Right)
            { description = "Pause and unpause when viewing replays" });
            Tabs[1].AddItems(new OpKeyBinder(InstantReplay.pauseKey, new Vector2(100f, 455f), new Vector2(120f, 15f), collisionCheck: false)
            { description = "Pause and unpause when viewing replays" });

            //Forwards Key
            Tabs[1].AddItems(new OpLabel(new Vector2(10f, 420f), new Vector2(80f, 20f), "Foward key:", alignment: FLabelAlignment.Right)
            { description = "Go forward one frame (tap) or fast forward (hold) in the replay viewer" });
            Tabs[1].AddItems(new OpKeyBinder(InstantReplay.forwardKey, new Vector2(100f, 415f), new Vector2(120f, 15f), collisionCheck: false)
            { description = "Go forward one frame (tap) or fast forward (hold) in the replay viewer" });

            //Backwards Key
            Tabs[1].AddItems(new OpLabel(new Vector2(10f, 380f), new Vector2(80f, 20f), "Rewind key:", alignment: FLabelAlignment.Right)
            { description = "Go backwards one frame (tap) or rewind (hold) in the replay viewer" });
            Tabs[1].AddItems(new OpKeyBinder(InstantReplay.rewindKey, new Vector2(100f, 375f), new Vector2(120f, 15f), collisionCheck: false)
            { description = "Go backwards one frame (tap) or rewind (hold) in the replay viewer" });

            //Fullscreen Key
            Tabs[1].AddItems(new OpLabel(new Vector2(10f, 340f), new Vector2(80f, 20f), "Fullscreen toggle:", alignment: FLabelAlignment.Right)
            { description = "Toggle between the fullscreen and popup version of the player while it's active" });
            Tabs[1].AddItems(new OpKeyBinder(InstantReplay.fullscreenKey, new Vector2(100f, 335f), new Vector2(120f, 15f), collisionCheck: false)
            { description = "Toggle between the fullscreen and popup version of the player while it's active" });

            //Export PNG Key
            Tabs[1].AddItems(new OpLabel(new Vector2(10f, 300f), new Vector2(80f, 20f), "PNG export:", alignment: FLabelAlignment.Right)
            { description = "Export the current frame of the replay viewer into an PNG" });
            Tabs[1].AddItems(new OpKeyBinder(InstantReplay.exportPNGKey, new Vector2(100f, 295f), new Vector2(120f, 15f), collisionCheck: false)
            { description = "Export the current frame of the replay viewer into an PNG" });

        }

        private void ValidatePath(UIfocusable trigger)
        {

            InstantReplay.ME.Logger_p.LogDebug($"Validating {imagePathTb.value}");
            if (Directory.Exists(imagePathTb.value))
            {
                if (IsDirectoryWritable(imagePathTb.value))
                {
                    pathErrorLb.text = "";
                    imagePathTb.colorText = Color.green;
                }
                else
                {
                    pathErrorLb.text = "Path exists but not writeable";
                    imagePathTb.colorText = Color.red;
                }
            }
            else
            {
                pathErrorLb.text = "Path invalid / directory does not exist";
                imagePathTb.colorText = Color.red;
            }
        }

        public bool IsDirectoryWritable(string dirPath)
        {
            try
            {
                using (FileStream fs = File.Create(
                    Path.Combine(
                        dirPath,
                        Path.GetRandomFileName()
                    ),
                    1,
                    FileOptions.DeleteOnClose)
                )
                { }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}