using System;
using System.IO;
using UnityEngine;
using static InstantReplay.InstantReplay;

namespace InstantReplay.Overlays
{
    enum ReplayOverlayState
    {
        Starting,
        Running,
        Exiting,
        Shutdown
    }

    public class ScreenOverlay : IDisposable
    {
        //Can't just have a Texture2DArray for every room camera, neither would it make sense so this is static
        //Also two arrays one used live during playback while the other gets ready in the background
        //private static Texture2DArray[] replayFrames = new Texture2DArray[2];
        //Array on which we actively work on, aka process frames into in the background
        //private int currentTextureArray = 0;

        private Texture2D currentFrame;
        private readonly FContainer container = new();
        private readonly FSprite[] sprites = new FSprite[3];

        public ScreenOverlay(RainWorld rainWorld)
        {
            //Replayer
            sprites[1] = new FSprite("Futile_White");
            sprites[1].shader = rainWorld.Shaders["InstantReplay"];
            sprites[1].isVisible = false;
            //Play Pause icon
            sprites[2] = new FSprite("InstantReplayPlay");
            sprites[2].shader = rainWorld.Shaders["Basic"];
            sprites[2].isVisible = false;
            //Background/Border
            sprites[0] = new FSprite("Futile_White");
            sprites[0].shader = rainWorld.Shaders["Basic"];
            sprites[0].color = new UnityEngine.Color(0.922f, 0.251f, 0.204f);
            sprites[0].isVisible = false;
            AllignSprites(rainWorld);
            AddToContainer();
        }
        public void AddToContainer()
        {
            foreach (FSprite sprite in sprites)
            {
                container.AddChild(sprite);
            }
        }

        //const int TextureArraySlices = 2;
        float pool = 0f;
        //The frame/index of the current TextureArray the shader is displaying
        //int currentShaderIndex = 0;
        //How many frames we already loaded for the upcoming texture array
        //int loadedFrames = 0;
        bool keypressedFull = false;
        bool keypressedPlay = false;
        bool keypressedSeek = false;
        bool keypressedExport = false;
        bool fullscreen = false;
        bool playing = true;
        int seekCounter = 0;
        //Gets called even when the game is paused
        //Strangly enough nothing in the game itself actually uses this method
        //There also seems to a bug/feature in the original code where this would be called twice per frame since the room camera itself and the spriteleaser will call this
        //This isn't affected because we live inside RoomCameraExtension which has its own separate leaser list, so we will only get called from the SpriteLeaser not the rCam
        internal void ReplayUpdate(float dt, GameCapture capture, ref ReplayOverlayState state, RainWorld rainWorld)
        {
            //Exit code
            if (state == ReplayOverlayState.Exiting)
            {
                for (int i = 0; i < sprites.Length; i++)
                {
                    sprites[i].isVisible = false;
                }
                Futile.stage.RemoveChild(container);
                UnityEngine.Object.Destroy(currentFrame);
                ME.Logger_p.LogDebug("Overlay exiting");
                return;
            }
            //Startup Code
            if (state == ReplayOverlayState.Starting)
            {
                capture.RewindToStart();
                //Move back to the current frame, because the call to GetCurrentUncompressedFrame in play will move use forward by one
                if (playing)
                    capture.MoveRead(-1);
                //Init texture2d
                if (downscaleReplay.Value)
                {
                    currentFrame = new Texture2D(capture.frameSize.x / 2, capture.frameSize.y / 2, TextureFormat.RGB24, false)
                    {
                        filterMode = FilterMode.Point,
                    };
                }
                else
                {
                    currentFrame = new Texture2D(capture.frameSize.x, capture.frameSize.y, TextureFormat.RGB24, false)
                    {
                        filterMode = FilterMode.Point,
                    };
                }
                Shader.SetGlobalTexture("Gamer025_InstantReplayTex", currentFrame);
                for (int i = 0; i < sprites.Length; i++)
                {
                    sprites[i].isVisible = true;
                }
                Futile.stage.AddChild(container);
                fullscreen = replayMode.Value == DefaultReplayMode.Fullscreen;
                AllignSprites(rainWorld);
                currentFrame.SetPixelData<byte>(capture.GetCurrentUncompressedFrame(0, downscale: InstantReplay.downscaleReplay.Value), 0);
                currentFrame.Apply();
                ME.Logger_p.LogDebug("Overlay startup done, going into running state");
                state = ReplayOverlayState.Running;
            }
            //Key input code
            //Fullscreen/popup switch
            bool fullscreenKey = Input.GetKey(InstantReplay.fullscreenKey.Value);
            if (fullscreenKey && !keypressedFull)
            {
                fullscreen = !fullscreen;
                AllignSprites(rainWorld);
            }
            keypressedFull = fullscreenKey;

            //Play/Pause
            bool pauseKey = Input.GetKey(InstantReplay.pauseKey.Value);
            if (pauseKey && !keypressedPlay)
            {
                playing = !playing;
                sprites[2].SetElementByName(playing ? "InstantReplayPlay" : "InstantReplayPause");
            }
            keypressedPlay = pauseKey;


            //Export current frame as PNG
            bool exportPNGKey = Input.GetKey(InstantReplay.exportPNGKey.Value);
            if (exportPNGKey && !keypressedExport)
            {
                string path = Path.Combine(InstantReplay.imageSavePath.Value, $"{string.Format("{0:yyyy-MM-dd_HH-mm-ss-fff}", DateTime.Now)}.png");
                ME.Logger_p.LogInfo($"Export PNG to: {path}");
                try
                {
                    File.WriteAllBytes(path, ImageConversion.EncodeToPNG(currentFrame));
                }
                catch (Exception ex)
                {
                    ME.Logger_p.LogError($"Error exporting: {ex.Message}\n{ex.StackTrace}");
                }
            }
            keypressedExport = exportPNGKey;

            bool rewind = Input.GetKey(InstantReplay.rewindKey.Value);
            bool forward = Input.GetKey(InstantReplay.forwardKey.Value);
            if (forward || rewind)
            {
                if (playing)
                {
                    playing = false;
                    sprites[2].SetElementByName("InstantReplayPause");
                }
                seekCounter++;

                if (!keypressedSeek || seekCounter > 15)
                {
                    currentFrame.SetPixelData<byte>(capture.GetCurrentUncompressedFrame(forward ? 1 : -1, downscale: InstantReplay.downscaleReplay.Value), 0);
                    currentFrame.Apply();
                }
            }
            else
                seekCounter = 0;
            keypressedSeek = rewind || forward;

            //Play code
            pool += dt;
            if (playing && pool > 1f / capture.FPS)
            {
                pool = 0;
                //Load next frame
                currentFrame.SetPixelData<byte>(capture.GetCurrentUncompressedFrame(downscale: InstantReplay.downscaleReplay.Value), 0);
                currentFrame.Apply();
            }
        }

