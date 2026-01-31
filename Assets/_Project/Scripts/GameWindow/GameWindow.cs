using System;
using System.Collections.Generic;
using SoraTehk.E7Helper.Interop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SoraTehk.E7Helper {
    public class GameWindow : SingletonBehaviour<GameWindow> {
        private IntPtr m_HWnd;

        private void Start() {
            if (Application.isEditor) return;

            m_HWnd = User32Interop.GetActiveWindow();
            // Keep window in front (overlay)
            User32Interop.SetWindowPos(m_HWnd,
                Win32Constants.HWND_TOPMOST,
                0, 0, 0, 0, 0
            );
            var margins = new MARGINS {
                cxLeftWidth = -1
            };
            DwmapiInterop.DwmExtendFrameIntoClientArea(m_HWnd, ref margins);
            // Make window default as not click-thought
            SetClickThrough(false);
        }

        private void Update() {
            // Default as click through
            var isClickThrough = true;

            // Check for interceptors
            var eventData = new PointerEventData(EventSystem.current) {
                // position = Mouse.current.position.ReadValue()
                // "New" input system doesn't work here
                position = Input.mousePosition,
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var result in results)
                if (result.gameObject.TryGetComponent(out UIClickInterceptor uiInterceptor))
                    // If UI is not click through
                    if (!uiInterceptor.IsClickThrough) {
                        isClickThrough = uiInterceptor.IsClickThrough;
                        break;
                    }

            SetClickThrough(isClickThrough);
        }

        public void SetClickThrough(bool isClickThrough) {
            if (Application.isEditor) return;

            User32Interop.SetWindowLong(m_HWnd,
                Win32Constants.GWL_EXSTYLE,
                isClickThrough
                    ? Win32Constants.WS_EX_LAYERED | Win32Constants.WS_EX_TRANSPARENT
                    : Win32Constants.WS_EX_LAYERED
            );
        }
    }
}