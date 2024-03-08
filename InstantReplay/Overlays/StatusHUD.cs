using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace InstantReplay.Overlays
{
    internal class StatusHUD : HUD.HudPart
    {
        private readonly FLabel errorLabel;
        private readonly List<StatusMessage> statusMessages = [];

        public StatusHUD(HUD.HUD hud) : base(hud)
        {
            errorLabel = new FLabel(RWCustom.Custom.GetFont(), String.Empty)
            {
                y = hud.rainWorld.screenSize.y * 0.15f,
                x = hud.rainWorld.screenSize.x / 2,
                scale = 2f
            };
            hud.fContainers[1].AddChild(errorLabel);
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
                if (errorLabel.text != String.Empty)
                {
                    displayCounter++;
                    //Display status for 10 seconds
                    if (displayCounter > 100)
                    {
                        errorLabel.text = String.Empty;
                        displayCounter = 0;
                    }
                }

                //Status message countdown/cleanup
                for (int i = 0; i < statusMessages.Count; i++)
                {
                    statusMessages[i].displayTime--;
                    if (statusMessages[i].displayTime < 0)
                    {
                        hud.fContainers[1].RemoveChild(statusMessages[i].message);
                    }
                }
                statusMessages.RemoveAll(item => item.displayTime < 0);

                timePool -= 4;
                //FPS seem to be really low / game window was froozen, reset timePool
                if (timePool > 4f) timePool = 0;
            }
        }

        public void SetError(string text)
        {
            errorLabel.text = text;
            errorLabel.color = Color.red;
            displayCounter = 0;
        }

        public void AddStatusMessage(string text, int displayTime)
        {
            FLabel statusLabel = new FLabel(RWCustom.Custom.GetFont(), text)
            {
                y = hud.rainWorld.screenSize.y - (25 + statusMessages.Count * 17),
                x = 25,
                scale = 0.99f,
                alignment = FLabelAlignment.Left
            };
            statusMessages.Add(new StatusMessage(statusLabel, displayTime));
            hud.fContainers[1].AddChild(statusLabel);
        }

        private class StatusMessage
        {
            public FLabel message;
            public int displayTime;

            public StatusMessage(FLabel message, int displayTime)
            {
                this.message = message;
                this.displayTime = displayTime;
            }
        }
    }
}
