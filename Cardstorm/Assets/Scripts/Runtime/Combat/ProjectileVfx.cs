using UnityEngine;

namespace Cardstorm.Combat
{
    // Aspecto de los proyectiles de una carta. Lo que quede vacío no se dibuja;
    // sin sprite, el proyectil usa el círculo teñido con el color del tipo.
    [CreateAssetMenu(menuName = "Cardstorm/VFX de proyectil", fileName = "Vfx_")]
    public class ProjectileVfx : ScriptableObject
    {
        [Tooltip("Ya va coloreado: no se tiñe y se dibuja a su tamaño en píxeles.")]
        public Sprite sprite;

        [Tooltip("Partículas que deja por el camino. Su Rate over Distance marca cada cuánto.")]
        public ParticleSystem trail;

        [Tooltip("Destello al disparar.")]
        public ParticleSystem muzzle;

        [Tooltip("Al golpear a un enemigo o un obstáculo.")]
        public ParticleSystem impact;

        [Tooltip("Gira la bala, la salida y el impacto hacia donde va el proyectil. Para sprites alargados.")]
        public bool orient;
    }
}
