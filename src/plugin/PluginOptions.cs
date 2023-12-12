using Menu.Remix.MixedUI;
using UnityEngine;

namespace GateScanner
{
    public class PluginOptions : OptionInterface
    {
        public static PluginOptions Instance = new();

        public static Configurable<bool> UnlockScannerCheat = Instance.config.Bind("UnlockScannerCheat", false, new ConfigurableInfo("Allows you to scan pearls without completing the usual requirements for doing so. Will not apply if you do not have the Mark."));

        public override void Initialize()
        {
            base.Initialize();
            Tabs = new OpTab[1];

            Tabs[0] = new(Instance, "Options");
            CheckBoxOption(UnlockScannerCheat, 0, "Unlock Scanner Cheat");
        }

        private void CheckBoxOption(Configurable<bool> setting, float pos, string label)
        {
            Tabs[0].AddItems(new OpCheckBox(setting, new(50, 550 - pos * 30)) { description = setting.info.description }, new OpLabel(new Vector2(90, 550 - pos * 30), new Vector2(), label, FLabelAlignment.Left));
        }
    }
}