using System;
using UnityEngine;

namespace InstantReplay.Overlays
{
    internal class StatusHUD : HUD.HudPart
    {
        private readonly FLabel statusLabel;

        public StatusHUD(HUD.HUD hud) : base(hud)
        {
            statusLabel = new FLabel(RWCustom.Custom.GetFont(), String.Empty)
            {
                y = hud.rainWorld.screenSize.y * 0.15f,
                x = hud.rainWorld.screenSize.x / 2,
                scale = 2f
            };
            hud.fContainers[1].AddChild(statusLabel);
        }

        //Should be called 40 times a second
        int timePool = 0;
        int displayCounter = 0;
        public override void Update()
        {
            timePool += 1;
            //Should run about every 100ms assuming FPS is fine
            if (timePool >= 4)
            {
                //Otherwise keep up the current text for around config seconds and then remove it
                if (statusLabel.text != String.Empty)
                {
                    displayCounter++;
                    //Display status for 10 seconds
                    if (displayCounter > 100)
                    {
                        statusLabel.text = String.Empty;
                        displayCounter = 0;
                    }

                }
                timePool -= 4;
                //FPS seem to be really low / game window was froozen, reset timePool
                if (timePool > 4f) timePool = 0;
            }
        }

        public void SetStatus(string text, Color color)
        {
            statusLabel.text = text;
            statusLabel.color = color;
            displayCounter = 0;
        }
    }
}