        private void AllignSprites(RainWorld rainWorld)
        {
            if (fullscreen)
            {
                sprites[1].scaleX = rainWorld.options.ScreenSize.x / 16f;
                sprites[1].scaleY = rainWorld.options.ScreenSize.y / 16f;
                sprites[1].x = rainWorld.options.ScreenSize.x / 2;
                sprites[1].y = rainWorld.options.ScreenSize.y / 2;
                sprites[2].scaleX = 1f;
                sprites[2].scaleY = 1f;
                sprites[2].x = rainWorld.options.ScreenSize.x - 40;
                sprites[2].y = rainWorld.options.ScreenSize.y - 40;
            }
            else
            {
                sprites[1].scaleX = rainWorld.options.ScreenSize.x / 30f;
                sprites[1].scaleY = rainWorld.options.ScreenSize.y / 30f;
                sprites[1].x = rainWorld.options.ScreenSize.x / 2;
                sprites[1].y = rainWorld.options.ScreenSize.y - rainWorld.options.ScreenSize.y * 0.4f;
                sprites[2].scaleX = 0.6f;
                sprites[2].scaleY = 0.6f;
                sprites[2].x = sprites[1].x + sprites[1].scaleX * 16 / 2 - 24;
                sprites[2].y = sprites[1].y + sprites[1].scaleY * 16 / 2 - 24;
                sprites[0].scaleX = sprites[1].scaleX + 0.5f;
                sprites[0].scaleY = sprites[1].scaleY + 0.5f;
                sprites[0].x = sprites[1].x;
                sprites[0].y = sprites[1].y;
            }
        }

        public void Dispose()
        {
            Futile.stage.RemoveChild(container);
        }


