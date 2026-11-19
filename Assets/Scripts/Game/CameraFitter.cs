using SweetBananas.Data;
using UnityEngine;
using Zenject;

namespace SweetBananas.Game
{
    public sealed class CameraFitter : MonoBehaviour
    {
        Camera _camera;
        GameConfig _config;
        int _width = -1;
        int _height = -1;

        [Inject]
        public void Construct(GameConfig config)
        {
            _config = config;
        }

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
        }

        void Start()
        {
            Apply();
        }

        void Update()
        {
            if (Screen.width != _width || Screen.height != _height)
                Apply();
        }

        public void Apply()
        {
            _width = Screen.width;
            _height = Mathf.Max(1, Screen.height);
            var worldWidth = _config != null ? _config.WorldWidthUnits : 10.8f;
            var size = worldWidth * 0.5f * _height / Mathf.Max(1, _width);
            _camera.orthographicSize = size;
            var position = _camera.transform.position;
            position.x = worldWidth * 0.5f;
            position.y = size;
            position.z = -10f;
            _camera.transform.position = position;
        }
    }
}
