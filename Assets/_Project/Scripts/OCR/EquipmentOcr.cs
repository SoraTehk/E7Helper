using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Cysharp.Threading.Tasks;
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

        [FoldoutGroup("Scene"), SceneObjectsOnly] public UwcWindowTexture WindowCapture = null!;
        [FoldoutGroup("Scene"), SceneObjectsOnly] public Canvas Canvas = null!;
        [FoldoutGroup("Scene"), SceneObjectsOnly] public CanvasScaler CanvasScaler = null!;
        [FoldoutGroup("Scene"), SceneObjectsOnly] public RawImage CapturedWindowImage = null!;

        [FoldoutGroup("Config")] public string OcrLanguageId = "eng";

        [FoldoutGroup("Runtime")] public Texture2D? m_BufferTexture2D;

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

                BufferImage.texture = m_BufferTexture2D;
                BufferImage.SetNativeSize();

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
    }
}