        //A old monument of failure, uploading like 10 frames at once to the GPU just causes too much of a freeze
        //internal void ReplayUpdate(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, GameCapture capture, ref ReplayOverlayState state)
        //{
        //    if (state == ReplayOverlayState.Exiting)
        //    {
        //        sLeaser.sprites[0].isVisible = false;
        //        //Clean up texture arrays
        //        for (int i = 0; i < replayFrames.Length; i++)
        //        {
        //            Texture2DArray array = replayFrames[i];
        //            if (array != null)
        //            {
        //                UnityEngine.Object.Destroy(array);
        //                replayFrames[i] = null;
        //            }
        //        }
        //        loadedFrames = 0;
        //        currentShaderIndex = 0;
        //        currentTextureArray = 0;
        //        ME.Logger_p.LogDebug("Overlay exiting");
        //        return;
        //    }
        //    if (state == ReplayOverlayState.Starting)
        //    {
        //        capture.RewindToStart();
        //        //Fill up the current array with TextureArraySize frames so we have some buffer
        //        if (InstantReplay.downscaleReplay.Value)
        //        {
        //            replayFrames[currentTextureArray] = new Texture2DArray(capture.frameSize.x / 2, capture.frameSize.y / 2, TextureArraySlices, TextureFormat.RGB24, false)
        //            {
        //                filterMode = FilterMode.Point,
        //            };
        //        }
        //        else
        //        {
        //            replayFrames[currentTextureArray] = new Texture2DArray(capture.frameSize.x, capture.frameSize.y, TextureArraySlices, TextureFormat.RGB24, false)
        //            {
        //                filterMode = FilterMode.Point,
        //            };
        //        }
        //        for (int i = 0; i < TextureArraySlices; i++)
        //        {
        //            replayFrames[currentTextureArray].SetPixelData<byte>(capture.GetCurrentUncompressedFrame(downscale: InstantReplay.downscaleReplay.Value), 0, i);
        //        }
        //        replayFrames[currentTextureArray].Apply(false, makeNoLongerReadable: true);
        //        Shader.SetGlobalTexture("Gamer025_InstantReplayTex", replayFrames[currentTextureArray]);
        //        Shader.SetGlobalFloat("Gamer025_InstantReplayTextIndex", 0f);
        //        //Switch over to the other array
        //        currentTextureArray = 1 - currentTextureArray;
        //        sLeaser.sprites[0].isVisible = true;
        //        ME.Logger_p.LogDebug("Overlay startup done, going into running state");
        //        state = ReplayOverlayState.Running;
        //    }
        //    bool key = Input.GetKey("f");
        //    if (key && !keypressed)
        //    {
        //        if (fullscreen)
        //        {
        //            sLeaser.sprites[0].scaleX = rCam.game.rainWorld.options.ScreenSize.x / 24f;
        //            sLeaser.sprites[0].scaleY = 36f;
        //            sLeaser.sprites[0].x = rCam.game.rainWorld.options.ScreenSize.x / 2 - rCam.game.rainWorld.options.ScreenSize.x / 24f * 16 / 2;
        //            sLeaser.sprites[0].y = rCam.game.rainWorld.options.ScreenSize.y - rCam.game.rainWorld.options.ScreenSize.y * 0.8f;
        //        }
        //        else
        //        {
        //            sLeaser.sprites[0].scaleX = rCam.game.rainWorld.options.ScreenSize.x / 16f;
        //            sLeaser.sprites[0].scaleY = 48f;
        //            sLeaser.sprites[0].x = 0;
        //            sLeaser.sprites[0].y = 0;
        //        }
        //        fullscreen = !fullscreen;
        //    }
        //    keypressed = key;
        //    pool += timeStacker / rCam.game.framesPerSecond;
        //    if (pool > 1f / FPS.Value)
        //    {
        //        pool = 0;
        //        currentShaderIndex++;
        //        Shader.SetGlobalFloat("Gamer025_InstantReplayTextIndex", currentShaderIndex);
        //        //We reached the end of the current texture array
        //        if (currentShaderIndex >= TextureArraySlices)
        //        {
        //            //Uh uh not all frames could were loaded in the background, need to catch up
        //            if (loadedFrames < TextureArraySlices)
        //            {
        //                replayFrames[currentTextureArray].SetPixelData<byte>(capture.GetCurrentUncompressedFrame(), 0, loadedFrames);
        //                loadedFrames++;
        //            }
        //            //Make the current working array to the rendered one
        //            replayFrames[currentTextureArray].Apply(false, true);
        //            Shader.SetGlobalTexture("Gamer025_InstantReplayTex", replayFrames[currentTextureArray]);
        //            currentShaderIndex = 0;
        //            Shader.SetGlobalFloat("Gamer025_InstantReplayTextIndex", currentShaderIndex);
        //            //Switch over to the other array as working array
        //            currentTextureArray = 1 - currentTextureArray;
        //            loadedFrames = 0;
        //        }
        //        //Load upcoming frames in the background
        //        if (loadedFrames == 0)
        //        {
        //            if (replayFrames[currentTextureArray] != null)
        //            {
        //                UnityEngine.Object.Destroy(replayFrames[currentTextureArray]);
        //            }
        //            if (InstantReplay.downscaleReplay.Value)
        //            {
        //                replayFrames[currentTextureArray] = new Texture2DArray(capture.frameSize.x / 2, capture.frameSize.y / 2, TextureArraySlices, TextureFormat.RGB24, false)
        //                {
        //                    filterMode = FilterMode.Point,
        //                };
        //            }
        //            else
        //            {
        //                replayFrames[currentTextureArray] = new Texture2DArray(capture.frameSize.x, capture.frameSize.y, TextureArraySlices, TextureFormat.RGB24, false)
        //                {
        //                    filterMode = FilterMode.Point,
        //                };
        //            }
        //        }
        //        if (loadedFrames < TextureArraySlices)
        //        {
        //            replayFrames[currentTextureArray].SetPixelData<byte>(capture.GetCurrentUncompressedFrame(downscale: InstantReplay.downscaleReplay.Value), 0, loadedFrames);
        //            loadedFrames++;
        //        }
        //    }
        //}

        //This empty method needs to exist otherwise CosmeticSprites base implementation is going to delete us because we are not in the room
    }
}
