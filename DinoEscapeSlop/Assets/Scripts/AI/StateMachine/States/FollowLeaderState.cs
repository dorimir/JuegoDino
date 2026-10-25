using UnityEngine;

public class FollowLeaderState : IState
{
    protected EnemyAI ai;
    protected float repathTimer;
    protected const float RepathInterval = 0.35f;

    private Vector3 currentSmoothedSlotPos;
    private Vector3 slotVelocity;
    private bool isSlotInitialized = false;
    private const int ObstacleMask = ~((1 << 3) | (1 << 2));

    public FollowLeaderState(EnemyAI ai)
    {
        this.ai = ai;
    }

    public virtual void Enter()
    {
        ai.SetVisualState(EnemyAI.DinoVisualState.Normal);
        repathTimer = 0f;
        isSlotInitialized = false;
        slotVelocity = Vector3.zero;

        if (ai.Movement != null)
        {
            ai.Movement.speed = ai.patrolSpeed;
            ai.Movement.ClearPath();
        }
    }

    public virtual void Update()
    {
        EnemyAI leader = ai.LeaderAI;
        if (leader == null || !leader.gameObject.activeInHierarchy)
        {
            ai.PromoteToLeader();
            return;
        }

        if (ai.CanSeeThreat(out EnemyAI threat))
        {
            if (ai.IsSmallDino) ai.NotifyPackFlee(threat);
            else ai.TriggerFlee(threat);
            return;
        }

        if (ai.CanSeePlayer())
        {
            if (ai.IsSmallDino) ai.NotifyPackAttack(ai.Player);
            else ai.TriggerAttack(ai.Player);
            return;
        }

        if (ai.preyCooldownTimer <= 0f && ai.CanSeeHuntablePrey(out EnemyAI prey))
        {
            ai.TriggerAttack(prey.transform, prey);
            return;
        }

        if (ai.Movement == null) return;

        int slotIndex = leader.packFollowers.IndexOf(ai);
        if (slotIndex < 0)
        {
            leader.packFollowers.Add(ai);
            slotIndex = leader.packFollowers.IndexOf(ai);
        }

        int row = (slotIndex / 2) + 1;
        float side = (slotIndex % 2 == 0) ? -1f : 1f;

        float rowSpacing = ai.IsSmallDino ? 1.35f : 2.2f;
        float lateralSpacing = ai.IsSmallDino ? 0.85f : 1.35f;
        float bodyRadius = ai.IsSmallDino ? 0.25f : 0.45f;
        float rayHeight = ai.IsSmallDino ? 0.35f : 0.75f;

        float backDist = rowSpacing * row;
        float desiredLateral = lateralSpacing * row;
        float lateralDist = side * desiredLateral;

        Vector3 leaderPos = leader.transform.position;
        Vector3 leaderFwd = leader.transform.forward;
        Vector3 leaderRight = leader.transform.right;

        if (Physics.Raycast(leaderPos + Vector3.up * rayHeight, -leaderFwd, out RaycastHit spineWallHit, backDist + bodyRadius + 0.15f, ObstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (IsValidFormationObstacle(spineWallHit))
            {
                backDist = Mathf.Max(0.4f, spineWallHit.distance - bodyRadius - 0.15f);
            }
        }

        Vector3 spinePos = leaderPos - (leaderFwd * backDist);
        spinePos.y = ai.transform.position.y;

        Vector3 sideDir = leaderRight * side;
        if (Physics.Raycast(spinePos + Vector3.up * rayHeight, sideDir, out RaycastHit wallHit, desiredLateral + bodyRadius + 0.20f, ObstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (IsValidFormationObstacle(wallHit))
            {
                float safeLateral = Mathf.Max(0.12f, wallHit.distance - bodyRadius - 0.20f);
                lateralDist = side * safeLateral;
            }
        }

        Vector3 idealSlotPos = spinePos + (leaderRight * lateralDist);
        idealSlotPos.y = ai.transform.position.y;

        Collider[] slotWalls = Physics.OverlapSphere(idealSlotPos + Vector3.up * rayHeight, bodyRadius + 0.15f, ObstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < slotWalls.Length; i++)
        {
            Collider col = slotWalls[i];
            if (col == null || col.isTrigger || col.CompareTag("Enemy") || col.CompareTag("Player") || col.CompareTag("canPickUp")) continue;
            string cname = col.gameObject.name.ToLower();
            if (cname.Contains("plane") || cname.Contains("floor") || cname.Contains("suelo")) continue;

            Vector3 closest = col.ClosestPoint(idealSlotPos + Vector3.up * rayHeight);
            Vector3 push = (idealSlotPos + Vector3.up * rayHeight) - closest;
            push.y = 0f;
            float pDist = push.magnitude;
            float req = bodyRadius + 0.15f;
            if (pDist < req && pDist > 0.001f)
            {
                idealSlotPos += (push / pDist) * (req - pDist);
            }
        }

        float safeClearance = bodyRadius * 0.75f;

        if (!ai.Movement.HasClearLineTo(idealSlotPos, safeClearance))
        {
            if (ai.Movement.HasClearLineTo(spinePos, safeClearance))
            {
                idealSlotPos = spinePos;
            }
            else
            {
                idealSlotPos = leaderPos;
            }
        }

        if (!isSlotInitialized)
        {
            currentSmoothedSlotPos = idealSlotPos;
            isSlotInitialized = true;
        }
        else
        {
            currentSmoothedSlotPos = Vector3.SmoothDamp(currentSmoothedSlotPos, idealSlotPos, ref slotVelocity, 0.12f);
        }

        float distToSlot = Vector3.Distance(ai.transform.position, currentSmoothedSlotPos);
        float distToLeader = Vector3.Distance(ai.transform.position, leaderPos);

        Vector3 leaderVel = (leader.Movement != null) ? leader.Movement.Velocity : Vector3.zero;
        float leaderCurrentSpeed = leaderVel.magnitude;
        float leaderTargetSpeed = (leader.Movement != null) ? leader.Movement.speed : ai.patrolSpeed;

        float targetSpeed;
        if (leaderCurrentSpeed < 0.08f && distToSlot <= 0.40f)
        {
            targetSpeed = 0f;
        }
        else
        {
            float baseRefSpeed = (leaderCurrentSpeed > 0.1f) ? leaderCurrentSpeed : leaderTargetSpeed;
            float catchUpFactor = Mathf.Clamp(1.0f + (distToSlot - 0.25f) * 0.40f, 0.45f, 1.65f);
            targetSpeed = baseRefSpeed * catchUpFactor;
        }

        ai.Movement.speed = Mathf.MoveTowards(ai.Movement.speed, targetSpeed, 12f * Time.deltaTime);

        if (distToSlot <= 0.55f && leader.Movement != null)
        {
            Quaternion targetRot = Quaternion.Euler(0f, leader.transform.eulerAngles.y, 0f);
            ai.transform.rotation = Quaternion.Slerp(ai.transform.rotation, targetRot, 8f * Time.deltaTime);
        }

        if (ai.Movement.HasClearLineTo(currentSmoothedSlotPos, safeClearance))
        {
            ai.Movement.MoveTo(currentSmoothedSlotPos);
        }
        else if (ai.Movement.HasClearLineTo(leaderPos, safeClearance))
        {
            ai.Movement.MoveTo(leaderPos);
        }
        else
        {
            repathTimer += Time.deltaTime;
            if (repathTimer >= RepathInterval || ai.Movement.HasReachedDestination)
            {
                repathTimer = 0f;
                ai.Movement.MoveTo(leaderPos);
            }
        }
    }

    private bool IsValidFormationObstacle(RaycastHit hit)
    {
        if (hit.collider == null || hit.collider.isTrigger) return false;
        if (hit.collider.CompareTag("Enemy") || hit.collider.CompareTag("Player") || hit.collider.CompareTag("canPickUp")) return false;
        if (hit.normal.y >= 0.65f) return false;
        string name = hit.collider.gameObject.name.ToLower();
        if (name.Contains("plane") || name.Contains("floor") || name.Contains("suelo")) return false;
        return true;
    }

    public virtual void Exit()
    {
        isSlotInitialized = false;
    }
}
