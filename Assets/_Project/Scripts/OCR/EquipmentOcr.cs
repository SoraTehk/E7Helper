using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Cysharp.Threading.Tasks;
using Freya;
using PixelSquare.TesseractOCR;
using Sirenix.OdinInspector;
using SoraTehk.E7Helper.Interop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using uWindowCapture;

namespace SoraTehk.E7Helper {
    public class EquipmentOcr : MonoBehaviour {
        [Button(ButtonSizes.Medium)]
        private void EncodeBufferTextureToPng() {
            var bytes = m_BufferTexture2D.EncodeToPNG();
            File.WriteAllBytes(Application.dataPath + "/_Project/template.png", bytes);
        }

        [FoldoutGroup("Debug")] public TMP_Text WindowCaptureDebugText = null!;
        [FoldoutGroup("Debug")] public RawImage BufferImage = null!;
        [FoldoutGroup("Debug")] public RawImage MainStatImage = null!;
        [FoldoutGroup("Debug")] public RawImage SubStatsImage = null!;
        [FoldoutGroup("Debug"), MultiLineProperty(5)] public string OutputString = null!;

        [FoldoutGroup("Scene"), SceneObjectsOnly] public UwcWindowTexture WindowCapture = null!;
        [FoldoutGroup("Scene"), SceneObjectsOnly] public Canvas Canvas = null!;
        [FoldoutGroup("Scene"), SceneObjectsOnly] public CanvasScaler CanvasScaler = null!;
        [FoldoutGroup("Scene"), SceneObjectsOnly] public RawImage CapturedWindowImage = null!;
        [FoldoutGroup("Scene"), SceneObjectsOnly] public RectTransformController MainStatRectController = null!;
        [FoldoutGroup("Scene"), SceneObjectsOnly] public RectTransformController SubStatsRectController = null!;

        [FoldoutGroup("Config")] public string OcrLanguageId = "eng";
        [FoldoutGroup("Config"), Range(0f, 1f)] public float BlackThreshold = 0.54f;
        [FoldoutGroup("Config"), Range(0f, 1f)] public float AlphaThreshold = 0.88f;

        [FoldoutGroup("Runtime")] public Texture2D? m_BufferTexture2D;
        [FoldoutGroup("Runtime")] public Texture2D? m_MainStatTexture2D;
        [FoldoutGroup("Runtime")] public Texture2D? m_SubStatsTexture2D;

        private IOpticalCharacterReader m_Ocr = null!;
        private Color32[] m_CurrPixels32;
        private GCHandle m_GcHandle;
        private IntPtr m_CurrPixels32Ptr = IntPtr.Zero;

        private bool m_IsScanning;

        private void Awake() {
            CanvasScaler = Canvas.GetComponent<CanvasScaler>();

            // We only need input image visualize in the editor
            if (!Application.isEditor) {
                CapturedWindowImage.enabled = false;
            }

            CapturedWindowImage.rectTransform.anchorMin = new Vector2(0, 1); // Top-left corner
            CapturedWindowImage.rectTransform.anchorMax = new Vector2(0, 1); // Top-left corner
            CapturedWindowImage.rectTransform.pivot = new Vector2(0, 1); // Top-left corner

            // TODO: Forced settings (but saved performance since we don't need to calculate later in Update)
            MainStatRectController.RectTransform.anchorMin = new Vector2(0, 0); // Bottom-left corner
            MainStatRectController.RectTransform.anchorMax = new Vector2(0, 0); // Bottom-left corner
            MainStatRectController.RectTransform.pivot = new Vector2(0, 0); // Bottom-left corner

            SubStatsRectController.RectTransform.anchorMin = new Vector2(0, 0); // Bottom-left corner
            SubStatsRectController.RectTransform.anchorMax = new Vector2(0, 0); // Bottom-left corner
            SubStatsRectController.RectTransform.pivot = new Vector2(0, 0); // Bottom-left corner
        }

        private void Start() {
            m_Ocr = new TesseractOCRImpl();
            m_Ocr.Initialize(OcrLanguageId);

            Async().Forget();

            async UniTask Async() {
                while (WindowCapture.window == null
                       || WindowCapture.window.buffer == IntPtr.Zero
                       || WindowCapture.window.texture == null) {
                    await UniTask.Yield();
                }

                // TODO: Move this to OnEnable/OnDisable
                WindowCapture.window.onCaptured.AddListener(Scan);
            }
        }

        private void OnDestroy() {
            if (m_GcHandle.IsAllocated) m_GcHandle.Free();
        }

