namespace Cardstorm.Core
{
    // Índices de las capas del proyecto. GreyboxBuilder las registra en el TagManager con estos mismos valores.
    public static class Layers
    {
        public const int Player = 6;
        public const int Enemy = 7;
        public const int Obstacle = 8;
        public const int PlayerAttack = 9;  // Reservada para las cartas
        public const int EnemyAttack = 10;  // Reservada para proyectiles enemigos
        public const int Pickup = 11;       // Reservada para experiencia y oro

        public const int PlayerMask = 1 << Player;
        public const int EnemyMask = 1 << Enemy;
        public const int ObstacleMask = 1 << Obstacle;
        public const int PlayerAttackMask = 1 << PlayerAttack;
        public const int EnemyAttackMask = 1 << EnemyAttack;
        public const int PickupMask = 1 << Pickup;

        public const string PlayerName = "Player";
        public const string EnemyName = "Enemy";
        public const string ObstacleName = "Obstacle";
        public const string PlayerAttackName = "PlayerAttack";
        public const string EnemyAttackName = "EnemyAttack";
        public const string PickupName = "Pickup";
    }
}
