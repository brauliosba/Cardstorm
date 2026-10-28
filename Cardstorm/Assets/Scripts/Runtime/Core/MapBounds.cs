using UnityEngine;

namespace Cardstorm.Core
{
    // Área jugable del mapa, centrada en este objeto. La usan la cámara y el spawner.
    public class MapBounds : MonoBehaviour
    {
        [SerializeField] Vector2 size = new(120f, 120f);

        public Vector2 Size => size;

        public Rect Rect => new((Vector2)transform.position - size * 0.5f, size);

        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