        [Button((ButtonSizes.Medium))]
        public void Scan() {
            // TODO: We just drop new scan if old scan are still in progress
            if (m_IsScanning) return;

            Async().Forget();
            async UniTask Async() {
                if (!ServiceLocator.TryGet<TextureManager>(out var texMgr)) return;
                m_IsScanning = true;

                var window = WindowCapture.window;
                var width = window.texture.width;
                var height = window.texture.height;
                var format = window.texture.format;

                // WindowCapture texture size changed so we need to create new texture and re-pin it
                if (m_BufferTexture2D == null || width != m_BufferTexture2D.width || height != m_BufferTexture2D.height) {
                    texMgr.Release(m_BufferTexture2D);
                    m_BufferTexture2D = texMgr.Acquire(width, height, format);
                    m_BufferTexture2D.filterMode = FilterMode.Bilinear;
                    // Pin the target texture's pixel data
                    m_CurrPixels32 = m_BufferTexture2D.GetPixels32();
                    if (m_GcHandle.IsAllocated) m_GcHandle.Free();
                    m_GcHandle = GCHandle.Alloc(m_CurrPixels32, GCHandleType.Pinned);
                    m_CurrPixels32Ptr = m_GcHandle.AddrOfPinnedObject();
                }

                // Copy the pixel data from uWindowTexture (this will directly set m_CurrPixels32)
                Msvcrt.memcpy(m_CurrPixels32Ptr, window.buffer, width * height * sizeof(byte) * 4);

                // Fix texture rotation (being upside down flipped)
                var flippedPixels = new Color32[m_CurrPixels32.Length];
                for (var y = 0; y < height; y++) {
                    var srcRow = y * width;
                    var destRow = (height - 1 - y) * width;
                    for (var x = 0; x < width; x++) flippedPixels[destRow + x] = m_CurrPixels32[srcRow + x];
                }

                // Apply the pixel data to the texture
                m_BufferTexture2D.SetPixels32(flippedPixels);
                m_BufferTexture2D.Apply();

                // TODO: Fix magic number (it was added by manually adjusting)
                // Calculate the scale factor based on the referenceResolution and matchWidthOrHeight
                var canvasScaleX = Screen.width / CanvasScaler.referenceResolution.x;
                var canvasScaleY = Screen.height / CanvasScaler.referenceResolution.y;
                // Calculate the actual window scale factor based on the referenceResolution and matchWidthOrHeight
                var windowScaleX = m_BufferTexture2D.width / CanvasScaler.referenceResolution.x;
                var windowScaleY = m_BufferTexture2D.height / CanvasScaler.referenceResolution.y;
                // Position will be scaled to fit inside referenceResolution
                var titleRectHeight = 39;
                // TODO: Find another way to do this
                bool isMaximized = window.isMaximized || (window.width >= 2559 && window.height >= 1368);
                bool isFullScreen = window.width >= 2559 && window.height >= 1439;
                var outputPos = isFullScreen
                    ? new Vector2(0, 0)
                    : isMaximized
                        ? new Vector2(0, -16)
                        : new Vector2(
                            (window.x + 8) / canvasScaleX,
                            (-window.y - titleRectHeight + 8) / canvasScaleY
                        );
                // Size will be scaled to fit inside Screen
                var outputScale = new Vector2(
                    (float)m_BufferTexture2D.width / Screen.width,
                    (float)m_BufferTexture2D.height / Screen.height
                );

                // Syncing CapturedWindowImage with the actual window
                CapturedWindowImage.texture = m_BufferTexture2D;
                CapturedWindowImage.rectTransform.anchoredPosition = outputPos;
                CapturedWindowImage.rectTransform.localScale = outputScale;

                // Cropping the main stat
                int mainStatTexStartX = Mathfs.RoundToInt(MainStatRectController.GetAnchoredX() * windowScaleX);
                int mainStatTexStartY = Mathfs.RoundToInt(MainStatRectController.GetAnchoredY() * windowScaleY);
                int mainStatTexWidth = Mathfs.RoundToInt(MainStatRectController.GetWidth() * windowScaleX);
                int mainStatTexHeight = Mathfs.RoundToInt(MainStatRectController.GetHeight() * windowScaleY);
                var mainStatTex = texMgr.Acquire(mainStatTexWidth, mainStatTexHeight, m_BufferTexture2D.format);
                Texture2DExtension.CopyCroppedPixels(mainStatTex, m_BufferTexture2D, mainStatTexStartX, mainStatTexStartY);
                mainStatTex.ApplyBlackMask(BlackThreshold, true);

                // Ocr texture to crop
                texMgr.Release(m_MainStatTexture2D);
                // TextureFormat.RGB24 because Tesseract only supports 3 bytes per pixel
                m_MainStatTexture2D = texMgr.Acquire(mainStatTexWidth, mainStatTexHeight, TextureFormat.RGB24);
                m_MainStatTexture2D.SetPixels(mainStatTex.GetPixels());
                m_MainStatTexture2D.Apply();

                // Scan
                m_Ocr.SetImage(m_MainStatTexture2D);
                string scannedStr = m_Ocr.GetText().Trim() + "\n";

                // Cropping the sub stats
                int subStatsTexStartX = Mathfs.RoundToInt(SubStatsRectController.GetAnchoredX() * windowScaleX);
                int subStatsTexStartY = Mathfs.RoundToInt(SubStatsRectController.GetAnchoredY() * windowScaleY);
                int subStatsTexWidth = Mathfs.RoundToInt(SubStatsRectController.GetWidth() * windowScaleX);
                int subStatsTexHeight = Mathfs.RoundToInt(SubStatsRectController.GetHeight() * windowScaleY);
                var subStatsTex = texMgr.Acquire(subStatsTexWidth, subStatsTexHeight, m_BufferTexture2D.format);
                Texture2DExtension.CopyCroppedPixels(subStatsTex, m_BufferTexture2D, subStatsTexStartX, subStatsTexStartY);

                var subStatsTexWithWhiteMask = texMgr.AcquireClone(subStatsTex);
                subStatsTexWithWhiteMask.ApplyBlackMask(BlackThreshold, true);

                var subStatsTexWithAlphaContrast = texMgr.AcquireClone(subStatsTex);
                subStatsTexWithAlphaContrast.ApplyAlphaContrast(AlphaThreshold);

                // Ocr texture to crop
                texMgr.Release(m_SubStatsTexture2D);
                // TextureFormat.RGB24 because Tesseract only supports 3 bytes per pixel
                m_SubStatsTexture2D = texMgr.Acquire(subStatsTexWidth, subStatsTexHeight, TextureFormat.RGB24);
                Texture2DExtension.BlendMultiply(m_SubStatsTexture2D, subStatsTexWithWhiteMask, subStatsTexWithAlphaContrast);

                // Scan
                m_Ocr.SetImage(m_SubStatsTexture2D);
                scannedStr += m_Ocr.GetText();

                BufferImage.texture = m_BufferTexture2D;
                BufferImage.SetNativeSize();
                MainStatImage.texture = m_MainStatTexture2D;
                MainStatImage.SetNativeSize();
                SubStatsImage.texture = m_SubStatsTexture2D;
                SubStatsImage.SetNativeSize();

                OutputString = scannedStr;

                ParseEquipment();

                // Clean up
                texMgr.Release(mainStatTex);
                texMgr.Release(subStatsTex);
                texMgr.Release(subStatsTexWithWhiteMask);
                texMgr.Release(subStatsTexWithAlphaContrast);

                var sb = new StringBuilder();
                sb.Append(window.width).Append('(').Append(window.rawWidth).Append(')');
                sb.Append('x');
                sb.Append(window.height).Append('(').Append(window.rawHeight).Append(')');
                sb.Append('-');
                sb.Append("Maximized=").Append(window.isMaximized);

                WindowCaptureDebugText.text = sb.ToString();

                m_IsScanning = false;
            }
        }

