public class DinoPresaSeguidorAI : PreyEnemyAI
{
    protected override void SetupDefaultStats()
    {
        base.SetupDefaultStats();
        isLeader = false;
        eyeHeight = 0.32f;
        DetectionRange = 15f;
        LoseRange = 25f;
        AttackRange = 1.3f;
    }
}
