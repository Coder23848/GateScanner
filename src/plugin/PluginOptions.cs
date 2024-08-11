using Menu.Remix.MixedUI;
using UnityEngine;

namespace GateScanner
{
    public class PluginOptions : OptionInterface
    {
        public static PluginOptions Instance = new();

        public static Configurable<bool> UnlockScannerCheat = Instance.config.Bind("UnlockScannerCheat", false, new ConfigurableInfo("Allows you to interact with Looks to the Moon or Five Pebbles without completing the usual requirements for doing so. Will not apply if you do not have the Mark."));
        public static Configurable<string> IteratorCodeBoxContents = Instance.config.Bind("", "");
        public static Configurable<bool> UnlockChasingWind = Instance.config.Bind("UnlockChasingWindCheat", false, new ConfigurableInfo("Allows you to interact with Chasing Wind without completing the usual requirements for doing so. Will not apply if you do not have the Mark."));

        private OpTextBox iteratorCodeBox;
        private OpHoldButton iteratorCodeButton;
        private (OpCheckBox, OpLabel) chasingWindOption;

        public override void Initialize()
        {
            base.Initialize();
            Tabs = new OpTab[1];

            Color cheatColor = new(0.85f, 0.35f, 0.4f);

            Tabs[0] = new(Instance, "Options");
            Tabs[0].AddItems(
                new OpLabel(new Vector2(50, 520), new Vector2(500, 30), "Gate Scanner Cheats", FLabelAlignment.Center, true) { color = cheatColor },
                new OpCheckBox(UnlockScannerCheat, new(50, 460)) { description = UnlockScannerCheat.info.description, colorEdge = cheatColor },
                new OpLabel(new Vector2(90, 460), new Vector2(), "Unlock Scanner", FLabelAlignment.Left) { color = cheatColor }
                );
            chasingWindOption = (
                new(UnlockChasingWind, new(50, 430)) { description = UnlockChasingWind.info.description, colorEdge = cheatColor },
                new(new Vector2(90, 430), new Vector2(), "Unlock Scanner For Chasing Wind", FLabelAlignment.Left) { color = cheatColor }
                );
            // show the option if the user has already found and enabled it before
            if (!UnlockChasingWind.Value)
            {
                chasingWindOption.Item1.Hide();
                chasingWindOption.Item2.Hide();
            }
            Tabs[0].AddItems(chasingWindOption.Item1, chasingWindOption.Item2);

            iteratorCodeBox = new(IteratorCodeBoxContents, new(200, 180), 200) { allowSpace = true };
            iteratorCodeButton = new(new(250, 50), 50, "Reveal", 40);
            Tabs[0].AddItems(
                new OpLabelLong(new(50, 220), new Vector2(500, 30), "Enter the name of a supported modded iterator here to\nreveal options pertaining to them. Beware of spoilers!", false, FLabelAlignment.Center),
                iteratorCodeBox,
                iteratorCodeButton
                );
            iteratorCodeBox.OnChange += IteratorCodeBoxChange;
            iteratorCodeButton.OnPressDone += IteratorCodeButtonPressDone;

            iteratorCodeButton.greyedOut = true;
        }

        private void IteratorCodeBoxChange()
        {
            if (iteratorCodeBox.value != "")
            {
                iteratorCodeButton.greyedOut = false;
            }
        }

        private void IteratorCodeButtonPressDone(UIfocusable trigger)
        {
            string value = iteratorCodeBox.value.ToLower().Replace(" ", "");
            if (value == "chasingwind" && chasingWindOption.Item1.Hidden)
            {
                chasingWindOption.Item1.ShowConfig();
                chasingWindOption.Item1.Show();
                chasingWindOption.Item2.Show();
            }

            iteratorCodeBox.value = "";
            iteratorCodeButton.Reset();
            iteratorCodeButton.greyedOut = true;
        }
    }
}