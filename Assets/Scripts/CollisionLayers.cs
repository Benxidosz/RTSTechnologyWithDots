using System;

[Flags]
public enum CollisionLayers {
    Core = 1 << 0,
    Zombie = 1 << 1,
    Soldier = 1 << 2,
    Selection = 1 << 3,
    Ground = 1 << 4,
    Bullet = 1 << 5
}