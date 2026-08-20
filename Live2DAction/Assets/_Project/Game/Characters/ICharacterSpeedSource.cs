namespace Live2DAction.Characters
{
    // 2026-08-20, explicit user request ("敵人的移動動作採用跟玩家一樣地踏步") - lets
    // CharacterAnimatorLink drive a locomotion Speed parameter from WHICHEVER movement system a
    // character actually has, not just CharacterMovement specifically. Player uses
    // CharacterMovement; Enemy has its own entirely separate movement implementation inside
    // EnemyAI (deliberately not CharacterMovement - see that class's own header comment on why),
    // so a single shared interface - same "small interface, implemented by whichever concrete
    // class this instance actually has" idiom already used throughout this project
    // (IInputCommand, ICameraYawSource, ILockOnSource) - lets one Link component serve both
    // without either movement implementation needing to know about the other.
    public interface ICharacterSpeedSource
    {
        float CurrentHorizontalSpeed { get; }

        // Enemy never flies (see CharacterMovement.IsFlying's own comment history - "Enemy
        // flight scope" was an explicit, already-settled decision) - EnemyAI's own implementation
        // just always returns false, same "stub returns false for the non-applicable side"
        // convention as EnemyAI's IInputCommand.FlyPressed.
        bool IsFlying { get; }
    }
}