        [FoldoutGroup("Legacy")] public TMP_Text GearRankText = null!;
        [FoldoutGroup("Legacy")] public TMP_Text GearScore1Text = null!;
        [FoldoutGroup("Legacy")] public TMP_Text GearScore2Text = null!;
        [FoldoutGroup("Legacy")] public TMP_Text GearScore3Text = null!;
        [FoldoutGroup("Legacy")] public TMP_Text GearScore4Text = null!;
        [FoldoutGroup("Legacy")] public TMP_Text GearScoreTotalText = null!;
        [FoldoutGroup("Legacy")] public TMP_Text GearScoreTotalModdedText = null!;

        private void ParseEquipment() {
            var equipment = new Equipment();
            equipment.TryParse(OutputString);

            GearRankText.text = equipment.Rank.ToString();

            var gearScoreTextArr = new TMP_Text[] {
                GearScore1Text,
                GearScore2Text,
                GearScore3Text,
                GearScore4Text
            };
            foreach (var gsText in gearScoreTextArr) {
                gsText.text = 0m.ToString("F1");
            }

            decimal gearScoreTotal = 0m;
            decimal gearScoreLowest = 8m;
            decimal gearScoreTotalModded = 0m;
            for (var i = 0; i < equipment.Stats.Count; i++) {
                decimal gearScore = equipment.Stats[i].GetGearScore();
                gearScoreTextArr[i].text = gearScore.ToString("F1");
                gearScoreTotal += gearScore;
                gearScoreTotalModded += gearScore;
                // Finding out which are the lowest gear score stat
                gearScoreLowest = Math.Min(gearScoreLowest, gearScore);
            }

            // Replace the lowest gear score stat with 8
            gearScoreTotalModded += -gearScoreLowest + 8;

            GearScoreTotalText.text = gearScoreTotal.ToString("F2");
            GearScoreTotalModdedText.text = gearScoreTotalModded.ToString("F2");

            // Text formating for fast gear check
            if (Constants.EquipmentRank2GearScoreMinMax.TryGetValue(equipment.Rank, out var value)) {
                GearScoreTotalText.color = gearScoreTotal < value.Min ? Color.red :
                    gearScoreTotal >= value.Max ? Color.green : Color.yellow;
                GearScoreTotalModdedText.color = gearScoreTotalModded < value.Min ? Color.red :
                    gearScoreTotalModded >= value.Max ? Color.green : Color.yellow;
            }
            else {
                GearScoreTotalText.color = Color.white;
                GearScoreTotalModdedText.color = Color.white;
            }
        }
    }
}