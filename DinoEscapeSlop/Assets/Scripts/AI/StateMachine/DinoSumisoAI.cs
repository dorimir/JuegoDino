public class DinoSumisoAI : PredatorEnemyAI
{
    protected override void SetupDefaultStats()
    {
        base.SetupDefaultStats();
        isLeader = false;
        DetectionRange = 15f;
        LoseRange = 25f;
        AttackRange = 2.5f;
    }
}
