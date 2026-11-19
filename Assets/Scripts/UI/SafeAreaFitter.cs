using UnityEngine;

namespace SweetBananas.UI
{
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rect;
        Rect _safeArea;
        int _width = -1;
        int _height = -1;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            if (Screen.width != _width || Screen.height != _height || Screen.safeArea != _safeArea)
                Apply();
        }

        void Apply()
        {
            _width = Screen.width;
            _height = Mathf.Max(1, Screen.height);
            _safeArea = Screen.safeArea;
            var anchorMax = _rect.anchorMax;
            anchorMax.y = (_safeArea.y + _safeArea.height) / _height;
            _rect.anchorMax = anchorMax;
            var offsetMax = _rect.offsetMax;
            offsetMax.y = 0f;
            _rect.offsetMax = offsetMax;
        }
    }
}
