public class DinoPresaLiderAI : PreyEnemyAI
{
    protected override void SetupDefaultStats()
    {
        base.SetupDefaultStats();
        isLeader = true;
        eyeHeight = 0.35f;
        DetectionRange = 7.5f;
        LoseRange = 18f;
        AttackRange = 1.3f;
    }
}
