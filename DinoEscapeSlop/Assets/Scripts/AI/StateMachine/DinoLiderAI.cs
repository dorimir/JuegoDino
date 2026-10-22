public class DinoLiderAI : PredatorEnemyAI
{
    protected override void SetupDefaultStats()
    {
        base.SetupDefaultStats();
        isLeader = true;
        DetectionRange = 7.5f;
        LoseRange = 18f;
        AttackRange = 1.8f;
    }
}
