public class DinoCiegoAI : BlindEnemyAI
{
    protected override void SetupDefaultStats()
    {
        base.SetupDefaultStats();
        AttackRange = 2.2f;
        patrolSpeed = 2.2f;
        chaseSpeed = 8.5f;
    }
}
