using UnityEngine;

namespace GateScanner
{
    public class GateScannerSignData
    {
        /// <summary>
        /// A multiplier for the alpha value of the sign.
        /// </summary>
        public float FadeMultiplier { get; set; }
        /// <summary>
        /// The value of <see cref="FadeMultiplier"/> in the previous frame.
        /// </summary>
        public float LastFadeMultipler { get; set; }
        /// <summary>
        /// If this is true, the sprite does not reflect what the gate is doing, and the sprite should fade out in preparation for being switched.
        /// </summary>
        public bool WantToSwitch { get; set; }
        /// <summary>
        /// Whether or not the sign is currently being used to scan a pearl.
        /// </summary>
        public bool InScanMode { get; set; }
        public int Step1AnimationTimer { get; set; }
        public int Step2AnimationTimer { get; set; }
        /// <summary>
        /// Controls the scrolling text in the sign when the speaker is active. There are 9 frames in the animation, and each frame is 3 ticks, so the counter resets at 27 ticks.
        /// </summary>
        public int ScrollCounter { get; set; }
        /// <summary>
        /// When 1, the color of the sign is changed to the ID color of whoever is currently talking.
        /// </summary>
        public float IteratorColorIntensity { get; set; }
        /// <summary>
        /// The value of <see cref="IteratorColorIntensity"/> in the previous frame.
        /// </summary>
        public float LastIteratorColorIntensity { get; set; }
        /// <summary>
        /// The color the sign uses when an iterator is talking. Should match the ID color of the iterator.
        /// </summary>
        public Color IteratorColor { get; set; }
        /// <summary>
        /// The value of <see cref="IteratorColor"/> in the previous frame.
        /// </summary>
        public Color LastIteratorColor { get; set; }
        /// <summary>
        /// When 1, the sign is red. Used in error messages.
        /// </summary>
        public float ErrorColorIntensity { get; set; }
        /// <summary>
        /// The value of <see cref="ErrorColorIntensity"/> in the previous frame.
        /// </summary>
        public float LastErrorColorIntensity { get; set; }
        /// <summary>
        /// Whether or not the scanner has control of Sofanthiel.
        /// </summary>
        public bool ScanControllingRobo { get; set; }

        public GateScannerSignData()
        {
            FadeMultiplier = 1f;
            LastFadeMultipler = 1f;
            WantToSwitch = false;
            InScanMode = false;
            Step1AnimationTimer = 0;
            Step2AnimationTimer = 0;
            ScrollCounter = 0;
            IteratorColorIntensity = 0f;
            LastIteratorColorIntensity = 0f;
            ErrorColorIntensity = 0f;
            LastErrorColorIntensity = 0f;
            ScanControllingRobo = false;
        }
    }
}
